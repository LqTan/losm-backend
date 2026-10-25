#!/usr/bin/env python3
"""
04_latency_benchmark.py
Đánh giá độ trễ truy vấn (Latency Benchmark) theo yêu cầu mục 6 của bài báo khoa học:
- Chạy tối thiểu 30 lần đo cho mỗi kịch bản (sau bước khởi động warm-up)
- Đo chi tiết từng công đoạn độc lập và toàn trình:
  1. Truy xuất địa điểm ứng viên từ DB (Candidate Retrieval)
  2. Tính điểm BM25 cho tập ứng viên (BM25 Relevance Scoring)
  3. Tính ma trận hành trình Haversine (Haversine Travel Matrix)
  4. Tính ma trận hành trình OSRM (OSRM Road Network API Latency)
  5. Xếp hạng CRADIS-DN4 (CRADIS-DN4 Multi-Criteria Ranking)
  6. Toàn trình End-to-End API (HTTP POST /api/search/meeting-places)
- Tính toán: Min, Mean, Median (p50), 95th Percentile (p95), Max, Std Dev (đơn vị: ms)
- Xuất file: research/latency_results.csv
"""

import csv
import json
import math
import os
import subprocess
import time
import urllib.request
import statistics

# Nạp các hàm tính toán từ 03_run_eval_and_rankings
from importlib.machinery import SourceFileLoader
eval_module = SourceFileLoader("eval_module", "research/scripts/03_run_eval_and_rankings.py").load_module()

def percentile(data, p):
    sorted_data = sorted(data)
    k = (len(sorted_data) - 1) * (p / 100.0)
    f = math.floor(k)
    c = math.ceil(k)
    if f == c:
        return sorted_data[int(k)]
    d0 = sorted_data[int(f)] * (c - k)
    d1 = sorted_data[int(c)] * (k - f)
    return d0 + d1

