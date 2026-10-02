# Hướng dẫn kỹ thuật cho Tester

Tài liệu này bổ sung cho báo cáo tiến độ, giúp người kiểm thử nắm nhanh cấu trúc source, cách chạy local, các endpoint và luồng xử lý chính của backend.

---

## 1. Stack công nghệ

- **Ngôn ngữ / framework**: .NET 10, ASP.NET Core Web API (`src/LocationSearch.Api`).
- **Kiến trúc**: Modular Monolith + Clean Architecture (Domain / Application / Infrastructure / Presentation).
- **Cơ sở dữ liệu**: SQL Server 2022, truy cập qua EF Core 10. Mỗi module có `DbContext` riêng.
- **Xác thực**: JWT Bearer (`HS256`), secret đọc từ `Jwt:SecretKey`.
- **API bên ngoài**: HERE Discover API (`https://discover.search.hereapi.com`) để lấy dữ liệu địa điểm thực tế.
- **LLM**: MiniMax M3 (dùng cho module AgentCore).
- **Tài liệu API**: OpenAPI (`/openapi/v1.json`) + giao diện Scalar (`/scalar`, `/scalar-login.html`).

---

## 2. Cấu trúc thư mục (src/)

```
src/
├── LocationSearch.Api/                 # Entry point, middleware, OpenAPI, Scalar
└── Modules/
    ├── Places/                         # Địa điểm + Saved Places + Provider HERE
    ├── Search/                         # Pipeline lấy candidate và xếp hạng
    ├── Reviews/                        # Đánh giá + điểm trung bình
    ├── Users/                          # Đăng ký, đăng nhập, JWT, hồ sơ
    ├── AgentCore/                      # Planner, Tool, Memory, Orchestrator, Grounding
    └── Sandbox/                        # Todos mẫu
```

Mỗi module đăng ký DI qua extension `AddXxx(IServiceCollection)` được gọi tại `Program.cs`.

---

## 3. Chạy local

```bash
docker compose -f docker/dev/docker-compose.yml up -d   # SQL Server
cd src/LocationSearch.Api
dotnet run                                              # http://localhost:5232
```

- File `appsettings.Development.json` chứa connection string local và `Here:ApiKey`.
- Migrations EF Core tự chạy khi khởi động (xem `Program.cs`).
- Database `LocationSearch` được tự tạo nếu chưa tồn tại.

---

## 4. Môi trường production

| Hạng mục | Giá trị |
| --- | --- |
| Base URL | `http://103.200.22.67:8082` |
| Database container | `mssql/server:2022-latest`, host `sqlserver:1433` |
| OpenAPI JSON | `/openapi/v1.json` |
| Scalar UI | `/scalar-login.html` (đăng nhập lấy cookie trước, sau đó vào `/scalar`) |
| Login riêng cho Scalar | `POST /scalar/login` rồi truy cập `/scalar` |

### Tài khoản test

```
email:    lequantan1974@gmail.com
password: Tan123456!
```

```bash
curl -X POST http://103.200.22.67:8082/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"lequantan1974@gmail.com","password":"Tan123456!"}'
```

Response trả về `{ id, username, email, token }`. Dùng token trong header `Authorization: Bearer {token}` cho các request tiếp theo.

---

## 5. Danh sách endpoint

### 5.1. Users (`/api/users`)

| Method | Route | Mô tả | Auth |
| --- | --- | --- | --- |
| POST | `/api/users/register` | Đăng ký tài khoản mới | Không |
| POST | `/api/users/login` | Đăng nhập, trả token JWT | Không |
| GET | `/api/users/{id}` | Lấy thông tin theo id | Có |
| GET | `/api/users/me` | Lấy thông tin người dùng hiện tại | Có |
| PUT | `/api/users/me` | Cập nhật hồ sơ | Có |

### 5.2. Places (`/api/places`, `/api/saved-places`)

| Method | Route | Mô tả | Auth |
| --- | --- | --- | --- |
| GET | `/api/places/search` | Tìm địa điểm qua HERE (gọi trực tiếp provider) | Không |
| GET | `/api/places/{id}` | Lấy địa điểm theo id nội bộ | Không |
| GET | `/api/saved-places` | Danh sách địa điểm đã lưu của user | Có |
| POST | `/api/saved-places` | Lưu địa điểm | Có |
| DELETE | `/api/saved-places/{placeId}` | Bỏ lưu địa điểm | Có |

### 5.3. Search (`/api/search`)

| Method | Route | Mô tả | Auth |
| --- | --- | --- | --- |
| GET | `/api/search` | Tìm + xếp hạng (gọi pipeline chính) | Không |

Query params: `query` (chuỗi, bắt buộc), `latitude`, `longitude`, `radiusKm` (mặc định `5`).

### 5.4. Reviews (`/api/reviews`)

