# Diagnostics and reporting problems

[Documentation](README.md) · [Polski](../DIAGNOSTICS.md)

## Diagnostics window

Click **Diagnostyka** (Diagnostics). The dialog shows the application version, active .NET runtime, OS/process architecture, located FFmpeg/FFprobe executables, log path and most recent status. FFmpeg/FFprobe version details are read during export preflight; an unread version beforehand is not itself an error.

## Local log

~~~text
%LOCALAPPDATA%\Win10WebmCut\Logs\app.log
~~~

Entries include UTC timestamps, event type, operation IDs and, where relevant, external process PIDs. Exceptions can contain stack traces, filenames and a bounded portion of tool stderr. The active log is limited to approximately **1 MiB** with three rotated backups (app.log.1–.3). Logs are not automatically uploaded.

Read the latest lines with PowerShell:

~~~powershell
$log = Join-Path $env:LOCALAPPDATA 'Win10WebmCut\Logs\app.log'
Get-Content -LiteralPath $log -Tail 150
~~~

Optional console capture when running from sources:

~~~powershell
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release 2>&1 | Tee-Object -FilePath .\crash-console.log
~~~

## Symptom guide

| Symptom | What to check |
|---|---|
| Tools not found | tools beside the EXE or absolute PATH directories, not source repo/tools |
| Encoder/muxer unavailable | ffmpeg -encoders / -muxers and the installed FFmpeg build |
| HDR preview not available | Preview deliberately lacks HDR tone mapping; Copy may be available |
| Cannot open input | Source recording must be finished; ffprobe must read metadata |
| Invalid crop | Even X/Y/W/H, minimum 2 × 2, within picture, changes applied |
| Export disabled | Unapplied crop, incompatible profile, missing tools, or busy operation |
| PTS/decode error | Unsupported or inconsistent stream encountered by decoder |
| Timeout/cleanup lock | Check log/PID; close the app instance instead of retrying repeatedly |
| Cannot write destination | Permissions, free space, another process, or filesystem restrictions |

## Report an issue

Use [GitHub Issues](https://github.com/KeyffMS/win10-webm-cut/issues). Include Windows version/architecture, app and .NET runtime versions, FFmpeg/FFprobe versions, chosen profile, reproducible steps, expected result and actual result. Only attach **reviewed and redacted** log excerpts.

**Kopiuj bez ścieżek** (Copy without paths) attempts to redact recognized filesystem paths, but **does not guarantee anonymization**. Ordinary copying preserves the text verbatim. Never publish private video, dumps, secrets, personal paths or unreviewed logs. See [Privacy](../../PRIVACY.md#english) and [FAQ](FAQ.md).
