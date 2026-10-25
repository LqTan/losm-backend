#!/usr/bin/env python3
"""
05_generate_plots.py
Tạo các biểu đồ khoa học chất lượng cao (Publication-Quality Charts) bằng PyCairo:
1. research/travel_time_comparison.png: Biểu đồ thời gian di chuyển và tính công bằng (T_avg, T_max, Delta_T)
   phân biệt mạng đường OSRM thực tế với mô hình xấp xỉ Haversine (25 km/h) qua 3 phương pháp xếp hạng.
2. research/latency_comparison.png: Biểu đồ độ trễ từng công đoạn (Truy xuất ứng viên, BM25, Ma trận OSRM, CRADIS-DN4, Toàn trình API)
   so sánh trung vị p50 và phân vị p95.
3. research/ranking_comparison.png: Biểu đồ đánh giá tổng hợp mức độ thỏa hiệp giữa Độ liên quan (Relevance) và Độ công bằng (Fairness).
"""

import cairo
import csv
import math
import os

# ==============================================================================
# HÀM VẼ KHUNG CHUẨN KHOA HỌC
# ==============================================================================
def draw_card_bg(ctx, width, height, title, subtitle):
    # Background
    ctx.set_source_rgb(0.98, 0.985, 0.99)
    ctx.paint()

    # Title
    ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
    ctx.set_font_size(24)
    ctx.set_source_rgb(0.08, 0.12, 0.22)
    ctx.move_to(50, 48)
    ctx.show_text(title)

    # Subtitle
    ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_NORMAL)
    ctx.set_font_size(14)
    ctx.set_source_rgb(0.35, 0.42, 0.52)
    ctx.move_to(50, 75)
    ctx.show_text(subtitle)

    # Divider line
    ctx.set_source_rgb(0.85, 0.88, 0.92)
    ctx.set_line_width(1.5)
    ctx.move_to(50, 92)
    ctx.line_to(width - 50, 92)
    ctx.stroke()

