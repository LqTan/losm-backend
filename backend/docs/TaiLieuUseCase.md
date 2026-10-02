# Use case cho hệ thống AI Agent tìm kiếm và tổ chức cuộc hẹn

Phạm vi MVP dùng để rà soát chức năng backend, mô hình xếp hạng và workflow tự động hóa.

Tài liệu mô tả năm use case chính của hệ thống hỗ trợ tìm địa điểm và tổ chức cuộc hẹn với đồng nghiệp, khách hàng hoặc đối tác. Nhóm phát triển dùng các use case này để xác nhận chức năng hiện có, xác định phần cần bổ sung và thống nhất tiêu chí nghiệm thu cho phiên bản MVP.

| Mã   | Use case                            | Mục tiêu chính                                       |
| ---- | ----------------------------------- | ---------------------------------------------------- |
| UC01 | Tìm địa điểm gần một vị trí        | Overpass/Nominatim retrieval, khoảng cách và xếp hạng Top 5 |
| UC02 | Tìm điểm gặp công bằng              | Hỗ trợ nhiều vị trí xuất phát                        |
| UC03 | Điều chỉnh yêu cầu trong hội thoại | Sử dụng session và memory                            |
| UC04 | Xếp hạng lại bằng LambdaRoBERTa     | Semantic re-ranking cho candidate từ Overpass/Nominatim |
| UC05 | Chọn địa điểm và tổ chức cuộc hẹn   | Xác nhận, n8n, Calendar và email                     |

## UC01  Tìm địa điểm gần một vị trí

Mục tiêu: Tìm và đề xuất địa điểm phù hợp gần vị trí của người dùng hoặc địa điểm được nhắc trong yêu cầu.

Tác nhân chính: Nhân viên cần gặp đồng nghiệp, khách hàng hoặc đối tác.

Tiền điều kiện: Ứng dụng lấy được GPS hoặc xác định được tọa độ của địa danh; dịch vụ Overpass/Nominatim và dịch vụ xếp hạng đang hoạt động.

### Yêu cầu mẫu

> "Sáng mai tôi gặp khách hàng ở sân bay Tân Sơn Nhất. Tìm giúp một quán cà phê gần đó để hai bên trao đổi công việc."

### Dữ liệu cần trích xuất

| Trường         | Giá trị kỳ vọng                                |
| -------------- | ---------------------------------------------- |
| Loại địa điểm  | Quán cà phê                                    |
| Vị trí         | Sân bay Tân Sơn Nhất hoặc tọa độ tương ứng     |
| Mục đích       | Gặp khách hàng và trao đổi công việc           |
| Thời gian      | Sáng mai, quy đổi theo thời điểm tham chiếu    |
| Số kết quả     | Top 5                                          |

### Luồng chính

1.  Người dùng nhập yêu cầu bằng ngôn ngữ tự nhiên.
2.  LLM xác định loại địa điểm, vị trí, mục đích và thời gian cuộc gặp.
3.  Agent gọi tool `geocode_place` (Nominatim) để lấy `boundingBox` của địa danh, rồi gọi tool `search_places` (Overpass/Nominatim với viewbox = boundingBox) để lấy tối đa 20 địa điểm ứng viên.
4.  Backend tính khoảng cách từ vị trí tìm kiếm đến từng địa điểm.
5.  LambdaRoBERTa chấm mức độ phù hợp của từng cặp query và địa điểm.
6.  Hệ thống kết hợp điểm phù hợp với điểm khoảng cách và trả Top 5.

### Kết quả và tiêu chí nghiệm thu

- Kết quả có tên, địa chỉ, tọa độ, danh mục, khoảng cách, giờ mở cửa nếu có và điểm xếp hạng.
- Danh sách chỉ gồm địa điểm đúng loại và được sắp xếp theo finalScore giảm dần.
- Địa điểm nằm trong vùng tìm kiếm (viewbox/bounding box từ geocode + padding, hoặc bán kính mặc định).
- Agent không khẳng định Wi-Fi, rating, độ yên tĩnh hoặc giờ mở cửa nếu dữ liệu nguồn không cung cấp.
- Provider hoặc model timeout không làm API trả lỗi 500; hệ thống trả thông báo hoặc dùng phương án dự phòng đã thống nhất.

## UC02  Tìm điểm gặp công bằng cho nhiều người

Mục tiêu: Đề xuất địa điểm phù hợp về nội dung và tương đối thuận tiện cho tất cả người tham gia.

Tác nhân chính: Nhân viên tổ chức cuộc gặp cho nhóm, khách hàng hoặc đối tác ở nhiều vị trí khác nhau.

