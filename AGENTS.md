# Location Search Backend

ASP.NET Core 10 Web API for the Location Search platform.

## Stack

- .NET 10 / ASP.NET Core
- SQL Server 2022 (via EF Core 10)
- JWT Bearer authentication
- HERE API for place data

## Modules

- `Places` — place search via HERE API, persisted to SQL
- `Search` — query routing + ranking (haversine distance + final score)
- `Reviews` — place reviews + average rating
- `Users` — auth, registration, profile
- `AgentCore` — agent plan + tool execution
- `Sandbox` — `Todos` sample module

## Production Environment

- API base URL: `http://103.200.22.67:8082`
- Database: `mssql/server:2022-latest` container, `Server=sqlserver,1433;Database=LocationSearch`
- API docs: `http://103.200.22.67:8082/scalar-login.html` (login first → cookie → `/scalar`)

## Test Account

```
email:    lequantan1974@gmail.com
password: Tan123456!
```

Login endpoint:
```
POST /api/users/login
Content-Type: application/json

{ "email": "lequantan1974@gmail.com", "password": "Tan123456!" }
```
Returns `{ id, username, email, token }`.

## Local Development

```bash
docker compose -f docker/dev/docker-compose.yml up -d   # SQL Server only
cd src/LocationSearch.Api
dotnet run                                               # http://localhost:5232
```

`appsettings.Development.json` provides local connection string + HERE API key.

## Project Layout

```
src/
├── LocationSearch.Api/        — entry point, middleware, OpenAPI/Scalar
└── Modules/
    ├── Places/                 — domain, infrastructure, providers
    ├── Search/                 — query handlers + ranking
    ├── Reviews/                — review CRUD
    ├── Users/                  — auth, JWT
    ├── AgentCore/              — agent plans + tool registry
    └── Sandbox/                — Todos sample
```

Each module exposes `AddXxx(IServiceCollection)` extension called from `Program.cs`.

## Database Migrations

EF Core migrations are applied automatically on app startup (see `Program.cs`):

1. Connect to `master`, create `LocationSearch` database if missing
2. Run `MigrateAsync()` for each module's `DbContext`

Migrations files live under each module's `Infrastructure/Persistence/Migrations/`.

## Deploy

Push to `main` → GitHub Actions → VPS `git pull` → `docker compose up -d --build`.
API auto-runs migrations on startup; new tables are created without manual steps.

## Known Issues
### `/api/search` returns 500 when `latitude`/`longitude` are non-zero

Reproduction:
- `GET /api/search?query=cafe` → `200 []`
- `GET /api/search?query=cafe&latitude=10&longitude=20` → `500`

Root cause: HERE API sometimes returns items missing `Position` (or other required
fields) which caused `System.Text.Json` deserialization to throw an unhandled
exception, and the request bubbled up as 500.

Fix applied:
- `HereItem.Position` is now nullable
- `HerePlaceProvider` filters out items missing required fields before mapping
- All exceptions in HERE call are caught and logged; returns empty list
- DB upsert failures no longer abort the response (logged and returned)

Tests: call the endpoint with various lat/lon combinations, verify:
- Valid place data → 200 with results
- No data / HERE error → 200 with `[]`, no 500

## Operational rules for opencode

### CẤM tự ý chạy — user phải chạy tay
- `dotnet ef migrations add <Name>` / `dotnet ef migrations remove` — chỉ tạo file, **không chạy lệnh**
- `dotnet ef database update` — chỉ gợi ý cho user, **không tự chạy**
- `dotnet run` / `dotnet run --project ...` — **không start app backend**
- `dotnet build` cho production/release config — **không chạy**
- `docker compose up` / `docker compose down` — **không start/stop container**
- `pkill -f dotnet` / `pkill -f LocationSearch` / `kill <pid>` cho process backend — **không kill**
- `git commit` / `git push` / `git merge` — **không tự commit**, chỉ gợi ý message
- Bất kỳ lệnh nào dài >30s hoặc chạy daemon/server — **phải hỏi user trước**

### ĐƯỢC phép chạy (read-only + verify)
- `dotnet build` (để verify code compile pass) — OK
- `dotnet test` (unit test) — OK
- `grep` / `find` / `ls` / `cat` / `head` / `tail` / `file` / `wc` — OK
- `git status` / `git diff` / `git log` — OK (chỉ đọc)
- `curl http://localhost:5232/...` — OK (test API user đã chạy)
- File read/write/edit trong source code — OK
- `npm install` / `npm ci` (cài package) — OK (read-only thực tế)
- `mkdir` / `rm` cho file/folder trong source — OK

### Khi cần chạy lệnh bị cấm
Báo user kèm lý do + đề xuất command cụ thể, đợi user chạy rồi kêu kiểm tra tiếp.

### Geocoding & Dữ liệu địa điểm
- **TUYỆT ĐỐI KHÔNG gọi API bên ngoài (Nominatim, Photon, v.v.)** khi geocode hoặc tìm kiếm địa điểm, dù cấu hình vẫn giữ nguyên trong `appsettings.*.json` (cấm xóa cấu hình).
- Hệ thống chỉ dùng **100% dữ liệu địa điểm nội bộ từ backend** (`Places` và `Areas`).
- **Phạm vi khu vực hoạt động (Areas)**: Nghiệp vụ quản lý khu vực **LUÔN LUÔN là cấp QUẬN** (tên quận như "Quận 1" + `Bbox`). Hệ thống **không phân cấp level hay phường**.
- Khi geocode không ra toạ độ hoặc địa điểm nằm ngoài các khu vực đang kích hoạt (`IAreaRepository`), **tuyệt đối không tự ý bịa toạ độ ảo**. Phải báo rõ phạm vi dữ liệu hệ thống đang có và xin xác nhận từ người dùng.
- **Cơ chế xác thực dữ liệu địa điểm (2-Tier Data Integrity)**:
  - **Tầng 1 (C# Rule-based Scoring)**: Lọc trùng lặp & chấm điểm ưu tiên danh mục biểu tượng/công cộng (`historic_site`, `market`, `shopping_mall`, `public_plaza`, v.v.) hơn dịch vụ nhỏ lẻ (`grocery_store`, `atm`, `spa`, `bank`). Tự động phát hiện và loại bỏ các địa điểm có toạ độ nằm trong Bbox của quận nhưng địa chỉ ghi rõ thuộc quận khác (ngoại khu).
  - **Tầng 2 (MiniMax Integrity Verification & Tool `flag_invalid_place`)**: MiniMax kiểm tra tính toàn vẹn dữ liệu trước khi đưa ra gợi ý. Nếu phát hiện địa điểm có toạ độ sai/lệch vùng nghiêm trọng, MiniMax gọi tool `flag_invalid_place` để đánh dấu `IsNotSafe = true` lưu vào DB và loại khỏi kết quả. Phân biệt rõ: địa điểm chỉ "không hợp gu" thì không gắn cờ; chỉ gắn cờ khi toạ độ hoặc địa chỉ bị SAI / LỆCH rõ ràng.
  - Các truy vấn tìm kiếm/geocode luôn lọc bỏ các bản ghi `IsNotSafe = true`.

### Workflow bàn giao
1. Implement code + build pass locally
2. Báo user: "build pass, cần chạy `<command>` để pick up code"
3. User chạy, restart, xác nhận xong
4. Mình test qua curl + verify end-to-end

