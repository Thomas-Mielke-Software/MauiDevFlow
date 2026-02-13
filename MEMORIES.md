# MauiDevFlow — Project Memories

This file is the **single source of truth** for project conventions, patterns, and lessons learned.
Always read this file at the start of a session. Update it as new knowledge is gained.
Do NOT use `store_memory` or retrieve from memory tools — use this file instead.

---

## Building & Testing

- **CI build**: `dotnet build ci.slnf` and `dotnet test ci.slnf` (excludes SampleMauiApp which requires MAUI workloads)
- **Sample app**: `dotnet build src/SampleMauiApp -f net10.0-maccatalyst` (or `-ios`, `-android`)
- **Local CLI**: Always use `dotnet run --project src/MauiDevFlow.CLI --` or the built binary — never the globally installed tool when testing local changes
- **NuGet local testing**: Use a low prerelease version (e.g. `0.0.999-as8d33f`) to avoid conflicting with published versions. Never install the tool globally when testing locally — use `dotnet run --project` instead
- **iOS build timing**: iOS simulator builds finish faster than expected — check agent status early (~60s) instead of waiting the full 120s
- **Incremental build caching**: Mac Catalyst builds may use stale linked DLLs. Always clean both Agent AND SampleMauiApp `bin`+`obj` for the target TFM when debugging Agent code changes
- **Always verify with sample app**: Build, deploy (Mac Catalyst is fastest on macOS), and test end-to-end before marking tasks complete

## Git & Release

- **Do NOT auto-commit or push** unless the user explicitly says to
- **Release process**: Create GitHub release with tag `vX.Y.Z` at https://github.com/Redth/MauiDevFlow — always use the correct org/repo link
- **Versioning**: `Directory.Build.props` has shared version for Agent/Blazor. CLI has its own version in its `.csproj`. Publish workflow uses Git tag as `PackageVersion`

## Architecture

