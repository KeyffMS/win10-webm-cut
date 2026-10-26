# Obsługa edytora

Wymagania i odnajdywanie narzędzi są opisane tylko w [BUILD](BUILD.md).
Kontrakt profili i ograniczenia mediów: [EXPORT-CONTRACT](EXPORT-CONTRACT.md).
Ta instrukcja opisuje kod gałęzi naprawczej; nie jest zapisem testu GUI na Windows.

## Otwieranie i podgląd

Otwórz zakończony, lokalny plik. Nowszy wybór zastępuje starszy; starszy odczyt
metadanych nie może przywrócić poprzedniego pliku. Zmiana pliku zeruje zakres,
pozycję i crop. Niezastosowane pola cropu wymagają potwierdzenia ich odrzucenia.
Wybrany tryb i profil pozostają ustawieniami edytora, a ich zgodność jest
sprawdzana ponownie przy eksporcie.

Podgląd nie odtwarza dźwięku. Normalizuje obsługiwany obrót i SAR, następnie
zmniejsza obraz do obszaru okna z limitem 960×540 / 518 400 pikseli BGRA.
Odtwarzanie wybiera nie częściej niż około 12 klatek na sekundę osi źródła;
nie dopisuje sztucznych klatek, by wypełnić lukę VFR. Nie jest to limit pamięci
całego FFmpeg ani gwarancja szybkości dekodowania.

Zmiana rozmiaru okna rozciąga istniejący podgląd; nowy budżet jest używany przy
następnym żądaniu klatki lub odtwarzania. Nie ma nowego dekodera na każdy piksel
przesunięcia okna. Obsługiwane geometrie to opisane w kontrakcie kwadrantowe
obroty i SAR. Podgląd HDR jest wyłączony, zamiast pokazywać nieopisany tone mapping.

## Czas, klatki i klawiatura

`obraz:` oznacza PTS przyjętej, widocznej klatki. `Żądano:` oznacza cel, na który
czeka podgląd. Czas żądany nie udaje czasu już wyświetlonego obrazu. Suwak może
wskazywać cel, gdy etykieta obrazu nadal pokazuje ostatnią ukończoną klatkę.
Podczas szybkiego przewijania trwa najwyżej jedno dekodowanie i czeka jeden,
zastępowalny cel. Krótka operacja może skończyć się przed najnowszą; jej obraz
nie kasuje nowszego napisu „Żądano”. Zmiana pliku/zamknięcie unieważnia obie.

| Skrót | Działanie |
|---|---|
| ← / → | Żądanie przesunięcia o `1/FPS` sekundy, oznaczone jako ≈1 klatka |
| Shift + ← / → | `10/FPS` sekundy, ≈10 klatek |
| Ctrl + ← / → | 1 sekunda, także bez znanego FPS; Ctrl ma pierwszeństwo przed Shift |
| Esc w trakcie gestu | Anulowanie rysowania; przy suwaku powrót do pozycji sprzed gestu |

Nawigacja pauzuje odtwarzanie. Strzałki w polach tekstowych i listach wyboru
zachowują ich standardową funkcję; Alt/Windows nie uruchamiają nawigacji wideo.
Przy braku FPS używaj Ctrl lub suwaka. PTS jest odczytywany z dekodera, ale
**`czas×FPS` nie jest indeksem VFR**. Bieżąca klatka i łączna liczba są szacunkami
`≈`, numerowanymi od 0. Nawet dla deklarowanego stałego FPS żądanie seek może
wylądować na sąsiedniej rzeczywistej klatce. Ta wersja nie buduje pełnego indeksu.

## Przycinanie początku i końca

Tryb **Przycinanie czasu** eksportuje bez cropu, nawet jeśli wcześniejszy kadr
jest zapamiętany; etykieta to sygnalizuje. Wpisz sekundy z kropką lub przecinkiem
albo użyj znaczników. „Początek tutaj” bierze czas widocznego obrazu, a „Koniec
przed tą klatką” ustawia granicę wyłączną. Gdy nowy seek czeka na klatkę, znaczniki
są wyłączone, aby nie oznaczyć innej pozycji niż żądana.

Zakres to **`[początek, koniec)`**: początek należy do zakresu, koniec nie. Wpisanie
całkowitego czasu trwania jako końca pozwala zachować nagranie do jego końca;
EOF nie jest istniejącą klatką. Przy polu końca jest przybliżony numer granicy,
nie numer ostatniej wyświetlanej klatki. Liczba może wynosić N dla N klatek,
ponieważ jest to granica poza ostatnim indeksem N−1.

