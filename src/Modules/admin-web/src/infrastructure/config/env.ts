/**
 * Runtime configuration, read from Next.js environment variables.
 * Set NEXT_PUBLIC_API_BASE_URL in .env.local (see .env.example).
 */
export interface AppConfig {
  readonly apiBaseUrl: string;
  readonly defaultPageSize: number;
}

const DEFAULT_API_BASE_URL = "http://localhost:5232";

export const appConfig: AppConfig = {
  apiBaseUrl: (
    process.env.NEXT_PUBLIC_API_BASE_URL ?? DEFAULT_API_BASE_URL
  ).replace(/\/+$/, ""),
  defaultPageSize: 20,
};
