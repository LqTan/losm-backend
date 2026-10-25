#!/usr/bin/env python3
"""
02_prepare_queries.py
Chuẩn bị bộ dữ liệu kiểm thử gồm 75 truy vấn (nằm trong khoảng 60-100 theo yêu cầu),
chia thành 3 nhóm đại diện:
1. Nhóm 1: Tìm theo tên địa điểm và danh mục cụ thể (25 truy vấn)
2. Nhóm 2: Tìm theo nhu cầu bằng ngôn ngữ tự nhiên (25 truy vấn)
3. Nhóm 3: Tìm điểm hẹn cho nhiều người ở các vị trí khác nhau (25 truy vấn)
Xuất ra file: research/queries.csv
"""

import csv
import json
import os

# Danh sách toạ độ mốc chuẩn tại Quận 1
LANDMARKS = {
    "Ben_Thanh": {"name": "Chợ Bến Thành", "lat": 10.7725, "lon": 106.6980},
    "Notre_Dame": {"name": "Nhà thờ Đức Bà / Bưu điện", "lat": 10.7798, "lon": 106.6990},
    "Nguyen_Hue": {"name": "Phố đi bộ Nguyễn Huệ", "lat": 10.7745, "lon": 106.7035},
    "Turtle_Lake": {"name": "Hồ Con Rùa", "lat": 10.7828, "lon": 106.6961},
    "Tan_Dinh": {"name": "Nhà thờ Tân Định", "lat": 10.7884, "lon": 106.6908},
    "Nguyen_Du": {"name": "CLB Thể thao Nguyễn Du", "lat": 10.7738, "lon": 106.6925},
    "Bach_Dang": {"name": "Bến Bạch Đằng", "lat": 10.7720, "lon": 106.7070},
    "Da_Kao": {"name": "Vòng xoay Điện Biên Phủ / Đa Kao", "lat": 10.7915, "lon": 106.6988},
    "Bui_Vien": {"name": "Khu Bùi Viện / Phạm Ngũ Lão", "lat": 10.7675, "lon": 106.6935},
    "Bitexco": {"name": "Tòa tháp Bitexco", "lat": 10.7715, "lon": 106.7045},
    "Zoo": {"name": "Thảo Cầm Viên Sài Gòn", "lat": 10.7875, "lon": 106.7050},
    "Independence_Palace": {"name": "Dinh Độc Lập", "lat": 10.7770, "lon": 106.6955}
}

QUERIES_DATA = []

# ==============================================================================
# NHÓM 1: Tìm theo tên địa điểm và danh mục cụ thể (25 truy vấn)
# ==============================================================================
group1 = [
    ("Q01", "Highlands Coffee", "coffee_shop", "Highlands Coffee", "Quán cà phê chuỗi Highlands"),
    ("Q02", "The Coffee House", "coffee_shop", "The Coffee House", "Quán cà phê The Coffee House"),
    ("Q03", "Phúc Long", "coffee_shop", "Phúc Long", "Cửa hàng trà và cà phê Phúc Long"),
    ("Q04", "Katinat", "coffee_shop", "Katinat", "Cà phê chuỗi Katinat"),
    ("Q05", "Trung Nguyên Legend", "coffee_shop", "Trung Nguyên Legend", "Cà phê Trung Nguyên Legend"),
    ("Q06", "Pizza 4P's", "restaurant", "Pizza 4P's", "Nhà hàng pizza thủ công"),
    ("Q07", "Bún Bò Huế", "vietnamese_restaurant", "Bún bò", "Quán ăn món bún bò Huế"),
    ("Q08", "Cơm tấm", "vietnamese_restaurant", "Cơm tấm", "Quán cơm tấm truyền thống"),
    ("Q09", "Nhà hàng Chay", "restaurant", "Ẩm thực chay", "Nhà hàng món ăn thanh đạm chay"),
    ("Q10", "Tiệm bánh ngọt Tous Les Jours", "bakery", "Tous Les Jours", "Tiệm bánh phong cách Pháp"),
    ("Q11", "Quán lẩu Haidilao", "restaurant", "Haidilao", "Nhà hàng lẩu Trung Hoa"),
    ("Q12", "Sushi Tei", "japanese_restaurant", "Sushi Tei", "Nhà hàng ẩm thực Nhật Bản"),
    ("Q13", "Bia thủ công Pasteur Street Craft Beer", "bar", "Pasteur Street", "Quán bia thủ công địa phương"),
    ("Q14", "Trà sữa Koi Thé", "coffee_shop", "Koi Thé", "Cửa hàng trà sữa Đài Loan"),
    ("Q15", "Starbucks", "coffee_shop", "Starbucks", "Chuỗi cà phê quốc tế Starbucks"),
    ("Q16", "Nhà hàng hải sản", "restaurant", "Hải sản tươi sống", "Quán hải sản tươi ngon"),
    ("Q17", "Quán bar Rooftop", "bar", "Rooftop Bar", "Quán bar sân thượng tầng cao"),
    ("Q18", "Cà phê sách", "cafe", "Cà phê sách", "Không gian cà phê yên tĩnh đọc sách"),
    ("Q19", "Cửa hàng tiện lợi Circle K", "convenience_store", "Circle K", "Cửa hàng tiện lợi 24/7"),
    ("Q20", "Khách sạn Rex Hotel", "hotel", "Rex Hotel", "Khách sạn sang trọng trung tâm"),
    ("Q21", "Spa massage bấm huyệt", "spa", "Spa massage", "Cơ sở spa thư giãn chăm sóc sức khỏe"),
    ("Q22", "Bảo tàng Lịch sử", "historic_site", "Bảo tàng", "Điểm tham quan bảo tàng lịch sử"),
    ("Q23", "Trung tâm thương mại Takashimaya", "shopping", "Takashimaya", "Trung tâm thương mại Saigon Centre"),
    ("Q24", "Quán ốc Sài Gòn", "restaurant", "Quán ốc", "Quán ốc đêm phong cách Sài Gòn"),
    ("Q25", "Chợ Bến Thành", "shopping", "Chợ Bến Thành", "Địa điểm mua sắm du lịch biểu tượng")
]

