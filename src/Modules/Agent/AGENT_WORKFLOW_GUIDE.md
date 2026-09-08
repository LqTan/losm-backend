# Agent Workflow Module — Giải thích kỹ thuật

## 1. Mục đích và phạm vi

Module **Agent Workflow** là lớp điều phối logic cho hệ thống tìm kiếm và gợi ý địa điểm theo vị trí.

Module tiếp nhận truy vấn ngôn ngữ tự nhiên, tạo kế hoạch tìm kiếm, kiểm tra kế hoạch, lưu context hội thoại theo session và yêu cầu xác nhận trước các hành động có rủi ro hoặc ghi dữ liệu.

Module không tự truy vấn trực tiếp Search, Places, Reviews, Users hoặc XLM-R. Các nhiệm vụ đó thuộc lớp Orchestrator và các service/model chuyên trách. Module Agent chỉ cung cấp các capability qua interface để lớp bên ngoài gọi và điều phối.

Ba nhóm chức năng chính:

1. Planner + prompt flow.
2. Memory / context.
3. Validation + Human-in-the-Loop.

## 2. Luồng xử lý tổng quát

Luồng xử lý ở mức module:

```text
Query + session context + dữ liệu đầu vào
→ Planner tạo AgentPlan
→ Validator kiểm tra plan
→ Memory lưu hoặc truy xuất context theo session
→ Orchestrator sử dụng plan hợp lệ để gọi các service phù hợp
→ Trả response, clarification hoặc yêu cầu xác nhận
```

`AgentPlan` là hợp đồng dữ liệu trung tâm giữa Planner, Validator và lớp Orchestrator. Planner chỉ tạo kế hoạch; module Agent không trực tiếp thực thi tìm kiếm, đọc review hay gọi semantic reranker.

## 3. Domain model

### 3.1. AgentPlan

`Domain/AgentPlan.cs` biểu diễn kế hoạch mà Agent tạo ra từ query người dùng. Plan mang các thông tin cần để lớp điều phối quyết định pipeline xử lý tiếp theo, ví dụ intent, query, bộ lọc, bán kính tìm kiếm, GPS, cờ tải review và cờ dùng semantic rerank.

Các property dùng `init`, vì vậy sau khi một instance `AgentPlan` đã được tạo xong thì không cập nhật trực tiếp từng property của instance đó. Khi cần bổ sung dữ liệu đầu vào, chẳng hạn `Latitude` và `Longitude` thật từ client/API, lớp gọi phải tạo một `AgentPlan` mới.

Các field GPS được để nullable có chủ đích. Planner/LLM không được tự tạo tọa độ; GPS phải đến từ client hoặc API đáng tin cậy.

Một số default quan trọng của plan:

- `Intent` mặc định là `search_place`.
- `RadiusKm` mặc định là `5`.
- `ShouldLoadReviews` mặc định là `true`.
- `ShouldUseSemanticRerank` mặc định là `true`.

### 3.2. AgentAction

`Domain/AgentAction.cs` biểu diễn loại hành động mà plan hoặc workflow có thể yêu cầu. Action giúp phân biệt các hành vi chỉ đọc dữ liệu với các hành vi cần kiểm tra hoặc xác nhận.

### 3.3. ValidationDecision

`Domain/ValidationDecision.cs` là kết quả kiểm tra của Validator. Kết quả cho biết plan được phép tiếp tục, bị từ chối, cần làm rõ thông tin hoặc cần xác nhận bởi người dùng.

### 3.4. ConversationTurn

`Domain/ConversationTurn.cs` biểu diễn một lượt hội thoại được lưu trong memory. Turn thường chứa dữ liệu cần thiết để khôi phục ngữ cảnh ở các request tiếp theo trong cùng session.

## 4. Planner và prompt flow

### 4.1. IPlanner và SimplePlanner

`Application/Abstractions/IPlanner.cs` định nghĩa hợp đồng cho thành phần tạo kế hoạch.

`Application/Planner/SimplePlanner.cs` là implementation planner hiện tại. Thành phần này nhận input của người dùng và tạo `AgentPlan` theo các rule deterministic. Đây là nền tảng để workflow hoạt động mà không phụ thuộc bắt buộc vào LLM.

Planner chịu trách nhiệm suy luận các thông tin như:

- Intent của request.
- Search query hoặc keyword chính.
- Các cờ có cần tải review/rating hay semantic rerank.
- Giá trị bán kính tìm kiếm theo logic của module.
- Các thông tin cần clarification khi input chưa đủ.

Planner không tự gọi API tìm kiếm và không tự tạo GPS. Nó chỉ tạo plan mô tả việc lớp Orchestrator nên làm tiếp theo.

### 4.2. IPromptBuilder và PlannerPromptBuilder

`Application/Abstractions/IPromptBuilder.cs` định nghĩa hợp đồng tạo prompt cho LLM Planner.

`Application/Prompts/PlannerPromptBuilder.cs` xây dựng prompt có cấu trúc để một LLM có thể trả về JSON tương thích với `AgentPlan`. Prompt nêu rõ vai trò, input context, schema đầu ra và các rule giới hạn giá trị.

Output JSON giúp giảm phụ thuộc vào câu trả lời tự do của LLM và tạo contract rõ ràng giữa LLM Planner với code parse/validate phía sau.

Prompt không thay thế Validator. Dù LLM đã nhận rule trong prompt, plan sinh ra vẫn phải đi qua `PlanValidator` trước khi được dùng ở pipeline tiếp theo.

