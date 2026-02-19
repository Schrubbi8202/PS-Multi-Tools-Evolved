# Preview Guide (New 2026 UI Stack)

This guide runs the new stack introduced under `apps/` + `src/`.

## 1. Install Node dependencies

From repository root:

```powershell
npm install
```

## 2. Start the local API host

In terminal 1:

```powershell
dotnet run --project src/PSMultiTools.ApiHost/PSMultiTools.ApiHost.csproj
```

Expected API base URL: `http://127.0.0.1:5035`

Optional quick check:

```powershell
curl http://127.0.0.1:5035/api/system/health
```

## 3. Preview the React UI only

In terminal 2:

```powershell
npm run web:dev
```

Open `http://127.0.0.1:5173`.

## 4. Preview the Electron desktop shell

From repository root:

```powershell
npm run desktop:dev
```

Notes:
- This command builds `apps/web-ui` + `apps/desktop-electron` first, then launches Electron.
- Electron starts/stops `PSMultiTools.ApiHost` automatically in its current implementation.
- If your environment forces `ELECTRON_RUN_AS_NODE=1`, the script clears it automatically before launch.

## 5. Build artifacts

```powershell
dotnet build src/PSMultiTools.ApiHost/PSMultiTools.ApiHost.csproj
npm run web:build
npm run desktop:build
```
