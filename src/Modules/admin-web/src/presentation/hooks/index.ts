/**
 * Barrel export for global hooks. Components import from "@/presentation/hooks" only.
 */
export * from "./use-auth";
export * from "./use-async";
export * from "./use-click-outside";
export * from "./use-media-query";
export { useToast, ToastProvider, ToastViewport } from "@/presentation/components/ui/toast";
export type { ToastItem, ToastInput } from "@/presentation/components/ui/toast";