## 5. Memory và context

### 5.1. IConversationMemory

`Application/Abstractions/IConversationMemory.cs` định nghĩa hợp đồng lưu và truy xuất lịch sử hội thoại theo session.

Memory được dùng để:

- Giữ context giữa các lượt chat.
- Nhớ query hoặc preference gần đây của người dùng.
- Hỗ trợ clarification khi request hiện tại thiếu dữ liệu.
- Cung cấp context cô đọng cho Planner hoặc PromptBuilder.

### 5.2. InMemoryConversationMemory

`Application/Memory/InMemoryConversationMemory.cs` là implementation in-memory hiện tại.

Dữ liệu memory được tổ chức theo session. `ConcurrentDictionary` hỗ trợ quản lý nhiều session đồng thời, còn lock theo session bảo vệ các thao tác cần tính nhất quán trong cùng một cuộc hội thoại.

Đây là memory ở runtime, nên dữ liệu sẽ mất khi ứng dụng restart. Nếu cần lưu context lâu dài hoặc chạy nhiều instance ứng dụng, implementation sau này cần thay bằng persistent store hoặc distributed cache.

## 6. Validation và Human-in-the-Loop

### 6.1. IPlanValidator và PlanValidator

`Application/Abstractions/IPlanValidator.cs` định nghĩa hợp đồng kiểm tra `AgentPlan`.

`Application/Validation/PlanValidator.cs` thực thi các rule an toàn và hợp lệ trước khi plan được sử dụng. Validator kiểm tra các điều kiện quan trọng như query, intent, action, GPS khi cần, giá trị radius và các điều kiện liên quan đến hành vi có rủi ro.

Validator trả về `ValidationDecision`, thay vì chỉ trả true/false. Điều này cho phép lớp gọi biết cần:

- Cho phép pipeline tiếp tục.
- Từ chối plan không hợp lệ.
- Hỏi người dùng để làm rõ.
- Yêu cầu người dùng xác nhận trước khi tiếp tục.

### 6.2. Human-in-the-Loop

Human-in-the-Loop là cơ chế dừng workflow trước một hành động cần sự đồng ý hoặc cần bổ sung thông tin từ người dùng.

Module Agent chỉ đưa ra quyết định validation và lý do đi kèm. Lớp API/UI hoặc Orchestrator chịu trách nhiệm hiển thị câu hỏi xác nhận, nhận phản hồi của người dùng rồi gọi lại workflow với dữ liệu đã hoàn thiện.

Ví dụ:

```text
Người dùng yêu cầu hành động cần xác nhận
→ Planner tạo AgentPlan
→ Validator trả NeedHumanApproval
→ API/UI hiển thị yêu cầu xác nhận
→ Người dùng xác nhận hoặc từ chối
→ Workflow tiếp tục hoặc kết thúc
```

## 7. Giới hạn bán kính

`PlannerPromptBuilder` yêu cầu LLM chọn bán kính trong khoảng `0.5–20 km` để giảm khả năng sinh giá trị bất hợp lý từ prompt.

`PlanValidator` chấp nhận khoảng rộng hơn là `0.1–50 km`. Đây là chủ đích: Validator không chỉ xử lý plan do LLM tạo mà còn phải chấp nhận các plan hợp lệ đến từ Orchestrator hoặc application logic. Mọi giá trị ngoài phạm vi Validator đều bị xem là không hợp lệ.

## 8. Ranh giới tích hợp

Module Agent không gọi trực tiếp các module hoặc service sau:

- Search.
- Places.
- Reviews.
- Users.
- XLM-R hoặc semantic reranker.

Lớp Agent Orchestrator là consumer của các interface trong module Agent. Sau khi có plan hợp lệ, lớp này điều phối các service phù hợp, ví dụ tìm kiếm địa điểm, lấy review/rating và gọi semantic reranker nếu plan yêu cầu.

Ranh giới này giúp module Agent:

- Có thể unit test độc lập.
- Không bị coupling với transport, database hoặc model serving.
- Thay đổi implementation Search/Places/Reviews/reranker mà không làm thay đổi core logic Planner, Memory hoặc Validation.
- Dễ thay SimplePlanner bằng LLM Planner trong tương lai.

## 9. Trạng thái build và runtime integration

Đã chạy:

```powershell
dotnet build
```

Kết quả: `Build succeeded`.

Build thành công chứng minh các thay đổi hiện tại không gây lỗi compile trong solution/module được build. Điều này không tự chứng minh module Agent đã được đăng ký dependency injection, được API gọi hoặc đã hoạt động end-to-end ở runtime.

Để runtime integration hoạt động, lớp ứng dụng cần đăng ký implementation của các interface Agent vào dependency injection, nhận input cần thiết từ API, gọi Planner/Validator/Memory theo flow và kết nối plan hợp lệ với các service phía sau.

## 10. Tóm tắt

Module Agent Workflow cung cấp core logic độc lập cho quy trình:

```text
Input + context
→ tạo AgentPlan
→ validation
→ clarification hoặc human approval khi cần
→ lớp Orchestrator thực thi các service theo plan hợp lệ
```

Thiết kế tách Planner, Prompt flow, Memory và Validation thành các interface riêng. Nhờ đó, module có thể phát triển hoặc thay thế từng phần mà vẫn giữ ranh giới rõ ràng với phần API, tìm kiếm địa điểm, review/rating và semantic reranking.