# ==============================================================================
# 1. BIỂU ĐỒ THỜI GIAN DI CHUYỂN VÀ TÍNH CÔNG BẰNG (travel_time_comparison.png)
# ==============================================================================
def generate_travel_time_plot():
    # Đọc dữ liệu từ travel_time_results.csv
    csv_file = "research/travel_time_results.csv"
    if not os.path.exists(csv_file):
        print(f"Cảnh báo: không tìm thấy {csv_file}")
        return

    m_data = {
        "M1_Pure_BM25": {"osrm_avg": [], "osrm_max": [], "osrm_spread": [], "hav_avg": [], "hav_spread": []},
        "M2_BM25_Distance": {"osrm_avg": [], "osrm_max": [], "osrm_spread": [], "hav_avg": [], "hav_spread": []},
        "M3_CRADIS_DN4": {"osrm_avg": [], "osrm_max": [], "osrm_spread": [], "hav_avg": [], "hav_spread": []}
    }

    with open(csv_file, mode="r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for r in reader:
            m = r["method"]
            if m in m_data:
                m_data[m]["osrm_avg"].append(float(r["duration_avg_min"]))
                m_data[m]["osrm_max"].append(float(r["duration_max_min"]))
                m_data[m]["osrm_spread"].append(float(r["duration_spread_min"]))
                m_data[m]["hav_avg"].append(float(r["haversine_avg_min"]))
                m_data[m]["hav_spread"].append(float(r["haversine_spread_min"]))

    # Tính trung bình cho từng phương pháp
    summary = {}
    for m, d in m_data.items():
        n = max(len(d["osrm_avg"]), 1)
        summary[m] = {
            "osrm_avg": sum(d["osrm_avg"]) / n,
            "osrm_max": sum(d["osrm_max"]) / n,
            "osrm_spread": sum(d["osrm_spread"]) / n,
            "hav_avg": sum(d["hav_avg"]) / n,
            "hav_spread": sum(d["hav_spread"]) / n
        }

    width = 1200
    height = 750
    surface = cairo.ImageSurface(cairo.FORMAT_ARGB32, width, height)
    ctx = cairo.Context(surface)

    draw_card_bg(
        ctx, width, height,
        "So sánh Thời gian Di chuyển và Tính Công bằng (Group 3: Điểm hẹn Đa thành viên)",
        "Dữ liệu thực nghiệm trên 25 kịch bản điểm hẹn tại Quận 1: Mạng đường OSRM thực tế vs Mô hình Haversine (25 km/h)"
    )

    # Vùng đồ thị
    plot_x = 100
    plot_y = 150
    plot_w = 1000
    plot_h = 480

    # Grid và Trục tung (Y: 0 đến 3.5 phút)
    max_val = 3.5
    y_steps = 7 # 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5

    ctx.set_line_width(1.0)
    for i in range(y_steps + 1):
        val = i * 0.5
        y = plot_y + plot_h - (val / max_val) * plot_h
        
        # Grid line
        ctx.set_source_rgba(0.88, 0.90, 0.94, 0.8)
        ctx.move_to(plot_x, y)
        ctx.line_to(plot_x + plot_w, y)
        ctx.stroke()

        # Label
        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_NORMAL)
        ctx.set_font_size(12)
        ctx.set_source_rgb(0.4, 0.45, 0.55)
        text = f"{val:.1f} m"
        ctx.move_to(plot_x - 50, y + 4)
        ctx.show_text(text)

    # Trục Y tiêu đề
    ctx.save()
    ctx.translate(35, plot_y + plot_h / 2)
    ctx.rotate(-math.pi / 2)
    ctx.set_font_size(14)
    ctx.set_source_rgb(0.2, 0.25, 0.35)
    ctx.show_text("Thời gian di chuyển (Phút)")
    ctx.restore()

    # Vẽ các cột nhóm
    # 3 Cụm phương pháp: Pure BM25, BM25 + Distance, CRADIS-DN4
    # Mỗi cụm có 5 cột:
    # 1. OSRM T_avg
    # 2. OSRM T_max
    # 3. OSRM Spread (ΔT)
    # 4. Haversine T_avg
    # 5. Haversine Spread (ΔT)

    colors = [
        (0.20, 0.45, 0.85), # Xanh dương đậm - OSRM T_avg
        (0.85, 0.25, 0.20), # Đỏ - OSRM T_max (Thắt cổ chai)
        (0.15, 0.68, 0.38), # Xanh lá - OSRM Spread (Bất công bằng)
        (0.55, 0.70, 0.95), # Xanh dương nhạt - Haversine T_avg
        (0.50, 0.85, 0.65)  # Xanh lá nhạt - Haversine Spread
    ]

    metric_names = [
        "OSRM T_avg",
        "OSRM T_max",
        "OSRM Spread (ΔT)",
        "Haversine T_avg",
        "Haversine Spread (ΔT)"
    ]

    cluster_names = [
        ("BM25 Thuần túy", "M1_Pure_BM25"),
        ("BM25 + Khoảng cách", "M2_BM25_Distance"),
        ("CRADIS-DN4 (Đề xuất)", "M3_CRADIS_DN4")
    ]

    cluster_w = plot_w / len(cluster_names)
    bar_w = 42
    bar_gap = 10

    for c_idx, (c_label, m_key) in enumerate(cluster_names):
        c_center_x = plot_x + c_idx * cluster_w + cluster_w / 2
        vals = [
            summary[m_key]["osrm_avg"],
            summary[m_key]["osrm_max"],
            summary[m_key]["osrm_spread"],
            summary[m_key]["hav_avg"],
            summary[m_key]["hav_spread"]
        ]

        total_cluster_bars_w = len(vals) * bar_w + (len(vals) - 1) * bar_gap
        start_bx = c_center_x - total_cluster_bars_w / 2

        for b_idx, (val, color) in enumerate(zip(vals, colors)):
            bx = start_bx + b_idx * (bar_w + bar_gap)
            bh = (val / max_val) * plot_h
            by = plot_y + plot_h - bh

            # Cột có đổ màu gradient nhẹ
            ctx.set_source_rgb(*color)
            ctx.rectangle(bx, by, bar_w, bh)
            ctx.fill()

            # Viền cột
            ctx.set_source_rgba(color[0]*0.8, color[1]*0.8, color[2]*0.8, 0.9)
            ctx.set_line_width(1.0)
            ctx.rectangle(bx, by, bar_w, bh)
            ctx.stroke()

            # Số liệu trên đầu cột
            ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
            ctx.set_font_size(11)
            ctx.set_source_rgb(0.15, 0.2, 0.3)
            val_str = f"{val:.2f}"
            ctx.move_to(bx + 4, by - 6)
            ctx.show_text(val_str)

        # Nhãn tên phương pháp ở dưới trục hoành
        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
        ctx.set_font_size(15)
        ctx.set_source_rgb(0.1, 0.15, 0.25)
        ctx.move_to(c_center_x - 70, plot_y + plot_h + 30)
        ctx.show_text(c_label)

    # Chú thích Legend ở phía trên
    leg_x = plot_x + 50
    leg_y = 115
    for idx, (m_name, color) in enumerate(zip(metric_names, colors)):
        lx = leg_x + idx * 180
        ctx.set_source_rgb(*color)
        ctx.rectangle(lx, leg_y, 14, 14)
        ctx.fill()

        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_NORMAL)
        ctx.set_font_size(12)
        ctx.set_source_rgb(0.2, 0.25, 0.35)
        ctx.move_to(lx + 20, leg_y + 12)
        ctx.show_text(m_name)

    # Chú thích ghi chú học thuật ở góc dưới
    ctx.select_font_face("Sans", cairo.FONT_SLANT_ITALIC, cairo.FONT_WEIGHT_NORMAL)
    ctx.set_font_size(11)
    ctx.set_source_rgb(0.45, 0.50, 0.60)
    note_text = "Ghi chú: OSRM đo theo mạng đường đô thị thực tế; Haversine quy đổi với vận tốc 25 km/h. CRADIS-DN4 giảm chênh lệch (ΔT) xuống 1.18 phút mà vẫn giữ BM25 = 0.9909."
    ctx.move_to(plot_x, height - 25)
    ctx.show_text(note_text)

    out_file = "research/travel_time_comparison.png"
    surface.write_to_png(out_file)
    print(f"-> Đã xuất biểu đồ: {out_file}")

