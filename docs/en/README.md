# Win10 WebM Cut — Documentation

**English** · [Polski](../README.md) · [Repository](../../README.md)

Win10 WebM Cut is a local video editor for Windows 10/11 x64 written in C# and WPF (.NET 10). It supports start/end trimming, rectangular cropping, and five export profiles. **The application UI currently uses Polish labels**; the English documentation explains their meaning.

## Documentation index

| Topic | Document |
|---|---|
| Supported Windows, .NET, FFmpeg and hardware considerations | [Requirements](REQUIREMENTS.md) |
| Clone, build, run and publish | [Build and installation](BUILD.md) |
| Preview, shortcuts, crop and export | [User guide](USER-GUIDE.md) |
| Formats, streams, dimensions and data-loss limitations | [Export contract](EXPORT-CONTRACT.md) |
| Frequently asked questions | [FAQ](FAQ.md) |
| Local logs and issue reporting | [Diagnostics](DIAGNOSTICS.md) |
| Modules, states and process lifecycles | [Architecture](ARCHITECTURE.md) |
| Local distribution and legal documents | [Packaging](PACKAGING.md) |
| Local data handling | [Privacy](../../PRIVACY.md#english) |
| License and external components | [Third-party notices](../../THIRD-PARTY-NOTICES.md#english) |

## First steps

Install the .NET 10 SDK and trusted FFmpeg/FFprobe tools as described in [Requirements](REQUIREMENTS.md), then build the project using [Build and installation](BUILD.md). In the application, select **Otwórz wideo…** (Open video), set the time range or crop, select an export profile and click **Eksportuj** (Export).