Tiền điều kiện: Hệ thống có tọa độ của từng vị trí xuất phát (do frontend cung cấp hoặc qua tool `geocode_place`) và xác định được vùng tìm kiếm chung.

### Yêu cầu mẫu

> "Tôi ở sân bay Tân Sơn Nhất, một đồng nghiệp ở Quận 1 và một người ở Thủ Đức. Tìm quán cà phê thuận tiện cho cả ba để họp vào chiều thứ Sáu."

### Dữ liệu cần trích xuất

| Trường           | Giá trị kỳ vọng                                      |
| ---------------- | ---------------------------------------------------- |
| Số người         | 3                                                    |
| Vị trí xuất phát | Sân bay Tân Sơn Nhất, Quận 1, Thủ Đức                |
| Loại địa điểm    | Quán cà phê                                          |
| Tiêu chí         | Thuận tiện tương đối cho cả ba                       |
| Thời gian        | Chiều thứ Sáu gần nhất theo thời điểm tham chiếu     |

### Luồng chính

1.  LLM nhận diện số người tham gia và các vị trí xuất phát.
2.  Backend xác định tâm nhóm (centroid) hoặc vùng tìm kiếm phù hợp (bounding box bao quanh tất cả origins).
3.  Overpass/Nominatim trả danh sách địa điểm ứng viên trong vùng tìm kiếm.
4.  LambdaRoBERTa đánh giá mức độ phù hợp của địa điểm với nội dung query.
5.  Backend tính khoảng cách của từng người, khoảng cách trung bình, khoảng cách lớn nhất và độ chênh lệch khoảng cách.
6.  Hệ thống kết hợp relevance score, distance score và fairness score để trả Top 5.

### Kết quả và tiêu chí nghiệm thu

- API nhận danh sách nhiều vị trí thay vì chỉ một cặp latitude và longitude.
- Mỗi kết quả cho biết khoảng cách của từng người, khoảng cách trung bình, khoảng cách lớn nhất, fairness score và finalScore.
- Hệ thống không ưu tiên địa điểm rất gần một người nhưng quá xa những người còn lại nếu có lựa chọn cân bằng hơn.
- Agent giải thích ngắn gọn lý do đề xuất dựa trên dữ liệu khoảng cách đã tính.
- Nếu một vị trí không thể geocode, Agent yêu cầu người dùng làm rõ thay vì tự đoán.

## UC03  Điều chỉnh yêu cầu trong hội thoại

Mục tiêu: Cho phép người dùng bổ sung hoặc thay đổi tiêu chí mà không phải lặp lại toàn bộ yêu cầu ban đầu.

Tác nhân chính: Người dùng đang xem kết quả và muốn thu hẹp hoặc điều chỉnh danh sách.

Tiền điều kiện: AgentSession và lịch sử hội thoại được lưu đúng theo người dùng; kết quả tìm kiếm trước (cùng `LastSearchContext`) vẫn có thể tham chiếu.

### Hội thoại mẫu

> **Người dùng:** Tìm giúp tôi nhà hàng gần chợ Bến Thành để mời đối tác ăn tối.
> **Agent:** Đây là 5 nhà hàng phù hợp.
> **Người dùng:** Tôi muốn nhà hàng Việt Nam và gần hơn một chút.

### Luồng chính

1.  Agent lưu yêu cầu ban đầu và kết quả trả về trong đúng session (kèm `LastSearchContext` chứa query, center, radiusKm, filters).
2.  Ở câu tiếp theo, Agent re-inject `LastSearchContext` (kèm `filtersJson`) vào system prompt để LLM nhận diện cụm từ "gần hơn" đang tham chiếu danh sách trước.
3.  Agent giữ lại địa điểm chợ Bến Thành, mục đích gặp đối tác và thời điểm ăn tối.
4.  Agent thêm điều kiện nhà hàng Việt Nam và giảm bán kính tìm kiếm.
5.  Agent gọi lại tool `search_places` (với `Box` và filters mới) và trả một danh sách mới.

### Kết quả và tiêu chí nghiệm thu

- Người dùng không phải nhập lại địa điểm, mục đích và thời gian đã nêu trước đó.
- Agent áp dụng đúng điều kiện mới mà không làm mất các điều kiện cũ còn hiệu lực.
- Kết quả mới chỉ gồm nhà hàng Việt Nam và có phạm vi gần hơn danh sách trước.
- Dữ liệu giữa các session và giữa các tài khoản không bị trộn lẫn.
- Lịch sử session thể hiện đúng thứ tự message và các lần gọi tool.

