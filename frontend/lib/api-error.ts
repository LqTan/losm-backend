// API messages are display text, never status identifiers or control-flow values.
export function getApiErrorMessage(data: unknown, fallback: string): string {
  if (typeof data === "string" && data.trim()) {
    if (data.includes("<html") || data.includes("<!DOCTYPE") || data.includes("<head>")) {
      if (data.includes("504 Gateway Time-out")) {
        return "Máy chủ phản hồi quá thời gian chờ (504 Gateway Time-out). Yêu cầu tìm kiếm phức tạp cần thêm thời gian xử lý.";
      }
      if (data.includes("502 Bad Gateway")) {
        return "Máy chủ tạm thời không thể kết nối (502 Bad Gateway). Vui lòng thử lại sau.";
      }
      return fallback;
    }
    return data;
  }
  if (data && typeof data === "object") {
    const body = data as Record<string, unknown>;
    for (const key of ["message", "error", "detail", "title"]) {
      if (typeof body[key] === "string" && body[key].trim()) return body[key];
    }
  }
  return fallback;
}
