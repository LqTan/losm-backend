import { NextResponse, type NextRequest } from "next/server";

/**
 * Route guard that runs before rendering (Next 16 renamed middleware.ts to proxy.ts).
 *
 * The Edge runtime cannot read localStorage, so this relies on the cookie
 * mirror `losm_admin_token` written alongside it. This is only an early
 * guard to return a redirect fast; real authentication is still enforced by
 * the API (AdminOnly policy) and the AuthProvider.
 */
const TOKEN_COOKIE = "losm_admin_token";
const PUBLIC_PATHS = ["/login"];

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  const hasToken = Boolean(request.cookies.get(TOKEN_COOKIE)?.value);
  const isPublic = PUBLIC_PATHS.some((path) => pathname.startsWith(path));

  if (!hasToken && !isPublic) {
    const loginUrl = new URL("/login", request.url);
    if (pathname !== "/") {
      loginUrl.searchParams.set("next", `${pathname}${search}`);
    }
    return NextResponse.redirect(loginUrl);
  }

  if (hasToken && isPublic) {
    return NextResponse.redirect(new URL("/admin/dashboard", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|favicon.ico|assets/|.*\\.(?:svg|png|jpg|jpeg|gif|webp|woff|woff2|css|js)$).*)",
  ],
};
