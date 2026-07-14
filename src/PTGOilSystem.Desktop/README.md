# PTGOilSystem.Desktop

WPF + WebView2 desktop wrapper for the existing `PTGOilSystem.Web` ASP.NET Core
app. No web code is rewritten — the desktop app launches the web app as a local
loopback child process and shows it inside a WebView2 window.

## How it works
1. Single-instance guard (mutex) — only one window can run (`App.xaml.cs`).
2. `WebServerLauncher` picks a free port on `127.0.0.1` (or a fixed one), starts
   the web app, and waits for `/health` to return 200.
3. A loading screen is shown until the server is ready, then WebView2 navigates
   to the local URL.
4. On exit the whole web process tree is killed.

## Configuration — `appsettings.json` ("Web" section)
- `ConnectionString` — PostgreSQL string passed as
  `ConnectionStrings__DefaultConnection`. Empty = web app uses its own config.
- `Host` / `Port` — loopback bind. `Port: 0` = auto free port.
- `Environment` — `ASPNETCORE_ENVIRONMENT` (default `Production`).
- `AutoMigrate` — `false` by default; never migrates unless set `true`.
- `StartupTimeoutSeconds` — health-probe wait.
- `ExecutablePath` / `DllPath` / `DevProjectPath` — how the web app is located
  (published exe → published dll → run from source).

## Run from source (dev)
```
dotnet run --project src/PTGOilSystem.Desktop
```
Uses `DevProjectPath` to launch the web app via `dotnet run`.

## Publish (win-x64, self-contained)
```
# 1. Publish the web app into the desktop's web\ folder
dotnet publish src/PTGOilSystem.Web -c Release -r win-x64 --self-contained true \
  -o src/PTGOilSystem.Desktop/bin/Release/net8.0-windows/publish/win-x64/web

# 2. Publish the desktop wrapper
dotnet publish src/PTGOilSystem.Desktop -c Release -p:PublishProfile=win-x64
```
Ship the `publish/win-x64` folder. End users need the WebView2 Runtime
(pre-installed on current Windows 10/11; otherwise install the Evergreen runtime).
