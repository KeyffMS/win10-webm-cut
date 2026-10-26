# Diagnostyka lokalna

Przycisk **Diagnostyka** pokazuje wersję aplikacji z assembly, uruchomiony runtime,
system i architekturę procesu, zapamiętane ścieżki narzędzi oraz lokalizację logu.
Wersje FFmpeg/FFprobe są odczytywane przy preflight eksportu; przed nim widnieje
„jeszcze nie odczytano”, a nie zgadywana wersja. SDK nie jest odpytywany przez EXE.

## Log

Domyślna lokalizacja:

```text
%LOCALAPPDATA%\Win10WebmCut\Logs\app.log
```

`AppLog` zapisuje UTC, rodzaj zdarzenia, identyfikator operacji i PID narzędzia.
Wyjątki są zapisywane z ToString (stack trace), do limitu wpisu 65 536 znaków.
Plik ma limit polityki 1 MiB z trzema rotowanymi kopiami `app.log.1`–`.3`.
Limity dotyczą zapisów aplikacji, nie równoczesnej modyfikacji przez inny proces.
Błąd zapisu logu nie wywołuje rekurencyjnego logowania ani drugiej awarii; informacja
o niedostępności pojawia się w diagnostyce. Stderr narzędzia jest ograniczony,
nie jest bezlimitowym trwałym zapisem każdej klatki.

Przykłady dla użytkownika Windows, niewykonywane przez audyt/model:

```powershell
$log = Join-Path $env:LOCALAPPDATA 'Win10WebmCut\Logs\app.log'
Get-Content -LiteralPath $log -Tail 150
```

Log konsoli pozostaje możliwością pomocniczą:

```powershell
dotnet run --project .\src\Win10WebmCut\Win10WebmCut.csproj -c Release 2>&1 |
    Tee-Object -FilePath .\crash-console.log
```

Kod nie maskuje wszystkich wyjątków przez `Handled=true`. Nieobsłużona awaria
jest zapisywana i pozostaje awarią, zamiast wymuszać kontynuację w niespójnym stanie.

## Rozróżnianie problemów

| Informacja | Znaczenie i działanie |
|---|---|
| Brak narzędzia | Sprawdź wybraną ścieżkę według BUILD; sama obecność w repo/tools nie wystarcza |
| Brak enkodera/muksera/formatu FFV1 | Wybrany build nie deklaruje wymaganej możliwości; bez automatycznej zmiany jakości |
| Błąd probe / zmiana rewizji | Zakończ nagrywanie/modyfikowanie źródła i otwórz je ponownie |
| Błąd PTS / zero klatek | Dane podglądu niespójne lub format nieobsługiwany; nie jest to potwierdzenie uszkodzenia każdego pliku |
| Timeout | Upłynął nazwany limit danej operacji, nie dowód błędu systemu |
| Cleanup zablokował operacje | Nie potwierdzono końca narzędzia; sprawdź PID w logu i zakończ instancję aplikacji. Nie uruchamiaj pętli kolejnych eksportów |
| Odmowa cropu/profilu | Sprawdź ograniczenia geometryczne i kontrakt, nie obchodź walidacji przez ręczne zmiany argumentów |

## Zgłoszenie problemu

Opisz czynność, czy błąd jest powtarzalny, wybrany profil oraz zakres. Dołącz wersje
z diagnostyki i **zredagowany** fragment logu dla identyfikatora operacji. Nie
potrzeba automatycznie całego filmu, pełnej ścieżki użytkownika ani zrzutu pamięci.
Nie wysyłaj sekretów, mediów ani prywatnych logów do publicznego issue.

„Kopiuj bez ścieżek” usuwa rozpoznane ścieżki drive/UNC, ale jest pomocą, nie
certyfikowaną anonimizacją. Nazwy plików bez ścieżek i inne dane mogą pozostać.
Ręczne kopiowanie zaznaczonego tekstu kopiuje tekst dosłownie. Zawsze przeczytaj
wynik przed wysłaniem. Szczegóły: [PRIVACY](../PRIVACY.md).

## Wersje i historia

Źródłem wersji aplikacji jest `.csproj`, a opisem zmian [CHANGELOG](../CHANGELOG.md).
Polityka SDK/narzędzi należy do [BUILD](BUILD.md), pochodzenie dystrybuowanych
binarnych składników do [PACKAGING](PACKAGING.md). Logowanie nie kontaktuje się
z usługami chmurowymi i nie sprawdza samoczynnie aktualizacji.
