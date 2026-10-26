# Środowisko, uruchamianie i narzędzia

To jedyna kanoniczna instrukcja lokalizacji narzędzi. Kod: `ToolResolver.Resolve`.
Polecenia poniżej są instrukcją dla lokalnego komputera Windows; **nie były
uruchamiane w ramach implementacji/modelowania tej gałęzi**.

## SDK a uruchomienie

Do pracy ze źródłami potrzebny jest SDK .NET 10 z WPF. `global.json` wskazuje
`10.0.100`, `rollForward=latestFeature`, `allowPrerelease=false`: wybiera dostępny
stabilny feature band linii 10.0, nie przechodzi automatycznie do innej głównej
wersji. To polityka wyboru SDK, nie dowód zgodności wszystkich aktualizacji.
Nie trzeba usuwać innych SDK. Visual Studio, Node.js i npm nie są wymaganiami
projektu. Samo istnienie szablonu WPF w starszym SDK nie dowodzi dostępności SDK 10.

Z katalogu repozytorium:

```powershell
dotnet --list-sdks
dotnet --version
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release
```

Publikacja framework-dependent wymaga odpowiedniego .NET Desktop Runtime na
komputerze docelowym. Publikacja self-contained zawiera runtime; użytkownik
końcowy nie potrzebuje SDK. FFmpeg/FFprobe nie są częścią żadnego z tych wariantów.
Docelowy interfejs to Windows/WPF; nie deklaruje się przenośności na Linux ani
oficjalnego wsparcia każdego wydania Windows 10 przez Microsoft.

## FFmpeg i FFprobe: deterministyczne wyszukiwanie

Dla każdego z narzędzi kolejność jest taka sama:

1. `tools\ffmpeg.exe` / `tools\ffprobe.exe` względem **AppContext.BaseDirectory**,
   czyli katalogu uruchomionego EXE.
2. Plik o tej nazwie w pierwszym pasującym **bezwzględnym** katalogu `PATH`.

Puste i względne elementy PATH, aliasy powłoki i bieżący katalog nie są brane pod
uwagę. Nie ma automatycznego pobierania binariów ani wyszukiwania w całym dysku.
Narzędzia muszą pochodzić z zaufanego źródła; resolver nie weryfikuje ich podpisów.
`Diagnostyka` pokazuje rzeczywiście wybrane ścieżki, nie zgaduje lokalizacji.

Przykład dla `dotnet run -c Release` bez RID:

```text
repo/
  src/Win10WebmCut/bin/Release/net10.0-windows/
    Win10WebmCut.exe
    tools/ffmpeg.exe
    tools/ffprobe.exe
```

Przykład po publikacji:

```text
artifacts/win-x64/
  Win10WebmCut.exe
  tools/ffmpeg.exe       # opcjonalnie; alternatywą jest PATH
  tools/ffprobe.exe
```

**`repo/tools` nie jest automatycznie kopiowany do output/publish.** Uruchomienie
polecenia z katalogu repo nie zmienia `AppContext.BaseDirectory`. Zmiana CWD nie
może wybierać innego narzędzia przypadkiem. Wariant z binariami obok EXE jest
pakowany dopiero według [PACKAGING](PACKAGING.md).

## Możliwości i wersje

Podgląd wymaga filtrów normalizacji geometrii, `select`, `scale`, `format`,
`showinfo`, rawvideo/BGRA i opcji PTS użytych w `PreviewDecoder`. Eksport sprawdza
wersje FFmpeg/FFprobe, listę mukserów i enkoderów. FFV1 wymaga zadeklarowanego
przez enkoder źródłowego formatu pikseli. Copy nie wymaga enkodera wideo/audio;
WebM wymaga libvpx-vp9 i — tylko gdy istnieje audio — libopus. FFV1 kopiuje audio,
więc FLAC nie jest już wymaganiem aplikacji.

Preflight to kontrola zadeklarowanych możliwości, nie test jakości lub każdej
kombinacji kodek/kontener. Nie ustanowiono listy buildów FFmpeg zakwalifikowanych
przez testy tej gałęzi. Nieobsługiwany filtr/opcja powoduje jawny błąd narzędzia,
a nie cichą zmianę profilu. Cache możliwości jest związany ze ścieżką, rozmiarem
i czasem modyfikacji obu narzędzi. Po zmianie instalacji zaleca się restart.

Nie wymieniać źródła ani narzędzi w trakcie pracy. Zmiana rewizji źródła jest
odrzucana; nie jest to ochrona przed złośliwym procesem zmieniającym pliki między
kontrolą a użyciem.

## Publikacja lokalna — procedura, nie wykonana czynność

```powershell
dotnet publish .\src\Win10WebmCut\Win10WebmCut.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o .\artifacts\win-x64
```

Projekt nie włącza trimming. Natywne biblioteki mogą być wydobywane przez runtime
z single-file podczas uruchomienia. Dokumenty mają `ExcludeFromSingleFile=true`
i pozostają obok aplikacji. Nie udostępniać samego EXE bez pozostałych dokumentów.
Osobne kroki przygotowania/proweniencji paczki: [PACKAGING](PACKAGING.md).

## Polityka utrzymania

Nie aktualizować automatycznie SDK, FFmpeg ani biblioteki runtime użytkownika.
Zmiany wersji należy opisywać w CHANGELOG i informacjach o paczce. Wersję aplikacji
określa `.csproj`; diagnostyka odczytuje metadane assembly i uruchomiony runtime.
Stan „źródła przejrzane/modelowane” nie może zmienić się w „wersja wspierana na
Windows” bez odrębnych dowodów. W tej gałęzi nie ma Actions ani zdalnego builda.