### Trường hợp cần xử lý

- Nếu "gần hơn" không đủ rõ, Agent hỏi người dùng muốn giảm bán kính xuống bao nhiêu.
- Nếu session không còn tồn tại (đã bị xoá), Agent tự tạo session mới và yêu cầu người dùng cung cấp lại vị trí.

## UC04  Xếp hạng lại kết quả Overpass bằng LambdaRoBERTa

Mục tiêu: Dùng mô hình đã huấn luyện để đánh giá mức độ liên quan ngữ nghĩa giữa query và từng địa điểm do Overpass/Nominatim trả về.

Tác nhân chính: Search pipeline của backend.

Tiền điều kiện: Model service (`losm-model`) đã triển khai endpoint scoring với mô hình LambdaRoBERTa; phiên bản model và schema đầu vào được thống nhất.

### Yêu cầu mẫu

> "Tìm nhà hàng Việt Nam phù hợp để mời đối tác ăn tối."

### Dữ liệu gửi sang model

| Trường        | Mô tả                                                      |
| ------------- | ---------------------------------------------------------- |
| query         | Yêu cầu tìm kiếm của người dùng                           |
| placeId       | ID địa điểm do Overpass/Nominatim cung cấp (định dạng `osm_type:osm_id`, vd `node:714567078`) |
| title         | Tên địa điểm                                               |
| category      | Danh mục địa điểm                                          |
| address       | Địa chỉ nếu có                                             |
| openingHours  | Giờ mở cửa nếu có; không dùng giá trị suy đoán             |

### Luồng chính

1.  Overpass/Nominatim trả tối đa 20 địa điểm ứng viên.
2.  Backend chuẩn hóa candidate theo schema của model service.
3.  Backend gửi query và danh sách candidate tới endpoint scoring của `losm-model`.
4.  LambdaRoBERTa trả relevanceScore cho từng địa điểm.
5.  Backend kết hợp relevanceScore với distanceScore và fairnessScore nếu có nhiều người.
6.  Hệ thống sắp xếp finalScore giảm dần và trả Top 5.

### Response tối thiểu

| Trường          | Ví dụ                              |
| --------------- | ---------------------------------- |
| placeId         | node:714567078                     |
| relevanceScore  | 0.91                               |
| distanceScore   | 0.78                               |
| fairnessScore   | null khi chỉ có một vị trí         |
| finalScore      | 0.86                               |

### Tiêu chí nghiệm thu

- Backend dùng điểm thật từ model service khi dịch vụ hoạt động.
- Điểm trả về được ánh xạ đúng theo placeId, kể cả khi thứ tự candidate thay đổi.
- Model timeout hoặc lỗi không làm toàn bộ API trả 500; hệ thống dùng baseline hoặc trả trạng thái phù hợp.
- Log cho phép truy vết relevanceScore, distanceScore, fairnessScore, finalScore và phiên bản model.
- Không đưa khoảng cách dạng văn bản vào LambdaRoBERTa để thay cho phép tính địa lý của backend.

## UC05  Chọn địa điểm và tổ chức cuộc hẹn

Mục tiêu: Sau khi người dùng chọn địa điểm, hệ thống xác nhận thông tin rồi tự động tạo lịch và gửi thư mời.

Tác nhân chính: Người tổ chức cuộc hẹn; AgentCore; n8n; Google Calendar và dịch vụ email.

Tiền điều kiện: Agent còn kết quả Top 5 trong session (`LastSearchContext`); người tổ chức đã kết nối lịch; hệ thống có email hợp lệ của người tham gia.

### Yêu cầu mẫu

> "Chọn địa điểm số 2 và tạo lịch gặp vào 18 giờ thứ Sáu tuần này. Mời anh A và chị B giúp tôi."

### Luồng chính

1.  Agent xác định địa điểm số 2 từ danh sách gần nhất trong cùng session (qua `LastSearchContext.ResultPlaceIdsJson`).
2.  Agent lấy tên, địa chỉ, tọa độ và nguồn dữ liệu của địa điểm đã chọn.
3.  Agent xác định thời gian, thời lượng và email người tham gia (email phải hợp lệ theo format).
4.  Hệ thống hiển thị bản tóm tắt để người dùng xác nhận.
5.  Sau khi người dùng đồng ý, backend tạo `PendingAgentAction` và gọi webhook n8n kèm meeting request và idempotency key.
6.  n8n tạo sự kiện trên Google Calendar, thêm địa chỉ và danh sách người tham gia.
7.  n8n gửi email mời và trả trạng thái từng bước cho backend.
8.  Agent thông báo kết quả cuối cho người dùng và lưu lịch sử thực hiện.