for qid, text, cat, pref, desc in group1:
    QUERIES_DATA.append({
        "query_id": qid,
        "group_id": "G1",
        "group_name": "Tên và Danh mục cụ thể",
        "query_text": text,
        "target_category": cat,
        "specific_preference": pref,
        "description": desc,
        "origins": json.dumps([
            {"name": "User_Center", "lat": 10.7769, "lon": 106.7009}
        ], ensure_ascii=False)
    })

# ==============================================================================
# NHÓM 2: Tìm theo nhu cầu bằng ngôn ngữ tự nhiên (25 truy vấn)
# ==============================================================================
group2 = [
    ("Q26", "quán cafe yên tĩnh làm việc có ổ cắm điện và wifi mạnh", "coffee_shop", "yên tĩnh làm việc", "Tìm quán làm việc học tập"),
    ("Q27", "chỗ ăn tối lãng mạn cho 2 người hẹn hò cuối tuần", "restaurant", "lãng mạn hẹn hò", "Tìm không gian ẩm thực đôi lứa"),
    ("Q28", "quán cà phê view đẹp chụp ảnh sống ảo trung tâm quận 1", "cafe", "view đẹp chụp ảnh", "Tìm quán check-in chụp ảnh"),
    ("Q29", "quán ăn trưa văn phòng giá bình dân nhanh gọn", "vietnamese_restaurant", "cơm trưa văn phòng", "Ăn trưa công sở giá hợp lý"),
    ("Q30", "nhà hàng tiếp khách đối tác kinh doanh sang trọng lịch sự", "restaurant", "tiếp khách sang trọng", "Ẩm thực tiếp đối tác ngoại giao"),
    ("Q31", "quán trà sữa tụ tập bạn bè trò chuyện không gian rộng rãi", "coffee_shop", "trà sữa bạn bè", "Gặp mặt nhóm bạn trẻ"),
    ("Q32", "quán cà phê mở thâu đêm 24h yên tĩnh", "cafe", "mở xuyên đêm 24h", "Cà phê đêm cú đêm làm việc"),
    ("Q33", "quán ăn gia đình ấm cúng có món việt truyền thống", "vietnamese_restaurant", "ẩm thực gia đình", "Bữa cơm sum họp gia đình"),
    ("Q34", "quán cà phê sách yên tĩnh nhiều đầu sách hay", "cafe", "cà phê sách yên tĩnh", "Thư giãn đọc sách không ồn ào"),
    ("Q35", "quán rooftop ngắm cảnh hoàng hôn và đêm lung linh sài gòn", "bar", "rooftop view đẹp", "Ngắm cảnh thành phố về đêm"),
    ("Q36", "tiệm bánh ngọt phong cách pháp thưởng trà chiều", "bakery", "bánh ngọt trà chiều", "Thưởng thức trà chiều bánh ngọt"),
    ("Q37", "quán ăn chay thanh tịnh không gian nhẹ nhàng thanh lọc", "restaurant", "món chay thanh tịnh", "Ăn chay dưỡng tâm"),
    ("Q38", "quán bia chill có màn hình lớn xem bóng đá cùng bạn bè", "bar", "bia xem bóng đá", "Tụ tập xem thể thao uống bia"),
    ("Q39", "quán cà phê acoustic có nhạc sống trữ tình buổi tối", "cafe", "nhạc sống acoustic", "Cà phê nghe nhạc live"),
    ("Q40", "nhà hàng đồ nướng hàn quốc thịt tươi sốt đậm đà", "korean_restaurant", "nướng bbq hàn quốc", "Ăn thịt nướng Hàn Quốc"),
    ("Q41", "quán lẩu tokbokki buffet giá sinh viên", "restaurant", "lẩu tokbokki", "Ăn lẩu tokbokki buffet trẻ trung"),
    ("Q42", "quán sushi tươi ngon đúng điệu nhật bản đầu bếp chuyên nghiệp", "japanese_restaurant", "sushi tươi ngon", "Món Nhật chuẩn vị sashimi sushi"),
    ("Q43", "quán mì cay cấp độ chuẩn vị hàn quốc", "restaurant", "mì cay hải sản", "Mì cay 7 cấp độ ăn cay"),
    ("Q44", "tiệm cafe thú cưng thân thiện nhiều mèo dễ thương", "cafe", "cafe thú cưng", "Cà phê chơi với thú cưng"),
    ("Q45", "quán ăn vặt vỉa hè ngon bổ rẻ đậm chất đường phố sài gòn", "diner", "ăn vặt vỉa hè", "Ăn vặt đường phố"),
    ("Q46", "quán trà chiều kiểu anh quý tộc sang trọng", "cafe", "trà chiều kiểu anh", "Trà chiều cao cấp"),
    ("Q47", "nhà hàng dimsum trung hoa nóng hổi", "restaurant", "dimsum trung hoa", "Bữa điểm tâm dimsum nóng sốt"),
    ("Q48", "quán cà phê sân vườn thoáng mát nhiều cây xanh bóng mát", "cafe", "sân vườn nhiều cây xanh", "Không gian xanh mát gần thiên nhiên"),
    ("Q49", "quán cơm niêu truyền thống đập niêu cơm cháy giòn rụm", "vietnamese_restaurant", "cơm niêu truyền thống", "Cơm niêu đặc sản Việt Nam"),
    ("Q50", "nhà hàng bít tết beefsteak chuẩn âu kèm rượu vang", "restaurant", "beefsteak chuẩn âu", "Thưởng thức bít tết bò cao cấp")
]