- **Single port**: MauiDevFlow uses a single port (default 9223, configurable) for both MAUI native and CDP commands. No WebSocket — CDP uses HTTP POST `/api/cdp`
- **Config file**: `.mauidevflow` (hidden dotfile, no extension) in project directory. MSBuild targets and CLI both read port from this file
- **MSBuild port override**: `dotnet build -p:MauiDevFlowPort=XXXX`. The `.targets` file writes a `.g.cs` with `AssemblyMetadataAttribute` using `WriteLinesToFile` (not `AssemblyAttribute` items, which don't work)
- **MSBuild target conditions**: `Condition` on a target is evaluated before `BeforeTargets` dependencies run. Don't put `Condition` on a target that depends on a property set by its `BeforeTargets` dependency — move the Condition to inner tasks/ItemGroups instead
- **Agent/Blazor are `#if DEBUG` only** — never ship in production
- **Reflection for cross-package wiring**: `BlazorDevFlowExtensions.WireAgentCdp()` connects Blazor→Agent via reflection (Type.GetType/GetProperty/SetValue) to avoid NuGet dependency between packages

## Broker Daemon

- **Broker port**: 19223 (default). Agents connect via WebSocket, CLI queries via HTTP
- **Agent port assignment range**: 10223-10899 (raised from 9223-9899 to avoid collisions with old `.mauidevflow` config files)
- **CLI auto-starts broker**: `ResolveAgentPort()` calls `EnsureBrokerRunningAsync()` when the TCP check fails. The TCP check MUST be in its own try/catch — `Wait()` throws `AggregateException` on timeout rather than returning false
- **Agent retries broker**: If the broker isn't running at app startup, the agent starts a background reconnection timer (2s, 5s, 10s, 15s backoff, indefinite). Apps launched before the broker auto-register when it comes up
- **Agent reports currentPort**: Late reconnections include `currentPort` in the registration so the broker uses the agent's existing HTTP listener port instead of assigning a new one
- **Broker testing**: When changing broker code, **always kill the running broker process, rebuild, and restart** before testing. The broker is a detached daemon — old code keeps running until explicitly killed
- **Port conflicts with global tool**: When debugging MauiDevFlow locally, a broker from the globally installed CLI may already be on port 19223. Use a non-default port for local testing to avoid conflicts
- **Agent identity**: `SHA256(absolute_csproj_path + "|" + TFM)[:12]` in lowercase hex
- **Android connectivity**: `adb reverse tcp:19223 tcp:19223` for agent→broker. `adb forward tcp:{port} tcp:{port}` for CLI→agent
- **Broken pipe blocking**: When spawning background broker via `Process.Start()`, must redirect+close stdout/stderr/stdin. Otherwise `Console.WriteLine` blocks (not throws) after parent exits on macOS, preventing `HttpListener` from processing requests
- **HttpListener prefix**: Use `http://localhost:` not `http://+:` on macOS — the latter sometimes fails for WebSocket upgrades

## CLI

- **Global options**: `--agent-port`, `--agent-host`, `--platform` use `rootCommand.AddGlobalOption()` so they work on all subcommands (e.g. `MAUI status --agent-port 9225`). Don't add them to individual `Command` initializers
- **Port resolution priority**: Explicit `--agent-port` > broker discovery (exact ID → same project → single agent) > `.mauidevflow` config > default 9223

## Blazor / CDP

- **Chobitsu async behavior**: Chobitsu fires `onMessage` asynchronously (not in same JS turn as `sendRawMessage`). CDP commands need two JS evals: one to send + capture, one to read the response with a small polling delay
- **Chobitsu loading**: Must be loaded via a static `<script>` tag in `index.html`. Dynamic script tags fail with MAUI's `app://` scheme, and `EvaluateJavaScriptAsync` fails because chobitsu uses `eval`/`new Function` internally which CSP blocks
- **Blazor JS initializers**: DO work in .NET 10 MAUI Blazor Hybrid. Use `beforeStart()` to inject script tags
- **RCL static web assets**: Files served at root path (e.g. `chobitsu.js`) and `_content/{PackageId}/` via `fetch()`, but only root-relative paths work for `<script src>` tags in `app://` scheme
- **NuGet packaging**: MauiDevFlow.Blazor delivers `chobitsu.js` as an RCL static web asset. Two MSBuild targets remove `_framework/` assets from build and publish manifests to avoid conflicts
- **GenerateJSModuleManifest=false**: Required in MauiDevFlow.Blazor to prevent conflicting `_framework/blazor.modules.json` with consuming apps

## Platform-Specific

- **Assembly.GetEntryAssembly() returns null on Android/iOS**: Must scan `AppDomain.CurrentDomain.GetAssemblies()` for `AssemblyMetadataAttribute` values
- **DeviceInfo.Platform throws during DI registration**: MAUI isn't fully initialized when `AddMauiDevFlowAgent` runs. Fallback uses `OperatingSystem.IsAndroid()` etc.
- **SynchronizationContext deadlock**: `AddMauiDevFlowAgent` runs on the main thread which has a `SynchronizationContext`. Must use `Task.Run(() => ...).GetAwaiter().GetResult()` for async broker registration to avoid deadlock
- **Mac Catalyst sandbox**: `Path.GetTempPath()` returns `~/Library/Containers/{bundleId}/Data/tmp/` not `/tmp/`
- **Device compatibility**: MAUI TFM compile target (e.g. net10.0-android targets API 36) doesn't mean you need a matching emulator. Apps run on any device at or above `SupportedOSPlatformVersion`
- **TapGestureRecognizer**: `SendTapped` is `internal void` on `TapGestureRecognizer`. Call via reflection with `BindingFlags.NonPublic`, args: `(sender, null)`

## Logging

- **Buffered logging**: `FileLogWriter` uses `ConcurrentQueue` buffer + `Timer` drain + `ReaderWriterLockSlim` for thread-safe file writes
- **File sharing on Windows**: Always open log files with `FileShare.ReadWrite | FileShare.Delete` for reading. `File.ReadAllLines()` uses default `FileShare.Read` which conflicts with the writer on Windows
- **Log format**: Rotating JSONL files with compact property names (`t`, `l`, `c`, `m`, `e`, `s` for source: "native" or "webview")
- **WebView log capture**: JS `console.*` intercepted by `console-intercept.js` → buffered in `window.__webviewLogs` → drained every 2s by native timer → written to same JSONL files with `source: "webview"`