# ==============================================================================
# 2. BIỂU ĐỒ ĐÁNH GIÁ ĐỘ TRỄ HỆ THỐNG (latency_comparison.png)
# ==============================================================================
def generate_latency_plot():
    csv_file = "research/latency_results.csv"
    if not os.path.exists(csv_file):
        print(f"Cảnh báo: không tìm thấy {csv_file}")
        return

    stages = []
    with open(csv_file, mode="r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for r in reader:
            stages.append({
                "id": r["Stage_ID"],
                "name": r["Stage_Name"],
                "p50": float(r["Median_p50_ms"]),
                "p95": float(r["Percentile_p95_ms"])
            })

    width = 1200
    height = 750
    surface = cairo.ImageSurface(cairo.FORMAT_ARGB32, width, height)
    ctx = cairo.Context(surface)

    draw_card_bg(
        ctx, width, height,
        "Đánh giá Độ trễ Hệ thống theo từng Công đoạn và Toàn trình API",
        "Đo lường thời gian thực thi trung vị (p50) và phân vị 95 (p95) qua 35 lượt đo lặp lại thống nhất (đơn vị: mili giây)"
    )

    plot_x = 100
    plot_y = 150
    plot_w = 1000
    plot_h = 480

    # Max latency cho thang đo (toàn trình API ~ 650ms -> lấy trần 700ms)
    max_val = 700.0
    y_steps = 7

    ctx.set_line_width(1.0)
    for i in range(y_steps + 1):
        val = i * 100.0
        y = plot_y + plot_h - (val / max_val) * plot_h

        ctx.set_source_rgba(0.88, 0.90, 0.94, 0.8)
        ctx.move_to(plot_x, y)
        ctx.line_to(plot_x + plot_w, y)
        ctx.stroke()

        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_NORMAL)
        ctx.set_font_size(12)
        ctx.set_source_rgb(0.4, 0.45, 0.55)
        text = f"{int(val)} ms"
        ctx.move_to(plot_x - 55, y + 4)
        ctx.show_text(text)

    # Tiêu đề trục Y
    ctx.save()
    ctx.translate(35, plot_y + plot_h / 2)
    ctx.rotate(-math.pi / 2)
    ctx.set_font_size(14)
    ctx.set_source_rgb(0.2, 0.25, 0.35)
    ctx.show_text("Thời gian xử lý (Mili giây - ms)")
    ctx.restore()

    # Vẽ từng cặp cột (p50 và p95) cho 6 công đoạn
    stage_w = plot_w / len(stages)
    bar_w = 45
    bar_gap = 12

    color_p50 = (0.18, 0.45, 0.85) # Xanh dương
    color_p95 = (0.92, 0.45, 0.15) # Cam

    for s_idx, st in enumerate(stages):
        cx = plot_x + s_idx * stage_w + stage_w / 2
        vals = [st["p50"], st["p95"]]
        colors = [color_p50, color_p95]

        total_b_w = len(vals) * bar_w + bar_gap
        start_x = cx - total_b_w / 2

        for b_idx, (v, col) in enumerate(zip(vals, colors)):
            bx = start_x + b_idx * (bar_w + bar_gap)
            bh = (v / max_val) * plot_h
            by = plot_y + plot_h - bh

            ctx.set_source_rgb(*col)
            ctx.rectangle(bx, by, bar_w, bh)
            ctx.fill()

            ctx.set_source_rgba(col[0]*0.8, col[1]*0.8, col[2]*0.8, 0.9)
            ctx.set_line_width(1.0)
            ctx.rectangle(bx, by, bar_w, bh)
            ctx.stroke()

            # Nhãn giá trị
            ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
            ctx.set_font_size(11)
            ctx.set_source_rgb(0.15, 0.2, 0.3)
            val_str = f"{v:.1f}" if v >= 1.0 else f"{v:.2f}"
            ctx.move_to(bx + (4 if v >= 10 else 2), by - 6)
            ctx.show_text(val_str)

        # Nhãn công đoạn (Xuống dòng nếu dài)
        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
        ctx.set_font_size(12)
        ctx.set_source_rgb(0.15, 0.2, 0.3)

        name_parts = st["name"].split(" ")
        if len(name_parts) >= 3:
            line1 = " ".join(name_parts[:2])
            line2 = " ".join(name_parts[2:])
            ctx.move_to(cx - len(line1)*3.2, plot_y + plot_h + 24)
            ctx.show_text(line1)
            ctx.move_to(cx - len(line2)*3.2, plot_y + plot_h + 40)
            ctx.show_text(line2)
        else:
            ctx.move_to(cx - len(st["name"])*3.2, plot_y + plot_h + 28)
            ctx.show_text(st["name"])

    # Legend
    leg_x = plot_x + 350
    leg_y = 115

    ctx.set_source_rgb(*color_p50)
    ctx.rectangle(leg_x, leg_y, 16, 16)
    ctx.fill()
    ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
    ctx.set_font_size(13)
    ctx.set_source_rgb(0.2, 0.25, 0.35)
    ctx.move_to(leg_x + 24, leg_y + 13)
    ctx.show_text("Độ trễ trung vị (p50)")

    ctx.set_source_rgb(*color_p95)
    ctx.rectangle(leg_x + 220, leg_y, 16, 16)
    ctx.fill()
    ctx.move_to(leg_x + 244, leg_y + 13)
    ctx.show_text("Phân vị 95 (p95)")

    # Chú thích
    ctx.select_font_face("Sans", cairo.FONT_SLANT_ITALIC, cairo.FONT_WEIGHT_NORMAL)
    ctx.set_font_size(11)
    ctx.set_source_rgb(0.45, 0.50, 0.60)
    note_text = "Ghi chú: CRADIS-DN4 cực nhẹ (p50=0.17ms, p95=0.27ms). Phần lớn độ trễ toàn trình API (p50=563.6ms) nằm ở cuộc gọi HTTP mạng ngoài OSRM (p50=411.6ms) và SQL I/O."
    ctx.move_to(plot_x, height - 20)
    ctx.show_text(note_text)

    out_file = "research/latency_comparison.png"
    surface.write_to_png(out_file)
    print(f"-> Đã xuất biểu đồ: {out_file}")

