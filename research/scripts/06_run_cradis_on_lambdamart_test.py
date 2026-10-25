#!/usr/bin/env python3
"""
06_run_cradis_on_lambdamart_test.py
Thực thi kiểm chứng thuật toán CRADIS-DN4 trên đúng 15 truy vấn test của bộ dữ liệu LambdaMART:
- Sử dụng cùng tập ứng viên (20 POI/query, tổng 300 POI) và cùng nhãn đánh giá thực tế (relevance 0..3).
- Tính toán nDCG@5, nDCG@10, Precision@5 (ngưỡng relevance >= 2) và Average Precision (AP).
- Đối chiếu với các phương pháp đã có trong bộ dữ liệu LambdaMART:
  + Ngẫu nhiên (Random)
  + BM25 thuần túy (Pure BM25)
  + Baseline Heuristic mặc định (15/10)
  + Baseline Heuristic đã chỉnh thang (3/3)
  + CRADIS-DN4 (BM25)
  + CRADIS-DN4 (+ CategoryMatch)
  + Mô hình LambdaMART (LightGBM Ranker full 10 features)
- Xuất file:
  - research/test_metrics_comparison.csv
  - research/cradis_dn4_test_predictions.csv
  - research/ablation_analysis.csv
"""

import csv
import json
import math
import os

# ==============================================================================
# 1. THUẬT TOÁN CRADIS-DN4 (ĐỒNG NHẤT 100% CỦA BACKEND CradisRanker.cs)
# ==============================================================================
def cradis_dn4_scores(items, criteria):
    m = len(items)
    n = len(criteria)
    if m == 0:
        return []
    if m == 1:
        return [1.0]

    weights = [max(0.0, c["weight"]) for c in criteria]
    w_sum = sum(weights)
    norm_w = [w / w_sum if w_sum > 0 else 1.0 / n for w in weights]

    # Bước 2: Chuẩn hóa DN4 (Vector normalization)
    norm_mat = [[0.0] * n for _ in range(m)]
    for j in range(n):
        sum_sq = sum(items[i][j] ** 2 for i in range(m))
        denom = math.sqrt(sum_sq)
        is_benefit = criteria[j]["is_benefit"]
        for i in range(m):
            val = items[i][j]
            if denom <= 0:
                norm_mat[i][j] = 0.0 if is_benefit else 1.0
            else:
                norm_mat[i][j] = (val / denom) if is_benefit else (1.0 - (val / denom))

    # Bước 3: Ma trận chuẩn hóa có trọng số
    v_mat = [[norm_mat[i][j] * norm_w[j] for j in range(n)] for i in range(m)]

    # Bước 4: Xác định giải pháp lý tưởng (ideal) và phản lý tưởng (anti-ideal)
    ideal_best = [max(v_mat[i][j] for i in range(m)) for j in range(n)]
    anti_worst = [min(v_mat[i][j] for i in range(m)) for j in range(n)]

    # Bước 5 & 6: Khoảng cách tích lũy
    s_plus = [sum(max(0.0, ideal_best[j] - v_mat[i][j]) for j in range(n)) for i in range(m)]
    s_minus = [sum(max(0.0, v_mat[i][j] - anti_worst[j]) for j in range(n)) for i in range(m)]

    # Bước 7: S0+ = min(S+) và S0- = max(S-)
    s0_plus = min(s_plus)
    s0_minus = max(s_minus)

    # Bước 8: Độ thỏa dụng tương đối K+, K- và điểm thỏa hiệp Q_i
    scores = []
    for i in range(m):
        k_plus = (s0_plus / s_plus[i]) if s_plus[i] > 0 else 1.0
        k_minus = (s_minus[i] / s0_minus) if s0_minus > 0 else 1.0
        q_i = (k_plus + k_minus) / 2.0
        scores.append(q_i)
    return scores

# ==============================================================================
# 2. CÁC HÀM TÍNH TOÁN METRIC CHUẨN QUỐC TẾ (TREC / MS MARCO)
# ==============================================================================
def dcg_at_k(relevances, k=5):
    return sum((2.0 ** r - 1.0) / math.log2(i + 2) for i, r in enumerate(relevances[:k]))

