# Lokalna publikacja i pakowanie

[Spis dokumentacji](README.md) · [English](en/PACKAGING.md)

Dokument dla osób przygotowujących własne dystrybucje aplikacji. Repozytorium udostępnia skrypt i listę wymaganych składników, ale **nie** automatyczny instalator ani usługę budującą w chmurze.

## Warianty

**Framework-dependent:** docelowy komputer wymaga .NET 10 Desktop Runtime oraz zewnętrznych FFmpeg/FFprobe.

**Self-contained win-x64:** runtime .NET znajduje się w publikacji, ale ffmpeg.exe i ffprobe.exe trzeba zainstalować osobno lub jawnie dołączyć do tools przy EXE. Single-file nie oznacza, że dokumenty i legal notices są wbudowane w jeden EXE.

## Publikacja

~~~powershell
dotnet publish .\src\Win10WebmCut\Win10WebmCut.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\win-x64
~~~

Pliki dokumentacji są kopiowane obok EXE (ExcludeFromSingleFile=true). Minimalny zestaw jest zdefiniowany w [package-manifest.json](package-manifest.json): README, dokumentacja w obu językach, LICENSE, PRIVACY, THIRD-PARTY-NOTICES i pliki polityki paczki.

## Dokumenty licencyjne

Skrypt przygotowania paczki wymaga JSON z rzeczywistym pochodzeniem runtime (version, origin, variant) i ścieżkami do czterech dokumentów:

- DOTNET-LICENSE.txt
- DOTNET-THIRD-PARTY-NOTICES.txt
- WINDOWSDESKTOP-LICENSE.txt
- WINDOWSDESKTOP-THIRD-PARTY-NOTICES.txt

Dla opcjonalnych plików FFmpeg/FFprobe potrzebny jest osobny JSON zawierający version, origin, variant, configuration oraz pliki LICENSE.txt, NOTICES.txt i SOURCE-COMPLIANCE.txt. Pochodzenie i komplet dokumentów muszą odpowiadać **konkretnym buildom**, nie ogólnym wzorcom licencji. Skrypt nie ocenia zgodności prawnej.

## Złożenie paczki

Po publikacji i przygotowaniu lokalnego runtime-provenance.local.json:

~~~powershell
.\scripts\Prepare-Package.ps1 -PublishDirectory .\artifacts\win-x64 -Destination .\artifacts\package-win-x64 -RuntimeProvenance .\runtime-provenance.local.json -SourceRevision '<pelny-40-znakowy-SHA>'
~~~

Aby dołączyć własny FFmpeg, podaj także **-ToolsDirectory** i **-FfmpegProvenance**. Folder docelowy nie może już istnieć ani znajdować się wewnątrz folderu publikacji.

Skrypt używa allowlisty plików, odrzuca nieznane składniki i reparse points, liczy SHA256 i zapisuje PACKAGE-CONTENTS.json. Nie uruchamia ani nie pobiera binariów; nie modyfikuje wcześniejszej paczki. Po niepowodzeniu sprząta tylko swój nowy folder roboczy.

Nie pakuj .git, logów, dumpów, prywatnych wideo, sekretów, plików PDB ani przypadkowej zawartości katalogu użytkownika. Upewnij się, że wersje, notices i ścieżki dokumentacji w paczce odpowiadają faktycznym binariom. Przed dystrybucją przetestuj uruchomienie i eksport na docelowym komputerze oraz przejrzyj zobowiązania licencyjne. Manifest hashy nie jest certyfikatem zgodności ani testem aplikacji.

Szczegóły licencji: [THIRD-PARTY-NOTICES](../THIRD-PARTY-NOTICES.md).
