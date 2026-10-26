# Win10 WebM Cut

**[Polski](#polski) · [English](#english)**

A lightweight **local video trimmer and rectangular crop editor** for **Windows 10/11 x64**, built with **C# / .NET 10 / WPF**. No Electron, account or telemetry. FFmpeg and FFprobe are external tools.

## Polski

### Co robi aplikacja?

- **Przycinanie czasu:** wybierz początek i koniec w sekundach; zakres ma postać **[początek, koniec)**.
- **Prostokątny crop:** zaznacz obraz myszą lub wpisz X/Y/W/H, następnie zatwierdź. Dostępne są akcje „Cały obraz” i „Obszar środkowy”.
- **Podgląd klatek:** FFmpeg, przewijanie suwakiem i klawiaturą; podgląd **bez dźwięku**. Szacunkowe numery klatek są oznaczone ≈.
- **Pięć profili eksportu:** Copy bez rekompresji; obraz FFV1/MKV bezstratny z audio copy; stratny WebM VP9/Opus 1:1, 1/2 i 1/4 szerokości oraz wysokości.
- **Lokalny zapis:** przed zastąpieniem pliku docelowego tworzy plik roboczy i sprawdza podstawowe metadane wyniku. Zachowuj kopie ważnych materiałów.

**Ograniczenia:** Copy nie gwarantuje dokładności cięcia co do klatki; kodowanie może pomijać dodatkowe strumienie/napisy/metadane. HDR nie ma podglądu z tone mappingiem. Program nie zawiera FFmpeg/FFprobe. Interfejs jest obecnie **po polsku**.

### Wymagania

Windows 10/11 x64, **.NET 10 SDK** do kompilacji albo **.NET 10 Desktop Runtime** do uruchomienia wersji framework-dependent, oraz zaufane **ffmpeg.exe i ffprobe.exe**. Self-contained zawiera runtime .NET, ale nie dołącza narzędzi FFmpeg. Narzędzia muszą znajdować się w **tools obok uruchomionego EXE** lub w bezwzględnym katalogu **PATH**. Nie określono minimalnej kwalifikowanej wersji FFmpeg ani benchmarkowych wymagań RAM/CPU.

### Uruchomienie ze źródeł

~~~powershell
git clone https://github.com/KeyffMS/win10-webm-cut.git
cd win10-webm-cut
dotnet build .\src\Win10WebmCut\Win10WebmCut.csproj -c Release
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release --no-build
~~~

### Dokumentacja po polsku

[**Spis treści**](docs/README.md) · [Wymagania](docs/REQUIREMENTS.md) · [Instalacja](docs/BUILD.md) · [Obsługa](docs/USER-GUIDE.md) · [Profile eksportu](docs/EXPORT-CONTRACT.md) · [FAQ](docs/FAQ.md) · [Diagnostyka](docs/DIAGNOSTICS.md) · [Architektura](docs/ARCHITECTURE.md) · [Pakowanie](docs/PACKAGING.md)

## English

### Features

- **Trim:** define start/end times in seconds with a half-open **[start, end)** range.
- **Rectangular crop:** drag the picture or enter X/Y/W/H, then apply. Full-image reset and a separate central-area preset.
- **Video preview:** FFmpeg-backed silent preview; seek using a slider or keyboard. Frame numbers marked ≈ are FPS-based estimates.
- **Five export profiles:** stream-copy with no re-encoding; lossless FFV1 image/MKV with audio copy; lossy WebM VP9/Opus at 1:1, 1/2 or 1/4 of both image dimensions.
- **Local output:** prepare a working file and check basic metadata before committing to the destination. Keep backups of important media.

**Limitations:** Copy is not guaranteed frame-accurate; re-encoding may omit extra streams, subtitles or metadata. Preview does not tone-map HDR. FFmpeg/FFprobe are separate dependencies. The **application interface is in Polish**; full documentation is available in English.

### Requirements

Windows 10/11 x64, **.NET 10 SDK** to build or **.NET 10 Desktop Runtime** to run a framework-dependent build, and trusted **ffmpeg.exe + ffprobe.exe**. A self-contained build includes .NET, but **not** FFmpeg. Tools must be found in **tools next to the running EXE** or an absolute **PATH** directory. There is no qualified minimum FFmpeg version or benchmark-derived RAM/CPU minimum.

### Build from source

~~~powershell
git clone https://github.com/KeyffMS/win10-webm-cut.git
cd win10-webm-cut
dotnet build .\src\Win10WebmCut\Win10WebmCut.csproj -c Release
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release --no-build
~~~

### English documentation

[**Documentation index**](docs/en/README.md) · [Requirements](docs/en/REQUIREMENTS.md) · [Installation](docs/en/BUILD.md) · [User guide](docs/en/USER-GUIDE.md) · [Export formats](docs/en/EXPORT-CONTRACT.md) · [FAQ](docs/en/FAQ.md) · [Diagnostics](docs/en/DIAGNOSTICS.md) · [Architecture](docs/en/ARCHITECTURE.md) · [Packaging](docs/en/PACKAGING.md)

---

**Data and licenses / Dane i licencje:** [Privacy / Prywatność](PRIVACY.md) · [Third-party notices / Składniki zewnętrzne](THIRD-PARTY-NOTICES.md) · [MIT License](LICENSE). [Issues / Zgłoszenia](https://github.com/KeyffMS/win10-webm-cut/issues).

Source repository and build instructions do not imply an official downloadable installer. / Repozytorium źródeł i instrukcja budowania nie oznaczają dostępności oficjalnego instalatora.