Copy jest cięciem strumieni, a nie obietnicą dokładności wybranego czasu/klatki.
Komunikat przed eksportem przypomina o keyframes. Podgląd nie zmienia tej
własności formatu źródłowego.

## Crop obrazu

Wybierz **Crop obrazu**. Pojawia się ramka pełnego obrazu, uchwyty i pola X/Y/W/H.
Narysuj prostokąt lewym przyciskiem albo przeciągnij narożnik. Kliknięcie bez
obszaru nie zastępuje istniejącego cropu. Punkty w czarnych pasach nie zaczynają
nowego kadru. Utrata przechwycenia myszy, Esc, zmiana pliku lub rozmiaru podglądu
anulują nieukończony gest; zachowany zostaje poprzedni zatwierdzony kadr.

Jednostki X/Y/W/H to piksele **po obsługiwanym obrocie, przed rozciągnięciem SAR**.
Nie są to piksele ekranu ani DPI. X/Y mogą wynosić zero. Rzeczywisty crop wymaga
parzystych wartości i W/H co najmniej 2, wewnątrz obrazu. Rysowanie zaokrągla
brzegi w dół do parzystych pikseli. Pełny obraz jest specjalnym stanem bez filtra
crop, również przy nieparzystych wymiarach.

Zmiana tekstu pól tworzy **draft**. Ramka nadal pokazuje zatwierdzony kadr.
„Zastosuj” waliduje draft; błąd pozostawia poprzedni kadr bez zmian. „Odrzuć zmiany”
przywraca pola. Zmiana trybu i eksport są blokowane, gdy draft jest niezastosowany.

**Cały obraz** jawnie usuwa crop i draft. **Obszar środkowy** wybiera oddzielny
preset zbliżony do 75% obu wymiarów, zaokrąglony do parzystości. To nie jest reset.
Przy bardzo małym obrazie preset pozostawia pełny obraz. Samo przełączenie trybu
nie przycina środka automatycznie. Przy ustawieniu rzeczywistego cropu profil
Copy zostaje zamieniony na FFV1; możesz świadomie wybrać WebM.

Zakres czasu nadal obowiązuje w trybie crop. Przełączając z powrotem na trim,
zachowujesz zatwierdzony kadr w pamięci, ale nie stosujesz go do eksportu.

## Eksport, nadpisywanie i zamknięcie

Eksport najpierw tworzy niezmienny opis źródła, czasu, zatwierdzonego kadru i profilu.
Przed wyborem pliku pokazuje kontrakt strumieni/jakości. Istniejący wynik wymaga
oddzielnej zgody. Podgląd jest zatrzymywany, a edycja/otwieranie wyłączone na czas
eksportu; nie konkurują wtedy dwa dekodery z enkoderem.

Eksport zapisuje unikalny `.webmcut-….working.ext` **w katalogu docelowym**. Po
powodzeniu FFmpeg sprawdzane są podstawowe metadane/wymiary wyniku i rewizja
źródła. Dopiero następnie następuje Move/Replace. Błąd/anulowanie sprząta wyłącznie
własny plik roboczy. Plik wyjściowy, który zmienił się podczas operacji, nie jest
świadomie zastępowany. Aliasy wskazujące to samo wejście są odrzucane.

„Anuluj” wygrywa przed ostatnią kontrolą tokenu na granicy zatwierdzenia. Po
rozpoczęciu skutecznego Replace/Move wynik jest sukcesem, nawet gdy Cancel nadejdzie
równocześnie. Nie istnieje bezpieczne cofnięcie już zakończonej zamiany przez
skasowanie celu. Zamknięcie okna próbuje zakończyć właścicieli operacji asynchronicznie.

Przy odmowie/awarii systemu plików, utracie zasilania albo wymuszeniu zakończenia
procesu mogą pozostać pliki robocze. Program nie jest kopią zapasową ani ochroną
przed równoczesnym złośliwym zapisem. Reguły i ograniczenia: [EXPORT-CONTRACT](EXPORT-CONTRACT.md).

## Komunikaty

Własne komunikaty są ciemne; tekst można zaznaczyć, a „Kopiuj bez ścieżek” wykonuje
pomocniczą redakcję. Enter zatwierdza, Esc odrzuca/zamyka. Systemowe dialogi
otwierania/zapisu zachowują wygląd Windows. Błędy i wersje: [DIAGNOSTICS](DIAGNOSTICS.md).
Nie publikuj surowych ścieżek i logów bez przeglądu [PRIVACY](../PRIVACY.md).
