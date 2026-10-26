# Diagnostyka i zgłoszenia problemów

[Spis dokumentacji](README.md) · [English](en/DIAGNOSTICS.md)

## Okno diagnostyki

Przycisk **Diagnostyka** pokazuje wersję aplikacji, aktywny runtime .NET, system/architekturę procesu, rozpoznane lokalizacje ffmpeg.exe/ffprobe.exe, lokalizację dziennika i ostatni status. Numery wersji FFmpeg/FFprobe pojawiają się po odczycie ich możliwości przed eksportem; brak odczytu wcześniej nie oznacza błędu.

## Lokalny dziennik

~~~text
%LOCALAPPDATA%\Win10WebmCut\Logs\app.log
~~~

Log zawiera czas UTC, typ zdarzenia, identyfikator operacji i, w odpowiednich miejscach, PID narzędzia. Wyjątki mogą zawierać stack trace, nazwy plików oraz ograniczony stderr narzędzi. Limit rozmiaru aktywnego pliku to około **1 MiB** z trzema rotowanymi kopiami (app.log.1–.3). Lokalny log nie jest automatycznie wysyłany.

Odczyt ostatnich wpisów z PowerShell:

~~~powershell
$log = Join-Path $env:LOCALAPPDATA 'Win10WebmCut\Logs\app.log'
Get-Content -LiteralPath $log -Tail 150
~~~

Dodatkowe logowanie wyjścia uruchomienia:

~~~powershell
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release 2>&1 | Tee-Object -FilePath .\crash-console.log
~~~

## Objawy i działania

| Objaw | Sprawdzenie |
|---|---|
| Nie znaleziono narzędzi | tools przy EXE lub pełne katalogi PATH, nie automatycznie repo/tools |
| Brak encodera/muksera | ffmpeg -encoders i ffmpeg -muxers; build musi wspierać dany profil |
| Nie widać podglądu HDR | Jest wyłączony bez tone mappingu; sprawdź profil Copy |
| Błąd otwarcia | Czy zapis źródła jest zakończony i ffprobe odczytuje metadane |
| Crop jest odrzucany | Parzyste X/Y/W/H, minimum 2 × 2, poprawne granice, zastosowane zmiany |
| Eksport niedostępny | Nieukończony crop, wybrany profil, brak narzędzi, trwająca operacja |
| Błąd PTS/dekodowania | Nieobsługiwany lub niespójny strumień w źródle |
| Timeout albo blokada cleanup | Sprawdź log/PID; zamknij instancję zamiast uruchamiać kolejne zadania |
| Nie można zapisać wyniku | Uprawnienia, brak miejsca, blokada przez inny proces lub właściwości dysku/udziału |

## Zgłaszanie problemów

[GitHub Issues](https://github.com/KeyffMS/win10-webm-cut/issues): podaj Windows i architekturę, wersję aplikacji/.NET, wersje FFmpeg/FFprobe, profil, kroki odtworzenia, oczekiwane zachowanie i rzeczywisty rezultat. Jeśli potrzebny log, wyślij **tylko sprawdzony, zredagowany fragment**.

„Kopiuj bez ścieżek” usuwa wybrane wzorce ścieżek, ale **nie gwarantuje anonimizacji**. Zwykłe kopiowanie tekstu zachowuje go dosłownie. Nie publikuj filmów, dumpów, prywatnych ścieżek ani sekretów. Zobacz [Prywatność](../PRIVACY.md) i [FAQ](FAQ.md).