def ndcg_at_k(relevances, k=5):
    dcg = dcg_at_k(relevances, k)
    idcg = dcg_at_k(sorted(relevances, reverse=True), k)
    return (dcg / idcg) if idcg > 0 else 0.0

def precision_at_k(relevances, k=5, threshold=2):
    top = relevances[:k]
    return sum(1 for r in top if r >= threshold) / k

def average_precision(relevances, threshold=2):
    hits = 0
    sum_precs = 0.0
    for i, r in enumerate(relevances):
        if r >= threshold:
            hits += 1
            sum_precs += hits / (i + 1)
    return sum_precs / hits if hits > 0 else 0.0

# ==============================================================================
# 3. THỰC THI CHÍNH
# ==============================================================================
def main():
    data_dir = "research/data/lambdamart"
    test_pred_path = os.path.join(data_dir, "test_predictions.csv")
    test_metrics_path = os.path.join(data_dir, "test_metrics.csv")
    ablation_path = os.path.join(data_dir, "ablation_results.csv")

    if not os.path.exists(test_pred_path):
        raise FileNotFoundError(f"Không tìm thấy {test_pred_path}")

    print("=== ĐỌC DỮ LIỆU TEST TỪ LAMBDAMART DATASET ===")
    with open(test_pred_path, mode="r", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    # Nhóm theo query_id
    queries = {}
    for r in rows:
        qid = r.get("\ufeffquery_id") or r.get("query_id")
        if qid not in queries:
            queries[qid] = []
        queries[qid].append(r)

    print(f"-> Đã nạp {len(rows)} bản ghi ứng viên thuộc {len(queries)} truy vấn kiểm thử (Test Split).")
    print(f"   Mỗi truy vấn có đúng {len(next(iter(queries.values())))} ứng viên.")

    # 1. Chạy CRADIS-DN4
    # Tiêu chí: Relevance (0.5), Duration (0.3), Fairness (0.2)
    cradis_criteria = [
        {"name": "Relevance", "is_benefit": True, "weight": 0.5},
        {"name": "TravelDuration", "is_benefit": False, "weight": 0.3},
        {"name": "TravelFairness", "is_benefit": False, "weight": 0.2}
    ]

    cradis_pred_rows = []

    # Danh sách metric cho từng phương pháp trên 15 queries
    methods_eval = {
        "CRADIS-DN4 (BM25)": {"ndcg5": [], "ndcg10": [], "p5": [], "ap": []},
        "CRADIS-DN4 (+CatMatch)": {"ndcg5": [], "ndcg10": [], "p5": [], "ap": []}
    }

    for qid, cands in queries.items():
        # --- Cấu hình 1: CRADIS-DN4 với BM25 thuần túy ---
        mat_bm = [
            [float(c["bm25_norm"]), float(c["duration_avg_min"]), float(c["duration_spread_min"])]
            for c in cands
        ]
        scores_bm = cradis_dn4_scores(mat_bm, cradis_criteria)

        # --- Cấu hình 2: CRADIS-DN4 với max(BM25, CategoryMatch) theo backend SearchMeetingPlacesHandler ---
        mat_cat = [
            [max(float(c["bm25_norm"]), float(c["category_match"])), float(c["duration_avg_min"]), float(c["duration_spread_min"])]
            for c in cands
        ]
        scores_cat = cradis_dn4_scores(mat_cat, cradis_criteria)

        for c, s_bm, s_cat in zip(cands, scores_bm, scores_cat):
            c["score_cradis_bm25"] = s_bm
            c["score_cradis_catmatch"] = s_cat

        # Sắp xếp theo quy tắc backend: score desc, dur_avg asc, dur_spread asc, bm25 desc, poi_id asc
        sorted_bm = sorted(cands, key=lambda x: (
            x["score_cradis_bm25"],
            -float(x["duration_avg_min"]),
            -float(x["duration_spread_min"]),
            float(x["bm25_norm"]),
            x["poi_id"]
        ), reverse=True)

        sorted_cat = sorted(cands, key=lambda x: (
            x["score_cradis_catmatch"],
            -float(x["duration_avg_min"]),
            -float(x["duration_spread_min"]),
            float(x["bm25_norm"]),
            x["poi_id"]
        ), reverse=True)

        # Tính metrics cho Config 1
        rels_bm = [int(x["relevance"]) for x in sorted_bm]
        methods_eval["CRADIS-DN4 (BM25)"]["ndcg5"].append(ndcg_at_k(rels_bm, 5))
        methods_eval["CRADIS-DN4 (BM25)"]["ndcg10"].append(ndcg_at_k(rels_bm, 10))
        methods_eval["CRADIS-DN4 (BM25)"]["p5"].append(precision_at_k(rels_bm, 5, threshold=2))
        methods_eval["CRADIS-DN4 (BM25)"]["ap"].append(average_precision(rels_bm, threshold=2))

        # Tính metrics cho Config 2
        rels_cat = [int(x["relevance"]) for x in sorted_cat]
        methods_eval["CRADIS-DN4 (+CatMatch)"]["ndcg5"].append(ndcg_at_k(rels_cat, 5))
        methods_eval["CRADIS-DN4 (+CatMatch)"]["ndcg10"].append(ndcg_at_k(rels_cat, 10))
        methods_eval["CRADIS-DN4 (+CatMatch)"]["p5"].append(precision_at_k(rels_cat, 5, threshold=2))
        methods_eval["CRADIS-DN4 (+CatMatch)"]["ap"].append(average_precision(rels_cat, threshold=2))

        # Lưu chi tiết dự đoán
        for rank_idx, c in enumerate(sorted_cat, start=1):
            cradis_pred_rows.append({
                "query_id": qid,
                "poi_id": c["poi_id"],
                "name": c["name"],
                "category_raw": c["category_raw"],
                "category_match": c["category_match"],
                "relevance": c["relevance"],
                "bm25_norm": round(float(c["bm25_norm"]), 4),
                "duration_avg_min": round(float(c["duration_avg_min"]), 2),
                "duration_spread_min": round(float(c["duration_spread_min"]), 2),
                "score_cradis_bm25": round(c["score_cradis_bm25"], 6),
                "score_cradis_catmatch": round(c["score_cradis_catmatch"], 6),
                "rank_cradis_catmatch": rank_idx
            })

    # Xuất file cradis_dn4_test_predictions.csv
    cradis_pred_out = "research/cradis_dn4_test_predictions.csv"
    with open(cradis_pred_out, mode="w", newline="", encoding="utf-8") as f:
        fieldnames = [
            "query_id", "poi_id", "name", "category_raw", "category_match", "relevance",
            "bm25_norm", "duration_avg_min", "duration_spread_min",
            "score_cradis_bm25", "score_cradis_catmatch", "rank_cradis_catmatch"
        ]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(cradis_pred_rows)
    print(f"-> Đã lưu bảng dự đoán chi tiết: {cradis_pred_out}")

    # Đọc kết quả test_metrics.csv có sẵn
    existing_metrics = []
    with open(test_metrics_path, mode="r", encoding="utf-8") as f:
        reader = csv.reader(f)
        header = next(reader)
        for row in reader:
            if row:
                existing_metrics.append({
                    "method": row[0].strip(),
                    "ndcg5": float(row[1]),
                    "ndcg10": float(row[2]),
                    "p5": float(row[3]),
                    "ap": float(row[4])
                })

    # Tổng hợp toàn bộ các phương pháp vào bảng đối chiếu hoàn chỉnh
    comparison_table = []
    # 1. Ngẫu nhiên
    m_rand = next((m for m in existing_metrics if "ngẫu nhiên" in m["method"]), None)
    if m_rand: comparison_table.append(m_rand)

    # 2. Chỉ BM25
    m_bm = next((m for m in existing_metrics if "chỉ BM25" in m["method"]), None)
    if m_bm: comparison_table.append(m_bm)

    # 3. Baseline mặc định
    m_base1 = next((m for m in existing_metrics if "baseline 15/10" in m["method"]), None)
    if m_base1: comparison_table.append(m_base1)

    # 4. Baseline đã chỉnh thang
    m_base2 = next((m for m in existing_metrics if "baseline đã chỉnh thang" in m["method"]), None)
    if m_base2: comparison_table.append(m_base2)

    # 5. CRADIS-DN4 (BM25)
    c1 = methods_eval["CRADIS-DN4 (BM25)"]
    comparison_table.append({
        "method": "CRADIS-DN4 (BM25)",
        "ndcg5": round(sum(c1["ndcg5"]) / len(c1["ndcg5"]), 3),
        "ndcg10": round(sum(c1["ndcg10"]) / len(c1["ndcg10"]), 3),
        "p5": round(sum(c1["p5"]) / len(c1["p5"]), 3),
        "ap": round(sum(c1["ap"]) / len(c1["ap"]), 3)
    })

    # 6. CRADIS-DN4 (+CatMatch)
    c2 = methods_eval["CRADIS-DN4 (+CatMatch)"]
    comparison_table.append({
        "method": "CRADIS-DN4 (+ CategoryMatch)",
        "ndcg5": round(sum(c2["ndcg5"]) / len(c2["ndcg5"]), 3),
        "ndcg10": round(sum(c2["ndcg10"]) / len(c2["ndcg10"]), 3),
        "p5": round(sum(c2["p5"]) / len(c2["p5"]), 3),
        "ap": round(sum(c2["ap"]) / len(c2["ap"]), 3)
    })

    # 7. LambdaMART
    m_lmart = next((m for m in existing_metrics if "LambdaMART" in m["method"]), None)
    if m_lmart: comparison_table.append(m_lmart)

    # Xuất ra research/test_metrics_comparison.csv
    metrics_out = "research/test_metrics_comparison.csv"
    with open(metrics_out, mode="w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["method", "ndcg@5", "ndcg@10", "p@5", "ap"])
        for row in comparison_table:
            writer.writerow([row["method"], f"{row['ndcg5']:.3f}", f"{row['ndcg10']:.3f}", f"{row['p5']:.3f}", f"{row['ap']:.3f}"])
    print(f"-> Đã lưu bảng so sánh thực nghiệm tổng hợp: {metrics_out}")

    # 2. Xử lý Ablation Results
    ablation_rows = []
    with open(ablation_path, mode="r", encoding="utf-8") as f:
        reader = csv.reader(f)
        head = next(reader)
        base_score = None
        for r in reader:
            if not r: continue
            removed = r[0].strip()
            nf = int(r[1])
            sc = float(r[2])
            m_sc = float(r[3])
            if base_score is None:
                base_score = sc
                delta = 0.0
                pct = 0.0
            else:
                delta = sc - base_score
                pct = (delta / base_score) * 100

            feature_desc = {
                "(không bỏ gì)": "Mô hình đầy đủ 10 đặc trưng",
                "category_match": "Độ khớp nhóm danh mục AI",
                "bm25_norm": "Điểm liên quan nội dung BM25",
                "thời gian (avg/max/spread)": "Nhóm thời gian lái xe OSRM (avg, max, spread)",
                "khoảng cách (km)": "Nhóm cự ly OSRM (avg, max)",
                "rank trong query": "Thứ hạng chuẩn hóa thời gian trong query (avg_rank, spread_rank)",
                "participant_count": "Số lượng thành viên cuộc hẹn (2..5 người)"
            }.get(removed, "")

            ablation_rows.append({
                "feature_removed": removed,
                "n_features": nf,
                "ndcg5": sc,
                "delta_ndcg5": round(delta, 4),
                "drop_percent": round(pct, 2),
                "feature_description": feature_desc
            })

    ablation_out = "research/ablation_analysis.csv"
    with open(ablation_out, mode="w", newline="", encoding="utf-8") as f:
        fieldnames = ["feature_removed", "n_features", "ndcg5", "delta_ndcg5", "drop_percent", "feature_description"]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(ablation_rows)
    print(f"-> Đã lưu bảng phân tích Ablation Study: {ablation_out}")

    print("\n=== BẢNG SO SÁNH KẾT QUẢ THỰC NGHIỆM TRÊN 15 TRUY VẤN TEST ===")
    print(f"{'Phương pháp':<32} | {'nDCG@5':<8} | {'nDCG@10':<8} | {'P@5':<8} | {'AP':<8}")
    print("-" * 75)
    for r in comparison_table:
        print(f"{r['method']:<32} | {r['ndcg5']:<8.3f} | {r['ndcg10']:<8.3f} | {r['p5']:<8.3f} | {r['ap']:<8.3f}")

if __name__ == "__main__":
    main()
