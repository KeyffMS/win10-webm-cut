# Wymagania systemowe

[Spis dokumentacji](README.md) · [English](en/REQUIREMENTS.md)

| Składnik | Wymaganie |
|---|---|
| System | **Windows 10/11 x64** jako platforma docelowa WPF; ręcznie sprawdzono Windows 10 Pro 22H2 x64 |
| Budowanie | **.NET 10 SDK** z narzędziami WPF/Windows Desktop |
| Uruchomienie wydania zależnego od .NET | **.NET 10 Desktop Runtime x64**; zwykły runtime bez Windows Desktop jest niewystarczający |
| Uruchomienie self-contained win-x64 | Runtime .NET jest dołączony; nadal wymagane są FFmpeg i FFprobe |
| Multimedia | Zaufane pliki **ffmpeg.exe** i **ffprobe.exe** |
| Inne | Brak wymagań Electron, Node.js, npm, konta lub dodatkowych pakietów NuGet |

Program działa na plikach lokalnych i nie ma funkcji automatycznego pobierania narzędzi. Nie ustalono wymagań minimalnych CPU, RAM, GPU lub wolnego miejsca na dysku w oparciu o benchmarki. Wymagana ilość przestrzeni zależy od długości/formatu wideo i pliku roboczego eksportu. Nie deklaruje się zgodności z Linux, macOS czy Windows ARM; nie przetestowano wszystkich wersji Windows 11.

## Zestaw narzędzi FFmpeg

Wymagane są nie tyle określone wersje, ile funkcje faktycznie dostępne w wybranym buildzie:

- Podgląd: dekoder źródła, skala/obrót, select, showinfo oraz rawvideo BGRA.
- Copy: zgodny mukser docelowego kontenera; bez enkodera audio/wideo.
- FFV1/MKV: enkoder **ffv1**, mukser **matroska** oraz akceptacja źródłowego formatu pikseli. Audio jest kopiowane.
- WebM VP9: enkoder **libvpx-vp9**, mukser **webm** oraz **libopus**, jeśli film zawiera audio.
- FFprobe: odczyt metadanych, strumieni, czasu i geometrii.

Nie określono minimalnej certyfikowanej wersji FFmpeg — różne kompilacje tej samej wersji mają różne kodeki. Polecenia kontrolne w PowerShell:

~~~powershell
ffmpeg -version
ffprobe -version
ffmpeg -hide_banner -encoders
ffmpeg -hide_banner -muxers
~~~

## Lokalizacja ffmpeg.exe i ffprobe.exe

Program szuka najpierw w katalogu **tools przy uruchomionym EXE**, potem w pierwszym bezwzględnym katalogu PATH z danym plikiem. Katalog tools w głównym katalogu źródeł **nie** jest kopiowany automatycznie. Względny PATH, alias powłoki i bieżący katalog nie wystarczą. Lokalizacja narzędzi opisana jest w [instrukcji budowania](BUILD.md).

## Ograniczenia wejścia/wyjścia

Nie każdy kodek/strumień może być eksportowany przez każdy profil. Copy ma konserwatywną listę zgodnych połączeń kontenera i strumieni; profile kodujące obsługują ograniczone formaty SDR. Crop wymaga prawidłowej geometrii, a HDR i nieobsługiwany obrót mogą być dostępne wyłącznie przez Copy. Pełne wymagania mediów: [Kontrakt eksportu](EXPORT-CONTRACT.md).

Dostęp do źródła i prawa zapisu w katalogu docelowym są konieczne. Wynik jest tworzony w pliku roboczym w katalogu celu. Program nie zastępuje kopii zapasowej ani nie gwarantuje odporności na awarie sprzętu, zasilania czy nietypowe udziały sieciowe.
