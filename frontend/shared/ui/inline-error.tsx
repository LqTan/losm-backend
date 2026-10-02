import { AlertCircle } from "lucide-react";

export interface InlineErrorProps {
  readonly message: string;
}

export function InlineError({ message }: InlineErrorProps) {
  return (
    <div
      role="alert"
      className="flex items-start gap-2 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-[13px] text-destructive"
    >
      <AlertCircle size={14} strokeWidth={1.5} className="mt-0.5 shrink-0" aria-hidden />
      <p className="leading-[1.5]">{message}</p>
    </div>
  );
}