### Dữ liệu gửi sang n8n

| Nhóm dữ liệu   | Trường tối thiểu                                      |
| -------------- | ----------------------------------------------------- |
| Cuộc hẹn       | Tiêu đề, mục đích, thời gian bắt đầu, thời lượng     |
| Địa điểm       | Tên, địa chỉ, tọa độ, placeId                         |
| Người tham gia | Tên và email                                          |
| Kiểm soát      | sessionId, userId, confirmationId, idempotencyKey     |

### Tiêu chí nghiệm thu

- Chưa có xác nhận thì hệ thống không tạo lịch và không gửi email.
- Địa điểm trong Calendar khớp với địa điểm người dùng đã chọn.
- Hệ thống không tạo sự kiện trùng khi người dùng xác nhận hoặc gửi lại request nhiều lần (idempotency key).
- Nếu thiếu email hoặc email sai format, Agent yêu cầu người dùng bổ sung trước khi gọi n8n.
- Hệ thống phân biệt được trạng thái Calendar thành công, email thất bại và cho phép xử lý lại đúng bước (qua tool `retry_meeting_emails`).
- Mỗi hành động được lưu trạng thái để tester có thể truy vết.

## Bảng xác nhận phạm vi với nhóm phát triển

Nhóm phát triển điền trạng thái và ghi chú cho từng hạng mục trước khi chốt kế hoạch triển khai.

| # | Hạng mục                                  | Use case | Trạng thái tài liệu | Kết quả test E2E | Ghi chú                                          |
| - | ----------------------------------------- | -------- | ------------------- | ---------------- | ------------------------------------------------ |
| 1 | Overpass/Nominatim search quanh một vị trí | UC01     | Đã có               | **PASS**         | Fix `JsonPropertyName` cho snake_case fields (`place_id`, `osm_type`, `osm_id`, `display_name`) tại `NominatimResult.cs`; trả 5 places cho `cafe` Quận 1 |
| 2 | Xếp hạng theo khoảng cách                 | UC01     | Đã có               | **PASS**         | 5 results, sorted desc by finalScore; relevance=0.5 mock (UC04 excluded) → finalScore phản ánh distance |
| 3 | Nhận nhiều vị trí xuất phát               | UC02     | Cần bổ sung         | **PASS**         | 3 origins → 5 results, centroid + searchRadiusKm đúng |
| 4 | Fairness ranking                          | UC02     | Cần bổ sung         | **PASS**         | Response có `DistanceKmByOrigin[3]`, `AverageDistanceKm`, `MaxDistanceKm`, `SpreadKm`, `FairnessScore`; #5 (Cà Kê Café) có spread=3.25, fairness=0.350 (tốt nhất), Top 1 theo finalScore = OKKIO Cafe (gần Quận 1) |
| 5 | Geocode tool (name → lat/lon + bbox)      | UC01/02  | Đã có               | **PASS**         | Agent gọi `geocode_place("Sân bay Tân Sơn Nhất")` → lat=10.818, lon=10.656, đúng |
| 6 | Session và memory                         | UC03     | Đã có nền tảng      | **PASS**         | Multi-turn reuse sessionId; missing sessionId → graceful 200; history endpoint trả messages + tool calls |
| 7 | LambdaRoBERTa scoring API                 | UC04     | Chưa hoàn thành     | **SKIP**         | User yêu cầu loại trừ                            |
| 8 | Fallback khi model lỗi                    | UC04     | Cần xác nhận        | **SKIP**         | User yêu cầu loại trừ                            |
| 9 | Chọn địa điểm từ Top 5                    | UC05     | Cần xác nhận        | **PASS**         | LLM gọi `create_meeting` với `SelectedIndex=1` → PendingAction status=Pending; LLM có cảnh báo khi SelectedIndex map sai place (do LLM reasoning) |
| 10 | Email format validation                   | UC05     | Đã có               | **PASS**         | Tool reject email `not-an-email`, `bad@` với message rõ ràng; valid emails pass |
| 11 | Retry meeting emails                      | UC05     | Đã có               | **PASS**         | Mark action status=PartiallyFailed (status=4) trong DB → Agent gọi `retry_meeting_emails(originalActionId)` → tạo PendingAction mới type=`retry_meeting_emails`, status=Pending |
| 12 | Webhook n8n                               | UC05     | Cần bổ sung         | **PASS** (mock)   | `FakeN8nMeetingClient` được gọi qua DI trong dev (env≠Production); confirm → status=Completed; không cần n8n thật để test status flow |
| 13 | Google Calendar và email                  | UC05     | Cần bổ sung         | **PARTIAL**       | Status flow + idempotency hoạt động đúng qua Fake client; Calendar/email thật cần n8n workflow + Google OAuth + SMTP credentials (chưa có) |
| 14 | Idempotency (test bổ sung)                | UC05     | (không trong bảng)  | **PASS**         | Gửi cùng `create_meeting` payload 2 lần → cùng `actionId`; SHA-256(userId+actionType+payloadJson) hoạt động đúng |

