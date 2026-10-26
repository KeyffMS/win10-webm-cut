# Instalacja, budowanie i uruchomienie

[Spis dokumentacji](README.md) · [English](en/BUILD.md)

**System i zależności:** [Wymagania](REQUIREMENTS.md). Polecenia dotyczą PowerShell na Windows.

## Źródła i SDK

~~~powershell
git clone https://github.com/KeyffMS/win10-webm-cut.git
cd win10-webm-cut
dotnet --list-sdks
dotnet --version
~~~

Plik global.json wybiera stabilne SDK z linii **10.0** od 10.0.100 z polityką latestFeature. Nie trzeba odinstalowywać innych wersji .NET. Visual Studio nie jest wymagane przy użyciu CLI.

## Instalowanie FFmpeg i FFprobe

Zainstaluj zaufany build zawierający ffmpeg.exe, ffprobe.exe i wymagane filtry/kodeki. Dwie obsługiwane lokalizacje to:

1. Absolutny katalog w PATH.
2. Folder **tools bezpośrednio obok uruchomionego EXE**.

Przykład po kompilacji Release:

~~~text
src/Win10WebmCut/bin/Release/net10.0-windows/
  Win10WebmCut.exe
  tools/
    ffmpeg.exe
    ffprobe.exe
~~~

**Repozytorium/tools nie jest kopiowane automatycznie.** Zmiana folderu roboczego nie zmienia sposobu wyszukiwania narzędzi. Po podmianie binariów zaleca się restart aplikacji.

## Kompilacja

~~~powershell
dotnet restore .\src\Win10WebmCut\Win10WebmCut.csproj
dotnet build .\src\Win10WebmCut\Win10WebmCut.csproj -c Release
~~~

Pliki kompilacji znajdują się w src/Win10WebmCut/bin/Release/net10.0-windows/. Aby uruchomić już zbudowany program:

~~~powershell
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release --no-build
~~~

## Publikacja self-contained win-x64

~~~powershell
dotnet publish .\src\Win10WebmCut\Win10WebmCut.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\win-x64
~~~

W self-contained runtime .NET jest w paczce, ale **FFmpeg i FFprobe nadal nie są dołączane automatycznie**. Dokumentacja jest kopiowana jako odrębne pliki (ExcludeFromSingleFile=true). Single-file może rozpakowywać wewnętrzne biblioteki natywne. Nie włączono PublishTrimmed.

## Problemy

- Brak SDK: sprawdź dotnet --list-sdks i global.json.
- Brak Windows Desktop Runtime: zainstaluj wariant Desktop x64 lub opublikuj self-contained.
- Brak FFmpeg/FFprobe: sprawdź tools obok EXE oraz bezwzględny PATH.
- Brak enkodera: zweryfikuj ffmpeg -encoders; profile nie przełączają jakości automatycznie.
- Problem z konkretnym plikiem: zobacz [Diagnostykę](DIAGNOSTICS.md) i [Kontrakt eksportu](EXPORT-CONTRACT.md).

Proces budowania nie wymaga GitHub Actions.
