# Przygotowanie paczki — wyłącznie lokalnie

Nie wykonano publikacji ani przygotowania binarnej paczki tej gałęzi. Ten dokument,
`package-manifest.json`, reguły `.csproj` i skrypt definiują przyszłą procedurę,
a nie dowodzą zawartości istniejącego EXE. Nie używa się GitHub Actions, artefaktów
CI, usług płatnych, podpisywania lub automatycznego wydania.

## Warianty i lista plików

Kanoniczna lista dołączanych plików: [package-manifest.json](package-manifest.json).
Projekt jawnie kopiuje do output/publish LICENSE, README, PRIVACY, CHANGELOG,
THIRD-PARTY-NOTICES i bieżące instrukcje użytkownika/architektury/diagnostyki.
`ExcludeFromSingleFile=true` zostawia te dokumenty jako pliki obok EXE.
Historyczne audyty, roadmapa, modele i źródła nie należą do minimalnej paczki;
linki do nich w README odnoszą się do repozytorium.

Podstawowy wariant self-contained dostarcza EXE i materiały .NET/Windows Desktop
Runtime, a FFmpeg i FFprobe pozostają zewnętrzną instalacją użytkownika. Wariant
rozszerzony dodaje oba narzędzia do tools obok EXE i dokumenty konkretnego buildu.
Nie wolno jednocześnie twierdzić, że paczka nie zawiera FFmpeg, i dołączać jego
binarium bez deklaracji.

Procedura publikacji źródeł jest w [BUILD](BUILD.md). Użyj nowego, czystego katalogu
output i właściwej gałęzi. Włączony single-file nie oznacza pominięcia notices.
Skrypt przygotowania nie buduje aplikacji ani nie uruchamia binariów. Nieznany
plik output powoduje odmowę: należy ustalić, czy to wymagana zależność, zamiast
skasować ją lub pominąć. Pliki PDB nie trafiają do paczki użytkownika.

## Rzeczywiste pochodzenie, nie szablon udający dowód

Przed przygotowaniem paczki dostarcz lokalny JSON pochodzenia runtime, np.:

```json
{
  "version": "REPLACE_ME: rzeczywiste wersje NETCore i WindowsDesktop",
  "origin": "REPLACE_ME: źródło dokładnie użytego runtime/SDK",
  "variant": "REPLACE_ME: win-x64 self-contained i wariant runtime",
  "documents": {
    "DOTNET-LICENSE.txt": "REPLACE_ME: lokalny plik",
    "DOTNET-THIRD-PARTY-NOTICES.txt": "REPLACE_ME: lokalny plik",
    "WINDOWSDESKTOP-LICENSE.txt": "REPLACE_ME: lokalny plik",
    "WINDOWSDESKTOP-THIRD-PARTY-NOTICES.txt": "REPLACE_ME: lokalny plik"
  }
}
```

Dla FFmpeg drugi JSON ma `version`, `origin`, `variant`, `configuration` i obiekt
`documents` z `LICENSE.txt`, `NOTICES.txt`, `SOURCE-COMPLIANCE.txt`. Dokument ostatni
opisuje faktyczny sposób zapewnienia wymaganych źródeł/oferty dla konkretnego
buildu; nie może być pustym zdaniem „spełniono GPL”. Uwzględnij dostarczone biblioteki
i powiązane obowiązki. Skrypt weryfikuje obecność danych i dokumentów, **nie ich
prawną kompletność**. W razie potrzeby wariant bez narzędzi ogranicza zakres
redystrybucji, ale nie znosi obowiązków dla dołączonego runtime.

Wartości `REPLACE_ME` nie są akceptowane jako rzeczywiste dane. Nie publikuj JSON
z lokalnymi ścieżkami lub sekretami: skrypt publikuje wybrane informacje pochodzenia,
nie surowe ścieżki dokumentów. Źródłem wersji aplikacji jest metadata EXE; źródłowy
commit podaje opiekun i musi odpowiadać użytemu kodowi. Nie zgaduj numerów składników.

## Polecenia do wykonania dopiero po odrębnej publikacji

Z katalogu repozytorium, przykład wariantu bez narzędzi:

```powershell
.\scripts\Prepare-Package.ps1 `
  -PublishDirectory .\artifacts\win-x64 `
  -Destination .\artifacts\package-win-x64 `
  -RuntimeProvenance .\runtime-provenance.local.json `
  -SourceRevision '<pełne 40 znaków SHA źródeł użytych przy publikacji>'
```

Dla wariantu z FFmpeg dodaj `-ToolsDirectory` i `-FfmpegProvenance`. Folder docelowy
nie może istnieć ani znajdować się wewnątrz output publikacji. Skrypt rezerwuje
nowy folder roboczy, kopiuje wyłącznie dozwolone składniki, odrzuca reparse points,
liczy SHA256 i zapisuje `PACKAGE-CONTENTS.json`. Dopiero na końcu przenosi folder
pod żądaną nazwę. Błąd sprząta tylko własny folder roboczy; nie usuwa output
publikacji ani wcześniejszej paczki. Nie wykonuje żadnych połączeń sieciowych.

Manifest wynikowy ma listę ścieżek, rozmiarów i hashy, wersję produktu, commit
źródeł, rzeczywiste dane runtime i opcjonalnych narzędzi. Nie jest dowodem działania
binarium ani zastępczym SBOM całego transitive runtime. Przed udostępnieniem trzeba
przejrzeć dane i wymagane notices. Dopiero niezależna późniejsza weryfikacja może
potwierdzić uruchamianie/medialne wyniki; model drzewa pakowania tego nie robi.

## Dane, których nie pakujemy

Logi/dumpy, media, klucze, pliki środowiska, profile użytkownika, pliki robocze
wyników oraz symbole PDB nie wchodzą do jawnej listy. Nie archiwizuj całego repo,
katalogu Downloads ani folderu użytkownika jako „paczkę”. `.gitignore` jest
pomocniczy; nie zastępuje allowlisty. Prywatność i redakcja: [PRIVACY](../PRIVACY.md).
