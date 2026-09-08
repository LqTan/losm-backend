# Agent Workflow Module

Module này cung cấp logic Agent Workflow cho Location-Based Search & Recommendation.

Module Agent không gọi trực tiếp Search, Places, Reviews, Users hoặc XLM-R.

Module cung cấp các interface để phần xử lý bên ngoài tạo plan, kiểm tra plan và quản lý context hội thoại.

## 1. Planner and prompt flow

**Mục tiêu:** Phân tích truy vấn ngôn ngữ tự nhiên và tạo `AgentPlan` mô tả nhu cầu tìm kiếm, yêu cầu GPS, việc tải review và semantic rerank.

### Files

- `Application/Abstractions/IPlanner.cs`
- `Application/Planner/SimplePlanner.cs`
- `Application/Abstractions/IPromptBuilder.cs`
- `Application/Prompts/PlannerPromptBuilder.cs`
- `Domain/AgentPlan.cs`

### Public contracts

```csharp
AgentPlan CreatePlan(string query, bool hasUserLocation);

string BuildPlannerPrompt(string userQuery, AgentPlan? previousPlan = null);
```

### Current behavior

- Nhận diện place type: `cafe`, `restaurant`, `library`, `hotel`, `bar`.
- Nhận diện preference: `quiet`, `nice_view`, `study_friendly`, `family_friendly`.
- Phát hiện yêu cầu location: “gần tôi”, “quanh đây”, “near me”.
- Tạo `SearchQuery`, `RadiusKm`, `ShouldLoadReviews`, `ShouldUseSemanticRerank`.
- `SimplePlanner` là fallback/rule-based; `PlannerPromptBuilder` chuẩn bị prompt JSON cho LLM trong tương lai.
- Planner không tự tạo GPS, địa điểm, rating, review hoặc giờ mở cửa.

## 2. Memory and context

**Mục tiêu:** Lưu context hội thoại tạm thời theo `sessionId`.

### Files

- `Application/Abstractions/IConversationMemory.cs`
- `Application/Memory/InMemoryConversationMemory.cs`
- `Domain/ConversationTurn.cs`

### Public contract

```csharp
void AddTurn(string sessionId, ConversationTurn turn);

IReadOnlyList<ConversationTurn> GetRecentTurns(
    string sessionId,
    int maximumTurns = 10);
```

### Current behavior

- Lưu các lượt `user`/`assistant` theo từng session.
- Trả về các lượt mới nhất, mặc định tối đa 10.
- Hỗ trợ request đồng thời bằng `ConcurrentDictionary` và lock riêng cho mỗi session.
- Đây là in-memory storage; dữ liệu mất khi API/container restart.
- Có thể thay bằng Redis/database sau này mà không đổi interface.

## 3. Validation and Human-in-the-Loop

**Mục tiêu:** Chặn plan/action không hợp lệ và yêu cầu user xác nhận trước khi ghi dữ liệu.

### Files

- `Application/Abstractions/IPlanValidator.cs`
- `Application/Validation/PlanValidator.cs`
- `Domain/AgentAction.cs`
- `Domain/ValidationDecision.cs`

### Public contract

```csharp
ValidationDecision Validate(
    AgentPlan plan,
    AgentAction action,
    bool hasUserLocation);
```

### Current behavior

- Reject query trống.
- Reject plan cần clarification.
- Reject search thiếu `SearchQuery`.
- Reject search radius ngoài 0.1–50 km.
- Prompt cho LLM giới hạn radius trong 0.5–20 km; Validator chấp nhận phạm vi rộng hơn 0.1–50 km cho các plan hợp lệ từ phần xử lý bên ngoài.
- Reject khi cần GPS nhưng app chưa có location.
- Allow `SearchPlace` nếu plan hợp lệ.
- Require human approval cho `SavePreference` và `CreateReview`.
- Reject action không hỗ trợ.

## Cách module được sử dụng

Luồng sử dụng ở mức khái quát:

```text
Input query + session context + GPS từ app/API
→ IPlanner.CreatePlan(...)
→ IPlanValidator.Validate(...)
→ Phần xử lý bên ngoài gọi các service phù hợp theo plan
→ Recommendation response, clarification hoặc yêu cầu xác nhận
```

Sau khi plan hợp lệ, phần xử lý bên ngoài có thể dùng thông tin trong `AgentPlan` để gọi Search, Places, Reviews hoặc semantic reranker theo kiến trúc ứng dụng.

- Module Agent không reference trực tiếp `Search`, `Places`, `Reviews`, `Users` hoặc XLM-R.
- GPS thật do app/API cung cấp; không do Planner hoặc LLM tạo.
- `ShouldUseSemanticRerank` cho biết plan có yêu cầu semantic rerank hay không.
- `ValidationDecision` cho biết workflow được tiếp tục, bị từ chối, cần clarification hoặc cần người dùng xác nhận.
- Module không quyết định API endpoint, dependency injection, response DTO hoặc cách triển khai các service phía sau.

## Verification

Đã chạy:

```powershell
dotnet build
```

Kết quả: `Build succeeded` cho toàn bộ module hiện có và `LocationSearch.Api`.

Lưu ý: build thành công chỉ chứng minh các thay đổi không gây lỗi compile. Module Agent chưa được gọi ở runtime cho đến khi phần tích hợp đăng ký dependency injection và nối endpoint.