| Method | Route | Mô tả | Auth |
| --- | --- | --- | --- |
| POST | `/api/reviews` | Tạo đánh giá | Có |
| GET | `/api/reviews/place/{placeId}` | Danh sách đánh giá của một địa điểm | Không |
| GET | `/api/reviews/place/{placeId}/average-rating` | Điểm trung bình | Không |

### 5.5. AgentCore (`/api/agent`)

| Method | Route | Mô tả | Auth |
| --- | --- | --- | --- |
| POST | `/api/agent` | Chạy agent đồng bộ, trả kết quả cuối | Có |
| POST | `/api/agent/stream` | Chạy agent streaming (SSE/từng bước) | Có |
| POST | `/api/agent/plans` | Tạo plan (Planner), trả plan id | Có |
| POST | `/api/agent/plans/{planId}/approve` | Duyệt plan (Human-in-the-loop) | Có |
| POST | `/api/agent/confirm` | Xác nhận pending action | Có |
| GET | `/api/agent/sessions/{sessionId}` | Lấy lịch sử phiên agent | Có |

### 5.6. Sandbox (`/api/todos`)

| Method | Route | Mô tả | Auth |
| --- | --- | --- | --- |
| GET | `/api/todos` | Danh sách todo (mẫu) | Có |
| GET | `/api/todos/{id}` | Chi tiết todo | Có |
| POST | `/api/todos` | Tạo todo | Có |

---

## 6. Chi tiết các luồng chính

### 6.1. Search pipeline

File: `src/Modules/Search/Application/Search/Queries/SearchPlaces/SearchPlacesHandler.cs`

1. Gọi `IPlaceSearchService` lấy candidate từ `HerePlaceProvider` (cache trong SQL qua `IPlaceRepository`).
2. Tính khoảng cách Haversine từ `(latitude, longitude)` đến từng place.
3. `IRankingService` kết hợp: điểm relevance (scorer tạm, dự kiến thay bằng XLM-RoBERTa) + khoảng cách → `final score` rồi sort.

### 6.2. Tích hợp HERE

File: `src/Modules/Places/Infrastructure/Providers/HerePlaceProvider.cs`

Request được dựng dạng:

```
GET https://discover.search.hereapi.com/v1/discover
    ?in=circle:{lat},{lon};r={radiusMeters}
    &q={query}
    &limit=20
    &apiKey={Here:ApiKey}
```

Response chính (`HereResponse.Items[]`) gồm:

- `id`: external id của HERE.
- `title`: tên địa điểm.
- `position.lat` / `position.lng`: tọa độ (nullable, an toàn nếu thiếu).
- `address.label`: địa chỉ hiển thị.
- `categories[0].name`: danh mục.
- `openingHours[].text[]`: giờ mở cửa.

`HerePlaceProvider` lọc bỏ item thiếu `id`, `title` hoặc `Position` rồi mới map sang entity `Place`. Mọi exception (HTTP, timeout, JSON) đều được log và trả về danh sách rỗng, không bao giờ làm request API bị 500.

Ví dụ test nhanh trực tiếp tới HERE:

```bash
curl -G "https://discover.search.hereapi.com/v1/discover" \
  --data-urlencode "in=circle:10.762622,106.660172;r=5000" \
  --data-urlencode "q=quán cafe" \
  --data-urlencode "limit=20" \
  --data-urlencode "apiKey=YOUR_HERE_API_KEY"
```

### 6.3. AgentCore flow

File orchestrate chính: `src/Modules/AgentCore/Application/Agent/Commands/ExecuteAgent/`.

1. `ExecuteAgentHandler` chạy.
2. `IAgentPlanner` (`CreateAgentPlanHandler`) tạo `AgentPlan` từ query + session memory.
3. `IAgentRunner` điều phối theo từng bước plan: chọn tool, gọi, ghi nhận kết quả.
4. Tool registry (`IAgentToolRegistry`) chứa các tool hiện có:
   - `SearchPlacesTool` — gọi `SearchPlacesHandler`.
   - `PlaceReviewsTool` — lấy danh sách review.
   - `AverageRatingTool` — điểm trung bình.
   - `CurrentUserTool` — thông tin user hiện tại.
   - `CreateReviewTool` — tạo đánh giá (yêu cầu user xác nhận).
   - `SavePlaceTool` — lưu địa điểm (yêu cầu user xác nhận).
5. `GroundingValidator` (`AgentCore/Infrastructure/Validation/GroundingValidator.cs`) kiểm tra câu trả lời LLM có dựa trên dữ liệu tool đã gọi không.
6. Kết quả cuối được lưu vào `AgentSession`, `AgentMessage`, `AgentToolCall` (qua `IAgentSessionRepository`).
7. Log workflow ở `AgentCore/Infrastructure/` qua `IAgentEventSink` để truy vết.