for qid, text, cat, pref, desc in group2:
    QUERIES_DATA.append({
        "query_id": qid,
        "group_id": "G2",
        "group_name": "Nhu cầu bằng ngôn ngữ tự nhiên",
        "query_text": text,
        "target_category": cat,
        "specific_preference": pref,
        "description": desc,
        "origins": json.dumps([
            {"name": "User_Center", "lat": 10.7769, "lon": 106.7009}
        ], ensure_ascii=False)
    })

# ==============================================================================
# NHÓM 3: Tìm điểm hẹn cho nhiều người ở các vị trí khác nhau (25 truy vấn)
# ==============================================================================
# Tạo 25 kịch bản cuộc hẹn với các thành viên xuất phát từ các toạ độ mốc khác nhau
group3_configs = [
    # 2 Người
    ("Q51", "cafe hẹn gặp trao đổi công việc giữa 2 người", "coffee_shop", "cafe làm việc", 
     [LANDMARKS["Ben_Thanh"], LANDMARKS["Tan_Dinh"]]),
    ("Q52", "ăn trưa đối tác nhanh gọn thuận tiện cho 2 người", "restaurant", "cơm trưa văn phòng", 
     [LANDMARKS["Nguyen_Hue"], LANDMARKS["Turtle_Lake"]]),
    ("Q53", "cafe sáng cuối tuần hàn huyên giữa 2 bạn thân", "coffee_shop", "cafe trò chuyện", 
     [LANDMARKS["Bui_Vien"], LANDMARKS["Notre_Dame"]]),
    ("Q54", "quán trà sữa cho 2 người bạn gặp nhau buổi tối", "coffee_shop", "trà sữa", 
     [LANDMARKS["Bach_Dang"], LANDMARKS["Da_Kao"]]),
    ("Q55", "ăn tối lãng mạn điểm giữa thuận tiện đi lại", "restaurant", "hẹn hò lãng mạn", 
     [LANDMARKS["Bitexco"], LANDMARKS["Independence_Palace"]]),
    ("Q56", "quán cà phê yên tĩnh đọc sách gặp nhau cuối tuần", "cafe", "yên tĩnh đọc sách", 
     [LANDMARKS["Tan_Dinh"], LANDMARKS["Nguyen_Du"]]),
    ("Q57", "quán pizza ăn tối nhẹ nhàng thuận tiện 2 bạn", "restaurant", "pizza", 
     [LANDMARKS["Zoo"], LANDMARKS["Ben_Thanh"]]),
    ("Q58", "tiệm bánh ngọt trà chiều gần trung tâm", "bakery", "trà chiều bánh ngọt", 
     [LANDMARKS["Da_Kao"], LANDMARKS["Nguyen_Hue"]]),
    ("Q59", "quán ăn chay cho 2 người đi làm ra về", "restaurant", "món chay", 
     [LANDMARKS["Turtle_Lake"], LANDMARKS["Bui_Vien"]]),
    ("Q60", "quán rooftop thư giãn cuối ngày ngắm cảnh", "bar", "rooftop bar", 
     [LANDMARKS["Notre_Dame"], LANDMARKS["Bach_Dang"]]),

    # 3 Người
    ("Q61", "quán cafe họp nhóm 3 người làm dự án đồ án", "coffee_shop", "họp nhóm có wifi", 
     [LANDMARKS["Ben_Thanh"], LANDMARKS["Tan_Dinh"], LANDMARKS["Bach_Dang"]]),
    ("Q62", "ăn trưa văn phòng cho 3 đồng nghiệp từ 3 công ty khác nhau", "restaurant", "ăn trưa văn phòng", 
     [LANDMARKS["Nguyen_Hue"], LANDMARKS["Turtle_Lake"], LANDMARKS["Da_Kao"]]),
    ("Q63", "quán lẩu tụ tập 3 bạn thân cuối tuần", "restaurant", "lẩu hải sản", 
     [LANDMARKS["Bui_Vien"], LANDMARKS["Notre_Dame"], LANDMARKS["Zoo"]]),
    ("Q64", "cafe không gian rộng rãi bàn lớn cho 3 người trao đổi", "coffee_shop", "bàn họp rộng", 
     [LANDMARKS["Independence_Palace"], LANDMARKS["Bitexco"], LANDMARKS["Tan_Dinh"]]),
    ("Q65", "quán đồ nướng bbq tụ tập 3 người buổi tối", "restaurant", "nướng bbq", 
     [LANDMARKS["Nguyen_Du"], LANDMARKS["Da_Kao"], LANDMARKS["Bach_Dang"]]),
    ("Q66", "quán bia craft chill nhẹ sau giờ làm cho 3 người", "bar", "bia thủ công craft beer", 
     [LANDMARKS["Turtle_Lake"], LANDMARKS["Ben_Thanh"], LANDMARKS["Notre_Dame"]]),
    ("Q67", "tiệm trà sữa tụ tập nhóm bạn 3 người", "coffee_shop", "trà sữa bạn bè", 
     [LANDMARKS["Bui_Vien"], LANDMARKS["Zoo"], LANDMARKS["Nguyen_Hue"]]),
    ("Q68", "quán sushi nhật bản ấm cúng cho 3 thành viên", "japanese_restaurant", "sushi nhật bản", 
     [LANDMARKS["Tan_Dinh"], LANDMARKS["Bitexco"], LANDMARKS["Nguyen_Du"]]),

    # 4 Người
    ("Q69", "quán cafe họp nhóm 4 người chuẩn bị thuyết trình", "coffee_shop", "họp nhóm bàn lớn", 
     [LANDMARKS["Ben_Thanh"], LANDMARKS["Notre_Dame"], LANDMARKS["Tan_Dinh"], LANDMARKS["Bach_Dang"]]),
    ("Q70", "nhà hàng ăn tối gia đình 4 người không gian thoải mái", "restaurant", "ẩm thực gia đình", 
     [LANDMARKS["Turtle_Lake"], LANDMARKS["Bui_Vien"], LANDMARKS["Da_Kao"], LANDMARKS["Nguyen_Hue"]]),
    ("Q71", "quán lẩu nướng cuối tuần cho nhóm 4 bạn học cũ", "restaurant", "lẩu nướng tụ tập", 
     [LANDMARKS["Nguyen_Du"], LANDMARKS["Zoo"], LANDMARKS["Bitexco"], LANDMARKS["Tan_Dinh"]]),
    ("Q72", "quán cà phê sân vườn thoáng mát cho 4 bạn trò chuyện", "cafe", "sân vườn thoáng mát", 
     [LANDMARKS["Independence_Palace"], LANDMARKS["Da_Kao"], LANDMARKS["Bui_Vien"], LANDMARKS["Bach_Dang"]]),
    ("Q73", "quán cơm niêu truyền thống ăn trưa nhóm 4 người", "vietnamese_restaurant", "cơm niêu", 
     [LANDMARKS["Ben_Thanh"], LANDMARKS["Turtle_Lake"], LANDMARKS["Zoo"], LANDMARKS["Nguyen_Hue"]]),
    ("Q74", "quán bar pub thư giãn nghe nhạc cuối tuần cho nhóm 4 người", "bar", "pub nghe nhạc", 
     [LANDMARKS["Bui_Vien"], LANDMARKS["Notre_Dame"], LANDMARKS["Tan_Dinh"], LANDMARKS["Bitexco"]]),
    ("Q75", "tiệm trà chiều bánh ngọt cho nhóm 4 chị em hẹn hò", "bakery", "trà chiều bánh ngọt", 
     [LANDMARKS["Bach_Dang"], LANDMARKS["Nguyen_Du"], LANDMARKS["Da_Kao"], LANDMARKS["Turtle_Lake"]])
]

