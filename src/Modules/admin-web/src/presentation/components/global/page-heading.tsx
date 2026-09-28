import { cn } from "@/shared/utils/classnames";

/** Page heading following Mazer's `.page-heading` style. */
export function PageHeading({
  title,
  subtitle,
  actions,
  className,
}: {
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "page-heading d-flex flex-wrap align-items-center justify-content-between gap-3",
        className,
      )}
    >
      <div>
        <h3 className="mb-0">{title}</h3>
        {subtitle !== undefined ? (
          <p className="text-muted mb-0 mt-1">{subtitle}</p>
        ) : null}
      </div>
      {actions !== undefined ? (
        <div className="d-flex align-items-center gap-2">{actions}</div>
      ) : null}
    </div>
  );
}
