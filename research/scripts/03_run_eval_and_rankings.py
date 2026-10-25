#!/usr/bin/env python3
"""
03_run_eval_and_rankings.py
Thực thi toàn bộ 75 truy vấn thực nghiệm trên dữ liệu thực tế:
- Nạp danh sách 53,341 địa điểm từ SQL Server
- Lọc ứng viên theo bán kính và từ khóa (tương đương PlaceSpatialSearchService của backend)
- Tính toán BM25 chuẩn hóa theo Robertson formula (k1=1.5, b=0.75)
- Tính toán ma trận khoảng cách hình học (Haversine) và ma trận mạng đường thực tế (OSRM)
- Xếp hạng theo 3 phương pháp đối chứng:
  1. BM25 thuần túy (Pure BM25)
  2. BM25 kết hợp khoảng cách địa lý (BM25 + Distance Heuristic)
  3. BM25 kết hợp xếp hạng đa tiêu chí CRADIS-DN4 (Relevance, Duration, Fairness)
- Xuất các file:
  - research/relevance_labels.csv (Bảng gán nhãn chuẩn bị cho người đánh giá độc lập 0-3)
  - research/ranking_comparison.csv (So sánh định lượng 3 phương pháp)
  - research/travel_time_results.csv (Chi tiết thời gian di chuyển và độ công bằng từng thành viên)
  - research/retrieval_metrics.csv (Quy chuẩn công thức và khung đánh giá Precision@5, nDCG@5)
"""

import csv
import json
import math
import os
import re
import subprocess
import time
import unicodedata
import urllib.request

