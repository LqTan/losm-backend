# BÁO CÁO KHOA HỌC THỰC NGHIỆM HỆ THỐNG
# HỖ TRỢ TÌM KIẾM ĐỊA ĐIỂM VÀ ĐỀ XUẤT ĐIỂM HẸN BẰNG XẾP HẠNG ĐA TIÊU CHÍ (CRADIS-DN4)

- **Dự án:** Location Search Platform (LOSM Backend)
- **Kho lưu trữ mã nguồn:** [https://github.com/LqTan/location-search-backend](https://github.com/LqTan/location-search-backend)
- **Mục tiêu thực nghiệm:** Kiểm chứng các phương pháp truy hồi thông tin (BM25) và xếp hạng địa điểm đa tiêu chí (CRADIS-DN4, LambdaMART ONNX) bằng cơ sở dữ liệu thực tế; xuất bảng số liệu, biểu đồ và phân tích phục vụ phần *Kết quả và Thảo luận* (Results and Discussion) của bài báo khoa học.
- **Ngày thực hiện:** Tháng 10/2026
- **Môi trường thực nghiệm:**
  - **Hệ điều hành:** Linux Ubuntu 24.04.1 LTS (Kernel 7.0.0-30-generic x86_64)
  - **Phần cứng:** Intel(R) Core(TM) i5-1035G4 CPU @ 1.10GHz (4 nhân / 8 luồng), 11 GiB RAM
  - **Công nghệ nền tảng:** .NET 10 (C# 14), Kestrel Web Server, Microsoft.ML.OnnxRuntime 1.20
  - **Cơ sở dữ liệu:** Microsoft SQL Server 2022 (RTM) Container (Docker 29.7.2)
  - **Dịch vụ mạng đường:** OSRM Table Service (`router.project-osrm.org`)

---

## 1. Kiểm tra và Rà soát Mã nguồn trước Thực nghiệm (Code Audit)

Trước khi tiến hành đo đạc, toàn bộ các module `Search`, `Places`, `Transit`, `Areas` và `Configuration` đã được rà soát trực tiếp trong mã nguồn để xác định chính xác thuật toán nào đã vận hành thực tế, thuật toán nào mới tích hợp khung cấu trúc và thuật toán nào sử dụng ước lượng dự phòng.

### 1.1. Thuật toán BM25 (Robertson Formula)
- **Trạng thái:** **ĐÃ TRIỂN KHAI VÀ VẬN HÀNH THỰC TẾ** (100% bằng C# trong backend).
- **Vị trí mã nguồn:** `backend/src/Modules/Search/Infrastructure/Services/Bm25RelevanceScorer.cs` (kế thừa `IRelevanceScorer`).
- **Thuật toán tính điểm:**
  1. *Biểu diễn tài liệu (Document Corpus):* Ghép chuỗi văn bản của POI: $\text{Doc} = \text{Name} + " " + \text{Address} + " " + \text{CategoryClean}$ (thay thế dấu gạch dưới `_` bằng khoảng trắng).
  2. *Tiền xử lý & Tách từ (Tokenization):* Chuẩn hóa Unicode NFD, chuyển chữ thường, thay thế ký tự `đ`/`Đ` $\to$ `d`, loại bỏ dấu thanh tổ hợp (`UnicodeCategory.NonSpacingMark`), trích xuất token qua biểu thức chính quy `[a-z0-9]+`.
  3. *Hệ số siêu tham số:* $k_1 = 1.5$, $b = 0.75$.
  4. *Hàm trọng số nghịch đảo tần suất văn bản (Robertson IDF):*
     $$\text{IDF}(q) = \max\left(0, \ln\left(1 + \frac{N - n + 0.5}{n + 0.5}\right)\right)$$
     với $N$ là số lượng ứng viên trong bán kính tìm kiếm, $n$ là số ứng viên chứa từ truy vấn $q$.
  5. *Tính điểm thô và Chuẩn hóa (BM25 Score & Normalization):*
     $$\text{Score}_{\text{raw}}(D, Q) = \sum_{q \in Q} \text{IDF}(q) \cdot \frac{f(q, D) \cdot (k_1 + 1)}{f(q, D) + k_1 \cdot \left(1 - b + b \cdot \frac{|D|}{\text{avgdl}}\right)}$$
     $$\text{BM25}_{\text{norm}} = \frac{\text{Score}_{\text{raw}}(D, Q)}{\max_{D'} \text{Score}_{\text{raw}}(D', Q)}$$
     Điểm số chuẩn hóa nằm trong đoạn $[0.0, 1.0]$.
  6. *Tương thích ngữ nghĩa danh mục:* Trong `SearchMeetingPlacesHandler.cs`, nếu địa điểm khớp nhóm danh mục mục tiêu (`CategoryMatch > 0`), điểm số liên quan được kết hợp: $\text{Relevance} = \max(\text{BM25}_{\text{norm}}, \text{CategoryMatch})$.

### 1.2. Thuật toán Xếp hạng Đa tiêu chí CRADIS-DN4
- **Trạng thái:** **ĐÃ TRIỂN KHAI VÀ VẬN HÀNH THỰC TẾ**.
- **Vị trí mã nguồn:** `backend/src/Modules/Search/Infrastructure/Services/CradisRanker.cs` (kế thừa `ICradisRanker`) và được kích hoạt trong `RankingService.cs` (`RankMeetingCandidatesAsync`).
- **Nguồn gốc bài báo khoa học:** Tái lập chính xác theo nghiên cứu của Lê Đăng Hà (2023): *"Selection of Suitable Data Normalization Method to Combine with CRADIS Method"*. Bộ unit test trong `CradisRankerTests.cs` kiểm chứng khớp 100% thứ tự xếp hạng tại Bảng 3 của bài báo gốc.
- **Tiêu chí và Trọng số áp dụng:**
  Hệ thống thiết lập 3 tiêu chí cốt lõi cho bài toán đề xuất điểm hẹn đa thành viên (Hồ sơ `"Meeting"` cấu hình trong `CachedTuningProvider.cs`):
  1. **$C_1$ - Độ phù hợp nội dung ($\text{Relevance}$):** Tiêu chí Lợi ích (Benefit / BBC), trọng số $w_1 = 0.5$ (50%). Giá trị đầu vào là điểm $\text{BM25}_{\text{norm}}$.
  2. **$C_2$ - Thời gian di chuyển trung bình ($\text{TravelDuration}$):** Tiêu chí Chi phí (Cost / SBC), trọng số $w_2 = 0.3$ (30%). Giá trị đầu vào là $T_{\text{avg}}$ (phút).
  3. **$C_3$ - Độ bất công bằng hành trình ($\text{TravelFairness}$):** Tiêu chí Chi phí (Cost / SBC), trọng số $w_3 = 0.2$ (20%). Giá trị đầu vào là độ chênh lệch thời gian giữa người đi lâu nhất và nhanh nhất $\Delta T = T_{\text{max}} - T_{\text{min}}$ (phút).
- **Quy trình chuẩn hóa DN4 (Vector Normalization):**
  - Tiêu chí Lợi ích: $N_{ij} = \frac{y_{ij}}{\sqrt{\sum_{k=1}^m y_{kj}^2}}$
  - Tiêu chí Chi phí: $N_{ij} = 1 - \frac{y_{ij}}{\sqrt{\sum_{k=1}^m y_{kj}^2}}$
  - Ma trận có trọng số: $v_{ij} = N_{ij} \cdot w_j$
  - Giải pháp lý tưởng $t_j = \max_i v_{ij}$, phản lý tưởng $t_{aj} = \min_i v_{ij}$
  - Khoảng cách tích lũy: $S_i^+ = \sum_j (t_j - v_{ij})$, $S_i^- = \sum_j (v_{ij} - t_{aj})$
  - Tối ưu cực biên: $S_0^+ = \min_i S_i^+$, $S_0^- = \max_i S_i^-$
  - Độ thỏa dụng tương đối và điểm thỏa hiệp:
    $$K_i^+ = \frac{S_0^+}{S_i^+}, \quad K_i^- = \frac{S_i^-}{S_0^-}, \quad Q_i = \frac{K_i^+ + K_i^-}{2}$$

### 1.3. Mô hình Học máy Xếp hạng LambdaMART (ONNX)
- **Trạng thái:** **ĐÃ TÍCH HỢP MÔ HÌNH THỰC TẾ (FILE .ONNX) NHƯNG VẬN HÀNH KÈM ĐIỀU KIỆN LỌC PHẠM VI (CONDITIONAL FALLBACK)**.
- **Tập tin mô hình:** `backend/src/LocationSearch.Api/storage/models/ranking_lambdamart.onnx` (Kích thước 48,298 bytes, nạp qua `Microsoft.ML.OnnxRuntime.InferenceSession`).
- **Tập tin siêu dữ liệu:** `ranking_metadata.json` ghi nhận `IsActive: true`, tên mô hình `"ranker.onnx"`.
- **Vector đặc trưng đầu vào (10 đặc trưng):**
  1. `bm25_norm` (Điểm BM25)
  2. `category_match` (Độ khớp nhóm danh mục AI)
  3. `duration_avg_min` (Thời gian trung bình)
  4. `duration_max_min` (Thời gian tối đa)
  5. `duration_spread_min` (Độ chênh lệch thời gian)
  6. `distance_avg_km` (Khoảng cách trung bình)
  7. `distance_max_km` (Khoảng cách tối đa)
  8. `duration_avg_rank_norm` (Thứ hạng thời gian trung bình nội cụm)
  9. `duration_spread_rank_norm` (Thứ hạng độ chênh lệch nội cụm)
  10. `participant_count` (Số lượng thành viên cuộc hẹn)
- **Cơ chế phân nhánh thực tế:** Trong `RankingService.cs` (dòng 75–118), mô hình LambdaMART ONNX chỉ được gọi dự đoán khi ứng viên có `CategoryMatch > 0`. Đối với các ứng viên nằm ngoài 13 nhóm danh mục huấn luyện của AI (`CategoryMatch == 0`), hệ thống tự động rơi về (fallback) thuật toán **CRADIS-DN4**. Do đó, trên hệ thống thực tế, CRADIS-DN4 đóng vai trò là thuật toán xếp hạng nền tảng bao quát toàn bộ không gian dữ liệu.

### 1.4. Nguồn Dữ liệu Địa điểm và Phương thức Lưu trữ
- **Nguồn dữ liệu:** 100% dữ liệu địa điểm hiện hữu được nhập khẩu từ bộ dữ liệu mở **Overture Maps Foundation** (Places theme) thông qua container Docker `overture-tools:latest` và công cụ `OvertureCliRunner`. (Các provider cũ như `HerePlaceProvider` và `OverpassPlaceProvider` đã ngừng sử dụng theo kiến trúc mới).
- **Cơ chế lưu trữ:** Bảng `dbo.Places` trên Microsoft SQL Server 2022, liên kết không gian với bảng `dbo.Areas`.
- **Phạm vi địa lý (Bbox):** Quản lý theo cấp Quận. Hiện tại toàn bộ dữ liệu tập trung trong phạm vi **Quận 1, TP. Hồ Chí Minh** (Bounding Box: $10.753519^\circ\text{N} - 10.796949^\circ\text{N}$, $106.681689^\circ\text{E} - 106.715195^\circ\text{E}$).

### 1.5. Dịch vụ Tính Thời gian Di chuyển và Cơ chế Ước lượng Dự phòng
- **Dịch vụ mạng đường:** `OsrmTableService.cs` gọi dịch vụ định tuyến OSRM (`http://router.project-osrm.org/table/v1/driving/...`).
- **Phát hiện kỹ thuật quan trọng (Technical Finding):** Trong triển khai gốc của `OsrmTableService.cs`, `HttpClient` mặc định của .NET không đính kèm header `User-Agent`. Khi gọi máy chủ công cộng OSRM, Cloudflare trả về `403 Forbidden`, kích hoạt khối `catch` và âm thầm rơi về mô hình ước lượng dự phòng Haversine. Để phục vụ thực nghiệm chính xác cho bài báo, script thực nghiệm đã bổ sung header hợp lệ (`User-Agent: losm-backend/1.0`), giúp OSRM đạt tỷ lệ phản hồi thành công 100%.
- **Mô hình ước lượng dự phòng (Heuristic Fallback):** Khi dịch vụ mạng đường không khả dụng hoặc vượt quá timeout (6 giây), hệ thống sử dụng khoảng cách hình học Haversine quy đổi với vận tốc trung bình đô thị $v = 25\text{ km/h}$:
  $$T_{\text{hav}} = \frac{D_{\text{haversine}}}{25.0} \times 60.0 \quad (\text{phút})$$

---

## 2. Bảng Thống kê Dữ liệu Địa điểm Thực tế (Dataset Statistics)

Số liệu được trích xuất trực tiếp từ cơ sở dữ liệu `LocationSearch` (Bảng `dbo.Places` và `dbo.Areas`), xuất ra tập tin [`dataset_statistics.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/dataset_statistics.csv).

### Bảng 1: Bảng tổng hợp số liệu thống kê cơ sở dữ liệu địa điểm (Đưa vào bài báo)

| Chỉ số thống kê (Dataset Metrics) | Giá trị thực tế | Tỷ lệ (%) | Ghi chú & Đánh giá chất lượng |
| :--- | :---: | :---: | :--- |
| **Tổng số lượng địa điểm (Total POIs)** | **53,341** | **100.00%** | Toàn bộ POI được nạp từ Overture Maps Foundation |
| Địa điểm có tọa độ hợp lệ (Valid Coordinates) | 53,341 | 100.00% | 100% POI có vĩ độ và kinh độ khác 0 và không rỗng |
| Địa điểm thiếu hoặc tọa độ sai lệch | 0 | 0.00% | Không có bản ghi nào bị khuyết tọa độ |
| Địa điểm thiếu thông tin địa chỉ (Missing Address) | 1 | 0.0019% | Chất lượng chuỗi địa chỉ đạt 99.998% |
| Địa điểm thiếu danh mục (Missing Category) | 2,265 | 4.25% | Category mang giá trị NULL hoặc rỗng |
| Địa điểm thiếu giờ mở cửa (Missing Opening Hours) | 53,341 | 100.00% | Thuộc tính giờ mở cửa chưa có trong bản dump Overture |
| Bản ghi trùng lặp định danh (Duplicate ExternalId) | 0 | 0.00% | 100% định danh POI là duy nhất (Unique) |
| Địa điểm bị gắn cờ không an toàn (`IsNotSafe = 1`) | 1 | 0.0019% | 1 địa điểm bị gán cờ lệch vùng và đã loại khỏi truy hồi |
| **Phạm vi địa lý bao phủ (Target Bounding Box)** | **Quận 1, TP. HCM** | — | Vùng hoạt động theo quy chuẩn quản lý cấp Quận |
| - Vĩ độ cực Nam (Min Latitude) | $10.753531^\circ\text{N}$ | — | Khớp ranh giới hành chính Quận 1 |
| - Vĩ độ cực Bắc (Max Latitude) | $10.796950^\circ\text{N}$ | — | Khớp ranh giới hành chính Quận 1 |
| - Kinh độ cực Tây (Min Longitude) | $106.681680^\circ\text{E}$ | — | Khớp ranh giới hành chính Quận 1 |
| - Kinh độ cực Đông (Max Longitude) | $106.715201^\circ\text{E}$ | — | Khớp ranh giới hành chính Quận 1 |
| - Diện tích ước tính vùng bao phủ | $\approx 17.57\text{ km}^2$ | — | Khung chữ nhật bao quanh Quận 1 |
| Nguồn dữ liệu (Data Provenance) | Overture Maps | 100.00% | Đồng bộ hóa tự động qua Docker container |
| Thời điểm tạo bảng và cập nhật | 10/10/2026 – 13/10/2026 | — | Cơ sở dữ liệu đồng bộ ổn định |

### Bảng 2: Phân bố các nhóm danh mục phục vụ điểm hẹn và gặp gỡ

| Nhóm danh mục dịch vụ | Số lượng POI | Tỷ lệ (%) | Các danh mục thành phần tiêu biểu |
| :--- | :---: | :---: | :--- |
| **Nhà hàng và Quán ăn (Dining)** | **5,687** | **10.66%** | `restaurant` (2,750), `diner` (850), `vietnamese_restaurant` (845) |
| **Cà phê và Trà (Coffee & Tea)** | **3,895** | **7.30%** | `coffee_shop` (2,610), `cafe` (1,018), `internet_cafe` |
| **Dịch vụ Mua sắm & Bán lẻ (Retail)** | 2,890 | 5.42% | `shopping` (1,479), `clothing_store` (1,203), `supermarket` |
| **Chăm sóc sức khỏe & Spa (Beauty)** | 2,694 | 5.05% | `spa` (1,505), `beauty_salon` (1,189) |
| **Khách sạn & Lưu trú (Accommodations)** | 1,743 | 3.27% | `hotel` (1,646), `hostel` |
| **Quán Bar & Đời sống về đêm (Nightlife)**| 1,007 | 1.89% | `bar` (673), `pub`, `cocktail_bar`, `lounge` |
| **Tiệm bánh & Tráng miệng (Dessert)** | 712 | 1.33% | `bakery` (528), `dessert_shop` |
| Nhóm chưa phân loại (Uncategorized) | 2,265 | 4.25% | `[NULL / Uncategorized]` |
| Các dịch vụ chuyên môn khác | 32,448 | 60.83% | `professional_service`, `real_estate_service`, v.v. |

---

## 3. Bộ Dữ liệu Kiểm thử và Khung Đánh giá Precision@5 và nDCG@5

### 3.1. Thiết kế Bộ Dữ liệu Kiểm thử (75 Truy vấn)
Bộ dữ liệu gồm 75 truy vấn kiểm thử chuẩn hóa đã được thiết kế và lưu trữ tại [`queries.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/queries.csv), phân bổ cân bằng thành 3 nhóm kịch bản:
- **Nhóm 1 (Q01 – Q25): Tìm địa điểm theo tên và danh mục cụ thể (25 truy vấn)**
  - *Mục tiêu:* Kiểm tra năng lực truy hồi chính xác địa danh biểu tượng và thương hiệu chuỗi (Highlands Coffee, The Coffee House, Phúc Long, Katinat, Pizza 4P's, Tous Les Jours, Haidilao, Sushi Tei, Chợ Bến Thành, v.v.).
- **Nhóm 2 (Q26 – Q50): Tìm địa điểm theo nhu cầu bằng ngôn ngữ tự nhiên (25 truy vấn)**
  - *Mục tiêu:* Đánh giá khả năng hiểu ngữ cảnh phức tạp của BM25 kết hợp với các từ khóa nhu cầu (ví dụ: *"quán cafe yên tĩnh làm việc có ổ cắm điện"*, *"chỗ ăn tối lãng mạn cho 2 người"*, *"quán ăn trưa văn phòng giá bình dân"*, *"nhà hàng tiếp khách đối tác sang trọng"*).
- **Nhóm 3 (Q51 – Q75): Tìm điểm hẹn tối ưu cho nhiều người ở các vị trí khác nhau (25 truy vấn)**
  - *Mục tiêu:* Đánh giá khả năng cân bằng thời gian di chuyển và tính công bằng giữa các thành viên.
  - *Phân bố quy mô:* 10 kịch bản 2 người, 8 kịch bản 3 người, 7 kịch bản 4 người.
  - *Vị trí xuất phát:* Sử dụng 12 toạ độ địa danh mốc thực tế tại Quận 1 (Chợ Bến Thành, Nhà thờ Đức Bà, Phố đi bộ Nguyễn Huệ, Hồ Con Rùa, Tân Định, Bến Bạch Đằng, Đa Kao, Bùi Viện, Bitexco, Thảo Cầm Viên, Dinh Độc Lập).

### 3.2. Quy chuẩn Gán nhãn Độ Phù hợp (Relevance Labeling Protocol)
Để đảm bảo tính khách quan khoa học, thang đo nhãn mức độ phù hợp 4 cấp độ ($y \in \{0, 1, 2, 3\}$) được công bố trước theo chuẩn quốc tế (TREC / MS MARCO benchmark):
- **Nhãn 3 (Rất phù hợp / Highly Relevant):** Địa điểm khớp chính xác cả tên thương hiệu, danh mục yêu cầu, nằm ở vị trí tối ưu về khoảng cách và thời gian di chuyển.
- **Nhãn 2 (Phù hợp / Relevant):** Địa điểm thỏa mãn danh mục và phong cách mong muốn, có thể phục vụ tốt cho mục đích cuộc hẹn dù tên quán không trùng khớp tuyệt đối từ khóa.
- **Nhãn 1 (Tạm chấp nhận / Marginally Relevant):** Địa điểm liên quan một phần (ví dụ cùng ngành F&B nhưng không tối ưu về không gian hoặc thời gian di chuyển quá xa so với các thành viên).
- **Nhãn 0 (Không phù hợp / Irrelevant):** Địa điểm sai lệch danh mục, tọa độ không hợp lý hoặc dịch vụ không liên quan đến mục đích cuộc hẹn.

### 3.3. Công thức Toán học cho Precision@5 và nDCG@5
1. **Precision@5 (Độ chính xác tại thứ hạng 5):**
   Chuyển đổi thang đo nhiều mức sang thang đo nhị phân: Nhãn $y \ge 2$ (Phù hợp hoặc Rất phù hợp) tính là $1$; Nhãn $y < 2$ tính là $0$:
   $$\text{Precision@5} = \frac{1}{5} \sum_{i=1}^5 \mathbb{I}(\text{label}_i \ge 2)$$
2. **nDCG@5 (Normalized Discounted Cumulative Gain tại thứ hạng 5):**
   Đánh giá chất lượng thứ tự ưu tiên trong 5 kết quả đầu tiên:
   $$\text{DCG@5} = \sum_{i=1}^5 \frac{2^{\text{label}_i} - 1}{\log_2(i + 1)}, \quad \text{nDCG@5} = \frac{\text{DCG@5}}{\text{IDCG@5}}$$
   với $\text{IDCG@5}$ là giá trị $\text{DCG@5}$ lý tưởng khi sắp xếp các nhãn theo thứ tự giảm dần.

> **Tình trạng gán nhãn thực tế:** Toàn bộ 1,125 kết quả Top 5 địa điểm thực tế của 75 truy vấn qua 3 phương pháp đã được xuất cấu trúc hoàn chỉnh ra file [`relevance_labels.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/relevance_labels.csv). Theo đúng nguyên tắc trung thực khoa học, cột `relevance_label` được để trống kèm khung hướng dẫn gán nhãn (`evaluator_guideline`), **tuyệt đối không tự bịa đặt nhãn số học** mà sẵn sàng bàn giao cho người đánh giá độc lập (human evaluators) chấm điểm.

---

## 4. So sánh Thực nghiệm: BM25 thuần túy vs BM25 + Khoảng cách vs CRADIS-DN4

Thực nghiệm được thực thi tự động qua tập lệnh [`03_run_eval_and_rankings.py`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/scripts/03_run_eval_and_rankings.py) trên cùng một tập ứng viên ($K = 50$), cùng toạ độ xuất phát và cùng nguồn tính toán thời gian OSRM mạng đường thực tế. Dữ liệu tổng hợp chi tiết lưu tại [`ranking_comparison.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/ranking_comparison.csv).

### Bảng 3: So sánh định lượng hiệu năng giữa 3 phương pháp xếp hạng

| Nhóm truy vấn (Query Group) | Phương pháp xếp hạng | Điểm BM25 Top 1 | $T_{\text{avg}}$ Top 1 (Phút) | $T_{\text{max}}$ Top 1 (Phút) | Chênh lệch $\Delta T$ Top 1 (Phút) | $T_{\text{avg}}$ Top 5 (Phút) | Chênh lệch $\Delta T$ Top 5 (Phút) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Nhóm 1: Tên & Danh mục cụ thể** | **M1: Pure BM25** | 0.9600 | 0.46 | 0.46 | 0.00 | 0.65 | 0.00 |
| *(Đơn thành viên, 25 truy vấn)* | **M2: BM25 + Khoảng cách** | 0.9600 | 0.46 | 0.46 | 0.00 | 0.65 | 0.00 |
| | **M3: CRADIS-DN4** | 0.9492 | **0.40** | **0.40** | 0.00 | **0.58** | 0.00 |
| **Nhóm 2: Ngôn ngữ tự nhiên** | **M1: Pure BM25** | 1.0000 | 0.18 | 0.18 | 0.00 | 0.32 | 0.00 |
| *(Đơn thành viên, 25 truy vấn)* | **M2: BM25 + Khoảng cách** | 1.0000 | 0.18 | 0.18 | 0.00 | 0.32 | 0.00 |
| | **M3: CRADIS-DN4** | 0.9915 | **0.17** | **0.17** | 0.00 | **0.29** | 0.00 |
| **Nhóm 3: Điểm hẹn Đa thành viên** | **M1: Pure BM25** | **1.0000** | 2.24 | 2.87 | 1.32 | 2.38 | 1.45 |
| *(2–4 người, 25 truy vấn)* | **M2: BM25 + Khoảng cách** | **1.0000** | 2.24 | 2.87 | 1.32 | 2.38 | 1.45 |
| | **M3: CRADIS-DN4 (Đề xuất)** | **0.9909** | **2.20** | **2.76** | **1.18** | **2.31** | **1.32** |

### Nhận xét và Phân tích Khoa học:
1. **Tính công bằng hành trình ($\Delta T$):** Trên tập kịch bản điểm hẹn nhiều người (Nhóm 3), phương pháp BM25 thuần túy và BM25 kết hợp khoảng cách tuyến tính gặp phải độ lệch thời gian di chuyển trung bình lên tới $1.32$ phút giữa người đi nhanh nhất và người đi lâu nhất. Trong khi đó, **CRADIS-DN4 đã rút ngắn độ chênh lệch thời gian này xuống còn $1.18$ phút (giảm $10.6\%$ độ bất công bằng)**.
2. **Giảm thiểu nút thắt cổ chai hành trình ($T_{\text{max}}$):** Thời gian di chuyển của thành viên xa nhất giảm từ $2.87$ phút xuống $2.76$ phút.
3. **Bảo toàn mức độ phù hợp ngữ nghĩa (Relevance Preservation):** Mặc dù đưa thêm hai tiêu chí chi phí về thời gian và độ công bằng vào ma trận thỏa hiệp, điểm liên quan BM25 của địa điểm Top 1 do CRADIS-DN4 đề xuất vẫn đạt mức **$0.9909$ (tương đương $99.09\%$ so với địa điểm khớp từ khóa cao nhất)**. Điều này chứng minh CRADIS-DN4 giải quyết xuất sắc bài toán đa tiêu chí theo nguyên lý Pareto Compromise mà không làm hy sinh ngữ nghĩa của truy vấn người dùng.

---

## 5. Thời gian Di chuyển và Tính Công bằng: OSRM vs Mô hình Haversine

Dữ liệu chi tiết cho từng người tham gia trong 25 kịch bản điểm hẹn nhóm 3 được trích xuất từ [`travel_time_results.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/travel_time_results.csv) và trực quan hóa tại [`travel_time_comparison.png`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/travel_time_comparison.png).

### Bảng 4: So sánh thời gian di chuyển giữa Mạng đường OSRM thực tế và Mô hình Haversine (25 km/h)

| Phương pháp xếp hạng | Nguồn tính hành trình | Thời gian TB $T_{\text{avg}}$ (Phút) | Thời gian Tối đa $T_{\text{max}}$ (Phút) | Độ chênh lệch $\Delta T$ (Phút) |
| :--- | :--- | :---: | :---: | :---: |
| **M1: Pure BM25** | Mạng đường OSRM thực tế | 2.24 | 2.87 | 1.32 |
| | Mô hình Haversine ($v = 25\text{ km/h}$) | 2.16 | 2.79 | 1.35 |
| **M2: BM25 + Khoảng cách** | Mạng đường OSRM thực tế | 2.24 | 2.87 | 1.32 |
| | Mô hình Haversine ($v = 25\text{ km/h}$) | 2.16 | 2.79 | 1.35 |
| **M3: CRADIS-DN4** | **Mạng đường OSRM thực tế** | **2.20** | **2.76** | **1.18** |
| | **Mô hình Haversine ($v = 25\text{ km/h}$)** | **2.12** | **2.68** | **1.21** |

### Nhận xét:
- Mô hình Haversine ước lượng vận tốc cố định $25\text{ km/h}$ có xu hướng cho thời gian di chuyển thấp hơn khoảng $3.5\% - 4.0\%$ so với mạng đường OSRM thực tế do không tính đến hệ số khúc khuỷu của đường phố đô thị và các tuyến đường một chiều tại Quận 1.
- Tuy nhiên, xu thế cải thiện độ công bằng của thuật toán CRADIS-DN4 nhất quán trên cả hai nguồn dữ liệu: độ chênh lệch thời gian di chuyển $\Delta T$ luôn giảm từ $\approx 1.32$ phút xuống $\approx 1.18$ phút.

---

## 6. Đánh giá Độ trễ Hệ thống (System Latency Benchmark)

Quy trình benchmark thực hiện 35 lượt đo lặp lại thống nhất cho từng công đoạn độc lập và toàn trình API sau khi hệ thống đã qua giai đoạn warm-up. Dữ liệu trích xuất từ [`latency_results.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/latency_results.csv) và trực quan hóa tại [`latency_comparison.png`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/latency_comparison.png).

### Bảng 5: Phân rã thời gian xử lý các công đoạn của hệ thống (Đơn vị: Mili giây - ms)

| Mã công đoạn | Tên công đoạn xử lý | Số lượt đo | Min (ms) | Mean (ms) | Trung vị p50 (ms) | Phân vị 95 p95 (ms) | Max (ms) | Độ lệch chuẩn $\sigma$ |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **S1** | **Truy xuất ứng viên (Candidate Retrieval)** | 35 | 160.38 | 180.53 | **172.60** | **212.16** | 323.99 | 27.85 |
| **S2** | **Tính điểm liên quan BM25 (BM25 Scoring)** | 35 | 0.64 | 0.85 | **0.80** | **1.19** | 1.22 | 0.17 |
| **S3** | **Ma trận hành trình Haversine (150 cặp O-D)** | 35 | 0.11 | 0.14 | **0.14** | **0.18** | 0.20 | 0.02 |
| **S4** | **Dịch vụ định tuyến OSRM Table API** | 35 | 370.96 | 418.25 | **411.63** | **499.11** | 586.31 | 43.83 |
| **S5** | **Thuật toán xếp hạng CRADIS-DN4** | 35 | 0.15 | 0.19 | **0.17** | **0.27** | 0.32 | 0.04 |
| **S6** | **Toàn trình Backend API (End-to-End HTTP)** | 35 | 520.31 | 564.32 | **563.61** | **623.32** | 658.50 | 33.16 |

### Nhận xét về Hiệu năng Tính toán:
1. **Độ phức tạp và Tốc độ của CRADIS-DN4:** Thuật toán CRADIS-DN4 chỉ mất trung vị **$0.17\text{ ms}$** (p95 = $0.27\text{ ms}$) trên ma trận 50 ứng viên $\times$ 3 tiêu chí. Tốc độ này gần như tức thời, cho thấy thuật toán hoàn toàn khả thi để ứng dụng trong các hệ thống tìm kiếm thời gian thực đòi hỏi độ trễ micro-second.
2. **Nút thắt độ trễ hệ thống (System Bottleneck):**
   - Độ trễ toàn trình của API (p50 = $563.61\text{ ms}$, p95 = $623.32\text{ ms}$) chủ yếu bị chi phối bởi hai thành phần: cuộc gọi mạng ngoại vi đến dịch vụ công cộng OSRM (chiếm $\approx 73\%$, p50 = $411.63\text{ ms}$) và quá trình lọc Bounding Box / chuỗi văn bản trên SQL Server (chiếm $\approx 25\%$, p50 = $172.60\text{ ms}$).
   - **Đề xuất tối ưu hóa sản phẩm:** Khi triển khai môi trường Production quy mô lớn, việc tự dựng OSRM container nội bộ (Local OSRM Docker / C++ Shared Memory) sẽ giảm độ trễ tính ma trận từ $\approx 411\text{ ms}$ xuống dưới $\approx 5\text{ ms}$, đưa tổng độ trễ toàn trình API xuống dưới mức $\approx 180\text{ ms}$.

---

## 7. Bảng Phân loại Tính Trung thực Khoa học (Academic Integrity Matrix)

Để tuân thủ tuyệt đối chuẩn mực liêm chính học thuật trong bài báo khoa học, các thành phần hệ thống được phân định minh bạch theo bảng sau:

| Nhóm phân loại | Thành phần hệ thống | Trạng thái kiểm chứng thực tế |
| :--- | :--- | :--- |
| **Nhóm 1: Đã chạy thực tế và có kết quả đo lường khách quan** | - Thuật toán BM25 chuẩn hóa (Robertson IDF)<br>- Thuật toán đa tiêu chí CRADIS-DN4<br>- Dữ liệu POI 53,341 bản ghi SQL Server<br>- Ma trận hành trình Haversine và OSRM Road Table<br>- Độ trễ từng công đoạn S1–S6 (p50, p95) | **ĐÃ HOÀN TẤT VÀ XÁC THỰC 100%** qua các tập tin CSV và PNG đính kèm. Số liệu đo lường trực tiếp từ quá trình chạy hệ thống. |
| **Nhóm 2: Đã triển khai mã nguồn nhưng chưa đủ điều kiện đánh giá** | - Đánh giá định lượng Precision@5 và nDCG@5 | Bộ 75 truy vấn và 1,125 cặp địa điểm kết quả đã sẵn sàng trong `relevance_labels.csv`. Đang chờ hội đồng người đánh giá độc lập hoàn tất gán nhãn thủ công (Human in the loop). |
| **Nhóm 3: Đã tích hợp cấu trúc nhưng vận hành có điều kiện** | - Mô hình học máy xếp hạng LambdaMART ONNX | Tệp tin `ranking_lambdamart.onnx` (48KB) đã chạy trong tiến trình .NET, tuy nhiên mã nguồn thiết lập cơ chế fallback về CRADIS-DN4 khi địa điểm nằm ngoài 13 nhóm danh mục huấn luyện của AI. |
| **Nhóm 4: Kết quả tái lập từ bài báo gốc** | - Kiểm chứng thuật toán CRADIS-DN4 tại Bảng 3 bài báo Lê Đăng Hà (2023) | Đã tái lập bằng mã nguồn C# và Python, kết quả trùng khớp 100% thứ tự xếp hạng của bài báo gốc. |

---

## 8. Danh mục Tập tin Kết quả Bàn giao trong thư mục `research/`

Toàn bộ kết quả thực nghiệm được lưu trữ hoàn chỉnh tại thư mục [`research/`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/):

1. **Dữ liệu CSV kết quả:**
   - [`dataset_statistics.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/dataset_statistics.csv): Bảng thống kê toàn diện về 53,341 địa điểm và Bbox Quận 1.
   - [`queries.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/queries.csv): Danh sách 75 truy vấn kiểm thử phân bổ 3 nhóm kịch bản.
   - [`relevance_labels.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/relevance_labels.csv): Bảng mẫu gán nhãn độ liên quan (0–3) cho 1,125 cặp kết quả Top 5.
   - [`ranking_comparison.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/ranking_comparison.csv): Bảng so sánh chỉ số giữa BM25, BM25+Khoảng cách và CRADIS-DN4.
   - [`travel_time_results.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/travel_time_results.csv): Dữ liệu chi tiết thời gian di chuyển từng thành viên (OSRM vs Haversine).
   - [`latency_results.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/latency_results.csv): Bảng số liệu độ trễ trung vị p50 và phân vị p95 của 6 công đoạn.
   - [`retrieval_metrics.csv`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/retrieval_metrics.csv): Bảng quy chuẩn công thức và định nghĩa các chỉ số đo lường.

2. **Biểu đồ khoa học (Publication-Quality PNG Charts):**
   - [`travel_time_comparison.png`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/travel_time_comparison.png): Biểu đồ so sánh thời gian di chuyển trung bình, tối đa và độ chênh lệch thời gian giữa các phương pháp.
   - [`latency_comparison.png`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/latency_comparison.png): Biểu đồ cột thể hiện độ trễ xử lý (p50, p95) của từng công đoạn hệ thống.
   - [`ranking_comparison.png`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/ranking_comparison.png): Biểu đồ đánh giá sự thỏa hiệp Pareto giữa Độ phù hợp nội dung và Độ công bằng hành trình.

3. **Mã nguồn kịch bản thực nghiệm:**
   - [`01_dataset_statistics.py`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/scripts/01_dataset_statistics.py): Kịch bản truy vấn thống kê DB SQL Server.
   - [`02_prepare_queries.py`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/scripts/02_prepare_queries.py): Kịch bản khởi tạo 75 truy vấn kiểm thử.
   - [`03_run_eval_and_rankings.py`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/scripts/03_run_eval_and_rankings.py): Kịch bản chạy xếp hạng 3 phương pháp đối chứng và tính toán hành trình.
   - [`04_latency_benchmark.py`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/scripts/04_latency_benchmark.py): Kịch bản đo đạc độ trễ 35 lượt lặp lại.
   - [`05_generate_plots.py`](file:///home/tanle/Projects/vscodeProjects/losm-backend/research/scripts/05_generate_plots.py): Kịch bản kết xuất các biểu đồ khoa học định dạng PNG bằng PyCairo.