def main():
    print("=== BẮT ĐẦU BENCHMARK ĐỘ TRỄ HỆ THỐNG (35 LƯỢT ĐO CHO MỖI CÔNG ĐOẠN) ===")
    
    # 0. Khởi động / Warm-up
    print("-> Khởi động dữ liệu và warm-up...")
    all_places = eval_module.load_all_places()
    
    # Kịch bản truy vấn chuẩn cho benchmark (Cuộc hẹn 3 người tại Quận 1)
    sample_query = "cafe họp nhóm yên tĩnh"
    sample_origins = [
        {"name": "Alice", "lat": 10.7725, "lon": 106.6980}, # Chợ Bến Thành
        {"name": "Bob", "lat": 10.7798, "lon": 106.6990},   # Nhà thờ Đức Bà
        {"name": "Charlie", "lat": 10.7884, "lon": 106.6908} # Tân Định
    ]
    c_lat = sum(o["lat"] for o in sample_origins) / 3.0
    c_lon = sum(o["lon"] for o in sample_origins) / 3.0
    search_radius = 5.0 + max(eval_module.haversine_km(c_lat, c_lon, o["lat"], o["lon"]) for o in sample_origins)
    
    sample_candidates = eval_module.filter_candidates(all_places, sample_query, c_lat, c_lon, search_radius, limit=50)
    
    cradis_criteria = [
        {"name": "Relevance", "is_benefit": True, "weight": 0.5},
        {"name": "TravelDuration", "is_benefit": False, "weight": 0.3},
        {"name": "TravelFairness", "is_benefit": False, "weight": 0.2}
    ]
    
    NUM_RUNS = 35
    WARMUP_RUNS = 5
    
    results = {}
    
    # --------------------------------------------------------------------------
    # 1. Đo Candidate Retrieval (Lọc Bbox + Từ khóa trong 53,340 địa điểm)
    # --------------------------------------------------------------------------
    print("\n1. Đo thời gian: Candidate Retrieval (Lọc 50 ứng viên trong 53,340 POI)...")
    for _ in range(WARMUP_RUNS):
        _ = eval_module.filter_candidates(all_places, sample_query, c_lat, c_lon, search_radius, limit=50)
        
    latencies_retrieval = []
    for _ in range(NUM_RUNS):
        t0 = time.perf_counter()
        _ = eval_module.filter_candidates(all_places, sample_query, c_lat, c_lon, search_radius, limit=50)
        t1 = time.perf_counter()
        latencies_retrieval.append((t1 - t0) * 1000.0) # ms
    results["1_Candidate_Retrieval"] = latencies_retrieval
    print(f"   p50={percentile(latencies_retrieval, 50):.2f} ms | p95={percentile(latencies_retrieval, 95):.2f} ms")

    # --------------------------------------------------------------------------
    # 2. Đo BM25 Scoring (Tokenize NFD, Robertson IDF, TF-IDF trên 50 ứng viên)
    # --------------------------------------------------------------------------
    print("\n2. Đo thời gian: BM25 Relevance Scoring (Tính điểm trên 50 ứng viên)...")
    for _ in range(WARMUP_RUNS):
        _ = eval_module.compute_bm25_scores(sample_query, sample_candidates)
        
    latencies_bm25 = []
    for _ in range(NUM_RUNS):
        t0 = time.perf_counter()
        _ = eval_module.compute_bm25_scores(sample_query, sample_candidates)
        t1 = time.perf_counter()
        latencies_bm25.append((t1 - t0) * 1000.0)
    results["2_BM25_Scoring"] = latencies_bm25
    print(f"   p50={percentile(latencies_bm25, 50):.2f} ms | p95={percentile(latencies_bm25, 95):.2f} ms")

    # --------------------------------------------------------------------------
    # 3. Đo Ma trận hành trình Haversine (Geometric Matrix 3 nguồn x 50 đích)
    # --------------------------------------------------------------------------
    print("\n3. Đo thời gian: Haversine Travel Matrix (3 nguồn x 50 đích = 150 cặp)...")
    for _ in range(WARMUP_RUNS):
        _ = [[eval_module.haversine_km(o["lat"], o["lon"], c["latitude"], c["longitude"]) / 25.0 * 60.0 for o in sample_origins] for c in sample_candidates]
        
    latencies_hav = []
    for _ in range(NUM_RUNS):
        t0 = time.perf_counter()
        _ = [[eval_module.haversine_km(o["lat"], o["lon"], c["latitude"], c["longitude"]) / 25.0 * 60.0 for o in sample_origins] for c in sample_candidates]
        t1 = time.perf_counter()
        latencies_hav.append((t1 - t0) * 1000.0)
    results["3_Haversine_Travel_Matrix"] = latencies_hav
    print(f"   p50={percentile(latencies_hav, 50):.2f} ms | p95={percentile(latencies_hav, 95):.2f} ms")

    # --------------------------------------------------------------------------
    # 4. Đo Ma trận hành trình OSRM Road Network (Gọi HTTP API OSRM public)
    # --------------------------------------------------------------------------
    print("\n4. Đo thời gian: OSRM Road Network API (Gọi HTTP Table 3 nguồn x 50 đích)...")
    sources = [(o["lat"], o["lon"]) for o in sample_origins]
    dests = [(c["latitude"], c["longitude"]) for c in sample_candidates]
    
    # Warmup
    _ = eval_module.get_osrm_table(sources, dests, timeout_sec=6)
    
    latencies_osrm = []
    for i in range(NUM_RUNS):
        t0 = time.perf_counter()
        _, durs = eval_module.get_osrm_table(sources, dests, timeout_sec=6)
        t1 = time.perf_counter()
        elapsed_ms = (t1 - t0) * 1000.0
        if durs is not None:
            latencies_osrm.append(elapsed_ms)
        time.sleep(0.1) # Tránh rate limit từ server demo
    results["4_OSRM_Network_Table_API"] = latencies_osrm
    print(f"   p50={percentile(latencies_osrm, 50):.2f} ms | p95={percentile(latencies_osrm, 95):.2f} ms ({len(latencies_osrm)}/{NUM_RUNS} thành công)")

    # --------------------------------------------------------------------------
    # 5. Đo Thuật toán xếp hạng đa tiêu chí CRADIS-DN4 (50 ứng viên x 3 tiêu chí)
    # --------------------------------------------------------------------------
    print("\n5. Đo thời gian: CRADIS-DN4 Ranking (50 phương án x 3 tiêu chí)...")
    cradis_inputs = [
        (c, [1.0, 5.0 + i*0.1, 1.0 + i*0.05])
        for i, c in enumerate(sample_candidates)
    ]
    for _ in range(WARMUP_RUNS):
        _ = eval_module.cradis_dn4_rank(cradis_inputs, cradis_criteria)
        
    latencies_cradis = []
    for _ in range(NUM_RUNS):
        t0 = time.perf_counter()
        _ = eval_module.cradis_dn4_rank(cradis_inputs, cradis_criteria)
        t1 = time.perf_counter()
        latencies_cradis.append((t1 - t0) * 1000.0)
    results["5_CRADIS_DN4_Ranking"] = latencies_cradis
    print(f"   p50={percentile(latencies_cradis, 50):.2f} ms | p95={percentile(latencies_cradis, 95):.2f} ms")

    # --------------------------------------------------------------------------
    # 6. Đo Toàn trình End-to-End API (POST /api/search/meeting-places trên backend Kestrel)
    # --------------------------------------------------------------------------
    print("\n6. Đo thời gian: Toàn trình End-to-End HTTP API (POST /api/search/meeting-places)...")
    api_url = "http://localhost:5232/api/search/meeting-places"
    api_payload = json.dumps({
        "query": sample_query,
        "origins": [
            {"name": "Alice", "latitude": 10.7725, "longitude": 106.6980},
            {"name": "Bob", "latitude": 10.7798, "longitude": 106.6990},
            {"name": "Charlie", "latitude": 10.7884, "longitude": 106.6908}
        ],
        "topK": 5
    }).encode("utf-8")
    
    # Warmup
    for _ in range(WARMUP_RUNS):
        req = urllib.request.Request(api_url, data=api_payload, headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=5) as resp:
                _ = resp.read()
        except Exception:
            pass
            
    latencies_api = []
    for _ in range(NUM_RUNS):
        req = urllib.request.Request(api_url, data=api_payload, headers={"Content-Type": "application/json"})
        t0 = time.perf_counter()
        try:
            with urllib.request.urlopen(req, timeout=5) as resp:
                _ = resp.read()
            t1 = time.perf_counter()
            latencies_api.append((t1 - t0) * 1000.0)
        except Exception as ex:
            print("   Lỗi gọi API:", ex)
        time.sleep(0.05)
    results["6_EndToEnd_Backend_API"] = latencies_api
    print(f"   p50={percentile(latencies_api, 50):.2f} ms | p95={percentile(latencies_api, 95):.2f} ms")

    # ==============================================================================
    # XUẤT KẾT QUẢ RA FILE CSV: research/latency_results.csv
    # ==============================================================================
    out_csv = "research/latency_results.csv"
    with open(out_csv, mode="w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow([
            "Stage_ID", "Stage_Name", "Runs_Count", "Min_ms", "Mean_ms",
            "Median_p50_ms", "Percentile_p95_ms", "Max_ms", "Std_Dev_ms", "Notes"
        ])
        
        stage_meta = {
            "1_Candidate_Retrieval": ("Truy xuất ứng viên", "Lọc Bounding Box + Category trong bộ nhớ 53,340 POI"),
            "2_BM25_Scoring": ("Tính điểm BM25", "NFD Tokenize + Robertson IDF + TF cho 50 ứng viên"),
            "3_Haversine_Travel_Matrix": ("Ma trận hành trình Haversine", "Tính khoảng cách và thời gian ước tính 150 cặp điểm O-D"),
            "4_OSRM_Network_Table_API": ("Dịch vụ bên ngoài OSRM API", "Gọi mạng qua HTTP Table API (router.project-osrm.org)"),
            "5_CRADIS_DN4_Ranking": ("Xếp hạng CRADIS-DN4", "Chuẩn hóa vector DN4 + tính độ thỏa dụng K+, K- trên 50 ứng viên"),
            "6_EndToEnd_Backend_API": ("Toàn trình Backend API", "Kestrel HTTP Server -> SQL DB -> BM25 -> Ranking -> JSON Response")
        }
        
        for stage_id, l_list in results.items():
            if not l_list:
                continue
            name, notes = stage_meta.get(stage_id, (stage_id, ""))
            writer.writerow([
                stage_id,
                name,
                len(l_list),
                f"{min(l_list):.3f}",
                f"{statistics.mean(l_list):.3f}",
                f"{percentile(l_list, 50):.3f}",
                f"{percentile(l_list, 95):.3f}",
                f"{max(l_list):.3f}",
                f"{statistics.stdev(l_list) if len(l_list)>1 else 0.0:.3f}",
                notes
            ])
            
    print(f"\n-> Đã xuất thành công bảng độ trễ ra: {out_csv}")

if __name__ == "__main__":
    main()
