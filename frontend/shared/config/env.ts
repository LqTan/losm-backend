function getEnv(key: string, fallback = ""): string {
  if (typeof process !== "undefined" && process.env[key]) {
    return process.env[key] as string;
  }
  return fallback;
}

export const env = {
  NEXT_PUBLIC_API_BASE_URL: getEnv(
    "NEXT_PUBLIC_API_BASE_URL",
    "http://103.200.22.67:8082",
  ),
  NEXT_PUBLIC_API_MODE: getEnv("NEXT_PUBLIC_API_MODE", "live"),
  API_MODE: getEnv("API_MODE", getEnv("NEXT_PUBLIC_API_MODE", "live")),
} as const;

export function isMockMode(): boolean {
  return env.API_MODE === "mock" || env.NEXT_PUBLIC_API_MODE === "mock";
}
