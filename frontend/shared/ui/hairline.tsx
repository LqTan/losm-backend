import { Separator } from "@/components/ui/separator";
import { cn } from "@/shared/lib/cn";

export interface HairlineProps {
  readonly orientation?: "horizontal" | "vertical";
  readonly className?: string;
}

export function Hairline({ orientation = "horizontal", className }: HairlineProps) {
  return (
    <Separator
      orientation={orientation}
      className={cn(
        orientation === "horizontal" ? "w-full" : "h-full self-stretch",
        className,
      )}
    />
  );
}