Khi một tool tạo side-effect (tạo review, lưu place), runner sẽ tạo `PendingAgentAction` và chờ endpoint `POST /api/agent/confirm` (Human-in-the-loop).

---

## 7. Lỗi đã biết và cách xử lý khi test

### `/api/search` 500 khi truyền lat/lon khác 0

- Triệu chứng:
  - `GET /api/search?query=cafe` → 200, mảng rỗng.
  - `GET /api/search?query=cafe&latitude=10&longitude=20` → 500 trước fix.
- Nguyên nhân: HERE trả về item thiếu `Position` → `System.Text.Json` ném exception không được xử lý.
- Fix đã áp dụng:
  - `HereItem.Position` nullable.
  - `HerePlaceProvider` lọc item thiếu trường bắt buộc trước khi map.
  - Mọi exception (HTTP, timeout, parse, DB upsert) được log, trả mảng rỗng.
- Kiểm chứng:
  - `query=cafe&lat=0&lon=0` → 200 `[]`.
  - `query=cafe&lat=10.7&lon=106.66` với HERE trả dữ liệu → 200 có kết quả.
  - Khi HERE lỗi/quota hết → 200 `[]`, không 500.

### Cookie Scalar và JWT

- `/scalar-login.html` set cookie `scalar_token`. Khi mở `/scalar` cùng domain, request mang cookie đó và `JwtBearerEvents.OnMessageReceived` tự gán vào `ctx.Token`. Không cần nhập tay trong UI.
- Production yêu cầu đăng nhập mới truy cập được `/openapi/v1.json`.

### Migrations

- Không cần chạy lệnh tay. Lần đầu khởi động sẽ tự tạo DB `LocationSearch` (qua kết nối `master`) rồi `MigrateAsync` từng `DbContext`.
- Thêm migration mới: `dotnet ef migrations add <Name> --project src/Modules/<Module> --startup-project src/LocationSearch.Api`. Sau khi merge, deploy tự áp dụng.

---

## 8. Kịch bản test đề xuất

1. **Auth**
   - Đăng ký user mới → 200.
   - Đăng nhập → trả token JWT hợp lệ.
   - Gọi `/api/users/me` không có token → 401.
   - Gọi `/api/users/me` có token hết hạn → 401.

2. **Search**
   - `GET /api/search?query=&lat=...&lon=...` đủ tổ hợp `lat/lon = 0` và khác 0 → luôn 200.
   - Kết quả có field `distanceKm`, `finalScore` hợp lệ, sort giảm dần.
   - Query tiếng Việt có dấu (`"quán cafe"`, `"nhà hàng"`) trả về item có `name` khớp ngữ nghĩa.

3. **Places**
   - `/api/places/search` gọi trực tiếp provider, kết quả khớp với `/api/search` về id.
   - Lưu 1 place qua `/api/saved-places` → hiện trong danh sách `/api/saved-places`.
   - Xóa lưu → không còn trong danh sách.

4. **Reviews**
   - Tạo review → 200/201, có `averageRating` cập nhật đúng.

5. **Agent đồng bộ**
   - `POST /api/agent` với prompt `"Tìm quán cafe gần đây"` → chạy tool `SearchPlaces`, trả câu trả lời có gắn địa điểm.
   - Prompt yêu cầu tạo review → chạy `CreateReviewTool`, trả `pendingAction` chờ confirm.
   - `POST /api/agent/confirm` đúng id → review được tạo, ghi log.

6. **Agent plan (Human-in-the-loop)**
   - `POST /api/agent/plans` trả `planId`.
   - Approve plan → runner chạy theo plan.
   - Không approve trong thời gian cho phép → trạng thái giữ nguyên, không side-effect.

7. **Logging / truy vết**
   - `GET /api/agent/sessions/{sessionId}` trả về message, tool call theo thứ tự.
   - `GroundingValidator` đánh dấu `Succeeded = false` nếu LLM bịa thông tin ngoài tool.

---

## 9. Mẹo debug nhanh

- Bật log DEBUG cho namespace:
  - `Logging:LogLevel:Default = Information`
  - `Logging:LogLevel:Places.Infrastructure.Providers.HerePlaceProvider = Debug`
- Kiểm tra migrations áp dụng đúng: truy vấn `SELECT name FROM sys.tables` trong DB `LocationSearch`.
- Nếu HERE trả chậm / timeout, tăng timeout ở HttpClient hoặc kiểm tra quota tại https://platform.here.com/.
- Nếu LLM trả lời sai ngữ nghĩa: xem `GetAgentSessionHistory` để biết tool nào đã gọi, đối chiếu dữ liệu tool trả về với câu trả lời.
- Cookie `scalar_token` chỉ dùng cho UI Scalar. Khi gọi API bằng Postman/curl vẫn cần gửi `Authorization: Bearer {token}` riêng.
