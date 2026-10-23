# Transit import and administration

Admin UI: `/admin/transit`. Admin-only API: `/api/admin/transit`.

This module manages bus/metro networks separately from destination POIs in Places.
Route numbers are positive SQL `int` values: `01` becomes `1`. The frontend pads
numbers below 10. Alphanumeric codes are rejected with a validation error, never
silently converted. A source/dataset has a fixed Bus or Metro mode, a name and an
explicit network scope. These are managed internally: the admin picks Bus or Metro
and uploads files directly, without creating/selecting a source. `GET /network?mode=Bus`
returns the current network without modifying the database. `POST /imports?mode=Bus`
reuses an existing source (active first, then oldest) or creates it automatically
under a SQL lock on first upload. Bus and Metro stay separate. Legacy source-specific
API endpoints and historical data remain available.

## Supported input

- One XLSX with sheets `Tuyen`, `Tuyen_Chieu`, `Tram`, `Tuyen_Tram` (English canonical
  names below are accepted too).
- Four UTF-8 CSVs uploaded together: `routes.csv`, `route_patterns.csv`, `stops.csv`,
  `route_stops.csv`. Comma or semicolon delimiters, quoted fields and multiline
  text are supported. Vietnamese sheet names can also be used as CSV filenames.
- The UI downloads header templates. File cells must contain values, not formulas.
- Total upload limit: 25 MiB; XLSX expanded content: 100 MiB; up to 100,000 rows per
  sheet and 100 columns. ASP.NET/Next proxy allow 27 MiB including multipart overhead.
  An external reverse proxy must also allow at least 27 MiB (e.g. Nginx
  `client_max_body_size 27m;`). The reverse proxy is not configured in this repository.

| Table | Required columns | Optional columns |
|---|---|---|
| routes | route_no, route_name | fare, source, collected_date, route_id_site, tickets_text |
| route_patterns | route_no, direction, direction_name | first_trip, last_trip, headway_min, headway_max, route_var_id, pattern_code, service_days |
| stops | stop_id, stop_name, lat, lon | address, stop_code, stop_kind, parent_stop_id |
| route_stops | route_no, direction, seq, stop_id | pattern_code, stop_name |

`direction` is 0 or 1. Branches use `pattern_code` (default `default`) consistently
in both pattern and route-stop tables. `seq` starts at 1 and is contiguous per
pattern. A stop may occur more than once in a loop; sequence numbers may not.
`route_var_id` is provider metadata, not a globally unique key.

Times accept HH:mm, HH:mm:ss, or Excel time serials. An end time before the start
is an overnight window, not an exact trip schedule. `service_days` accepts Daily,
Weekdays, Weekends, Unknown; absence means Unknown. `stop_kind` accepts Stop,
Station, Platform, Entrance. Metro defaults to Station; bus defaults to Stop.
Parents must exist in the same snapshot and may not form cycles.

`fare` is an unverified reference amount (VND), never a guaranteed journey fare.
`tickets_text` retains the conditions/multiple fares. Additional source columns
(e.g. routes_through, in_bbox, in_q1, operation_time, headway_text) remain in the
original uploaded file; they are not trusted to derive route relationships or
service-area membership. The directional service columns are authoritative for
normalized windows. No external geocoding is performed.

## Lifecycle and consistency

1. Files are saved privately; a database batch records uploader, source and names.
2. Hangfire validates into a private JSON snapshot, stores line/column issues and
   computes added/replaced/removed counts relative to the active source version.
3. The admin reviews actual data errors (if any) and clicks Apply directly once valid.
   There is no warning list or additional confirmation checkbox. Reference fares and
   absent optional schedules are stored as-is, without emitting import warnings.
   Legacy warnings are excluded from the details and downloadable error report.
4. Apply writes all normalized rows and switches `ActiveBatchId` in a single SQL
   transaction. Failures leave the prior version visible. Every route/stop row
   belongs to its import version. Reimporting does not duplicate the active view.
5. Historical successful versions can be restored with an expected-active-version
   check. Apply/restore actions are audited with actor and timestamp.

State flow: Queued → Validating → Ready / Invalid / ValidationFailed;
Ready → ApplyQueued → Applying → Applied / ApplyFailed.
Invalid inputs are corrected locally and uploaded as a new batch. Technical
failures can be retried; ApplyFailed can be revalidated if its baseline changed.

Batch jobs take a Hangfire distributed lock. Apply/restore serialize per dataset
with SQL `sp_getapplock`; applying a preview based on an outdated version fails
instead of overwriting a newer version. Hangfire retries transient failures twice.
A recurring recovery job runs every two minutes to repair interrupted enqueue
operations and reflect jobs which exhausted retries. DB status, not Hangfire job
retention, is the source of truth for import history.

Safety flags are keyed by dataset + source stop ID, outside the snapshots. Neither
replacement nor restore removes a flag. Admin inspection includes flagged rows
and highlights them; future user-facing network queries must exclude flagged stops.
Flagging is for clearly wrong coordinates/addresses, not preferences.

Complete source networks are retained, including out-of-area stops, to preserve
connectivity. Areas continues to define district-level end-user service scope;
importing a network does not expand Areas. Transit administration does not implement
journey recommendations or bus/metro transfers. Those consume this data in the
subsequent Search/AgentCore feature.

## Storage and retention

Default private directory: `<API content root>/storage/transit/<batch id>/`.
Override with `Transit:StoragePath` / `Transit__StoragePath`. All API and Hangfire
workers must share the same durable directory. The existing production
`/app/storage` bind mount already persists the default path.

Keep original uploads, validation snapshots, import reports and normalized versions
until an explicit retention policy is chosen; there is no automatic deletion that
could break rollback. Back up SQL and this directory together. Uploads are ignored
by git, Docker build context and API publishing and are not served as static files.

## Migrations and verification

The initial migration and model snapshot are included as source files. The API
runs Transit migrations in its normal startup migration pipeline. No EF CLI or
manual database-update command is needed. `TransitSchemaV1` is the frozen model used
by the initial migration/snapshot; future changes must use a new model configuration
and a new migration rather than changing this historical definition.

From repository root, compile and run unit tests:

```sh
dotnet build backend/src/LocationSearch.Api/LocationSearch.Api.csproj
dotnet test backend/tests/Modules/Transit.Tests/Transit.Tests.csproj
```

The supplied workbook round-trip test is optional so CI does not depend on a local
untracked attachment. Enable it with:

```sh
dotnet test backend/tests/Modules/Transit.Tests/Transit.Tests.csproj --environment TRANSIT_WORKBOOK=/absolute/path/to/bus_data.xlsx
```

After the user restarts the API and frontend, verify as Admin:

1. Select Bus and upload `bus_data.xlsx` directly; expect 39 routes, 78 patterns,
   2,155 stops, 4,160 route-stop rows and zero validation errors.
2. Expect no warnings; review the replacement preview and click Apply directly.
3. Inspect route 01, both directions, station order and the schematic map.
4. Import the same file again; active counts stay unchanged, history gains a version.
5. Upload a broken stop reference; activation is blocked, active data stays intact.
6. Flag a stop; reimport/restore; the flag remains.
7. Validate two batches against the same version; apply one; the second must require
   revalidation before it can replace the newly active version.
8. Select Metro and upload using the templates; route number 1 must not
   collide with Bus route number 1. Test parent stations and pattern branches.
9. Verify non-Admin access is denied and failed job retry/restore behavior with SQL
   and Hangfire running. These are runtime checks, separate from parser/model tests.
