"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { getToken } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import { ChevronDown, Search, Settings2, Wrench, MessageSquare, Mail, Sparkles } from "lucide-react";

type Entry = { key: string; group: string; variables: string[] };
type Detail = Entry & { content: string; defaultContent: string; version: number };
type Revision = { version: number; content: string; changedAt: string };
const base = "/api/admin/prompts";

export default function PromptsPage() {
  const [entries, setEntries] = useState<Entry[]>([]);
  const [search, setSearch] = useState("");
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const [selected, setSelected] = useState("");
  const [detail, setDetail] = useState<Detail | null>(null);
  const [content, setContent] = useState("");
  const [history, setHistory] = useState<Revision[]>([]);
  const [preview, setPreview] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    api.get<{ prompts: Entry[]; persistent: boolean }>(base, getToken())
      .then(data => { if (active) { setEntries(data.prompts); setSelected(data.prompts[0]?.key ?? ""); } })
      .catch(e => { if (active) setError(String(e.message)); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!selected) return;
    let active = true;
    Promise.all([
      api.get<Detail>(`${base}/${selected}`, getToken()),
      api.get<Revision[]>(`${base}/${selected}/history`, getToken()),
    ]).then(([data, revisions]) => {
      if (!active) return;
      setDetail(data); setContent(data.content); setHistory(revisions);
    }).catch(e => { if (active) setError(String(e.message)); });
    return () => { active = false; };
  }, [selected]);

  async function run(publish: boolean) {
    if (!detail) return;
    setBusy(true); setError(""); setNotice("");
    try {
      if (publish) {
        const result = await api.put<{ version: number }>(`${base}/${selected}`, { content, version: detail.version }, getToken());
        setDetail({ ...detail, content, version: result.version });
        setNotice("Đã lưu vào SQL Server. Server hiện tại áp dụng ngay; server khác cập nhật trong tối đa 30 giây.");
        setHistory(await api.get<Revision[]>(`${base}/${selected}/history`, getToken()));
      } else {
        const result = await api.post<{ content: string }>(`${base}/${selected}/preview`, { content }, getToken());
        setPreview(result.content);
      }
    } catch (e) { setError(e instanceof Error ? e.message : "Không thể thực hiện."); }
    finally { setBusy(false); }
  }

  function edit(value: string) { setContent(value); setPreview(""); setNotice(""); }

  return (
    <main className="max-w-6xl space-y-5">
      <div><h2 className="text-2xl font-semibold">Agent prompts</h2>
        <p className="text-sm text-muted-foreground">Quản lý prompt, câu trả lời mẫu và mẫu email. Chỉnh sửa chỉ có hiệu lực sau khi xuất bản.</p></div>
      <p className="text-xs text-muted-foreground">Nội dung đã xuất bản và lịch sử được lưu trong SQL Server.</p>
      {error && <p role="alert" className="text-red-600">{error}</p>}
      {notice && <p role="status" className="text-green-700">{notice}</p>}
      <div className="grid gap-6 md:grid-cols-[250px_1fr]">
        <nav aria-label="Danh sách prompt" className="self-start rounded-xl border bg-muted/20 md:sticky md:top-20">
          <div className="relative border-b p-3">
            <Search aria-hidden="true" className="absolute left-6 top-6 size-4 text-muted-foreground" />
            <Input aria-label="Tìm prompt" placeholder="Tìm prompt…" value={search} onChange={e => setSearch(e.target.value)} className="bg-background pl-9" />
          </div>
          <div className="max-h-[65vh] space-y-2 overflow-y-auto p-2">
            {[{ key: "System", title: "System", icon: Settings2 }, { key: "Tool", title: "Tools", icon: Wrench }, { key: "Meeting", title: "Meeting prompts", icon: Sparkles }, { key: "Messages", title: "Messages", icon: MessageSquare }, { key: "Email", title: "Email templates", icon: Mail }].map(group => {
              const all = entries.filter(entry => entry.group === group.key);
              const filtered = all.filter(entry => entry.key.toLowerCase().includes(search.trim().toLowerCase()));
              const open = !!search.trim() || !collapsed[group.key];
              const Icon = group.icon;
              if (!filtered.length) return null;
              return <div key={group.key}>
                <button type="button" aria-expanded={open} aria-controls={`prompt-group-${group.key}`}
                  onClick={() => setCollapsed({ ...collapsed, [group.key]: open })}
                  className="flex w-full items-center gap-2 rounded-md px-2 py-2.5 text-sm font-semibold hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring">
                  <Icon aria-hidden="true" className="size-4 text-muted-foreground" />
                  <span>{group.title}</span><span className="rounded bg-muted px-1.5 text-xs text-muted-foreground">{search.trim() ? `${filtered.length}/` : ""}{all.length}</span>
                  <ChevronDown aria-hidden="true" className={`ml-auto size-4 transition-transform ${open ? "" : "-rotate-90"}`} />
                </button>
                <div id={`prompt-group-${group.key}`} hidden={!open} className="ml-4 space-y-1 border-l py-1 pl-2">
                  {filtered.map(entry => <button key={entry.key} type="button" disabled={busy} aria-current={selected === entry.key ? "true" : undefined}
                    title={entry.key}
                    className={`block w-full truncate rounded-md px-3 py-2 text-left text-sm transition-colors focus-visible:outline-2 focus-visible:outline-ring disabled:opacity-50 ${selected === entry.key ? "bg-primary/10 font-medium text-primary" : "text-muted-foreground hover:bg-muted hover:text-foreground"}`}
                    onClick={() => {
                      if (selected === entry.key) return;
                      if (detail && content !== detail.content && !window.confirm("Bỏ các chỉnh sửa chưa xuất bản?")) return;
                      setDetail(null); setError(""); setPreview(""); setNotice("");
                      setSelected(entry.key);
                    }}>{entry.key.replace(/^tool\./, "")}</button>)}
                </div>
              </div>;
            })}
            {entries.length > 0 && !entries.some(entry => entry.key.toLowerCase().includes(search.trim().toLowerCase())) && <p className="p-3 text-sm text-muted-foreground">Không tìm thấy prompt.</p>}
          </div>
        </nav>
        {detail && <section className="min-w-0 space-y-4">
          <div className="flex items-center justify-between"><label htmlFor="prompt-content" className="font-semibold">{detail.key}</label><span className="text-sm">Phiên bản {detail.version || "mặc định"}</span></div>
          <Textarea id="prompt-content" disabled={busy} value={content} onChange={e => edit(e.target.value)} className="min-h-80 font-mono text-sm" maxLength={30000} />
          {detail.variables.length > 0 && (
            <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
              <span className="font-medium text-foreground">Biến hỗ trợ:</span>
              {detail.variables.map(v => (
                <span key={v} className="rounded border bg-muted/60 px-2 py-0.5 font-mono text-foreground">
                  {`{${v}}`}
                </span>
              ))}
            </div>
          )}
          <div className="flex flex-wrap items-center gap-2">
            <Button disabled={busy || !content.trim()} onClick={() => run(false)} variant="outline">Xem trước</Button>
            <Button disabled={busy || !content.trim() || content === detail.content} onClick={() => run(true)}>Xuất bản</Button>
            <Button disabled={busy} variant="ghost" onClick={() => edit(detail.defaultContent)}>Nạp bản mặc định</Button>
          </div>
          {preview && (
            <div className="space-y-2 rounded-lg border bg-card p-4">
              <div className="flex items-center justify-between">
                <span className="text-sm font-semibold">Kết quả xem trước (Dữ liệu mẫu tự động)</span>
                <Button size="sm" variant="ghost" onClick={() => setPreview("")}>Đóng</Button>
              </div>
              {detail.key.endsWith(".html") ? (
                <div className="overflow-hidden rounded border bg-white p-2">
                  <iframe srcDoc={preview} sandbox="allow-same-origin" className="h-80 w-full border-0" title="HTML Preview" />
                </div>
              ) : (
                <pre className="max-h-80 overflow-y-auto whitespace-pre-wrap break-words rounded bg-muted/40 p-3 font-mono text-sm">{preview}</pre>
              )}
            </div>
          )}
          <h3 className="font-semibold">Lịch sử xuất bản</h3>
          {!history.length && <p className="text-sm text-muted-foreground">Chưa có phiên bản tùy chỉnh.</p>}
          {history.map(revision => <div key={revision.version} className="flex items-center justify-between gap-3 border-b py-2 text-sm">
            <span>v{revision.version} · {new Date(revision.changedAt).toLocaleString("vi-VN")}</span>
            <Button disabled={busy} variant="outline" onClick={() => edit(revision.content)}>Nạp bản này</Button>
          </div>)}
        </section>}
      </div>
    </main>
  );
}