### Bugs đã fix trong quá trình test

| Bug | File | Triệu chứng | Fix |
|-----|------|-------------|-----|
| snake_case deserialize fail | `src/Modules/Places/Infrastructure/ExternalServices/Nominatim/Models/NominatimResult.cs` | `JsonSerializerDefaults.Web` chỉ case-insensitive, không convert snake_case → `OsmType`, `OsmId`, `PlaceId`, `DisplayName` thành default → filter `string.IsNullOrWhiteSpace(h.OsmType)` loại bỏ mọi place → API luôn trả `[]` | Thêm `[JsonPropertyName("osm_type")]` etc. cho 4 field snake_case |

## Đối chiếu API backend ↔ tích hợp trên `losm-web`

Rà soát ngày 16/09/2026. Loại trừ UC04 (LambdaRoBERTa scoring, chưa có service) và UC05 phần n8n/GCal/email thật.

### API backend hiện có

| Module | Endpoint | Method | Auth |
|---|---|---|---|
| Places | `/api/places/search` | GET | anon |
| Places | `/api/places/{id:guid}` | GET | anon |
| Search | `/api/search` | GET | anon |
| Search | `/api/search/meeting-places` | POST | anon |
| SavedPlaces | `/api/saved-places` | GET / POST | auth |
| SavedPlaces | `/api/saved-places/{placeId:guid}` | DELETE | auth |
| Reviews | `/api/reviews` | POST | auth |
| Reviews | `/api/reviews/place/{placeId:guid}` | GET | anon |
| Reviews | `/api/reviews/place/{placeId:guid}/average-rating` | GET | anon |
| Users | `/api/users/{id:guid}` | GET | auth |
| Users | `/api/users/me` | GET / PUT | auth |
| Users | `/api/users/register` | POST | anon |
| Users | `/api/users/login` | POST | anon |
| AgentCore | `/api/agent` | POST | auth |
| AgentCore | `/api/agent/stream` | POST | auth (SSE) |
| AgentCore | `/api/agent/plans` | POST | auth |
| AgentCore | `/api/agent/plans/{planId:guid}/approve` | POST | auth |
| AgentCore | `/api/agent/confirm` | POST | auth |
| AgentCore | `/api/agent/sessions` | GET | auth |
| AgentCore | `/api/agent/sessions/{sessionId:guid}` | GET | auth |
| Sandbox | `/api/todos` | POST / GET | n/a |
| Sandbox | `/api/todos/{id:int}` | GET | n/a |
| Configuration | `/api/admin/configurations[/{scope}[/history]]` | GET / PUT / DELETE | admin |

`retry_meeting_emails` không có HTTP endpoint riêng — chỉ là tool nội bộ của AgentCore (`src/Modules/AgentCore/Infrastructure/Tools/RetryMeetingEmailsTool.cs`), tạo `PendingAgentAction` mới để user confirm lại.

### Trạng thái tích hợp trên `losm-web`

