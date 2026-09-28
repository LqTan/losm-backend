import type { Metadata } from "next";
import { Nunito } from "next/font/google";
import { AppProviders } from "@/presentation/providers/app-providers";
import "@/presentation/styles/app-overrides.css";

const nunito = Nunito({
  subsets: ["latin"],
  weight: ["300", "400", "600", "700", "800"],
  variable: "--font-nunito",
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: "LocationSearch Admin",
    template: "%s · LocationSearch Admin",
  },
  description: "Management console for the LocationSearch platform.",
};

/**
 * Root layout — loads the static assets bundled with the Mazer template
 * (Bootstrap 5 + Bootstrap Icons) from the public/assets folder.
 *
 * Stylesheets are linked directly instead of @import-ed from globals.css to
 * avoid an extra round-trip. Link order decides override precedence, so
 * app.css (the Mazer theme) must come after bootstrap.css.
 */
export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // data-scroll-behavior="smooth": Mazer's app.css sets scroll-behavior
    // on <html>. This attribute tells Next to disable smooth scrolling
    // during route transitions so the page jumps straight to the top.
    <html
      lang="en"
      suppressHydrationWarning
      data-scroll-behavior="smooth"
      className={nunito.variable}
    >
      <head>
        {/* eslint-disable @next/next/no-css-tags -- static template stylesheets, not in-JSX CSS. */}
        <link
          rel="stylesheet"
          href="/assets/vendors/bootstrap-icons/bootstrap-icons.css"
        />
        <link rel="stylesheet" href="/assets/css/bootstrap.css" />
        <link rel="stylesheet" href="/assets/css/app.css" />
        <link rel="stylesheet" href="/assets/css/pages/auth.css" />
        {/* eslint-enable @next/next/no-css-tags */}
        <link
          rel="shortcut icon"
          href="/assets/images/logo/logo.svg"
          type="image/svg+xml"
        />
      </head>
      <body suppressHydrationWarning>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
