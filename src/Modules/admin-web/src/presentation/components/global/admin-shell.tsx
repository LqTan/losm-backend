"use client";

import { usePathname } from "next/navigation";
import { Suspense, useState } from "react";
import {
  ADMIN_NAV_ITEMS,
  AppHeader,
  AppSidebar,
} from "@/presentation/components/global";
import { useIsDesktop } from "@/presentation/hooks";
import { cn } from "@/shared/utils/classnames";

/**
 * App shell following Mazer: #app > (#sidebar + #main).
 * #main gets a 300px left margin (defined in app.css) that drops back to 0
 * whenever #sidebar lacks the `active` class.
 */
export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const isDesktop = useIsDesktop();
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  // The sidebar is always open on desktop and closed by default on mobile.
  const isSidebarVisible = isDesktop || isSidebarOpen;

  return (
    <div id="app">
      {/*
        AppSidebar reads useSearchParams (so the active list stays highlighted
        on its sub-routes), which needs a Suspense boundary on a statically
        prerendered route.
      */}
      <Suspense fallback={null}>
        <AppSidebar
          items={ADMIN_NAV_ITEMS}
          pathname={pathname}
          isOpen={isSidebarVisible}
          onClose={() => setIsSidebarOpen(false)}
        />
      </Suspense>

      <div id="main">
        <header className="mb-3 d-flex align-items-center">
          <button
            type="button"
            className="burger-btn btn btn-light d-block d-xl-none"
            aria-label="Open navigation"
            onClick={() => setIsSidebarOpen(true)}
          >
            <i className="bi bi-justify fs-3" aria-hidden="true" />
          </button>
          <div className="ms-auto">
            <AppHeader />
          </div>
        </header>

        {children}

        <footer>
          <div className="footer clearfix mb-0 text-muted">
            <div className="float-start">
              <p className="mb-0">
                {new Date().getFullYear()} &copy; LocationSearch
              </p>
            </div>
          </div>
        </footer>
      </div>

      {/* Overlay that closes the sidebar when it is open off-canvas on mobile. */}
      {isSidebarOpen && !isDesktop ? (
        <div
          className={cn("position-fixed top-0 start-0 w-100 h-100")}
          style={{ background: "rgba(0,0,0,.4)", zIndex: 9 }}
          aria-hidden="true"
          onClick={() => setIsSidebarOpen(false)}
        />
      ) : null}
    </div>
  );
}