for qid, text, cat, pref, landmarks in group3_configs:
    origins_list = []
    for idx, lm in enumerate(landmarks):
        member_name = f"Thành viên {idx+1} ({lm['name']})"
        origins_list.append({
            "name": member_name,
            "lat": lm["lat"],
            "lon": lm["lon"]
        })
    QUERIES_DATA.append({
        "query_id": qid,
        "group_id": "G3",
        "group_name": "Điểm hẹn nhiều người (Multi-origin Meeting)",
        "query_text": text,
        "target_category": cat,
        "specific_preference": pref,
        "description": f"Cuộc hẹn {len(origins_list)} người tại các vị trí phân tán Quận 1",
        "origins": json.dumps(origins_list, ensure_ascii=False)
    })

def main():
    out_path = "research/queries.csv"
    with open(out_path, mode="w", newline="", encoding="utf-8") as f:
        fieldnames = ["query_id", "group_id", "group_name", "query_text", "target_category", "specific_preference", "participant_count", "description", "origins_json"]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for q in QUERIES_DATA:
            origins = json.loads(q["origins"])
            writer.writerow({
                "query_id": q["query_id"],
                "group_id": q["group_id"],
                "group_name": q["group_name"],
                "query_text": q["query_text"],
                "target_category": q["target_category"],
                "specific_preference": q["specific_preference"],
                "participant_count": len(origins),
                "description": q["description"],
                "origins_json": q["origins"]
            })
    print(f"-> Đã tạo thành công {len(QUERIES_DATA)} truy vấn kiểm thử chuẩn tại: {out_path}")
    print(f"   - Nhóm 1 (Tên/Danh mục cụ thể): {len(group1)} truy vấn")
    print(f"   - Nhóm 2 (Ngôn ngữ tự nhiên): {len(group2)} truy vấn")
    print(f"   - Nhóm 3 (Điểm hẹn đa thành viên): {len(group3_configs)} truy vấn (gồm 10 kịch bản 2 người, 8 kịch bản 3 người, 7 kịch bản 4 người)")

if __name__ == "__main__":
    main()
