"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { cn } from "@/shared/utils/classnames";
import type { UserListScope } from "@/domain/value-objects/user-scope.vo";
import {
  parseUserScope,
  userScopePath,
} from "@/domain/value-objects/user-scope.vo";

export interface NavChildItem {
  readonly label: string;
  /**
   * The list this entry opens. The href is derived from it via
   * userScopePath so the sidebar, the "New user" button and the post-save
   * redirect can never point at different URLs.
   */
  readonly scope: UserListScope;
}

export interface NavItem {
  readonly href: string;
  readonly label: string;
  /** Bootstrap Icons class, e.g. "bi-speedometer2". */
  readonly icon: string;
  /** Renders a collapsible submenu when present. */
  readonly children?: readonly NavChildItem[];
}

export const ADMIN_NAV_ITEMS: readonly NavItem[] = [
  {
    href: "/admin/dashboard",
    label: "Dashboard",
    icon: "bi-speedometer2",
  },
  {
    href: "/admin/users",
    label: "Users",
    icon: "bi-people",
    children: [
      { label: "User management", scope: "staff" },
      { label: "Customers", scope: "customer" },
    ],
  },
];

export interface AppSidebarProps {
  items: readonly NavItem[];
  pathname: string;
  isOpen: boolean;
  onClose: () => void;
}

function isActive(pathname: string, href: string): boolean {
  return pathname === href || pathname.startsWith(`${href}/`);
}

/**
 * Sidebar using Mazer's markup: #sidebar > .sidebar-wrapper.active, with
 * Mazer toggling visibility through the `active` class on #sidebar.
 *
 * Two deviations from the stock template, both deliberate:
 *
 * 1. A group header is a <button>, not the <a> the template ships, because it
 *    toggles instead of navigating. The button is reset in CSS so it renders
 *    identically to a link.
 * 2. The group <li> never receives `active`. Mazer's
 *    `.sidebar-item.active .sidebar-link span { color: #fff }` paints the label
 *    and icon white for a "selected" item, which would make the group header
 *    disappear whenever a child was selected. The selected state is carried by
 *    `.submenu-item.active` instead, which Mazer already styles.
 */
export function AppSidebar({
  items,
  pathname,
  isOpen,
  onClose,
}: AppSidebarProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  /**
   * Groups the user explicitly opened or closed. Absent means "follow the
   * route" (auto-open the group that owns the current page). Storing only the
   * user's explicit choice keeps the toggle symmetric: each click flips the
   * value derived for the current render, so collapsing then expanding works.
   */
  const [groupOverrides, setGroupOverrides] = useState<
    Record<string, boolean>
  >({});

  function navigate(href: string) {
    onClose();
    router.push(href);
  }

  return (
    <div id="sidebar" className={cn(isOpen && "active")}>
      <div className="sidebar-wrapper active">
        <div className="sidebar-header">
          <div className="d-flex justify-content-between align-items-center">
            <div className="logo">
              <a
                href="/admin/dashboard"
                aria-label="LocationSearch home"
                onClick={(event) => {
                  event.preventDefault();
                  navigate("/admin/dashboard");
                }}
              >
                {/* eslint-disable-next-line @next/next/no-img-element -- static template logo, sized by the .sidebar-header CSS rule. */}
                <img src="/assets/images/logo/logo.svg" alt="LocationSearch" />
              </a>
            </div>
            <div className="toggler d-xl-none">
              <button
                type="button"
                className="sidebar-hide border-0 bg-transparent p-0"
                aria-label="Close navigation"
                onClick={onClose}
              >
                <i className="bi bi-x bi-middle" aria-hidden="true" />
              </button>
            </div>
          </div>
        </div>

        <div className="sidebar-menu">
          <ul className="menu">
            <li className="sidebar-title">Menu</li>
            {items.map((item) => {
              if (!item.children || item.children.length === 0) {
                const active = isActive(pathname, item.href);
                return (
                  <li
                    key={item.href}
                    className={cn("sidebar-item", active && "active")}
                  >
                    <a
                      href={item.href}
                      className="sidebar-link"
                      aria-current={active ? "page" : undefined}
                      onClick={(event) => {
                        event.preventDefault();
                        navigate(item.href);
                      }}
                    >
                      <i className={cn("bi", item.icon)} aria-hidden="true" />
                      <span>{item.label}</span>
                    </a>
                  </li>
                );
              }

              /*
                Auxiliary routes of a list — "New user" and the edit page —
                are not children, so no child path matches and the group would
                collapse while the admin is still working inside that list.
                They carry the originating scope in `?scope=`, which is used
                here to keep the right child highlighted.
              */
              const matchedChild = item.children.find((child) =>
                isActive(pathname, userScopePath(child.scope)),
              );
              const hasActiveChild = matchedChild !== undefined;
              const activeChildScope =
                matchedChild?.scope ??
                (pathname.startsWith(`${item.href}/`)
                  ? parseUserScope(searchParams.get("scope"))
                  : null);
              const isOpenGroup =
                groupOverrides[item.href] ?? hasActiveChild;

              return (
                <li key={item.href} className="sidebar-item has-sub">
                  <button
                    type="button"
                    className="sidebar-link sidebar-group-toggle"
                    aria-expanded={isOpenGroup}
                    onClick={() =>
                      setGroupOverrides((current) => ({
                        ...current,
                        [item.href]: !isOpenGroup,
                      }))
                    }
                  >
                    <i className={cn("bi", item.icon)} aria-hidden="true" />
                    <span>{item.label}</span>
                  </button>

                  <ul className={cn("submenu", isOpenGroup && "active")}>
                    {item.children.map((child) => {
                      const childPath = userScopePath(child.scope);
                      const childActive =
                        pathname === childPath ||
                        (activeChildScope === child.scope &&
                          pathname.startsWith(`${item.href}/`));
                      return (
                        <li
                          key={child.scope}
                          className={cn(
                            "submenu-item",
                            childActive && "active",
                          )}
                        >
                          <a
                            href={childPath}
                            aria-current={childActive ? "page" : undefined}
                            onClick={(event) => {
                              event.preventDefault();
                              navigate(childPath);
                            }}
                          >
                            {child.label}
                          </a>
                        </li>
                      );
                    })}
                  </ul>
                </li>
              );
            })}
          </ul>
        </div>
      </div>
    </div>
  );
}
