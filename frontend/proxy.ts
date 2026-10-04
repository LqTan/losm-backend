// import { NextResponse, type NextRequest } from "next/server";

// const AUTH_COOKIE = "scalar_token";

// export function proxy(request: NextRequest) {
//   const { pathname } = request.nextUrl;
//   const token = request.cookies.get(AUTH_COOKIE)?.value;

//   if (pathname === "/" || pathname.startsWith("/assistant") || pathname.startsWith("/profile")) {
//     if (!token) {
//       const url = request.nextUrl.clone();
//       url.pathname = "/login";
//       return NextResponse.redirect(url);
//     }
//   }

//   if (
//     (pathname.startsWith("/login") || pathname.startsWith("/register")) &&
//     token
//   ) {
//     const url = request.nextUrl.clone();
//     url.pathname = "/";
//     return NextResponse.redirect(url);
//   }

//   return NextResponse.next();
// }

// export const config = {
//   matcher: ["/", "/assistant/:path*", "/profile/:path*", "/login", "/register"],
// };
