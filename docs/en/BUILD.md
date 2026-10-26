# Installation, build and run

[Documentation](README.md) · [Polski](../BUILD.md)

See [Requirements](REQUIREMENTS.md) before proceeding. Run the following commands in Windows PowerShell.

## Source and SDK

~~~powershell
git clone https://github.com/KeyffMS/win10-webm-cut.git
cd win10-webm-cut
dotnet --list-sdks
dotnet --version
~~~

The global.json SDK policy selects stable .NET **10.0** feature bands, starting at 10.0.100, with latestFeature roll-forward. Other installed SDK versions do not need to be removed. Visual Studio is optional when using the command-line build.

## FFmpeg and FFprobe

Install a trusted FFmpeg build including ffmpeg.exe, ffprobe.exe and the filters/codecs needed for your files. Supported tool locations:

1. Absolute directories listed in PATH.
2. A **tools folder next to the running Win10WebmCut.exe**.

Example for a Release build:

~~~text
src/Win10WebmCut/bin/Release/net10.0-windows/
  Win10WebmCut.exe
  tools/
    ffmpeg.exe
    ffprobe.exe
~~~

**The repository's tools folder is not copied to build/publish output automatically.** Changing the working directory does not change tool discovery. Restart the application after replacing tool binaries.

## Build

~~~powershell
dotnet restore .\src\Win10WebmCut\Win10WebmCut.csproj
dotnet build .\src\Win10WebmCut\Win10WebmCut.csproj -c Release
~~~

Build output is stored under src/Win10WebmCut/bin/Release/net10.0-windows/. Run the existing compiled app:

~~~powershell
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release --no-build
~~~

## Self-contained win-x64 publishing

~~~powershell
dotnet publish .\src\Win10WebmCut\Win10WebmCut.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\win-x64
~~~

Self-contained publishing includes the .NET runtime; **FFmpeg and FFprobe are not bundled automatically**. Documentation files remain next to the EXE (ExcludeFromSingleFile=true); native components may be extracted by the runtime at startup. Trimming is disabled.

For a distributable package and component-license provenance, follow [Packaging](PACKAGING.md).

## Troubleshooting

- No suitable SDK: inspect dotnet --list-sdks and global.json.
- Missing Desktop Runtime: install matching .NET Desktop Runtime x64 or publish self-contained.
- Missing FFmpeg/FFprobe: verify tools next to the EXE or absolute PATH.
- Missing encoder: check ffmpeg -encoders; the app does not silently change the chosen profile.
- Problems with a file: see [Diagnostics](DIAGNOSTICS.md) and [Export contract](EXPORT-CONTRACT.md).

GitHub Actions are not required for local builds.
