# Dane lokalne i prywatność

Aplikacja otwiera wskazane lokalne pliki, odczytuje ich metadane i uruchamia lokalne
FFmpeg/FFprobe. Nie ma kont, telemetrii, analityki, zdalnego crash-reportingu,
automatycznej aktualizacji ani funkcji przesyłania filmu. Kod narzędzi zewnętrznych
pochodzi z instalacji użytkownika; nie należy uruchamiać nieznanych binariów.

Lokalne logi, ścieżki i komunikaty mogą zawierać nazwy konta, folderów, plików,
metadane materiału i fragmenty stderr. Format/retencja/lokalizacja należą do
[DIAGNOSTICS](docs/DIAGNOSTICS.md). Pliki robocze wyniku są tworzone w wybranym
katalogu docelowym, nie wysyłane nigdzie. Po awarii mogą wymagać ręcznej oceny
przed usunięciem. Nie kasuj źródła ani poprawnego wyniku jako „cleanup”.

Brak telemetrii nie oznacza automatycznej anonimowości ręcznie publikowanego logu.
Przycisk kopiowania redaguje rozpoznane ścieżki pomocniczo. Przed publikacją
sprawdź całość; usuń nazwy, dane osobowe, sekrety i treść, której nie chcesz ujawnić.
Zaznaczony tekst i ręczne Ctrl+C nie stosują redakcji. Nie publikuj dumpów/filmów
bez świadomej zgody osób, których dane lub wizerunek zawierają.

`.gitignore` ogranicza przypadkowe dodawanie logów, dumpów, mediów i binariów.
Nie chroni plików już śledzonych ani wymuszonego `git add -f`. Procedura pakowania
korzysta z jawnej listy plików, a nie archiwizowania całego katalogu użytkownika.
Nie ma automatycznego kasowania lub przesyłania logów w związku ze zgłoszeniem błędu.
