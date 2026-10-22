# Agent prompt administration

Open `/admin/prompts` as an administrator. System (5), Tools (12), Meeting prompts (9), Messages (17), and Email templates (12) are collapsible groups with search and independent scrolling. Editing stays local until **Xuất bản**. Preview substitutes sample variables without calling the model. Load a historical revision or the shipped default, review, and publish to restore it as a new version. There is no delete endpoint. The approved-plan template requires `{approvedPlan}`.

## SQL persistence

Published content lives in SQL Server table `AgentPromptRevisions` in `AgentCoreDbContext`. Each row holds the prompt key, version, content, UTC publication date and authenticated user ID. Latest version is the active prompt; previous rows form the history. The `(Key, Version)` primary key makes concurrent publication of the same next version fail rather than overwrite another admin's work, including the first publication. Admin reads and history go directly to SQL.

No dependency on Elasticsearch remains for prompts. Unpublished prompts still use shipped defaults. Configuration values from the earlier RAM/Elasticsearch prototype are not imported automatically: copy any custom content before restarting the old backend, then publish it from the updated UI.

## Cache

Agent reads use a singleton in-process cache with an absolute 30-second expiry, including missing overrides. Concurrent cache misses for the same key share a lock. Successful publication invalidates that key locally, synchronized with in-flight reads. Other backend instances pick up changes within 30 seconds; this is bounded staleness, not immediate distributed invalidation. Restart reloads published values from SQL on demand.

## API

All endpoints require `AdminOnly`:

- `GET /api/admin/prompts`
- `GET/PUT /api/admin/prompts/{key}`
- `POST /api/admin/prompts/{key}/preview`
- `GET /api/admin/prompts/{key}/history`

Publish accepts `{ content, version }`; a stale version returns 409. Tool names, parameter schemas, authorization and confirmation logic remain in code. The meeting-search controller now resolves its nine prompt templates and conversational fallback messages through this catalog. Technical tool-result/status/validation messages are English API text, not editable AI instructions.

## Migration and verification

Migration `20261020000000_AddAgentPromptRevisions` and its model snapshot are supplied as source. No EF CLI command or database update was run by the agent. The existing startup `MigrateAsync()` applies the migration when the user restarts the backend. Do not generate a duplicate migration.

From the repository root, after ensuring local SQL Server is running, start/restart the backend yourself with:

```sh
dotnet run --project backend/src/LocationSearch.Api
```

After restart: publish a prompt, refresh the page, verify its history, restart again and verify persistence. Restore a previous version and verify a new agent call uses it. For concurrency, open the same prompt in two tabs, publish in one, then publish from the stale tab: the second must return 409. Unauthenticated/non-admin requests must be rejected.

Automated checks cover placeholder validation, cache hits, invalidation, cross-instance TTL expiry, an in-flight read/publication race, and migration snapshot consistency without connecting to SQL. Live SQL persistence and browser end-to-end verification require the restarted backend.

The System group includes `area-context-system`: runtime area coverage, data integrity and routing instructions. Its required `{areasDesc}` variable is filled from active Areas on each run; the block is injected only when active areas exist, preserving existing behavior.

## API language and configurable content

Technical validation, errors, operation results, activity summaries and pending-action descriptions use English defaults. The audit also covered Users/Friends, Areas and Search admin API messages. HTTP status codes, JSON field names, action types and activity kinds are unchanged. Historical records and existing custom configuration are not rewritten. Names, addresses, search vocabulary and geographic data remain in their original language.

Conversational answers in `Answer` / `AiInsight` remain natural-language content: 17 fallback templates are editable under Messages. Nine NLU, verification, anchor and empty-result prompt templates are under Meeting prompts. All twelve tool descriptions remain editable under Tools.

Email templates cover scheduled meetings, Google Calendar invitations, friend invitations, attachment notices, subjects, calendar description and sender display name. `email.sender-name` overrides the existing SMTP display-name setting when published; credentials and sender email address remain deployment settings. HTML variable values are HTML-encoded on preview and delivery; preview is rendered as text in the admin, never as executable HTML. Placeholder replacement is a single pass, so user input containing `{variable}` is preserved as data. Template bodies are trusted administrator-authored content.

The frontend error reader supports `{message}`, `{error}`, Problem Details and plain-text errors. Origin identity uses `isMe` before legacy localized name fallback, and numbered avatars support both `Người N` and `Person N`. No API-message text comparison controls requests. Local UI labels remain localized independently of API message language.

Shipped defaults live in `Infrastructure/Prompts/managed-templates.json`; published overrides and history use the same SQL table/cache as existing prompts. No additional migration is required for these catalog entries.

Additional verification: backend tests cover catalog validation, stored overrides, single-pass rendering, HTML encoding, preview/delivery consistency, English API validation and email delivery with a customized subject. Frontend regression checks run with `node --experimental-strip-types --test frontend/tests/*.test.mjs`.
