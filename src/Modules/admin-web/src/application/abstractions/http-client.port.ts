export type QueryValue =
  | string
  | number
  | boolean
  | null
  | undefined;

export type QueryParams = Record<string, QueryValue>;

export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export interface HttpRequest {
  readonly method: HttpMethod;
  readonly path: string;
  readonly query?: QueryParams;
  readonly body?: unknown;
  /** Skip the token (used for anonymous endpoints). */
  readonly anonymous?: boolean;
}

export interface HttpResponse<T> {
  readonly status: number;
  readonly body: T;
}

export interface Paginated<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

/**
 * Port: transport layer. The application layer only knows this interface,
 * not whether fetch, axios or something else is behind it.
 */
export interface HttpClient {
  send<T>(request: HttpRequest): Promise<HttpResponse<T>>;
}
