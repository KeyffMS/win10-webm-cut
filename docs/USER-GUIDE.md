# Instrukcja użytkownika

[Spis dokumentacji](README.md) · [English](en/USER-GUIDE.md)

## Pierwszy eksport

1. Wybierz **Otwórz wideo…** i wskaż zakończony, lokalny plik.
2. Użyj przycisku odtwarzania lub suwaka do wyboru fragmentu.
3. Ustaw pola **Początek (s)** i **Koniec (s), wyłączny** albo przyciski znaczników.
4. Wybierz **1. Przycinanie czasu** lub **2. Crop obrazu**.
5. Wybierz profil i kliknij **Eksportuj**.
6. Przeczytaj komunikat o profilu i strumieniach, wybierz nowy plik wynikowy. Potwierdź nadpisanie tylko wtedy, gdy to zamierzone.

Otwarcie innego materiału resetuje zakres czasu i crop. Nie edytuj źródła w innym programie podczas eksportu. Program nie przetwarza wideo w chmurze.

## Podgląd i nawigacja

Podgląd obrazu używa FFmpeg i działa **bez dźwięku**. Nie jest wiernym odtwarzaczem wszystkich funkcji źródłowego kontenera. Budżet bitmapy podglądu to maksymalnie **960 × 540 i 518 400 pikseli BGRA**. Odtwarzanie próbuje prezentować około 12 klatek/sekundę; nie wpływa to na FPS pliku wyjściowego.

Etykieta **obraz** wskazuje PTS rzeczywiście pokazanej klatki. **Żądano** wskazuje nowy cel nawigacji, jeśli dekoder jeszcze nie zakończył pracy. Suwak może zatem wskazywać czas inny niż aktualnie pokazany obraz. Przewijanie pauzuje odtwarzanie.

| Skrót | Działanie |
|---|---|
| ← / → | Około 1 klatki, na podstawie FPS |
| Shift + ← / → | Około 10 klatek |
| Ctrl + ← / → | 1 sekunda, również bez FPS; Ctrl ma pierwszeństwo |
| Esc podczas cropu | Anuluje niezakończony gest |
| Esc podczas przesuwania suwaka | Wraca do pozycji sprzed gestu |

Strzałki w polach tekstowych/listach zachowują standardowe działanie. Modyfikatory Alt/Windows wyłączają nawigację klatkami. Liczby klatek oznaczone **≈** są szacunkami z FPS; dla zmiennego FPS (VFR) nie są indeksami klatek z pełnego indeksu pliku. Jeżeli nie ma FPS, użyj suwaka lub Ctrl + strzałka.

## Przycinanie początku i końca

W trybie **Przycinanie czasu** wpisz sekundy z przecinkiem lub kropką. Możesz także użyć **Początek tutaj** oraz **Koniec przed tą klatką** — operują na czasie już wyświetlonej klatki, a nie na oczekującym celu. W czasie przewijania przyciski znaczników są chwilowo wyłączone.

Zakres ma postać **[początek, koniec)**: początek jest włączny, koniec wyłączny. Koniec równy długości wideo oznacza materiał aż do EOF. Profil **Copy** nie wykonuje rekompresji, ale granice mogą zależeć od keyframes i timestampów. Nie daje gwarancji cięcia dokładnie na wskazanej klatce.

W trybie przycinania czasu **zapisany crop nie jest nakładany na wynik**, nawet jeśli nadal istnieje w edytorze.

## Prostokątny crop obrazu

Wybierz **2. Crop obrazu**. Na podglądzie pojawi się ramka z uchwytami. Narysuj nowy prostokąt lewym przyciskiem myszy lub przesuń narożnik. Można również ręcznie ustawić:

- **X/Y:** lewy górny narożnik;
- **W/H:** szerokość i wysokość.

Wartości są w pikselach **po obrocie obrazu, przed korekcją proporcji SAR**; nie są to piksele monitora. Prawdziwy crop wymaga parzystych X, Y, W i H, wymiarów co najmniej 2 × 2 oraz obszaru mieszczącego się w źródle. Rysowanie przyciąga wartości do parzystej siatki. Kliknięcie bez obszaru nie zastępuje dotychczasowego kadru, a rysowanie w czarnych pasach podglądu nie rozpoczyna cropu.

Zmiana tekstu X/Y/W/H tworzy **szkic**, nie zatwierdzony kadr. Ramka przedstawia poprzedni zatwierdzony obszar. **Zastosuj** sprawdza i zatwierdza zmiany; **Odrzuć zmiany** je cofa. Niezatwierdzony szkic blokuje zmianę trybu i eksport.

**Cały obraz** usuwa filtr crop i szkic. **Obszar środkowy** ustawia osobny preset około 75% wymiarów. Nie jest resetem. Samo wejście w Crop obrazu nie uruchamia presetu.

Jeśli ustawisz rzeczywisty crop przy profilu Copy, program automatycznie wybiera FFV1; możesz ręcznie zmienić profil na VP9. Zakres początku/końca jest stosowany również w trybie cropu. Wrócenie do cięcia czasu zachowuje crop w pamięci, lecz nie stosuje go przy tym eksporcie.

## Profile eksportu

| Wybór | Co powstaje |
|---|---|
| Bez rekompresji — oryginalny format | Szybkie kopiowanie strumieni z cięciem zależnym od keyframes |
| FFV1 / MKV — obraz bezstratny, audio copy | Obraz FFV1 w MKV; obsługiwany dźwięk kopiowany |
| WebM VP9 — wymiary 1:1 | Stratny VP9 / Opus w pełnym rozmiarze po cropie |
| WebM VP9 — 1/2 szerokości i wysokości | Połowa obu wymiarów, zaokrąglana do parzystych |
| WebM VP9 — 1/4 szerokości i wysokości | Jedna czwarta obu wymiarów, zaokrąglana do parzystych |

Zmniejszenie liczby pikseli **nie gwarantuje** proporcjonalnie mniejszego pliku. Profile kodujące nie zachowują wszystkich metadanych, napisów ani dodatkowych strumieni. Informacja przed eksportem opisuje ważne skutki wyboru. Szczegóły: [Kontrakt eksportu](EXPORT-CONTRACT.md).

## Bezpieczeństwo zapisu, anulowanie, zamknięcie

Podczas eksportu edycja/otwieranie są wyłączone, a przycisk **Anuluj** pozostaje dostępny. Wynik najpierw zapisuje się do unikalnego pliku roboczego w katalogu docelowym. Dopiero po powodzeniu FFmpeg i weryfikacji podstawowych metadanych program przenosi plik lub zastępuje wskazany wynik. Zastąpienie istniejącego pliku wymaga osobnego potwierdzenia.

Błąd/anulowanie nie powinny usuwać wcześniej istniejącego wyniku. Anulowanie **po dokonaniu** zamiany nie przywraca starej wersji. Awaria systemu, brak miejsca lub wymuszone zamknięcie mogą pozostawić plik roboczy. Zachowuj kopie ważnych materiałów.

**Diagnostyka** pokazuje środowisko, ścieżki narzędzi i log. Opisy błędów: [Diagnostyka](DIAGNOSTICS.md) i [FAQ](FAQ.md).

## Ograniczenia

Interfejs programu jest obecnie po polsku. Systemowe okna plików zachowują język Windows. Podgląd nie odtwarza audio i nie wykonuje tone mappingu HDR. Wybrane kodeki i profile wymagają zgodności metadanych, geometrii i instalacji FFmpeg.