# ==============================================================================
# 1. HÀM NẠP DỮ LIỆU TỪ SQL SERVER
# ==============================================================================
def load_all_places():
    print("-> Đang nạp danh sách địa điểm từ SQL Server...")
    cmd = [
        "docker", "exec", "location-search-sqlserver-dev",
        "/opt/mssql-tools18/bin/sqlcmd",
        "-S", "localhost", "-U", "sa", "-P", "T*n123456", "-C",
        "-d", "LocationSearch",
        "-W", "-s", "\t",
        "-Q", "SET NOCOUNT ON; SELECT Id, Name, ISNULL(Address, ''), Latitude, Longitude, ISNULL(Category, '') FROM Places WHERE IsNotSafe = 0;"
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        raise RuntimeError(f"Lỗi truy vấn SQL: {res.stderr}")
    lines = [l for l in res.stdout.strip().split("\n") if l.strip()]
    places = []
    # Skip header
    for line in lines[2:]:
        parts = line.split("\t")
        if len(parts) >= 6:
            pid = parts[0].strip()
            name = parts[1].strip()
            addr = parts[2].strip()
            try:
                lat = float(parts[3].strip())
                lon = float(parts[4].strip())
            except ValueError:
                continue
            cat = parts[5].strip()
            places.append({
                "id": pid,
                "name": name,
                "address": addr,
                "latitude": lat,
                "longitude": lon,
                "category": cat
            })
    print(f"   Đã nạp thành công {len(places)} địa điểm an toàn vào bộ nhớ.")
    return places

# ==============================================================================
# 2. HÀM TÍNH TOÁN KHOẢNG CÁCH HAVERSINE
# ==============================================================================
def haversine_km(lat1, lon1, lat2, lon2):
    R = 6371.0
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    delta_phi = math.radians(lat2 - lat1)
    delta_lambda = math.radians(lon2 - lon1)

    a = math.sin(delta_phi / 2.0)**2 + \
        math.cos(phi1) * math.cos(phi2) * math.sin(delta_lambda / 2.0)**2
    c = 2.0 * math.atan2(math.sqrt(a), math.sqrt(max(0.0, 1.0 - a)))
    return R * c

# ==============================================================================
# 3. THUẬT TOÁN BM25 CHUẨN HÓA (ĐỒNG NHẤT 100% VỚI Bm25RelevanceScorer.cs)
# ==============================================================================
def tokenize(text):
    if not text:
        return []
    # NFD normalization, lower, replace đ -> d, remove combining marks, regex [a-z0-9]+
    text_nfd = unicodedata.normalize('NFD', text).lower().replace('đ', 'd').replace('Đ', 'd')
    clean = ''.join(c for c in text_nfd if unicodedata.category(c) != 'Mn')
    tokens = re.findall(r'[a-z0-9]+', clean)
    return tokens

def compute_bm25_scores(query, candidates):
    N = len(candidates)
    if N == 0:
        return {}

    K1 = 1.5
    B = 0.75

    docs = []
    doc_lengths = []
    term_doc_freq = {}

    for c in candidates:
        cat_clean = c['category'].replace('_', ' ') if c.get('category') else ''
        doc_text = f"{c['name']} {c['address']} {cat_clean}"
        tokens = tokenize(doc_text)
        docs.append(tokens)
        doc_lengths.append(len(tokens))

        unique_tokens = set(tokens)
        for t in unique_tokens:
            term_doc_freq[t] = term_doc_freq.get(t, 0) + 1

    avg_doc_len = max(sum(doc_lengths) / N, 1e-9)

    query_tokens = tokenize(query)
    idf_map = {}
    for q_term in query_tokens:
        if q_term not in idf_map:
            n = term_doc_freq.get(q_term, 0)
            idf = math.log(1.0 + (N - n + 0.5) / (n + 0.5))
            idf_map[q_term] = max(0.0, idf)

    raw_scores = []
    for i in range(N):
        doc_tokens = docs[i]
        tf = {}
        for t in doc_tokens:
            tf[t] = tf.get(t, 0) + 1

        score = 0.0
        doc_len = doc_lengths[i]
        for q_term in query_tokens:
            if q_term in tf:
                f = tf[q_term]
                idf = idf_map[q_term]
                denom = f + K1 * (1.0 - B + B * doc_len / avg_doc_len)
                score += idf * f * (K1 + 1.0) / max(denom, 1e-9)
        raw_scores.append(score)

    max_score = max(raw_scores) if raw_scores else 0.0
    result = {}
    for i in range(N):
        norm = (raw_scores[i] / max_score) if max_score > 0 else 0.0
        result[candidates[i]['id']] = round(norm, 6)
    return result

# ==============================================================================
# 4. THUẬT TOÁN XẾP HẠNG ĐA TIÊU CHÍ CRADIS-DN4 (CradisRanker.cs)
# ==============================================================================
def cradis_dn4_rank(alternatives, criteria):
    m = len(alternatives)
    n = len(criteria)
    if m == 0:
        return []
    if m == 1:
        return [(alternatives[0][0], 1.0, [1.0]*n)]

    weights = [max(0.0, c['weight']) for c in criteria]
    w_sum = sum(weights)
    norm_w = [w / w_sum if w_sum > 0 else 1.0/n for w in weights]

    # Bước 2: Chuẩn hóa dữ liệu bằng phương pháp DN4 (Vector normalization)
    norm_mat = [[0.0]*n for _ in range(m)]
    for j in range(n):
        sum_sq = sum(alternatives[i][1][j]**2 for i in range(m))
        denom = math.sqrt(sum_sq)
        is_benefit = criteria[j]['is_benefit']
        for i in range(m):
            val = alternatives[i][1][j]
            if denom <= 0:
                norm_mat[i][j] = 0.0 if is_benefit else 1.0
            else:
                norm_mat[i][j] = (val / denom) if is_benefit else (1.0 - (val / denom))

    # Bước 3: Ma trận chuẩn hóa có trọng số
    v_mat = [[norm_mat[i][j] * norm_w[j] for j in range(n)] for i in range(m)]

    # Bước 4: Xác định giải pháp lý tưởng (ideal) và phản lý tưởng (anti-ideal)
    ideal_best = [max(v_mat[i][j] for i in range(m)) for j in range(n)]
    anti_worst = [min(v_mat[i][j] for i in range(m)) for j in range(n)]

    # Bước 5 & 6: Độ lệch và tổng khoảng cách
    s_plus = [sum(max(0.0, ideal_best[j] - v_mat[i][j]) for j in range(n)) for i in range(m)]
    s_minus = [sum(max(0.0, v_mat[i][j] - anti_worst[j]) for j in range(n)) for i in range(m)]

    # Bước 7: S0+ = min(S+) và S0- = max(S-)
    s0_plus = min(s_plus)
    s0_minus = max(s_minus)

    # Bước 8: Độ thỏa dụng tương đối K+ và K-, tính điểm thỏa hiệp Q_i
    scores = []
    for i in range(m):
        k_plus = (s0_plus / s_plus[i]) if s_plus[i] > 0 else 1.0
        k_minus = (s_minus[i] / s0_minus) if s0_minus > 0 else 1.0
        q_i = (k_plus + k_minus) / 2.0
        scores.append((alternatives[i][0], q_i, norm_mat[i]))
    return scores

# ==============================================================================
# 5. GỌI OSRM TABLE API (CÓ HEADER USER-AGENT) ĐỂ LẤY MA TRẬN MẠNG ĐƯỜNG
# ==============================================================================
def get_osrm_table(sources, destinations, timeout_sec=5):
    """
    sources: list of (lat, lon)
    destinations: list of (lat, lon)
    Trả về: (distances_km_matrix, durations_min_matrix)
    """
    try:
        all_coords = [f"{lon:.6f},{lat:.6f}" for lat, lon in sources] + \
                     [f"{lon:.6f},{lat:.6f}" for lat, lon in destinations]
        coords_str = ";".join(all_coords)
        s_idx = ";".join(str(i) for i in range(len(sources)))
        d_idx = ";".join(str(i) for i in range(len(sources), len(all_coords)))

        url = f"http://router.project-osrm.org/table/v1/driving/{coords_str}?sources={s_idx}&destinations={d_idx}&annotations=duration,distance"
        req = urllib.request.Request(url, headers={"User-Agent": "losm-backend/1.0"})
        with urllib.request.urlopen(req, timeout=timeout_sec) as resp:
            data = json.loads(resp.read().decode())
            if data.get("code") != "Ok":
                return None, None
            dists = [[round((d or 999000.0) / 1000.0, 2) for d in row] for row in data["distances"]]
            durs = [[round((t or 59940.0) / 60.0, 1) for t in row] for row in data["durations"]]
            return dists, durs
    except Exception as e:
        return None, None

# ==============================================================================
# 6. THUẬT TOÁN LỌC ỨNG VIÊN BÁN KÍNH (SPATIAL SEARCH)
# ==============================================================================
def filter_candidates(all_places, query_text, centroid_lat, centroid_lon, radius_km, limit=50):
    query_tokens = tokenize(query_text)
    
    matches = []
    fallback_pool = []
    
    for p in all_places:
        d = haversine_km(centroid_lat, centroid_lon, p["latitude"], p["longitude"])
        if d <= radius_km:
            p_cand = dict(p)
            p_cand["distance_to_center_km"] = d
            
            # Kiểm tra khớp từ khóa
            p_text = f"{p['name']} {p['address']} {p['category']}".lower()
            matched = any(t in p_text for t in query_tokens) if query_tokens else True
            if matched:
                matches.append(p_cand)
            else:
                fallback_pool.append(p_cand)
                
    if len(matches) < limit:
        fallback_pool.sort(key=lambda x: x["distance_to_center_km"])
        matches.extend(fallback_pool[:(limit - len(matches))])
        
    matches.sort(key=lambda x: x["distance_to_center_km"])
    return matches[:limit]

# ==============================================================================
# 7. THỰC THI CHÍNH
# ==============================================================================
def main():
    print("=== BẮT ĐẦU CHẠY THỰC NGHIỆM ĐÁNH GIÁ VÀ SO SÁNH 3 PHƯƠNG PHÁP XẾP HẠNG ===")
    
    all_places = load_all_places()
    
    queries_file = "research/queries.csv"
    if not os.path.exists(queries_file):
        raise FileNotFoundError(f"Chưa tìm thấy {queries_file}. Hãy chạy 02_prepare_queries.py trước.")
        
    queries = []
    with open(queries_file, mode="r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for r in reader:
            queries.append(r)
            
    print(f"-> Đã đọc {len(queries)} truy vấn từ {queries_file}.")
    
    # Chuẩn bị cấu trúc lưu kết quả
    relevance_labels_rows = []
    ranking_comparison_rows = []
    travel_time_results_rows = []
    
    # Định nghĩa tiêu chí CRADIS-DN4 mặc định cho Meeting (Relevance=0.5, Duration=0.3, Fairness=0.2)
    cradis_criteria = [
        {"name": "Relevance", "is_benefit": True, "weight": 0.5},
        {"name": "TravelDuration", "is_benefit": False, "weight": 0.3},
        {"name": "TravelFairness", "is_benefit": False, "weight": 0.2}
    ]
    
    osrm_success_count = 0
    osrm_fallback_count = 0
    
    start_time_all = time.time()
    
    for q_idx, q in enumerate(queries, start=1):
        qid = q["query_id"]
        gid = q["group_id"]
        qtext = q["query_text"]
        origins = json.loads(q["origins_json"])
        P = len(origins)
        
        # 1. Tính tâm cụm (centroid) và bán kính tìm kiếm
        c_lat = sum(o["lat"] for o in origins) / P
        c_lon = sum(o["lon"] for o in origins) / P
        max_orig_dist = max(haversine_km(c_lat, c_lon, o["lat"], o["lon"]) for o in origins)
        search_radius = 5.0 + max_orig_dist
        
        # 2. Lọc ứng viên
        candidates = filter_candidates(all_places, qtext, c_lat, c_lon, search_radius, limit=50)
        K = len(candidates)
        if K == 0:
            print(f"[{qid}] CẢNH BÁO: Không tìm thấy ứng viên nào!")
            continue
            
        # 3. Tính điểm BM25 cho tập ứng viên
        bm25_map = compute_bm25_scores(qtext, candidates)
        
        # 4. Tính toán khoảng cách và thời gian di chuyển
        # Ma trận Haversine (tất cả các truy vấn)
        # Tốc độ đô thị mặc định theo nghiệp vụ backend: 25 km/h -> 2.4 phút / km
        hav_durations = [] # K x P
        hav_distances = [] # K x P
        
        for cand in candidates:
            c_dists = [haversine_km(o["lat"], o["lon"], cand["latitude"], cand["longitude"]) for o in origins]
            c_durs = [d / 25.0 * 60.0 for d in c_dists]
            hav_distances.append(c_dists)
            hav_durations.append(c_durs)
            
        # Ma trận mạng đường thực tế OSRM (đặc biệt cho nhóm 3 hoặc kiểm chứng)
        osrm_durs_matrix = None
        osrm_dists_matrix = None
        
        if gid == "G3":
            sources = [(o["lat"], o["lon"]) for o in origins]
            dests = [(c["latitude"], c["longitude"]) for c in candidates]
            o_dists, o_durs = get_osrm_table(sources, dests, timeout_sec=5)
            if o_durs is not None and len(o_durs) == P and len(o_durs[0]) == K:
                osrm_success_count += 1
                # Chuyển vị thành K x P (từng candidate một hàng, các origins là các cột)
                osrm_durs_matrix = [[o_durs[p_idx][k_idx] for p_idx in range(P)] for k_idx in range(K)]
                osrm_dists_matrix = [[o_dists[p_idx][k_idx] for p_idx in range(P)] for k_idx in range(K)]
            else:
                osrm_fallback_count += 1
                
        # Dùng OSRM nếu có, ngược lại dùng Haversine
        active_durs_matrix = osrm_durs_matrix if osrm_durs_matrix is not None else hav_durations
        active_routing_type = "OSRM_Road" if osrm_durs_matrix is not None else "Haversine_25kmh"
        
        # 5. Xây dựng tập đánh giá cho 3 phương pháp xếp hạng:
        cand_eval_data = []
        for i, c in enumerate(candidates):
            bm25 = bm25_map.get(c["id"], 0.0)
            durs = active_durs_matrix[i]
            d_avg = sum(durs) / P
            d_max = max(durs)
            d_min = min(durs)
            d_spread = d_max - d_min
            
            # Khoảng cách địa lý trung bình
            dists = hav_distances[i]
            dist_avg = sum(dists) / P
            
            # Điểm Phương pháp 1: Pure BM25
            score_m1 = bm25
            
            # Điểm Phương pháp 2: BM25 + Distance Heuristic (Linear combination: 2/3 BM25 + 1/3 DistanceScore)
            dist_score = max(0.0, 1.0 - (dist_avg / search_radius))
            score_m2 = (2.0 / 3.0) * bm25 + (1.0 / 3.0) * dist_score
            
            cand_eval_data.append({
                "candidate": c,
                "bm25": bm25,
                "d_avg": d_avg,
                "d_max": d_max,
                "d_spread": d_spread,
                "dist_avg": dist_avg,
                "durs_by_origin": durs,
                "hav_durs_by_origin": hav_durations[i],
                "score_m1": score_m1,
                "score_m2": score_m2
            })
            
        # Tính toán Phương pháp 3: BM25 + CRADIS-DN4
        cradis_inputs = [
            (c_data, [c_data["bm25"], c_data["d_avg"], c_data["d_spread"]])
            for c_data in cand_eval_data
        ]
        cradis_results = cradis_dn4_rank(cradis_inputs, cradis_criteria)
        for c_data, q_score, norm_v in cradis_results:
            c_data["score_m3"] = q_score
            
        # 6. Sắp xếp kết quả theo từng phương pháp (Lấy Top 5)
        # M1: Pure BM25
        top5_m1 = sorted(cand_eval_data, key=lambda x: (x["score_m1"], -x["d_avg"], x["candidate"]["id"]), reverse=True)[:5]
        # M2: BM25 + Distance
        top5_m2 = sorted(cand_eval_data, key=lambda x: (x["score_m2"], -x["d_avg"], x["candidate"]["id"]), reverse=True)[:5]
        # M3: BM25 + CRADIS-DN4
        top5_m3 = sorted(cand_eval_data, key=lambda x: (x["score_m3"], -x["d_avg"], x["candidate"]["id"]), reverse=True)[:5]
        
        # 7. Ghi nhận dữ liệu vào bảng so sánh tổng hợp (ranking_comparison.csv)
        methods = [
            ("M1_Pure_BM25", top5_m1),
            ("M2_BM25_Distance", top5_m2),
            ("M3_CRADIS_DN4", top5_m3)
        ]
        
        for m_name, top_list in methods:
            top1 = top_list[0]
            # Trung bình Top 5
            top5_avg_dur = sum(x["d_avg"] for x in top_list) / len(top_list)
            top5_max_dur = sum(x["d_max"] for x in top_list) / len(top_list)
            top5_spread = sum(x["d_spread"] for x in top_list) / len(top_list)
            top5_bm25 = sum(x["bm25"] for x in top_list) / len(top_list)
            
            ranking_comparison_rows.append({
                "query_id": qid,
                "group_id": gid,
                "method": m_name,
                "candidate_pool_size": K,
                "routing_source": active_routing_type,
                "top1_place_id": top1["candidate"]["id"],
                "top1_place_name": top1["candidate"]["name"],
                "top1_category": top1["candidate"]["category"],
                "top1_bm25_score": round(top1["bm25"], 4),
                "top1_duration_avg_min": round(top1["d_avg"], 2),
                "top1_duration_max_min": round(top1["d_max"], 2),
                "top1_duration_spread_min": round(top1["d_spread"], 2),
                "top5_duration_avg_mean": round(top5_avg_dur, 2),
                "top5_duration_max_mean": round(top5_max_dur, 2),
                "top5_duration_spread_mean": round(top5_spread, 2),
                "top5_bm25_mean": round(top5_bm25, 4)
            })
            
            # Ghi nhận vào bảng nhãn đánh giá (relevance_labels.csv)
            for rank_idx, item in enumerate(top_list, start=1):
                relevance_labels_rows.append({
                    "query_id": qid,
                    "group_id": gid,
                    "query_text": qtext,
                    "method": m_name,
                    "rank_position": rank_idx,
                    "place_id": item["candidate"]["id"],
                    "place_name": item["candidate"]["name"],
                    "place_address": item["candidate"]["address"],
                    "place_category": item["candidate"]["category"],
                    "bm25_score": round(item["bm25"], 4),
                    "duration_avg_min": round(item["d_avg"], 2),
                    "duration_max_min": round(item["d_max"], 2),
                    "duration_spread_min": round(item["d_spread"], 2),
                    "relevance_label": "", # Để trống cho người đánh giá độc lập gán nhãn 0-3
                    "evaluator_guideline": "0=KhongPhuHop, 1=TamChapNhan, 2=PhuHop, 3=RatPhuHop",
                    "notes": ""
                })

        # 8. Ghi nhận dữ liệu thời gian di chuyển chi tiết cho các truy vấn nhóm 3 (travel_time_results.csv)
        if gid == "G3":
            for m_name, top_list in methods:
                top1 = top_list[0]
                row_dict = {
                    "query_id": qid,
                    "query_text": qtext,
                    "method": m_name,
                    "routing_type": active_routing_type,
                    "proposed_place_name": top1["candidate"]["name"],
                    "participant_count": P,
                    "duration_avg_min": round(top1["d_avg"], 2),
                    "duration_max_min": round(top1["d_max"], 2),
                    "duration_spread_min": round(top1["d_spread"], 2),
                    "member1_duration_min": round(top1["durs_by_origin"][0], 2),
                    "member2_duration_min": round(top1["durs_by_origin"][1], 2),
                    "member3_duration_min": round(top1["durs_by_origin"][2], 2) if P >= 3 else "",
                    "member4_duration_min": round(top1["durs_by_origin"][3], 2) if P >= 4 else "",
                    "haversine_avg_min": round(sum(top1["hav_durs_by_origin"]) / P, 2),
                    "haversine_spread_min": round(max(top1["hav_durs_by_origin"]) - min(top1["hav_durs_by_origin"]), 2)
                }
                travel_time_results_rows.append(row_dict)

        if q_idx % 15 == 0 or q_idx == len(queries):
            print(f"   [Tiến độ: {q_idx}/{len(queries)}] Đã xử lý xong truy vấn {qid}...")

    # ==============================================================================
    # 9. XUẤT CÁC FILE CSV KẾT QUẢ
    # ==============================================================================
    print("-> Đang ghi các file kết quả thực nghiệm ra thư mục research/ ...")
    
    # 1. ranking_comparison.csv
    with open("research/ranking_comparison.csv", mode="w", newline="", encoding="utf-8") as f:
        fieldnames = [
            "query_id", "group_id", "method", "candidate_pool_size", "routing_source",
            "top1_place_id", "top1_place_name", "top1_category", "top1_bm25_score",
            "top1_duration_avg_min", "top1_duration_max_min", "top1_duration_spread_min",
            "top5_duration_avg_mean", "top5_duration_max_mean", "top5_duration_spread_mean", "top5_bm25_mean"
        ]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(ranking_comparison_rows)
    print("   -> research/ranking_comparison.csv")

    # 2. relevance_labels.csv
    with open("research/relevance_labels.csv", mode="w", newline="", encoding="utf-8") as f:
        fieldnames = [
            "query_id", "group_id", "query_text", "method", "rank_position",
            "place_id", "place_name", "place_address", "place_category",
            "bm25_score", "duration_avg_min", "duration_max_min", "duration_spread_min",
            "relevance_label", "evaluator_guideline", "notes"
        ]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(relevance_labels_rows)
    print("   -> research/relevance_labels.csv")

    # 3. travel_time_results.csv
    with open("research/travel_time_results.csv", mode="w", newline="", encoding="utf-8") as f:
        fieldnames = [
            "query_id", "query_text", "method", "routing_type", "proposed_place_name",
            "participant_count", "duration_avg_min", "duration_max_min", "duration_spread_min",
            "member1_duration_min", "member2_duration_min", "member3_duration_min", "member4_duration_min",
            "haversine_avg_min", "haversine_spread_min"
        ]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(travel_time_results_rows)
    print("   -> research/travel_time_results.csv")

    # 4. retrieval_metrics.csv
    with open("research/retrieval_metrics.csv", mode="w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["Metric", "Definition", "Formula", "Evaluation_Status", "Notes"])
        writer.writerow([
            "Precision@5",
            "Tỷ lệ địa điểm thực sự phù hợp trong top 5 kết quả",
            "P@5 = (1/5) * sum_{i=1}^5 I(label_i >= 2)",
            "Cho_nguoi_danh_gia_doc_lap_gan_nhan",
            "Quy tắc nhị phân hóa: Nhãn 2 (Phù hợp) và 3 (Rất phù hợp) tính là 1; Nhãn 0 và 1 tính là 0"
        ])
        writer.writerow([
            "nDCG@5",
            "Độ lợi tích lũy giảm dần chuẩn hóa tại thứ hạng 5",
            "DCG@5 = sum_{i=1}^5 (2^{rel_i} - 1) / log2(i + 1); nDCG@5 = DCG@5 / IDCG@5",
            "Cho_nguoi_danh_gia_doc_lap_gan_nhan",
            "Thang đo độ liên quan 4 mức (0 đến 3), phản ánh thứ tự ưu tiên tối ưu của danh sách gợi ý"
        ])
        writer.writerow([
            "Mean_Travel_Duration_Tavg",
            "Thời gian di chuyển trung bình của các thành viên (phút)",
            "T_avg = (1/P) * sum_{p=1}^P t_{p, place}",
            "Da_tinh_toan_thuc_te",
            "Đo trên ma trận OSRM mạng đường thực tế và mô hình vận tốc đô thị Haversine 25km/h"
        ])
        writer.writerow([
            "Max_Travel_Duration_Tmax",
            "Thời gian di chuyển của người đi lâu nhất (phút)",
            "T_max = max_{p} t_{p, place}",
            "Da_tinh_toan_thuc_te",
            "Chỉ số thắt cổ chai hành trình trong cuộc hẹn"
        ])
        writer.writerow([
            "Travel_Time_Spread_DeltaT",
            "Độ chênh lệch thời gian giữa người đi lâu nhất và nhanh nhất (phút)",
            "Delta_T = max_{p} t_{p, place} - min_{p} t_{p, place}",
            "Da_tinh_toan_thuc_te",
            "Chỉ số công bằng hành trình (Fairness) cốt lõi của bài báo"
        ])
    print("   -> research/retrieval_metrics.csv")

    elapsed = time.time() - start_time_all
    print(f"\n=== HOÀN TẤT THỰC NGHIỆM TRONG {elapsed:.2f} GIÂY ===")
    print(f"   OSRM Road Network API: {osrm_success_count} lần thành công, {osrm_fallback_count} lần dự phòng Haversine.")

if __name__ == "__main__":
    main()
