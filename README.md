# Win10 WebM Cut

Minimalny edytor wideo dla Windows 10/11 (x64), C# / WPF / .NET 10. Projekt w stylu DictaMute i SightAdapt: bez Electrona, kont i telemetrii.

## Funkcje
- Przycinanie początku i końca nagrania (szybki eksport `-c copy`; granice cięcia zależą od keyframes).
- Crop obrazu: wybór obszaru myszą na podglądzie.
- Eksport: **Copy (bez rekompresji)**, **FFV1/MKV (bezstratny crop)**, **WebM VP9/Opus 1:1, 1/2 i 1/4 wymiarów**.
- Crop i trim można łączyć przy eksporcie z rekompresją.
- Bez dodatkowych zależności NuGet; osobne binaria `ffmpeg.exe` i `ffprobe.exe`.

> **Uwaga:** Crop zawsze wymaga ponownego kodowania. FFV1 zachowuje bezstratnie obraz po przycięciu, ale nie kopiuje oryginalnego skompresowanego strumienia. `-c copy` jest dostępne tylko bez cropu.

## Uruchomienie

1. Zainstaluj .NET 10 SDK na Windows i pobierz binaria FFmpeg dla Windows.
2. Umieść `ffmpeg.exe` i `ffprobe.exe` w folderze `tools` obok pliku EXE **albo** dodaj je do `PATH`.
3. Z katalogu repozytorium:

```powershell
dotnet run --project src/Win10WebmCut/Win10WebmCut.csproj
```

Publikacja samodzielnego EXE:

```powershell
dotnet publish src/Win10WebmCut/Win10WebmCut.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

FFmpeg nie jest dołączany automatycznie; jeśli dystrybuujesz binaria FFmpeg, dołącz ich licencje i wymagane informacje o pochodzeniu.

## Ograniczenia MVP
- Podgląd korzysta z lokalnego FFmpeg: dekoduje obraz do 960 px i maksymalnie 12 klatek/s bez instalowania kodeków Windows. Podgląd jest bez dźwięku; eksport zachowuje audio zgodnie z wybranym profilem.
- Crop jest wybierany na prostokątnym podglądzie; przy materiałach z nietypową orientacją/metadanymi wyświetlania rezultat może wymagać korekty.
- Nie ma obsługi wielu segmentów ani zaawansowanego timeline.
- Eksport VP9 używa `libvpx-vp9` i `libopus`; wymagany build FFmpeg z tymi enkoderami.
- Rozmiar wyjściowego WebM nie jest gwarantowany; opcje 1/2 i 1/4 oznaczają redukcję wymiarów, a nie rozmiaru pliku.

## Architektura
WPF jest tylko GUI. `FfmpegService` uruchamia lokalne narzędzia FFmpeg/FFprobe przez `ProcessStartInfo.ArgumentList`, bez powłoki. Log wyjściowy FFmpeg przekazywany jest do okna. Zatrzymanie eksportu anuluje proces.

## Licencja
MIT — zobacz `LICENSE`.