| API | UC | Frontend hook | Status |
|---|---|---|---|
| `GET /api/search` | UC01 | `entities/place/api.ts:searchPlaces` | OK |
| `POST /api/search/meeting-places` | UC02 | `entities/place/api.ts:searchMeetingPlaces` | OK |
| `GET /api/places/{id}` | UC01 | `entities/place/api.ts:fetchPlace` — định nghĩa nhưng **không gọi** | Chưa dùng |
| `GET /api/places/search` | — | — | Chưa dùng (đã có `/api/search`) |
| `GET /api/saved-places` | UC01 | `features/saved-places-list/use-saved-places.ts` | OK |
| `POST /api/saved-places` | UC01 | `features/save-place/use-save-place.ts:17` gọi sai path | **Bug** |
| `DELETE /api/saved-places/{id}` | UC01 | `features/saved-places-list/use-saved-places.ts:81` | OK |
| `POST /api/reviews` | UC05 | `features/create-review/use-create-review.ts:15` gọi sai path + sai field | **Bug** |
| `GET /api/reviews/place/{id}` | UC01 | — | Chưa dùng |
| `GET /api/reviews/place/{id}/average-rating` | UC01 | — | Chưa dùng |
| `GET /api/users/me` | UC05 | `entities/user/api.ts:fetchCurrentSession` | OK |
| `PUT /api/users/me` | UC05 | — | Chưa dùng (profile chỉ hiển thị, không sửa) |
| `GET /api/users/{id}` | — | — | Chưa dùng |
| `POST /api/users/login` | UC05 | `features/auth/login.ts` | OK |
| `POST /api/users/register` | UC05 | `features/auth/register.ts` | OK |
| `POST /api/agent/stream` | UC03/05 | qua BFF `app/api/agent/stream/route.ts` | OK |
| `GET /api/agent/sessions` | UC03 | `entities/chat-session/api.ts:fetchSessionList` (proxy) | OK |
| `GET /api/agent/sessions/{id}` | UC03 | `entities/chat-session/api.ts:fetchSessionHistory` (proxy) | OK |
| `POST /api/agent/confirm` | UC05 | `entities/meeting/api.ts:confirmMeeting` | OK |
| `POST /api/agent` | UC01 | — | Chưa dùng (đã có stream) |
| `POST /api/agent/plans` | UC03 | — | Chưa dùng |
| `POST /api/agent/plans/{id}/approve` | UC03 | — | Chưa dùng |
| `POST /api/meetings/{id}/retry-emails` (FE tự đặt) | UC05 | `entities/meeting/api.ts:retryMeetingEmails` — endpoint **không tồn tại** trên backend | **Bug** |
| `PUT /api/users/{id}` | — | — | Không có trên backend |

### Bugs frontend cần sửa (không liên quan UC04/n8n)

| Bug | File | Triệu chứng | Fix tối thiểu |
|---|---|---|---|
| Save place sai path | `losm-web/src/features/save-place/use-save-place.ts:17` | Gọi `POST /api/users/me/saved-places` → backend chỉ có `POST /api/saved-places` | Đổi path thành `/api/saved-places` |
| Create review sai path + sai body | `losm-web/src/features/create-review/use-create-review.ts:15-17` | Gọi `POST /api/places/{id}/reviews` body `{score, comment}`; backend là `POST /api/reviews` body `{placeId, rating, comment}` | Đổi path `/api/reviews`; đổi field `score → rating`, thêm `placeId` vào body |
| Retry meeting emails gọi endpoint không tồn tại | `losm-web/src/entities/meeting/api.ts:70-75` | Gọi `POST /api/meetings/{id}/retry-emails` không có trên backend; tool `retry_meeting_emails` chỉ chạy qua agent | Gọi `POST /api/agent` (message: "retry email cho meeting …") hoặc thêm backend endpoint mới |
| Saved places map sai field + thiếu lat/lon | `losm-web/src/features/saved-places-list/use-saved-places.ts:20-49` | Đọc `name`/`address`/`latitude`/`longitude`; backend trả `placeName`/`placeAddress` (không có lat/lon) | Map lại field; bỏ qua lat/lon (không có) hoặc gọi thêm `GET /api/places/{id}` để lấy location |

### Bugs backend cần sửa (ảnh hưởng UC03 + UX mở lại session)

| Bug | File | Triệu chứng | Fix tối thiểu |
|---|---|---|---|
| Session history mất attached places | `src/Modules/AgentCore/Application/Agent/Queries/GetAgentSessionHistory/GetAgentSessionHistoryResult.cs` | `AgentMessageHistoryResult` chỉ có `Role/Content/CreatedAt`, không có danh sách địa điểm gợi ý kèm theo câu trả lời của agent → mở lại session cũ thấy chat text nhưng mất hết card địa điểm | Thêm `IReadOnlyList<AttachedPlaceHistoryResult> AttachedPlaces` vào message result (snapshot attachedPlaces của SSE result event) |
| Session history không trả LastSearchContext | `GetAgentSessionHistoryResult.cs` | UC03 yêu cầu "Agent re-inject LastSearchContext" khi user nói "gần hơn một chút" → mở lại session cũ, context search gần nhất đã mất | Lưu `LastSearchContext` lên AgentSession row và trả kèm history |

### Chức năng frontend đang thiếu so với backend (chưa làm)

