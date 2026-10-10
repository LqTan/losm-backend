"use client";

import { useEffect, useState } from "react";

export default function HangfirePage() {
  const [src, setSrc] = useState<string | null>(null);

  useEffect(() => {
    const token =
      typeof window !== "undefined" ? localStorage.getItem("losm_token") : null;
    const base = "/api/admin/hangfire-proxy";
    setSrc(token ? `${base}/?access_token=${encodeURIComponent(token)}` : base);
  }, []);

  if (!src) {
    return (
      <div className="space-y-4">
        <h2 className="text-2xl font-bold">Hangfire Dashboard</h2>
        <p className="text-sm text-muted-foreground">Loading…</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold">Hangfire Dashboard</h2>
          <p className="text-muted-foreground">
            Background jobs and recurring tasks.
          </p>
        </div>
        <a
          href={`${src}`}
          target="_blank"
          rel="noopener noreferrer"
          className="text-sm underline"
        >
          Open in new tab →
        </a>
      </div>
      <iframe
        src={src}
        title="Hangfire Dashboard"
        className="h-[calc(100vh-220px)] w-full rounded-md border bg-white"
      />
    </div>
  );
}