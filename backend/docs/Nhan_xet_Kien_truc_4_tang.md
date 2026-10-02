# Nhận xét — Sơ đồ `Kien truc he thong 4 tang.jpg`

**Ngày rà soát:** 19/09/2026 — đối chiếu trực tiếp với `src/Modules/*` và `docker/*/docker-compose.yml`.

## Kết luận chính

Phân tích của bạn **đúng cả 3 mục** vẽ nét đứt. Chi tiết:

### 1. Tầng 2 — `OSM Text + Geo Index` (Elasticsearch/OpenSearch) — **CHƯA LÀM** ✅ đánh dấu đúng
- Container `elasticsearch:8.13.0` có trong `docker/dev/docker-compose.yml` và `docker/production/docker-compose.yml`, **nhưng chỉ phục vụ module `Configuration`** (admin config store: `EsConfigurationStore`, `ConfigurationIndexBootstrapper`). Không có index POI nào.
- Tìm kiếm POI hiện gọi **trực tiếp** external API tại thời điểm query: `OverpassPlaceProvider` → Photon/Nominatim/Overpass (`src/Modules/Places/Infrastructure/Providers/OverpassPlaceProvider.cs`). Không có BM25, không có category filter server-side, không có bán kính index.
- `HerePlaceProvider` đã bị đánh dấu `deprecated` (`HerePlaceProvider.cs:113`).

### 2. Tầng 3 — `LambdaMART Scoring API` — **CHƯA LÀM** ✅ đánh dấu đúng
- `MockRelevanceScorer.cs:16` trả về hard-coded `0.5` cho mọi candidate.
- Tài liệu UC đã chính thức loại trừ UC04 (`TaiLieuUseCase.md:248`): "Loại trừ UC04 (LambdaRoBERTa scoring, chưa có service)".
- Pipeline hiện tại = Baseline: relevance mock + distance score + fairness score (`RankingService.cs`, `SearchPlacesHandler.cs:83-130`). Chưa có model train từ 100 query, chưa có relevance label 0–3, chưa có re-rank Top 5.

### 3. Tầng 4 — `n8n + Google Calendar / Email` — **ĐANG BỊ ĐỀ XUẤT THAY THẾ** ⚠️ cần cập nhật diagram
- Code vẫn đang reference n8n: `N8nMeetingClient` + `FakeN8nMeetingClient` trong DI (`AgentCore/DependencyInjection.cs:111,128`); n8n container còn chạy ở dev (`docker/dev/docker-compose.yml:38`).
- Tuy nhiên `docs/TaiLieuUseCase.md` (mục "Thư viện thay thế n8n", dòng 401–423) đã đề xuất **bỏ n8n**, backend tự gọi Google Calendar API + SMTP. Hiện trạng: PASS (mock qua `FakeN8nMeetingClient` trong env ≠ Production), Calendar/email thật = PARTIAL (`TaiLieuUseCase.md:236–237`).
- **Đề xuất:** trên diagram nên đổi nhãn "n8n" → "Calendar/Email adapter" và để nét **đứt**, vì:
  - GCal OAuth + SMTP credentials chưa có
  - `n8n` đang được lên kế hoạch loại bỏ
  - Trong code hiện tại chỉ có `FakeN8nMeetingClient` chạy thật ở dev

## Ghi chú thêm
- Tầng 1 (`ASP.NET Core API` + `AgentCore + MiniMax LLM` + `Tool Registry`) và `Agent State Store` (Tầng 2) đã có trong code: `AgentCore` module có plan/approve/confirm/sessions + `PendingAgentAction` table.
- Tầng 4 `Hiển thị Top 5` + `Human in the Loop` + `Pending Action` đều đã pass test E2E (xem bảng test trong `TaiLieuUseCase.md`).

## Đề xuất chỉnh sửa diagram
1. Đổi nhãn **"n8n + Google Calendar / Email"** → **"Google Calendar / SMTP (thay thế n8n)"**, giữ nét đứt.
2. Ghi chú nhỏ ở legend: "ES trong docker hiện chỉ phục vụ module Configuration, chưa có POI index."
3. Nếu muốn trung thực 100%, có thể vẽ thêm mũi tên nét đứt từ `Agent State Store` → `Google Calendar adapter` (mới, thay cho n8n).