- `GET /api/places/{id}` — mở chi tiết place từ danh sách saved/session
- `GET /api/reviews/place/{id}` + `/average-rating` — hiển thị review + rating trung bình trong `PlaceDetailDialog`
- `PUT /api/users/me` — sửa tên hiển thị trong profile
- `POST /api/agent/plans` + `/approve` — luồng plan/approve thay vì streaming trực tiếp (không bắt buộc, đã có stream)

## Cải thiện trang Profile (`losm-web/app/profile`)

Hiện trạng: chỉ hiển thị `email / displayName / username / memberSince` + list saved places; thiếu lịch sử cuộc hẹn, review của user, kết nối Google Calendar.

### Dữ liệu backend đã lưu nhưng chưa expose ra API

| Dữ liệu | Đang lưu | API hiện có | Endpoint cần thêm |
|---|---|---|---|
| Meeting đã tạo (Completed) | `PendingAgentAction` (status=Completed) | — | `GET /api/users/me/meetings?status=upcoming\|past` |
| Meeting đang chờ xác nhận | `PendingAgentAction` (status=Pending) | — | `GET /api/users/me/pending-actions` |
| Review user đã viết | bảng `Reviews` | chỉ query theo `placeId` | `GET /api/users/me/reviews` |
| Search history gần đây | `LastSearchContext` trên session | — | trả kèm `GET /api/agent/sessions/{id}` (đã list ở mục bug backend) |
| Chat sessions | `AgentSession` | `GET /api/agent/sessions` ✅ | OK |
| Kết nối Google Calendar | chưa lưu | — | `GET /api/users/me/google-calendar/status` + OAuth callback `/api/users/me/google-calendar/connect` |

### Mockup trang profile đầy đủ (đề xuất)

```
┌──────────────────────────────────────────┐
│ TÀI KHOẢN                              │
│  email      lequantan1974@gmail.com     │
│  Tên        Lê Quân Tân     [Sửa] │
│  Username   lequantan                   │
│  Tham gia   16/09/2026                  │
├──────────────────────────────────────────┤
│ GOOGLE CALENDAR                         │
│  Trạng thái ● Đã kết nối (a@gmail)  │
│  [Ngắt kết nối]                          │
├──────────────────────────────────────────┤
│ CUỘC HẸN SẮP TỚI (3)                  │
│  • Họp khách A — 18:00 18/09           │
│    Highlands Coffee · 2 người           │
│    [Mở Calendar] [Retry email]          │
│  • Họp team — 14:00 20/09              │
│    ...                                  │
├──────────────────────────────────────────┤
│ LỊCH SỬ CUỘC HẸN (12)                 │
│  • 12/09 — Đã huỷ                      │
│  • 05/09 — Hoàn thành                  │
│  [Xem tất cả]                           │
├──────────────────────────────────────────┤
│ REVIEW CỦA TÔI (4)                     │
│  • Highlands Coffee — 4★                │
│    "Cà phê ngon, yên tĩnh"             │
│  • Phở 25 — 5★                          │
├──────────────────────────────────────────┤
│ ĐÃ LƯU (7)                              │
│  (giữ nguyên list hiện tại)             │
└──────────────────────────────────────────┘
```

### Lộ trình triển khai (3 pha)

| Pha | Phạm vi | Backend | Frontend | Đầu ra |
|---|---|---|---|---|
| **Pha 1 — Quick win** | Render Meetings + Reviews với data hiện có | Không thêm endpoint | Dùng `GET /api/agent/sessions` (đã có) + thêm component "Hoạt động gần đây" từ session history (sau khi fix bug backend) | Trang profile hiển thị hoạt động gần đây |
| **Pha 2 — Endpoint mới** | Thêm 2 endpoint `meetings` + `reviews` | `GET /api/users/me/meetings?status=upcoming\|past`; `GET /api/users/me/reviews`; cập nhật `ProfileView` để query và render 2 section mới | Section "Cuộc hẹn sắp tới" + "Lịch sử" + "Review của tôi" hoàn chỉnh |
| **Pha 3 — Google OAuth** | Kết nối Google Calendar từ profile | `GET /api/users/me/google-calendar/status`; OAuth flow + callback; bảng `UserGoogleTokens`; `IGoogleCalendarClient` đặt cùng vị trí `IN8nMeetingClient` (sau khi chuyển sang Google trong phần "Thư viện thay thế n8n") | Section "Google Calendar" với nút Kết nối/Ngắt + nút "Mở Calendar" trên từng meeting |

### Quick win không cần backend mới

- Tận dụng `GET /api/agent/sessions` + `LastSearchContext` (sau khi fix bug) để hiển thị "Hoạt động gần đây" ngay trong profile (5 session gần nhất + place đã đính kèm).
- Dùng `GET /api/agent/sessions/{id}` đã có để mở lại session → render chat history với place cards (sau khi fix bug attached places).

