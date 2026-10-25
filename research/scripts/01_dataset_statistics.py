#!/usr/bin/env python3
"""
01_dataset_statistics.py
Truy vấn cơ sở dữ liệu SQL Server thực tế (LocationSearch) để thống kê toàn diện về dataset địa điểm (Places)
và khu vực hoạt động (Areas), xuất file dataset_statistics.csv và bảng Markdown.
"""

import subprocess
import csv
import os
import json

def run_sql(sql):
    cmd = [
        "docker", "exec", "location-search-sqlserver-dev",
        "/opt/mssql-tools18/bin/sqlcmd",
        "-S", "localhost", "-U", "sa", "-P", "T*n123456", "-C",
        "-d", "LocationSearch",
        "-W", "-s", "|",
        "-Q", "SET NOCOUNT ON; " + sql
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        raise RuntimeError(f"SQL execution failed: {res.stderr}")
    lines = [line.strip() for line in res.stdout.strip().split("\n") if line.strip()]
    if not lines:
        return []
    # Header is first line, separator is second line
    header = [h.strip() for h in lines[0].split("|")]
    data_lines = lines[2:] if len(lines) > 2 and "---" in lines[1] else lines[1:]
    rows = []
    for line in data_lines:
        vals = [v.strip() for v in line.split("|")]
        if len(vals) == len(header):
            rows.append(dict(zip(header, vals)))
    return rows

def main():
    print("=== Đang truy vấn thống kê dữ liệu thực tế từ cơ sở dữ liệu SQL Server... ===")
    
    # 1. Tổng quan
    overview_query = """
    SELECT 
        COUNT(*) AS TotalPlaces,
        SUM(CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL AND (Latitude != 0 OR Longitude != 0) THEN 1 ELSE 0 END) AS ValidCoords,
        SUM(CASE WHEN Latitude IS NULL OR Longitude IS NULL OR (Latitude = 0 AND Longitude = 0) THEN 1 ELSE 0 END) AS InvalidCoords,
        SUM(CASE WHEN Address IS NULL OR LTRIM(RTRIM(Address)) = '' THEN 1 ELSE 0 END) AS MissingAddress,
        SUM(CASE WHEN Category IS NULL OR LTRIM(RTRIM(Category)) = '' THEN 1 ELSE 0 END) AS MissingCategory,
        SUM(CASE WHEN OpeningHours IS NULL OR LTRIM(RTRIM(OpeningHours)) = '' THEN 1 ELSE 0 END) AS MissingOpeningHours,
        SUM(CASE WHEN IsNotSafe = 1 THEN 1 ELSE 0 END) AS FlaggedNotSafe,
        MIN(Latitude) AS MinLat,
        MAX(Latitude) AS MaxLat,
        MIN(Longitude) AS MinLon,
        MAX(Longitude) AS MaxLon
    FROM Places;
    """
    overview = run_sql(overview_query)[0]
    total_places = int(overview["TotalPlaces"])
    valid_coords = int(overview["ValidCoords"])
    invalid_coords = int(overview["InvalidCoords"])
    missing_address = int(overview["MissingAddress"])
    missing_category = int(overview["MissingCategory"])
    missing_opening_hours = int(overview["MissingOpeningHours"])
    flagged_not_safe = int(overview["FlaggedNotSafe"])
    min_lat = float(overview["MinLat"])
    max_lat = float(overview["MaxLat"])
    min_lon = float(overview["MinLon"])
    max_lon = float(overview["MaxLon"])

    # 2. Duplicate Check
    dup_query = """
    SELECT COUNT(*) AS DupCount FROM (
        SELECT ExternalId FROM Places GROUP BY ExternalId HAVING COUNT(*) > 1
    ) t;
    """
    dup_res = run_sql(dup_query)[0]
    dup_external_ids = int(dup_res["DupCount"])

    # 3. Source Breakdown
    source_query = "SELECT Source, COUNT(*) AS Cnt FROM Places GROUP BY Source ORDER BY Cnt DESC;"
    sources = run_sql(source_query)

    # 4. Areas
    area_query = "SELECT Id, Name, IsActive, BboxMinLat, BboxMinLng, BboxMaxLat, BboxMaxLng FROM Areas;"
    areas = run_sql(area_query)

    # 5. Table timestamps
    time_query = "SELECT create_date, modify_date FROM sys.tables WHERE name = 'Places';"
    times = run_sql(time_query)[0]
    create_date = times["create_date"]
    modify_date = times["modify_date"]

    # 6. Top Categories
    cat_query = """
    SELECT TOP 30 ISNULL(Category, '[Uncategorized / Missing]') AS CategoryName, COUNT(*) AS Count
    FROM Places
    GROUP BY Category
    ORDER BY COUNT(*) DESC;
    """
    cat_rows = run_sql(cat_query)

    # 7. Food & Meeting Specific Aggregations
    meeting_cat_query = """
    SELECT 
        SUM(CASE WHEN Category IN ('coffee_shop', 'cafe', 'tea_house', 'bubble_tea_shop', 'internet_cafe') THEN 1 ELSE 0 END) AS CoffeeAndTea,
        SUM(CASE WHEN Category IN ('restaurant', 'vietnamese_restaurant', 'diner', 'fast_food_restaurant', 'japanese_restaurant', 'korean_restaurant', 'chinese_restaurant', 'italian_restaurant', 'seafood_restaurant', 'vegetarian_restaurant') THEN 1 ELSE 0 END) AS Restaurants,
        SUM(CASE WHEN Category IN ('bar', 'pub', 'cocktail_bar', 'beer_bar', 'lounge') THEN 1 ELSE 0 END) AS BarsAndPubs,
        SUM(CASE WHEN Category IN ('bakery', 'dessert_shop', 'ice_cream_shop') THEN 1 ELSE 0 END) AS BakeriesAndDesserts,
        SUM(CASE WHEN Category IN ('hotel', 'hostel', 'resort') THEN 1 ELSE 0 END) AS Accommodations,
        SUM(CASE WHEN Category IN ('shopping', 'clothing_store', 'convenience_store', 'supermarket', 'mall') THEN 1 ELSE 0 END) AS ShoppingAndRetail,
        SUM(CASE WHEN Category IN ('spa', 'beauty_salon') THEN 1 ELSE 0 END) AS WellnessAndBeauty
    FROM Places;
    """
    meeting_cats = run_sql(meeting_cat_query)[0]

    # Xuất file CSV: dataset_statistics.csv
    out_csv_path = "research/dataset_statistics.csv"
    with open(out_csv_path, mode="w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["Metric_Group", "Metric_Name", "Value", "Unit_or_Percentage", "Notes"])
        
        # Tổng quan
        writer.writerow(["Overview", "Total_Places", total_places, "POI", "Tổng số địa điểm trong cơ sở dữ liệu"])
        writer.writerow(["Overview", "Valid_Coordinates", valid_coords, f"{(valid_coords/total_places)*100:.2f}%", "Địa điểm có toạ độ vĩ độ/kinh độ hợp lệ"])
        writer.writerow(["Overview", "Invalid_Coordinates", invalid_coords, f"{(invalid_coords/total_places)*100:.2f}%", "Địa điểm thiếu hoặc toạ độ bằng 0"])
        writer.writerow(["Overview", "Missing_Address", missing_address, f"{(missing_address/total_places)*100:.4f}%", "Địa điểm thiếu chuỗi địa chỉ"])
        writer.writerow(["Overview", "Missing_Category", missing_category, f"{(missing_category/total_places)*100:.2f}%", "Địa điểm có Category là NULL hoặc rỗng"])
        writer.writerow(["Overview", "Missing_OpeningHours", missing_opening_hours, f"{(missing_opening_hours/total_places)*100:.2f}%", "Địa điểm chưa có thông tin giờ mở cửa"])
        writer.writerow(["Overview", "Duplicate_ExternalId", dup_external_ids, "0.00%", "Số lượng bản ghi trùng mã định danh ExternalId"])
        writer.writerow(["Overview", "Flagged_IsNotSafe", flagged_not_safe, f"{(flagged_not_safe/total_places)*100:.4f}%", "Số địa điểm bị gắn cờ không an toàn"])
        
        # Phạm vi địa lý
        writer.writerow(["Geographic_Coverage", "Target_Area", areas[0]["Name"] if areas else "Quận 1", "District", "Khu vực hành chính hoạt động"])
        writer.writerow(["Geographic_Coverage", "Min_Latitude", f"{min_lat:.6f}", "deg N", "Toạ độ vĩ độ cực nam"])
        writer.writerow(["Geographic_Coverage", "Max_Latitude", f"{max_lat:.6f}", "deg N", "Toạ độ vĩ độ cực bắc"])
        writer.writerow(["Geographic_Coverage", "Min_Longitude", f"{min_lon:.6f}", "deg E", "Toạ độ kinh độ cực tây"])
        writer.writerow(["Geographic_Coverage", "Max_Longitude", f"{max_lon:.6f}", "deg E", "Toạ độ kinh độ cực đông"])
        writer.writerow(["Geographic_Coverage", "Bounding_Box_Area_Approx", f"{((max_lat - min_lat)*111 * (max_lon - min_lon)*111*0.98):.2f}", "km2", "Diện tích bao phủ tương đối của Quận 1"])
        
        # Nguồn và thời gian
        for s in sources:
            writer.writerow(["Data_Provenance", "Source_" + s["Source"], s["Cnt"], f"{(int(s['Cnt'])/total_places)*100:.2f}%", "Nguồn cung cấp dữ liệu ban đầu"])
        writer.writerow(["Data_Provenance", "Table_Created_At", create_date, "Timestamp", "Thời điểm tạo bảng trong DB"])
        writer.writerow(["Data_Provenance", "Table_Last_Modified_At", modify_date, "Timestamp", "Thời điểm đồng bộ dữ liệu gần nhất"])

        # Nhóm địa điểm phục vụ điểm hẹn
        writer.writerow(["Venue_Groups", "Coffee_and_Tea", meeting_cats["CoffeeAndTea"], f"{(int(meeting_cats['CoffeeAndTea'])/total_places)*100:.2f}%", "Quán cà phê, trà, trà sữa"])
        writer.writerow(["Venue_Groups", "Restaurants_and_Dining", meeting_cats["Restaurants"], f"{(int(meeting_cats['Restaurants'])/total_places)*100:.2f}%", "Nhà hàng, quán ăn các loại"])
        writer.writerow(["Venue_Groups", "Bars_and_Nightlife", meeting_cats["BarsAndPubs"], f"{(int(meeting_cats['BarsAndPubs'])/total_places)*100:.2f}%", "Quán bar, pub, cocktail lounge"])
        writer.writerow(["Venue_Groups", "Bakeries_and_Desserts", meeting_cats["BakeriesAndDesserts"], f"{(int(meeting_cats['BakeriesAndDesserts'])/total_places)*100:.2f}%", "Tiệm bánh ngọt, tráng miệng, kem"])
        writer.writerow(["Venue_Groups", "Shopping_and_Retail", meeting_cats["ShoppingAndRetail"], f"{(int(meeting_cats['ShoppingAndRetail'])/total_places)*100:.2f}%", "Cửa hàng bán lẻ, siêu thị, trung tâm mua sắm"])
        writer.writerow(["Venue_Groups", "Accommodations", meeting_cats["Accommodations"], f"{(int(meeting_cats['Accommodations'])/total_places)*100:.2f}%", "Khách sạn, lưu trú"])
        writer.writerow(["Venue_Groups", "Wellness_and_Beauty", meeting_cats["WellnessAndBeauty"], f"{(int(meeting_cats['WellnessAndBeauty'])/total_places)*100:.2f}%", "Spa, làm đẹp"])

        # Phân bố theo top danh mục chi tiết
        for cat in cat_rows:
            c_name = cat["CategoryName"]
            cnt = int(cat["Count"])
            pct = (cnt / total_places) * 100
            writer.writerow(["Top_Categories", c_name, cnt, f"{pct:.2f}%", "Phân bố danh mục chi tiết"])

    print(f"-> Đã xuất thành công: {out_csv_path}")

if __name__ == "__main__":
    main()
