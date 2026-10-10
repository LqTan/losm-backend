"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { api, ApiError } from "@/lib/api";
import { getToken } from "@/lib/auth";
import type { Area, PagedResult, Place } from "@/lib/types";
import {
  Activity,
  ArrowRight,
  CheckCircle2,
  Database,
  ExternalLink,
  Map,
  MapPin,
  RefreshCw,
  Sparkles,
} from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

interface RecurringConfig {
  enabled?: boolean;
  cron?: string;
  batchSize?: number;
  areaId?: string;
}

export default function AdminOverviewPage() {
  const [areas, setAreas] = useState<Area[]>([]);
  const [placesCount, setPlacesCount] = useState<number | null>(null);
  const [recurring, setRecurring] = useState<RecurringConfig | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      try {
        const token = getToken();
        const [areasData, settingsData, placesData] = await Promise.allSettled([
          api.get<Area[]>("/api/areas", token),
          api.get<{ key: string; valueJson: string }>(
            "/api/admin/overture/settings/overture.recurring",
            token
          ),
          api.get<PagedResult<Place>>("/api/places?page=1&pageSize=1", token),
        ]);

        if (areasData.status === "fulfilled") {
          setAreas(areasData.value);
        }

        if (placesData.status === "fulfilled") {
          setPlacesCount(placesData.value.totalCount);
        }

        if (settingsData.status === "fulfilled" && settingsData.value?.valueJson) {
          try {
            const parsed = JSON.parse(settingsData.value.valueJson);
            setRecurring(parsed);
          } catch {
            // ignore JSON parse error
          }
        }
      } catch {
        // ignore load errors
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const recurringTargetArea = areas.find((a) => a.id === recurring?.areaId);

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-2xl font-bold tracking-tight">System Overview</h2>
        <p className="text-sm text-muted-foreground">
          Monitor your location database, scheduled data syncs, and background jobs.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {/* Places Card */}
        <Card className="relative overflow-hidden transition-all hover:shadow-md">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Places</CardTitle>
            <MapPin className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {loading ? "…" : placesCount !== null ? placesCount.toLocaleString() : "0"}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Locations in catalog
            </p>
            <div className="mt-4 pt-3 border-t">
              <Link
                href="/admin/places"
                className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              >
                <span>Manage places</span>
                <ArrowRight className="h-3 w-3" />
              </Link>
            </div>
          </CardContent>
        </Card>

        {/* Areas Card */}
        <Card className="relative overflow-hidden transition-all hover:shadow-md">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Administrative Areas</CardTitle>
            <Map className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {loading ? "…" : areas.length}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Configured boundary boxes & regions
            </p>
            <div className="mt-4 pt-3 border-t">
              <Link
                href="/admin/overture/areas"
                className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              >
                <span>Manage boundaries</span>
                <ArrowRight className="h-3 w-3" />
              </Link>
            </div>
          </CardContent>
        </Card>

        {/* Overture Sync Card */}
        <Card className="relative overflow-hidden transition-all hover:shadow-md">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overture Ingestion</CardTitle>
            <Database className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="flex items-center gap-2">
              <span className="text-2xl font-bold">
                {recurring?.enabled ? "Active" : "Idle"}
              </span>
              <Badge variant={recurring?.enabled ? "default" : "secondary"}>
                {recurring?.enabled ? "Scheduled" : "Manual Only"}
              </Badge>
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              {recurring?.enabled && recurring?.cron
                ? `Cron: ${recurring.cron} (${recurringTargetArea?.name ?? "Default area"})`
                : "No automatic sync schedule enabled"}
            </p>
            <div className="mt-4 pt-3 border-t">
              <Link
                href="/admin/overture"
                className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              >
                <span>Configure sync & trigger</span>
                <ArrowRight className="h-3 w-3" />
              </Link>
            </div>
          </CardContent>
        </Card>

        {/* Hangfire Jobs Card */}
        <Card className="relative overflow-hidden transition-all hover:shadow-md">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Background Workers</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">Hangfire</div>
            <p className="text-xs text-muted-foreground mt-1">
              Process queue, recurring tasks & retries
            </p>
            <div className="mt-4 pt-3 border-t">
              <Link
                href="/admin/hangfire"
                className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              >
                <span>Open worker dashboard</span>
                <ArrowRight className="h-3 w-3" />
              </Link>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Quick Access Section */}
      <div className="space-y-4">
        <h3 className="text-base font-semibold">Quick Actions</h3>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Link
            href="/admin/overture"
            className="group flex flex-col justify-between rounded-lg border bg-card p-4 transition-colors hover:border-primary/50 hover:bg-muted/30"
          >
            <div className="space-y-1.5">
              <div className="flex items-center gap-2 font-medium">
                <RefreshCw className="h-4 w-4 text-primary" />
                <span>Run Data Import</span>
              </div>
              <p className="text-xs text-muted-foreground">
                Fetch latest places for an area from Overture Maps
              </p>
            </div>
            <span className="mt-4 text-xs font-semibold text-primary opacity-0 group-hover:opacity-100 transition-opacity">
              Launch &rarr;
            </span>
          </Link>

          <Link
            href="/admin/overture/areas"
            className="group flex flex-col justify-between rounded-lg border bg-card p-4 transition-colors hover:border-primary/50 hover:bg-muted/30"
          >
            <div className="space-y-1.5">
              <div className="flex items-center gap-2 font-medium">
                <Map className="h-4 w-4 text-primary" />
                <span>Manage Areas</span>
              </div>
              <p className="text-xs text-muted-foreground">
                Add provinces, draw bounding boxes & import GeoJSON
              </p>
            </div>
            <span className="mt-4 text-xs font-semibold text-primary opacity-0 group-hover:opacity-100 transition-opacity">
              Manage &rarr;
            </span>
          </Link>

          <Link
            href="/admin/places"
            className="group flex flex-col justify-between rounded-lg border bg-card p-4 transition-colors hover:border-primary/50 hover:bg-muted/30"
          >
            <div className="space-y-1.5">
              <div className="flex items-center gap-2 font-medium">
                <MapPin className="h-4 w-4 text-primary" />
                <span>Manage Places</span>
              </div>
              <p className="text-xs text-muted-foreground">
                Search, inspect, create, edit and delete locations
              </p>
            </div>
            <span className="mt-4 text-xs font-semibold text-primary opacity-0 group-hover:opacity-100 transition-opacity">
              Manage &rarr;
            </span>
          </Link>

          <Link
            href="/admin/hangfire"
            className="group flex flex-col justify-between rounded-lg border bg-card p-4 transition-colors hover:border-primary/50 hover:bg-muted/30"
          >
            <div className="space-y-1.5">
              <div className="flex items-center gap-2 font-medium">
                <Activity className="h-4 w-4 text-primary" />
                <span>Monitor Jobs</span>
              </div>
              <p className="text-xs text-muted-foreground">
                Inspect running, scheduled, and failed tasks
              </p>
            </div>
            <span className="mt-4 text-xs font-semibold text-primary opacity-0 group-hover:opacity-100 transition-opacity">
              Inspect &rarr;
            </span>
          </Link>

          <Link
            href="/"
            className="group flex flex-col justify-between rounded-lg border bg-card p-4 transition-colors hover:border-primary/50 hover:bg-muted/30"
          >
            <div className="space-y-1.5">
              <div className="flex items-center gap-2 font-medium">
                <Sparkles className="h-4 w-4 text-primary" />
                <span>Client Map App</span>
              </div>
              <p className="text-xs text-muted-foreground">
                View search map, AI Agent planner & places
              </p>
            </div>
            <span className="mt-4 text-xs font-semibold text-primary opacity-0 group-hover:opacity-100 transition-opacity">
              Open App &rarr;
            </span>
          </Link>
        </div>
      </div>
    </div>
  );
}