### Phụ thuộc với phần khác

- **Confirm meeting dialog** (đề xuất ở phần "Thư viện thay thế n8n"): chia sẻ `react-day-picker` + `Meeting` model với profile "Cuộc hẹn sắp tới".
- **Bug session history attached places** (mục bugs backend): phải fix trước khi làm Pha 1, nếu không Quick win không hiển thị được place cards khi mở lại session.

## Thư viện thay thế n8n (miễn phí)

Mục tiêu: bỏ n8n khỏi flow UC05, backend tự gọi Google Calendar + gửi email qua SMTP. `docker/dev/docker-compose.yml` và `docker/production/docker-compose.yml` giữ nguyên (service `n8n` có thể xoá sau, không bắt buộc).

### Backend (.NET 10) — đã verify free + compatible

| Chức năng | Thư viện | License | Ghi chú |
|---|---|---|---|
| Google Calendar (OAuth2 + CRUD event) | `Google.Apis.Calendar.v3` + `Google.Apis.Auth.OAuth2` | Apache-2.0 | Client chính thức của Google, hỗ trợ Service Account + Web OAuth flow; tạo event có attendees + location + meet link |
| SMTP email (gửi lời mời) | `MailKit` + `MimeKit` | MIT | SMTP client hiện đại, hỗ trợ OAuth2 SASL; pair với Gmail App Password / SendGrid free tier / Mailtrap dev |
| iCalendar (.ics) đính kèm email | `iCal.NET` (`Ical.Net`) | MIT | Generate file `.ics` chuẩn RFC 5545, đính kèm vào email để người nhận Add to Calendar 1-click |
| Microsoft Outlook / Graph (alternative) | `Microsoft.Graph` v5 | MIT | Dùng nếu user dùng Microsoft 365 thay Google |

### Frontend (`losm-web`, Next.js/React) — đã verify free + license MIT

| Chức năng | Thư viện | License | Ghi chú |
|---|---|---|---|
| Date/time picker trong confirm-meeting dialog | `react-day-picker` v9 | MIT | Nhẹ (~10KB), localizable tiếng Việt, hỗ trợ range/time addon |
| Picker đầy đủ (date + time + range) | `react-datepicker` v7 | MIT | Mature, dễ style, có locale `vi` |
| Lịch tháng view (preview event) | `react-big-calendar` v1 | MIT | Month/week/day view, drag-drop optional; phù hợp nếu muốn hiển thị calendar trong profile |
| Calendar mới, dependency nhẹ | `@schedule-x/react` | MIT | Alternative hiện đại cho fullcalendar core; built-in views + event modal |

### Đề xuất combo tối thiểu (UC05 thay n8n)

- Backend thêm package: `Google.Apis.Calendar.v3`, `Google.Apis.Auth.OAuth2`, `MailKit`, `Ical.Net`. Implement `IGoogleCalendarClient` + `IEmailSender` đặt cùng vị trí `IN8nMeetingClient` hiện tại (swap qua DI).
- Frontend thêm: `react-day-picker` (chọn ngày/giờ trong `use-confirm-meeting.ts`). Bỏ qua `react-big-calendar` nếu không cần preview lịch user.
- Env mới (đã thêm vào `appsettings.Development.json`):
  ```json
  "Google": {
    "ClientId": "",          // TODO: lấy từ Google Cloud Console
    "ClientSecret": "",      // TODO: lấy từ Google Cloud Console
    "RedirectUri": "http://localhost:5232/api/users/me/google-calendar/callback",
    "Scopes": [
      "https://www.googleapis.com/auth/calendar.events",
      "https://www.googleapis.com/auth/userinfo.email"
    ],
    "ApplicationName": "LocationSearch.Api (dev)"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseStartTls": true,
    "User": "",              // TODO: Gmail App Password user
    "Password": "",          // TODO: Gmail App Password
    "FromAddress": "",       // TODO: email gửi đi
    "FromName": "LocationSearch"
  },
  "Meeting": {
    "DefaultDurationMinutes": 60,
    "CalendarTimeZone": "Asia/Ho_Chi_Minh",
    "AttachIcs": true
  }
  ```
- RefreshToken lưu theo user (`UserGoogleTokens` table) — không có trong config.
- Giữ nguyên flow: tạo `PendingAgentAction` → confirm → backend gọi Google Calendar + gửi email với `.ics` đính kèm; vẫn giữ idempotency key + retry.
