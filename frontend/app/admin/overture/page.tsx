"use client";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { api, ApiError } from "@/lib/api";
import { getToken } from "@/lib/auth";
import type { Area } from "@/lib/types";
import {
  Activity,
  ArrowRight,
  Check,
  CheckCircle2,
  Clock,
  Database,
  ExternalLink,
  Loader2,
  Play,
  Save,
} from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { toast } from "sonner";

interface RecurringSetting {
  enabled?: boolean;
  cron?: string;
  areaId?: string;
  batchSize?: number;
  bbox?: {
    minLat: number;
    minLng: number;
    maxLat: number;
    maxLng: number;
  } | null;
}

const CRON_PRESETS = [
  { label: "Daily (02:00 UTC)", value: "0 2 * * *" },
  { label: "Weekly (Sunday)", value: "0 2 * * 0" },
  { label: "Every 12 Hours", value: "0 */12 * * *" },
  { label: "Hourly (Testing)", value: "0 * * * *" },
];

export default function OvertureSettingsPage() {
  const [areas, setAreas] = useState<Area[]>([]);
  const [loading, setLoading] = useState(true);

  // Recurring schedule state
  const [scheduleEnabled, setScheduleEnabled] = useState(false);
  const [scheduleCron, setScheduleCron] = useState("0 2 * * *");
  const [scheduleAreaId, setScheduleAreaId] = useState<string>("");
  const [scheduleBatchSize, setScheduleBatchSize] = useState(1000);
  const [savingSchedule, setSavingSchedule] = useState(false);

  // Manual on-demand import state
  const [manualAreaId, setManualAreaId] = useState<string>("");
  const [manualBatchSize, setManualBatchSize] = useState(1000);
  const [triggeringManual, setTriggeringManual] = useState(false);
  const [lastJobId, setLastJobId] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      const token = getToken();
      try {
        const [areasData, settingData] = await Promise.allSettled([
          api.get<Area[]>("/api/areas", token),
          api.get<{ key: string; valueJson: string }>(
            "/api/admin/overture/settings/overture.recurring",
            token
          ),
        ]);

        let loadedAreas: Area[] = [];
        if (areasData.status === "fulfilled") {
          loadedAreas = areasData.value;
          setAreas(loadedAreas);
          if (loadedAreas.length > 0) {
            setManualAreaId(loadedAreas[0].id);
            setScheduleAreaId(loadedAreas[0].id);
          }
        }

        if (settingData.status === "fulfilled" && settingData.value?.valueJson) {
          try {
            const parsed: RecurringSetting = JSON.parse(settingData.value.valueJson);
            if (typeof parsed.enabled === "boolean") setScheduleEnabled(parsed.enabled);
            if (parsed.cron) setScheduleCron(parsed.cron);
            if (parsed.batchSize) setScheduleBatchSize(parsed.batchSize);
            if (parsed.areaId && loadedAreas.some((a) => a.id === parsed.areaId)) {
              setScheduleAreaId(parsed.areaId);
            }
          } catch {
            // ignore parse error
          }
        }
      } catch (err) {
        toast.error(
          err instanceof ApiError ? err.message : "Failed to load Overture settings",
        );
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function onSaveSchedule() {
    if (!scheduleAreaId) {
      toast.error("Please select a target area for the schedule.");
      return;
    }
    setSavingSchedule(true);
    try {
      const targetArea = areas.find((a) => a.id === scheduleAreaId);
      const payload: RecurringSetting = {
        enabled: scheduleEnabled,
        cron: scheduleCron.trim() || "0 2 * * *",
        areaId: scheduleAreaId,
        batchSize: scheduleBatchSize > 0 ? scheduleBatchSize : 1000,
        bbox: targetArea
          ? {
              minLat: targetArea.bboxMinLat,
              minLng: targetArea.bboxMinLng,
              maxLat: targetArea.bboxMaxLat,
              maxLng: targetArea.bboxMaxLng,
            }
          : null,
      };

      await api.put(
        "/api/admin/overture/settings/overture.recurring",
        payload,
        getToken()
      );
      toast.success("Schedule settings saved & Hangfire updated!");
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Failed to save schedule settings",
      );
    } finally {
      setSavingSchedule(false);
    }
  }

  async function onTriggerManual() {
    if (!manualAreaId) {
      toast.error("Please select an area first.");
      return;
    }
    setTriggeringManual(true);
    try {
      const area = areas.find((a) => a.id === manualAreaId);
      const res = await api.post<{ jobId: string }>(
        "/api/admin/overture/import",
        {
          areaId: manualAreaId,
          bbox: area && {
            minLat: area.bboxMinLat,
            minLng: area.bboxMinLng,
            maxLat: area.bboxMaxLat,
            maxLng: area.bboxMaxLng,
          },
          batchSize: manualBatchSize > 0 ? manualBatchSize : 1000,
        },
        getToken(),
      );
      const jobId = res?.jobId ?? "queued";
      setLastJobId(jobId);
      toast.success(`Import job enqueued. Job ID: ${jobId}`);
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Failed to trigger import job",
      );
    } finally {
      setTriggeringManual(false);
    }
  }

  const selectedManualArea = areas.find((a) => a.id === manualAreaId);

  if (loading) {
    return (
      <div className="flex h-48 items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-8 max-w-4xl">
      <div>
        <h2 className="text-2xl font-bold tracking-tight">Overture Maps Ingestion</h2>
        <p className="text-sm text-muted-foreground">
          Configure automated background synchronization or trigger on-demand data imports.
        </p>
      </div>

      {/* Card 1: Automatic Recurring Schedule */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="space-y-1">
              <CardTitle className="text-lg flex items-center gap-2">
                <Clock className="h-5 w-5 text-primary" />
                <span>Automated Recurring Schedule</span>
              </CardTitle>
              <CardDescription>
                Hangfire automatically triggers Overture sync according to this schedule.
              </CardDescription>
            </div>
            <Badge variant={scheduleEnabled ? "default" : "outline"}>
              {scheduleEnabled ? "Enabled" : "Disabled"}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="flex items-center gap-3 rounded-lg border p-3 bg-muted/20">
            <input
              type="checkbox"
              id="scheduleEnabled"
              checked={scheduleEnabled}
              onChange={(e) => setScheduleEnabled(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
            />
            <label
              htmlFor="scheduleEnabled"
              className="text-sm font-medium leading-none cursor-pointer"
            >
              Enable automated recurring sync (overture-sync job)
            </label>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="scheduleArea">Target Area</Label>
              <select
                id="scheduleArea"
                value={scheduleAreaId}
                onChange={(e) => setScheduleAreaId(e.target.value)}
                disabled={!scheduleEnabled}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-sm disabled:opacity-50"
              >
                {areas.map((a) => (
                  <option key={a.id} value={a.id}>
                    [{a.levelName}] {a.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="scheduleBatch">Batch Size</Label>
              <Input
                id="scheduleBatch"
                type="number"
                min={100}
                max={5000}
                value={scheduleBatchSize}
                onChange={(e) => setScheduleBatchSize(Number(e.target.value))}
                disabled={!scheduleEnabled}
                placeholder="1000"
              />
              <p className="text-[11px] text-muted-foreground">
                Recommended: 1000 - 2000 per SQL batch.
              </p>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="scheduleCron">Cron Expression (UTC)</Label>
            <Input
              id="scheduleCron"
              value={scheduleCron}
              onChange={(e) => setScheduleCron(e.target.value)}
              disabled={!scheduleEnabled}
              placeholder="0 2 * * *"
            />
            <div className="flex flex-wrap gap-2 pt-1">
              {CRON_PRESETS.map((preset) => (
                <button
                  key={preset.value}
                  type="button"
                  disabled={!scheduleEnabled}
                  onClick={() => setScheduleCron(preset.value)}
                  className="rounded border bg-muted/40 px-2 py-0.5 text-xs text-muted-foreground hover:bg-muted hover:text-foreground transition-colors disabled:opacity-50"
                >
                  {preset.label}
                </button>
              ))}
            </div>
          </div>

          <div className="pt-2">
            <Button onClick={onSaveSchedule} disabled={savingSchedule}>
              {savingSchedule ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save Schedule Settings
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Card 2: Manual Import On-Demand */}
      <Card>
        <CardHeader>
          <div className="space-y-1">
            <CardTitle className="text-lg flex items-center gap-2">
              <Play className="h-5 w-5 text-primary" />
              <span>Manual Import on Demand</span>
            </CardTitle>
            <CardDescription>
              Select an administrative region and trigger an immediate background job.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="manualArea">Select Area</Label>
              <select
                id="manualArea"
                value={manualAreaId}
                onChange={(e) => setManualAreaId(e.target.value)}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-sm"
              >
                {areas.map((a) => (
                  <option key={a.id} value={a.id}>
                    [{a.levelName}] {a.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="manualBatch">Batch Size</Label>
              <Input
                id="manualBatch"
                type="number"
                min={100}
                max={5000}
                value={manualBatchSize}
                onChange={(e) => setManualBatchSize(Number(e.target.value))}
                placeholder="1000"
              />
            </div>
          </div>

          {/* Area Bbox preview */}
          {selectedManualArea && (
            <div className="rounded-lg border bg-muted/20 p-3 space-y-1 text-xs">
              <div className="flex items-center justify-between font-medium">
                <span>Selected: {selectedManualArea.name} ({selectedManualArea.levelName})</span>
                <Link
                  href="/admin/overture/areas"
                  className="text-primary hover:underline inline-flex items-center gap-0.5"
                >
                  Edit Bbox &rarr;
                </Link>
              </div>
              <p className="text-muted-foreground font-mono">
                Bbox: [{selectedManualArea.bboxMinLat.toFixed(4)}, {selectedManualArea.bboxMinLng.toFixed(4)}] to [
                {selectedManualArea.bboxMaxLat.toFixed(4)}, {selectedManualArea.bboxMaxLng.toFixed(4)}]
              </p>
            </div>
          )}

          <div className="flex items-center gap-3">
            <Button onClick={onTriggerManual} disabled={triggeringManual}>
              {triggeringManual ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Play className="mr-2 h-4 w-4" />
              )}
              Trigger Import Now
            </Button>

            <Link
              href="/admin/hangfire"
              className="inline-flex items-center gap-1.5 rounded-md border px-3 py-2 text-xs font-medium text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
            >
              <Activity className="h-3.5 w-3.5" />
              <span>Open Hangfire Dashboard</span>
            </Link>
          </div>

          {/* Last job enqueued banner */}
          {lastJobId && (
            <div className="flex items-center justify-between rounded-lg border border-primary/30 bg-primary/5 p-3 text-xs">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-primary" />
                <span>
                  Job <span className="font-mono font-bold">#{lastJobId}</span> successfully queued!
                </span>
              </div>
              <Link
                href="/admin/hangfire"
                className="font-semibold text-primary hover:underline inline-flex items-center gap-1"
              >
                <span>Track live worker</span>
                <ArrowRight className="h-3 w-3" />
              </Link>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}