# ==============================================================================
# 3. BIỂU ĐỒ SO SÁNH PHƯƠNG PHÁP XẾP HẠNG (ranking_comparison.png)
# ==============================================================================
def generate_ranking_tradeoff_plot():
    width = 1200
    height = 750
    surface = cairo.ImageSurface(cairo.FORMAT_ARGB32, width, height)
    ctx = cairo.Context(surface)

    draw_card_bg(
        ctx, width, height,
        "Đánh giá Đa tiêu chí: Mức độ Thỏa hiệp giữa Độ Liên Quan và Độ Công Bằng",
        "Đối chiếu giữa BM25 thuần túy, BM25 kết hợp Khoảng cách, và Thuật toán thỏa hiệp đa tiêu chí CRADIS-DN4"
    )

    plot_x = 120
    plot_y = 150
    plot_w = 960
    plot_h = 480

    # Trục X: Độ công bằng hành trình (Fairness Index = 1 - (Delta_T / 5 phút), 0.5 -> 1.0)
    # Trục Y: Điểm liên quan BM25 trung bình (0.90 -> 1.00)

    # Grid Lines
    ctx.set_line_width(1.0)
    for i in range(6):
        # Y grid: 0.90 đến 1.00
        val_y = 0.90 + i * 0.02
        y = plot_y + plot_h - (i / 5.0) * plot_h
        ctx.set_source_rgba(0.88, 0.90, 0.94, 0.8)
        ctx.move_to(plot_x, y)
        ctx.line_to(plot_x + plot_w, y)
        ctx.stroke()

        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_NORMAL)
        ctx.set_font_size(12)
        ctx.set_source_rgb(0.4, 0.45, 0.55)
        ctx.move_to(plot_x - 55, y + 4)
        ctx.show_text(f"{val_y:.2f}")

    for j in range(6):
        # X grid: 0.50 đến 1.00
        val_x = 0.50 + j * 0.10
        x = plot_x + (j / 5.0) * plot_w
        ctx.set_source_rgba(0.88, 0.90, 0.94, 0.8)
        ctx.move_to(x, plot_y)
        ctx.line_to(x, plot_y + plot_h)
        ctx.stroke()

        ctx.move_to(x - 14, plot_y + plot_h + 20)
        ctx.show_text(f"{val_x:.2f}")

    # Tiêu đề trục
    ctx.save()
    ctx.translate(45, plot_y + plot_h / 2)
    ctx.rotate(-math.pi / 2)
    ctx.set_font_size(14)
    ctx.set_source_rgb(0.2, 0.25, 0.35)
    ctx.show_text("Điểm liên quan BM25 trung bình (Relevance Score)")
    ctx.restore()

    ctx.set_font_size(14)
    ctx.set_source_rgb(0.2, 0.25, 0.35)
    ctx.move_to(plot_x + plot_w / 2 - 130, plot_y + plot_h + 45)
    ctx.show_text("Chỉ số công bằng hành trình (Fairness Index: càng cao càng công bằng)")

    # Dữ liệu 3 phương pháp trên Group 3 (Multi-origin):
    # BM25 Pure: Relevance = 1.0000, Spread = 1.32 min -> Fairness = 1 - 1.32/5 = 0.736
    # BM25 + Dist: Relevance = 1.0000, Spread = 1.32 min -> Fairness = 0.736
    # CRADIS-DN4: Relevance = 0.9909, Spread = 1.18 min -> Fairness = 1 - 1.18/5 = 0.764
    points = [
        ("BM25 Thuần túy", 0.736, 1.000, (0.85, 0.25, 0.20), "Ưu tiên tuyệt đối ngữ nghĩa văn bản\nBất công bằng thời gian di chuyển cao nhất (ΔT = 1.32m)"),
        ("BM25 + Khoảng cách", 0.736, 1.000, (0.20, 0.45, 0.85), "Cải thiện vị trí trung tâm cơ bản\nChưa giải quyết chênh lệch giữa các thành viên"),
        ("CRADIS-DN4 (Thỏa hiệp tối ưu)", 0.764, 0.9909, (0.15, 0.68, 0.38), "Tối ưu hóa đa tiêu chí Pareto Compromise\nGiảm chênh lệch ΔT còn 1.18m (-10.6%) mà BM25 giữ 99.1%")
    ]

    for label, fx, ry, col, desc in points:
        px = plot_x + ((fx - 0.50) / 0.50) * plot_w
        py = plot_y + plot_h - ((ry - 0.90) / 0.10) * plot_h

        # Vòng tròn điểm
        ctx.set_source_rgba(col[0], col[1], col[2], 0.2)
        ctx.arc(px, py, 22, 0, 2*math.pi)
        ctx.fill()

        ctx.set_source_rgb(*col)
        ctx.arc(px, py, 10, 0, 2*math.pi)
        ctx.fill()

        ctx.set_source_rgb(1, 1, 1)
        ctx.set_line_width(2.5)
        ctx.arc(px, py, 10, 0, 2*math.pi)
        ctx.stroke()

        # Callout card
        card_x = px + 25 if px < plot_x + plot_w - 250 else px - 280
        card_y = py - 35
        ctx.set_source_rgba(1, 1, 1, 0.95)
        ctx.rectangle(card_x, card_y, 270, 75)
        ctx.fill()

        ctx.set_source_rgb(*col)
        ctx.set_line_width(1.5)
        ctx.rectangle(card_x, card_y, 270, 75)
        ctx.stroke()

        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_BOLD)
        ctx.set_font_size(13)
        ctx.set_source_rgb(*col)
        ctx.move_to(card_x + 10, card_y + 18)
        ctx.show_text(label)

        ctx.select_font_face("Sans", cairo.FONT_SLANT_NORMAL, cairo.FONT_WEIGHT_NORMAL)
        ctx.set_font_size(11)
        ctx.set_source_rgb(0.25, 0.3, 0.4)
        lines = desc.split("\n")
        for l_idx, l in enumerate(lines):
            ctx.move_to(card_x + 10, card_y + 36 + l_idx * 16)
            ctx.show_text(l)

    out_file = "research/ranking_comparison.png"
    surface.write_to_png(out_file)
    print(f"-> Đã xuất biểu đồ: {out_file}")

def main():
    print("=== ĐANG TẠO CÁC BIỂU ĐỒ BÁO CÁO KHOA HỌC XUẤT RA FILE PNG... ===")
    generate_travel_time_plot()
    generate_latency_plot()
    generate_ranking_tradeoff_plot()
    print("=== HOÀN TẤT TẠO BIỂU ĐỒ KHOA HỌC ===")

if __name__ == "__main__":
    main()
