# LocationSearch Admin Panel

Next.js admin site for the LocationSearch API. Lives inside the backend
repository at `src/Modules/admin-web` so the FE and the .NET API ship together.

## Stack

- Next.js 16 (App Router, `src/` dir) + React 19 + TypeScript
- Tailwind CSS v4
- shadcn/ui (`base-nova` style, built on Base UI — note: use the `render` prop
  instead of Radix's `asChild`)
- Recharts for the dashboard charts
- JWT stored in `localStorage` (+ a cookie mirror so `src/proxy.ts` can redirect)

## Run

```bash
# 1. API first (http://localhost:5232)
dotnet run --project ../../../LocationSearch.Api/LocationSearch.Api.csproj

# 2. Admin site
npm install
npm run dev      # http://localhost:3000
```

Set `NEXT_PUBLIC_API_BASE_URL` if the API is not on `http://localhost:5232`
(see `.env.example`).

> **Open this folder in VS Code, not Visual Studio.** Visual Studio's Solution
> Explorer only renders content of projects that have a `.csproj`, so it will
> never show this Next.js app. Keep `LocationSearch.slnx` for the .NET side and
> open `admin-web/` separately in VS Code. If Visual Studio ever offers to
> create an `.esproj` / `.njsproj` here, decline — those files must not be
> committed.

## Scripts

| Command         | Description                    |
| --------------- | ------------------------------ |
| `npm run dev`   | Dev server on port 3000        |
| `npm run build` | Production build               |
| `npm start`     | Serve the production build     |
| `npm run lint`  | ESLint                         |
| `npx tsc --noEmit` | Type check                 |

## First administrator

The API seeds an administrator on startup when `Admin:Seed:Enabled` is `true`
and no administrator exists yet. In development it defaults to:

```
admin@locationsearch.local / Admin@12345
```

Change it in `src/LocationSearch.Api/appsettings.Development.json`
(git-ignored) or override with environment variables.

## Routes

| Route               | Auth    | Description                                  |
| ------------------- | ------- | -------------------------------------------- |
| `/login`            | public  | Administrator sign in                        |
| `/admin/dashboard`  | admin   | Customer metrics, filterable by date range   |
| `/admin/users`      | admin   | User list with search, filters and paging    |
| `/admin/users/new`  | admin   | Create user                                  |
| `/admin/users/[id]` | admin   | Edit user, reset password                    |
