# Audyt Win10 WebM Cut

**Data:** 2026-10-26. **Repozytorium:** `KeyffMS/win10-webm-cut`.
**Audytowany commit:** `a124e98ea9171b2f24e2ea1555801fffd5634580` (2026-10-25, 20:35:09 UTC).
**Metoda:** przegląd statyczny całego repozytorium oraz lokalne modele arytmetyczne i scenariusze kolejności zdarzeń. **To nie są testy aplikacji ani benchmark.**

## 1. Wniosek i granice audytu

Kod realizuje mały, zrozumiały produkt bez Electrona i dodatkowych pakietów NuGet, ale wymaga uporządkowania bezpieczeństwa zapisu, czasu prezentacji i cyklu życia operacji asynchronicznych. Najpierw należy usunąć ryzyka utraty wyniku eksportu i nieaktualnego stanu aplikacji; dopiero potem optymalizować alokacje i porządkować kosmetykę kodu.

Przejrzano **14 plików** wskazanych w [manifeście źródeł](source-manifest.json): wszystkie C#, XAML, plik projektu, całą istniejącą dokumentację, licencję i konfigurację. Odwołania [S1]–[S14] prowadzą do **niezmiennego audytowanego commitu**, nie do ruchomego `main`. Miejsca problemów wskazano przez nazwę pliku i symbol; nie podajemy niezweryfikowanych numerów linii.

W sandboxie uruchomiono wyłącznie [simulate.py](simulate.py). [simulation-results.json](simulation-results.json) zawiera jego rzeczywisty wynik. Model nie uruchamia ani nie importuje kodu C#, WPF, .NET i FFmpeg, nie otwiera mediów, nie ma połączeń sieciowych i nie emuluje całego środowiska Windows. Zawiera założone sekwencje zdarzeń i przepisane z kodu wzory. Nie ma asercji ani wyniku „testy przeszły”.

**Nie wykonano:** kompilacji, testów jednostkowych/integracyjnych/GUI, pomiarów CPU/RAM, dekodowania lub eksportu rzeczywistego filmu, oceny jakości obrazu, testów instalacji, kontroli artefaktów wcześniejszego CI ani audytu prawnego. Wcześniejsze sukcesy kompilacji i zgłoszenia użytkownika nie zastępują weryfikacji tej wersji. Nie deklarujemy, że aktualna wersja ponownie powoduje wcześniej naprawioną awarię.

**GitHub służy tu tylko do odczytu źródeł i zapisu dokumentacji.** Nie uruchamiano Actions ani innych usług obliczeniowych GitHuba. Commity audytu mają `[skip ci] [skip actions]`; istniejący workflow nie jest zmieniany ani uruchamiany. To nie wyłącza Actions dla przyszłych, niezwiązanych z audytem commitów. Działanie znaczników dla zdarzeń `push` i `pull_request` opisuje [G1].

### Oznaczenia dowodów i priorytetów

- **KOD:** konstrukcja bezpośrednio obecna w źródle. Nie oznacza odtworzenia awarii w działającym programie.
- **MODEL:** obliczenie lub przykładowa kolejność zdarzeń w sandboxie; warunki są jawne.
- **RYZYKO:** możliwy skutek zależny od pliku, obciążenia lub harmonogramu; niepotwierdzony pomiarem.
- **LUKA:** brak opisu lub mechanizmu w audytowanym drzewie.

**P1** — przed udostępnianiem programu do pracy na ważnych danych; **P2** — kolejny etap stabilizacji; **P3** — porządkowanie i ułatwienie utrzymania. Priorytety są oceną audytora, nie skalą CVSS. Nie stwierdzono podstaw do ogłoszenia katastrofalnej awarii P0; to nie jest certyfikat bezpieczeństwa.

| ID | Priorytet | Ustalenie | Rodzaj dowodu |
|---|---|---|---|
| F01 | P1 | Eksport bez transakcyjnego pliku tymczasowego | KOD + MODEL |
| F02 | P1 | Brak oczekiwania na zakończenie podglądu przed zwolnieniem CTS | KOD + RYZYKO |
| F03 | P1 | Nakładające się otwieranie plików może przywrócić starszy wybór | KOD + MODEL |
| F04 | P1 | Zegar UI nie jest zegarem wyświetlonej klatki | KOD + MODEL |
| F05 | P2 | Nowe bitmapy i nieograniczona liczba oczekujących aktualizacji UI | KOD + MODEL |
| F06 | P2 | Nowe żądanie dekodowania przy każdym kroku nawigacji | KOD + MODEL |
| F07 | P2 | Limit szerokości bez limitu wysokości/liczby pikseli | KOD + MODEL |
| F08 | P3 | Timer odświeża niezmieniony podgląd podczas pauzy | KOD + MODEL |
| F09 | P2 | Etykieta „bezstratny” nie określa całego kontraktu formatu | KOD + LUKA |
| F10 | P2 | Crop nie uwzględnia jawnie rotacji, SAR i geometrii dekodera | KOD + RYZYKO |
| F11 | P2 | Tekst pól cropu i zatwierdzony crop są dwoma stanami | KOD + MODEL |
| F12 | P2 | Przybliżone klatki, niejednoznaczny koniec i Ctrl zależny od FPS | KOD + MODEL |
| F13 | P2 | Niejednolita obsługa procesów, końca dekodowania i diagnostyki | KOD + RYZYKO |
| F14 | P2 | Brak pełnej walidacji bardzo małych wymiarów | KOD + MODEL |
| F15 | P2 | Profil eksportu zależy od kolejności elementów ComboBox | KOD |
| F16 | P2 | MainWindow łączy zbyt wiele reguł i właścicieli stanu | KOD |
| F17 | P3 | Powielone reguły uruchamiania narzędzi i formatowania | KOD |
| F18 | P3 | Motyw jest częściowo scentralizowany; luki dialogów i dostępności | KOD + LUKA |
| F19 | P2 | Instrukcja lokalnego katalogu tools jest sprzeczna z kodem | KOD + LUKA |
| F20 | P2 | Dokumentacja użytkownika i kontraktów nie pokrywa funkcji | LUKA |
| F21 | P2 | Brak jawnego pakowania dokumentacji i informacji o składnikach | KOD + LUKA |
| F22 | P3 | Brak opisanej diagnostyki, wersjonowania i higieny logów | KOD + LUKA |

## 2. Bezpieczeństwo operacji i poprawność stanu

### F01 — eksport bez transakcyjnego zapisu — P1

**Miejsce:** [S2], `ExportAsync` (`-y`, bezpośrednie `args.Add(output)`); [S1], `Export_Click`, obsługa anulowania przez `File.Delete(dlg.FileName)`.

Dialog pyta o nadpisanie, ale po akceptacji FFmpeg pisze od razu do docelowej nazwy. Nie ma pliku roboczego i finalnego zatwierdzenia. Błąd pozostawia częściowy wynik; anulowanie usuwa plik docelowy. Przy istniejącym wyniku jego wcześniejsza zawartość może zostać utracona już przed ukończeniem operacji. S08 pokazuje tę sekwencję **w modelu słownika**, bez kasowania plików. Nie dotyczy to normalnego nadpisania pliku wejściowego pod identyczną ścieżką: taki przypadek ma zabezpieczenie `GetFullPath`.

**Zalecenie:** unikalny plik roboczy w katalogu docelowym z poprawnym rozszerzeniem/mukserem, weryfikacja zakończenia procesu, dopiero potem zamiana wyniku; po błędzie usuwać wyłącznie plik roboczy należący do danej operacji. Zamiana musi uwzględnić ograniczenia docelowego systemu plików. Samo usunięcie `File.Delete` nie naprawia wcześniejszego nadpisania przez `-y`.

### F02 — cykl życia zadania i CTS — P1, ryzyko, nie odtworzona awaria

**Miejsce:** [S1], `CancelPreview`, `StartPreview`, `Window_Closing`; [S3], `DecodeAsync`.

UI wykonuje `Cancel()` i natychmiast `Dispose()`, a zadanie uruchomione jako `_ = Task.Run(...)` nie jest przechowywane ani oczekiwane. Token pobierany jest przed uruchomieniem zadania, a worker nie wywołuje `Dispose` — to korzystne elementy obecnej wersji, ale nie tworzą gwarancji zakończenia pracy. `Cancel` nie oznacza, że skończyły się odczyty, rejestracja callbacku, zamykanie procesu i sprzątanie. Kontrakt `CancellationTokenSource.Dispose` wymaga zakończenia pozostałych operacji na źródle [N1]. S09 przedstawia możliwą kolejność, **nie symuluje implementacji CTS i nie generuje wyjątku .NET**.

**Zalecenie:** jeden właściciel sesji podglądu przechowuje CTS i Task. Unieważnienie żądania oraz anulowanie są oddzielone od asynchronicznego oczekiwania i `Dispose` w `finally`. Przechwytywać wynik zadania, także przy zamknięciu okna; nie wprowadzać blokującego `.Wait()` na wątku UI. Zwrócić uwagę na spóźnione callbacki Dispatchera po rozpoczęciu zamykania.

### F03 — wyścig podczas otwierania plików — P1

**Miejsce:** [S1], `Open_Click`, zwłaszcza `await ProbeAsync(..., CancellationToken.None)`.

Blokowany jest eksport, ale nie istnieje blokada kolejnego otwarcia, identyfikator żądania ani anulowanie starszego probe. Sekwencja „otwórz A, otwórz B, probe B kończy się, probe A kończy się” kończy ze stanem A. S07 ilustruje dokładnie tę kolejność. Późny wynik może zmienić `_input`, `_info`, zakres i crop, mimo że użytkownik wybrał już inny plik. Asynchroniczna kontynuacja może też nadejść po rozpoczęciu zamykania okna.

**Zalecenie:** generacja żądania `open`, anulowanie poprzedniego odczytu, sprawdzenie generacji i stanu okna po `await`. Dodatkowo eksport powinien pobierać jeden niezmienny snapshot wejścia, zakresu, cropu i profilu, zanim otworzy się dialog zapisu, oraz nie mieszać go z późniejszą zmianą UI.

### F04 — zegar aplikacji a prezentowana klatka — P1

**Miejsce:** [S1], timer, `Play_Click`, `PausePreview`, `MarkStart_Click`, `MarkEnd_Click`; [S3], `DecodeAsync`.

Pozycja wynika z `_playStart + Stopwatch.Elapsed`, a dekoder oddaje samo `BitmapSource`, bez czasu prezentacji PTS. Zegar rusza przed startem procesu i pierwszą klatką. Opóźnienie dekodowania albo wolniejsza konsumpcja nie koryguje pozycji UI. Przy hipotetycznym starcie trwającym 0,5 s (S12) pierwsza klatka może pojawić się, gdy zegar pokazuje już 0,5 s. **0,5 s jest założeniem, nie zmierzoną latencją.** Znaczniki początku/końca korzystają dodatkowo z `_position` ostatnio zaktualizowanej przez timer.

**Zalecenie:** rozdzielić pozycję żądaną, odkodowaną i wyświetloną; do oznaczania klatek używać czasu faktycznie zaprezentowanego obrazu. Powiązać klatkę z PTS/indeksem. W lekkiej wersji jasno oznaczyć nawigację jako przybliżoną, zamiast obiecywać precyzję nieobecną w protokole dekodera. Nie wymaga to automatycznie nowego frameworka GUI.

## 3. Audyt wydajności

### F05 — bitmapy i kolejka Dispatchera — P2

**Miejsce:** [S3], `BitmapSource.Create`, `Freeze`, `onFrame`; [S1], `Dispatcher.BeginInvoke` w `StartPreview`.

Bufor odczytu jest ponownie używany wewnątrz jednego procesu, co jest korzystne. Powstaje jednak nowe `BitmapSource` dla każdej klatki i osobny callback UI, który przechowuje tę klatkę do obsługi. Brak kolejki o ograniczonej długości i zasady „zachowaj tylko najnowszą”. Gdy UI nie nadąża, liczba oczekujących aktualizacji może rosnąć. Zabezpieczenie tożsamości żądania odrzuca przestarzałą klatkę dopiero przy wykonaniu callbacku.

S01 przelicza **wyłącznie surowy ładunek BGRA**. To nie jest przewidywany working set procesu ani tempo alokacji zarządzanej sterty; pomija obiekty, kopie, GC, DWM, buforowanie oraz pamięć FFmpeg.

| Wejście | Podgląd według wzoru w kodzie | MiB/klatkę | MiB/s przy 12 kl./s | MiB ładunku 60 oczekujących klatek |
|---|---|---:|---:|---:|
| 1920×948 | 960×474 | 1,736 | 20,830 | 104,150 |
| 3840×2160 | 960×540 | 1,978 | 23,730 | 118,652 |
| 1080×1920 | 960×1706 | 6,248 | 74,971 | 374,854 |
| 960×10000, przypadek skrajny | 960×10000 | 36,621 | 439,453 | 2197,266 |

Ostatnia kolumna zakłada produkcję 12 kl./s przez 5 s bez obsługi callbacków UI; **nie stwierdzono takiego zablokowania ani takiego tempa na realnym komputerze**.

**Zalecenie:** jedna aktualizacja w locie i wymienialny slot najnowszej klatki; następnie rozważyć ponowne wykorzystanie powierzchni `WriteableBitmap` z prawidłową własnością wątku/bufora. Nie wprowadzać poolingu bez określenia czasu życia danych.

### F06 — restart dekodera przy nawigacji — P2

**Miejsce:** [S1], `Window_PreviewKeyDown`, `Slider_MouseUp`, `StartPreview`; [S3], `process.Start()`.

Każdy obsłużony krok klawiatury tworzy nowe zadanie i próbuje rozpocząć FFmpeg. Nie ma łączenia kolejnych żądań. Dekoder sprawdza anulowanie w pętli dopiero po starcie procesu; może więc uruchomić proces dla żądania już nieaktualnego. Synchroniczne `Cancel` wykonuje callback zabijający poprzedni proces na wątku wywołującym. To potencjalnie blokuje UI; czasu blokady nie zmierzono.

S03 przyjmuje 30 zdarzeń klawiatury/s przez 2 s: daje 60 wywołań `StartPreview`, **nie zmierzonych 60 procesów równoległych**. Nie należy mylić ilości żądań z jednoczesnością procesów.

**Zalecenie:** jeden koordynator nawigacji, unieważnianie po generacji, scalenie oczekujących skoków, sprawdzenie tokenu przed `Start`. Najpierw prosta kolejka latest-wins; trwały dekoder dopiero po uzasadnieniu jego dodatkowej złożoności.

### F07 — ograniczenie tylko szerokości — P2

**Miejsce:** [S3], `maxWidth`, wyliczanie `height` i `frameBytes`.

960 px ogranicza szerokość, nie wysokość, nie liczbę pikseli i nie koszt dekodowania wejścia. Film pionowy 1080×1920 daje w modelu 960×1706, a nie obraz mieszczący się w prostokącie 960×540. Zawartość może być znacznie większa od powierzchni podglądu. Skrajny przypadek S01 pokazuje brak formalnej granicy pamięci, nie typowe obciążenie.

**Zalecenie:** wspólny limit szerokości, wysokości i liczby pikseli, dopasowanie do dostępnej powierzchni z uwzględnieniem DPI, kontrolowana redukcja dla bardzo dużych źródeł. Sprawdzić rozmiar przed alokacją; samo `checked` dla iloczynu chroni przed częścią przepełnień, ale nie przed nadmierną alokacją.

### F08 — odświeżanie w pauzie — P3

**Miejsce:** [S1], konstruktor i `_timer.Tick`.

Timer 180 ms startuje od razu i zatrzymuje się dopiero przy zamknięciu. Po wczytaniu pliku przelicza oraz przypisuje etykiety również podczas pauzy. S02 daje nominalnie 20 000 wywołań/h przy działającym Dispatcherze. Nie jest to miara zużycia CPU i nie dowodzi dużego problemu energetycznego.

**Zalecenie:** timer tylko podczas odtwarzania; w pauzie aktualizacja na zmianę pozycji lub danych. Nie optymalizować tego przed F01–F07.

### Profil eksportu i dodatnie cechy wydajnościowe

`Copy` korzysta z kopiowania strumieni, a podgląd nie tworzy pośrednich filmów na dysku. VP9 ma jawny, stały zestaw parametrów (`good`, `cpu-used=4`, `row-mt=1`, CRF 32); nie ma pomiaru pozwalającego uznać go za najszybszy lub optymalny dla komputera użytkownika. Nie należy podawać przewidywanej szybkości eksportu ani rozmiaru pliku. Zmniejszenie wymiarów zmniejsza liczbę pikseli, ale nie gwarantuje proporcjonalnej redukcji czasu/rozmiaru. Podczas eksportu użytkownik może ponownie uruchomić podgląd, więc oba zadania mogą konkurować o zasoby ([S1], `Play_Click`).

## 4. SPOT, SPOOA i DRY

### Definicje użyte w audycie

**SPOT** oznacza tutaj jedno autorytatywne źródło reguły lub stanu. **DRY** oznacza brak powielania wiedzy wymagającej równoległych zmian, a nie zakaz powtarzania każdej linijki XAML. Skrót **„SPOOA” nie jest rozwinięty ani zdefiniowany w repozytorium lub zleceniu**. Dla wykonania tej części przyjęto jawnie robocze kryterium: **jeden właściciel operacji, jej decyzji i cyklu życia**. Nie jest to twierdzenie o ustalonym rozwinięciu skrótu ani o zgodności z zewnętrzną normą.

| Obszar | Stan obecny | Ocena i powiązanie |
|---|---|---|
| Parametry kompresji | Główne argumenty eksportu w `FfmpegService` | Dobry punkt centralizacji, ale brak kontraktu profilu |
| Profil i rozszerzenie | enum, kolejność ComboBox i switch rozszerzenia | Naruszenie SPOT: F15 |
| Pozycja czasu | `_position`, `_playStart`, stopwatch, slider, klatka bez PTS | Kilka reprezentacji bez jednej semantyki: F04/F12 |
| Crop | `_crop`, cztery pola tekstowe, geometria Canvas | Brak jawnego rozróżnienia draft/applied: F11 |
| FFmpeg/FFprobe | resolver i konfiguracja procesu w dwóch miejscach | Powielona wiedza: F17 |
| Anulowanie podglądu | UI posiada CTS, zadanie posiada proces | Brak właściciela zakończenia obu: F02/F13 |
| Otwieranie pliku | Każda kontynuacja może zastąpić stan | Brak właściciela ostatniego żądania: F03 |
| Motyw | Zasoby aplikacji oraz literały w XAML/C# | Częściowe SPOT: F18 |
| Dialogi | Jeden `DarkDialog.Show` | Korzystna centralizacja komunikatów |

### F15 — profil zależny od indeksu — P2

**Miejsce:** [S2], `ExportProfile`; [S4], `ProfileBox`; [S1], `Export_Click`, `SetCrop`, `Profile_Changed`.

`(ExportProfile)ProfileBox.SelectedIndex` zakłada, że kolejność elementów XAML jest identyczna z kolejnością enum. Dodatkowe reguły „0 = copy, 1 = FFV1” są zakodowane jako liczby. Reorganizacja listy może zmienić kodek bez błędu kompilacji. Filtry dialogu, rozszerzenia i argumenty FFmpeg są rozproszone.

**Zalecenie:** jedna mała lista opisów profili: identyfikator enum, etykieta, rozszerzenie, możliwość cropu i parametry. UI wybiera identyfikator/obiekt, nie indeks. To nie wymaga systemu wtyczek.

### F16 — odpowiedzialności i stan MainWindow — P2

**Miejsce:** [S1], cała klasa.

Okno zarządza procesem otwierania, zegarem, klawiaturą, zadaniem podglądu, geometrią cropu, formatowaniem, walidacją eksportu, usuwaniem pliku i dialogami. Stany `_playing`, `_draggingSlider`, `_selectingCrop`, `_info`, `_previewCancellation` i `_exportCancellation` nie mają wspólnie zapisanych niezmienników. Przykładowo UI blokuje tylko część działań podczas eksportu; `Open_Click` chroni start nowego otwarcia, lecz nie wcześniej rozpoczęte kontynuacje.

**Zalecenie:** zapisać niezmienniki i wydzielić tylko wyraźne granice: sesję podglądu, czyste obliczenia czasu/cropu oraz snapshot żądania eksportu. Code-behind sam w sobie nie jest błędem. Pełne MVVM, kontener DI i rozbudowana hierarchia usług nie są koniecznym wynikiem audytu.

### F17 — powielona wiedza — P3

**Miejsce:** [S2], `Tool`, `StartInfo`; [S3], lokalne wyszukiwanie `ffmpeg.exe`, konfiguracja i zabijanie procesu; [S1], formatowanie czasu i reguły parzystości.

Resolver `tools`/PATH występuje w dwóch miejscach, ale polityka stderr, zakończenia i anulowania jest inna. Różne implementacje nie są automatycznie błędem, jednak zmiana lokalizacji narzędzia lub obsługi procesu wymaga pamiętania o obu. Dokumentacja opisuje trzeci wariant lokalizacji (F19).

**Zalecenie:** jeden resolver narzędzi i krótki kontrakt uruchamiania procesu, z rozróżnieniem stdout tekstowego i binarnego. Wspólne nazwy dla tolerancji czasu, rozmiaru podglądu i reguł wymiarów. Nie tworzyć uniwersalnej biblioteki tylko po to, by usunąć dwa podobne fragmenty.

## 5. KISS, clean code i poprawność kontraktów

### F09 — znaczenie „bezstratnego” eksportu — P2

**Miejsce:** [S2], `ExportAsync`; [S12], opis FFV1 i audio.

Kod rozróżnia stream copy oraz dekodowanie i ponowne kodowanie — to poprawne rozdzielenie. Profil FFV1 ustawia `ffv1` i `flac`, ale nie określa jawnego kontraktu formatu pikseli, głębi, próbek audio, HDR/kolorów ani zgodności wszystkich typów wejść. Sam wybór kodeka bezstratnego nie jest dowodem zachowania każdej próbki źródłowej po całym łańcuchu konwersji. Nie sprawdzano faktycznej negocjacji formatów przez zainstalowany FFmpeg.

W trybach kodowania mapowane są pierwszy strumień wideo oraz wszystkie dostępne strumienie audio (`0:v:0`, `0:a?`, `-sn`, `-dn`); Copy używa `-map 0`. Zmienia się więc również zakres zachowywanych strumieni. Nie wszystkie napisy, dane lub kolejne strumienie wideo przechodzą do wyników kodowanych. Przy Copy zgodność wszystkich strumieni z wybranym kontenerem nie jest weryfikowana w aplikacji.

**Zalecenie:** tabela zachowania strumieni i obsługiwanych formatów; komunikat, co oznacza „bezstratny” w danym profilu. Preflight możliwości narzędzia i formatu zamiast obietnicy identyczności całego pliku. Nie zmieniać ukradkiem celu użytkownika z bezstratnego na stratny.

### F10 — geometria cropu — P2

**Miejsce:** [S2], `VideoInfo`, `ProbeAsync`; [S3], `scale`; [S1], `TryGetVideoBounds`, `Crop_MouseUp`.

Metadane obejmują width/height/duration/FPS, ale nie SAR, macierz/rotację wyświetlania ani opis normalizacji. UI mapuje mysz według rozmiaru kodowanego obrazu; dekoder tworzy wymuszony rozmiar BGRA bez przekazania transformacji. Parzyste zaokrąglenie wymiarów podglądu również może lekko zmienić jego proporcje. README ostrzega o nietypowej orientacji, ale nie definiuje kontraktu współrzędnych.

**Zalecenie:** jedna geometria: jasno zdecydować, czy pracujemy w pikselach kodowanego strumienia, czy obrazu po rotacji. Wykorzystać ją w podglądzie, rysowaniu i eksporcie. Nie deklarujemy błędnego cropu dla każdego pliku: ryzyko zależy od metadanych i zachowania dekodera.

### F11 — draft cropu, zatwierdzony crop i reset — P2

**Miejsce:** [S1], `ApplyCrop_Click`, `SetCrop`, `ResetCrop`, `InitCrop`, `Export_Click`.

Eksport używa `_crop`, nie tekstu pól. Zmiana X/Y/W/H bez `Zastosuj` pozostawia poprzedni obszar; S10 pokazuje taki stan. Oddzielne zatwierdzanie jest dopuszczalnym projektem, lecz brakuje widocznej informacji, że pola zawierają niezapisany draft. `Reset crop` w aktywnym trybie ponownie wybiera środkowy obszar, a nie pełny obraz. Dla 1920×948 to `[240,118,1440,712]` (S06). Komunikat wymaga „dodatnich” X/Y, podczas gdy zero jest prawidłowo akceptowane.

**Zalecenie:** jawny stan niezastosowanych zmian; eksport zatwierdza/odrzuca draft z komunikatem. Rozdzielić „Cały obraz” od „Domyślny środkowy obszar” albo opisać reset jednoznacznie. Walidacja komunikatu ma odpowiadać walidacji kodu.

### F12 — semantyka klatek i skrótów — P2

**Miejsce:** [S1], `UpdateTimeDisplay`, `Window_PreviewKeyDown`, `Slider_MouseUp`, `StartPreview`; [S2], `Fps`.

Numer klatki pochodzi z `Round(seconds * fps)`, liczba klatek z `Ceiling(duration * fps)`. Nie ma oznaczenia przybliżenia ani ustalonej konwencji 0/1-based. Przy 1 s i 30 FPS etykieta może pokazać indeks 30 mimo zakresu indeksów 0–29 (S04). W VFR skok `1 / avg_fps` nie oznacza kolejnej rzeczywistej klatki: S05 ilustruje to na sztucznej osi PTS. Także czas kontenera może nie odpowiadać dokładnie długości strumienia wideo.

`_info.Fps <= 0` blokuje również Ctrl+strzałkę o sekundę, chociaż ten krok nie wymaga FPS (S11). Edycja TextBox/ComboBox celowo wyłącza skróty, ale instrukcja w UI tego nie mówi. Zakres końca jest w sekundach; obok pól początku/końca nadal nie ma numerów klatek, mimo istniejącego licznika bieżącej pozycji.

**Zalecenie:** Ctrl obsłużyć przed wymogiem FPS; zdefiniować półotwarty zakres `[start,end)` i numerację klatek. Pokazywać `≈` dla estymacji. Dokładne przechodzenie po klatkach wymaga rzeczywistych PTS/indeksu, a nie samego średniego FPS. Nie utożsamiać dokładnego wyboru w UI z możliwością dokładnego cięcia `-c copy` poza klatkami kluczowymi.

### F13 — procesy, zamykanie i diagnostyka — P2

**Miejsce:** [S2], `ProbeAsync`, `ExportAsync`; [S3], `DecodeAsync`; [S1], obsługa błędów i `Window_Closing`; [S7].

Podgląd nie sprawdza `ExitCode`, nie przekierowuje stderr, nie sygnalizuje jawnie „zero klatek” ani poprawnego końca. Proces kończący się błędem może więc dać tylko brak obrazu. Zadanie jest fire-and-forget; część wyjątków po anulowaniu nie pasuje do filtra `catch (Exception) when (!token.IsCancellationRequested)` i może pozostać niezaobserwowana.

Probe nie ma `finally` zabijającego proces po anulowaniu/błędzie; UI przekazuje `CancellationToken.None`, bez timeoutu. Eksport zabija proces dopiero w `catch` po `Task.WhenAll`, które czeka na wszystkie zadania [N2]. Jeżeli któryś odczyt nie kończy się sprawnie po anulowaniu, nie ma niezależnej granicy czasu zamknięcia. Nie stwierdzono, że taka blokada występuje na używanym Windows. Przy zamykaniu okna zadania nie są oczekiwane. Cleanup miejscami połyka wyjątki i nie pozostawia trwałego śladu.

**Zalecenie:** wspólny, niewielki kontrakt procesu: stderr o ograniczonym rozmiarze, kod wyjścia, anulowanie, limit oczekiwania i zakończenie cleanup. Rejestrować błąd lokalnie, ale nie dodawać globalnego „ignoruj wszystkie wyjątki”, które ukrywa uszkodzony stan. `App.xaml.cs` jest pustą klasą bez mechanizmu diagnostyki; nie ma trwałego logowania aplikacji.

### F14 — małe wymiary i walidacja domenowa — P2

**Miejsce:** [S2], `ProbeAsync`, `ExportAsync`; [S1], `InitCrop`, `Crop_MouseUp`, `ApplyCrop_Click`.

Probe akceptuje dodatni wymiar 1 px. Wówczas `width - 2` w `Math.Clamp` może dać maksimum mniejsze od minimum; domyślny crop po `& ~1` może mieć rozmiar zero. To warunkowe ścieżki wynikające ze źródeł, nie odtworzenie na realnym filmie.

Dla cropu 2×2 i skali 1/2 lub 6×6 i skali 1/4 wyrażenie `trunc(iw/divisor/2)*2` daje 0×0 (S06). To wynik wzoru, nie dowód utworzenia pliku zerowego rozmiaru: FFmpeg ma specjalną interpretację zerowych wymiarów scale [F1]. Profil nie zapewnia więc jawnie zamierzonego zmniejszenia w takich granicach. Dodatkowo usługa eksportu ma słabszą walidację niż GUI: brak pełnej kontroli skończoności czasu, zakresu enum i wymiarów cropu.

**Zalecenie:** jeden walidator żądania także na granicy usługi, minimalne wspierane wymiary, bezpieczna polityka małego cropu i gotowego rozmiaru wyjściowego. Nie polegać wyłącznie na kontrolkach UI.

### F18 — motyw, czytelność i dostępność — P3

**Miejsce:** [S4]–[S6], [S8], [S1] obsługa myszy.

Główne zasoby kolorów i `DarkDialog` są pozytywną centralizacją. Jednak kolor akcentu i kolory tekstu cropu są także literałami XAML, a kolory statusu dialogu są wpisane w C#. Nie ma jawnego stylu ScrollBar/ToolTip/ProgressBar, więc deklaracja całkowicie jednolitego motywu wymaga ostrożności. Systemowe dialogi otwierania/zapisu pozostają oddzielne.

Dialog wyświetla szczegóły przez TextBlock, bez przycisku kopiowania błędu. Ikonowy przycisk zamknięcia ma tooltip, ale brak pełnej jawnej dokumentacji nazw/relacji dostępności. Własne szablony kontrolek nie mają konsekwentnie zdefiniowanych stanów fokusu. Szerokie poziome układy czasu/cropu i okno o minimalnym rozmiarze mogą wymagać sprawdzenia przy skalowaniu DPI; nie przeprowadzono renderowania ani testów czytnika ekranu.

Brak obsługi utraty przechwycenia myszy w sliderze/cropie może pozostawić `_draggingSlider` lub `_selectingCrop` w stanie niezgodnym z intencją. `Open_Click` ukrywa `CropHint`, a przy otwarciu kolejnego pliku w już aktywnym trybie crop nie przywraca go jawnie.

**Zalecenie:** użyć wspólnych tokenów, dodać kopiowanie szczegółów i zdefiniować stany interakcji. Nie pisać nowego systemu skórek. Zdarzenia utraty capture powinny kończyć/anulować edycję; obsługa DPI i dostępności pozostaje niezweryfikowana runtime.

### Ocena KISS i clean code

**Zachować:** mały projekt, brak pakietów GUI, oddzielny dekoder i usługa eksportu, `ArgumentList` bez powłoki, niezmienne rekordy danych, brak plików proxy, jeden dialog komunikatów. To rzeczywiste zalety widoczne w kodzie.

**Zmienić:** odpowiedzialności F16, brak jawnych kontraktów F09–F14, indeksowe reguły F15, powieloną politykę narzędzi F17 i niewidoczne błędy F13. „Prosto” nie oznacza pomijania zakończenia procesu ani zapisu bez ochrony istniejącego wyniku. Nie zaleca się automatycznej migracji do MVVM, Rust, Tauri lub innej architektury; najważniejsze poprawki mieszczą się w obecnym C#/WPF.

## 6. Audyt pełnej dokumentacji

### Inwentaryzacja

W audytowanym drzewie są trzy pliki dokumentacyjne: [README.md][S12], [tools/README.md][S13] i [LICENSE][S14]. Komentarze C# i XAML oraz `.csproj`, `.gitignore` i YAML potraktowano jako źródła pomocnicze, nie jako instrukcję użytkownika. **Nie ma** odrębnych instrukcji użytkownika, architektury, diagnostyki, pakowania, polityki danych ani historii zmian. To stwierdzenie dotyczy tego commitu, nie historii rozmowy lub innych repozytoriów.

| Obszar | Co istnieje | Brak lub niezgodność |
|---|---|---|
| Cel i zakres | C#/WPF/.NET 10, Windows, podstawowe tryby | Brak statusu stabilności i listy znanych ryzyk |
| Wymagania | SDK i lokalne narzędzia, cztery enkodery w tools README | Brak rozdzielenia SDK/runtime/self-contained, polityki wersji narzędzi i preflight |
| Uruchamianie | Poprawna ścieżka `--project` w głównym README | Błędna sugestia repozytoryjnego `tools`: F19 |
| Publikacja | Polecenie win-x64 single-file/self-contained | Brak opisu całego pakietu i dokumentów dołączanych do dystrybucji: F21 |
| Tryb czasu | Keyframes i Copy opisane | Brak semantyki końca, zaokrągleń, wielościeżkowości |
| Klatki i skróty | Widoczna instrukcja w UI | Brak sekcji README, numeracji, VFR, wyjątku focus/FPS; początek/koniec bez indeksów |
| Crop | Wybór obszaru myszą | Brak narożników, X/Y/W/H, Zastosuj, resetu, jednostek i reguł parzystości |
| Podgląd | FFmpeg, brak audio, 960 px/12 FPS | Limit jest szerokością; brak wyjaśnienia zegara i charakteru przybliżenia |
| Profile eksportu | Copy, FFV1, VP9 i redukcje wymiarów | Brak tabeli strumieni, formatów próbek, polityki HDR/SAR/rotacji |
| Anulowanie/nadpisanie | Zdanie o anulowaniu procesu | Brak ostrzeżenia o częściowych plikach i poprzednim wyniku |
| Architektura | Krótki akapit | „WPF jest tylko GUI” nie oddaje reguł i operacji w MainWindow; brak cyklu życia i stanów |
| Diagnostyka | Wzmianka o logu FFmpeg | Brak lokalizacji logów, kopiowania błędów i procedury zgłoszenia |
| Prywatność | Deklaracja bez kont/telemetrii | Brak zasad udostępniania ścieżek i logów; brak osobnej polityki danych |
| Licencja | MIT i ostrzeżenie o licencjach FFmpeg | Brak jawnego dołączenia notice do paczki; build narzędzi pozostaje zewnętrzny |
| Wersjonowanie | Brak procedury | Brak changelogu, polityki wersji SDK/FFmpeg, oznaczenia wydania |
| Ograniczenia i weryfikacja | Lista ograniczeń MVP | Brak rozdzielenia „kompiluje się”, „zasymulowano”, „sprawdzono na Windows” |

### F19 — rozbieżna instrukcja tools — P2

[S13] sugeruje umieszczenie binariów w `tools` repozytorium podczas uruchamiania z katalogu projektu. Obie implementacje resolvera ([S2], [S3]) używają `AppContext.BaseDirectory/tools`. [S9] nie zawiera reguły kopiowania repozytoryjnego `tools` do wyjścia. Przy zwykłym `dotnet run` to różne lokalizacje. Główne README poprawnie mówi „obok EXE”.

**Zalecenie:** jeden opis i resolver. Albo wymagać PATH/`tools` obok EXE, albo jawnie wprowadzić kopiowanie na build/publish. Audyt nie zmienił tego zachowania.

### F20 — kompletność i zgodność opisów — P2

Macierz powyżej to pełny przegląd obecnej dokumentacji, nie deklaracja utworzenia brakujących instrukcji. Największe luki dotyczą operacji, które już istnieją: zatwierdzania cropu, nawigacji po klatkach, ograniczeń odtwarzania oraz zachowania wyniku po błędzie. Zdanie o „logu przekazywanym do okna” dotyczy tylko części wyjścia eksportu; nie istnieje trwały wspólny dziennik. Obietnicę anulowania należy opisać razem z cleanup i nadpisywaniem.

**Zalecenie:** najpierw krótki przewodnik użytkownika i znane ograniczenia; następnie kontrakty architektury/process lifecycle i instrukcja wydawania. Opisy muszą wskazywać faktyczne ograniczenia, a nie „poprawiać” ich słowami.

### F21 — dokumenty w paczce — P2

**Miejsce:** [S9], [S10], [S12]–[S14]. Projekt nie ma jawnych `CopyToOutputDirectory`/`CopyToPublishDirectory` dla README/LICENSE/notice; workflow publikuje katalog wynikowy. Bez zbadania artefaktu nie potwierdzamy jego kompletnej zawartości, ale repozytorium nie ustanawia mechanizmu dołączenia tych dokumentów. MIT wymaga zachowania noty i tekstu pozwolenia; binaria FFmpeg i .NET mają odrębny kontekst dystrybucji.

**Zalecenie:** zdefiniować manifest pakietu i włączyć kopiowanie wymaganych dokumentów; zapisywać wersję, pochodzenie i wariant narzędzi. Nie stwierdza się naruszenia licencji na podstawie samej analizy projektu; zakres nie obejmuje porady prawnej ani audytu konkretnego buildu FFmpeg.

### F22 — diagnostyka, wersje i logi — P3

**Miejsce:** [S7], [S9]–[S13]. Brak procedury trwałego logowania, identyfikatora wersji aplikacji w diagnostyce i opisanej polityki zgodnych wersji FFmpeg. Nie ma `global.json`; odtworzenie środowiska SDK nie jest przypięte w repozytorium. To nie oznacza, że bez niego projekt nie działa.

`.gitignore` wyklucza katalogi builda i pliki IDE, ale nie lokalne logi/dumpy oraz media. Nie znaleziono tu przesłanych logów użytkownika ani binariów — chodzi o brak zabezpieczenia przed ich przypadkowym przyszłym dodaniem. Raport nie publikuje prywatnych ścieżek ani logów z rozmowy.

**Zalecenie:** lokalny log z ograniczonym rozmiarem i zasadami anonimizacji, polecenia zebrania danych, changelog, jawna polityka SDK i narzędzi. Uzupełnić ignore dla artefaktów diagnostycznych bez ukrywania właściwych dokumentów audytu.

## 7. Minimalny plan naprawczy

| Kolejność | Zmiana | Warunek zakończenia w przyszłej implementacji |
|---|---|---|
| 1 | F01: eksport transakcyjny | Błąd/anulowanie nie usuwa poprzedniego wyniku; cleanup dotyczy tylko własnego pliku roboczego |
| 2 | F02/F03/F13: właściciele operacji | Każde żądanie ma generację, zadanie, anulowanie i jawne zakończenie; starsza operacja nie zastępuje nowszej |
| 3 | F04/F12: kontrakt pozycji | Etykieta, znaczniki i wyświetlona klatka odnoszą się do tej samej osi; estymacje są oznaczone |
| 4 | F05–F08: ograniczenia podglądu | Ograniczona liczba klatek oczekujących, limit pikseli, łączenie żądań i brak pracy timera w pauzie |
| 5 | F09–F18: profile i edycja | Jeden profil danych, walidacja domenowa, jawny draft cropu i transformacja współrzędnych |
| 6 | F19–F22: dokumentacja i paczka | Instrukcje odpowiadają kodowi, manifest dystrybucji i procedura diagnostyki istnieją |

Warunki powyżej są **propozycją kryteriów przyszłych zmian**, nie wynikami testów. W tym zleceniu nie zmieniono kodu aplikacji, nie naprawiono wskazanych usterek i nie uruchomiono testów. Nie należy traktować raportu jako potwierdzenia gotowości wydania.

## 8. Odtworzenie modeli lokalnie

Potrzebny wyłącznie Python 3; brak pakietów zewnętrznych. Z katalogu audytu:

```powershell
python .\simulate.py > .\simulation-results.local.json
```

To powtarza modele S01–S12, nie uruchamia programu. Założenia i jednostki są zapisane w kodzie i JSON. Nie korzysta to z .NET SDK, FFmpeg ani usług GitHuba. Ponowne uruchomienie nie zmieni mediów. Brak wyniku „pass/fail” jest celowy.

## 9. Źródła

### Źródła projektu — podstawa ustaleń

[S1]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/MainWindow.xaml.cs
[S2]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/FfmpegService.cs
[S3]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/PreviewDecoder.cs
[S4]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/MainWindow.xaml
[S5]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/App.xaml
[S6]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/DarkDialog.xaml.cs
[S7]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/App.xaml.cs
[S8]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/DarkDialog.xaml
[S9]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/src/Win10WebmCut/Win10WebmCut.csproj
[S10]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/.github/workflows/build.yml
[S11]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/.gitignore
[S12]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/README.md
[S13]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/tools/README.md
[S14]: https://github.com/KeyffMS/win10-webm-cut/blob/a124e98ea9171b2f24e2ea1555801fffd5634580/LICENSE

Pełna lista plików, rozmiary i identyfikatory blobów: [source-manifest.json](source-manifest.json). Hashy nie wyliczano z ponownie sklonowanego drzewa: pochodzą z odczytu Git tree i plików przypiętych do commitu przez API.

### Zewnętrzne kontrakty API — pomocnicze, nie obserwacje programu

[N1]: https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource?view=net-10.0
[N2]: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-10.0
[G1]: https://docs.github.com/actions/managing-workflow-runs/skipping-workflow-runs
[F1]: https://ffmpeg.org/pipermail/ffmpeg-devel/2017-June/212271.html

[N1] dokumentuje ograniczenia współbieżności `Dispose`; [N2] kontrakt `WhenAll`; [G1] pomijanie workflow przez wiadomość commitu; [F1] historyczne doprecyzowanie specjalnego znaczenia zerowych wymiarów scale. Nie użyto tych źródeł do dopisania funkcji, których nie ma w repozytorium. Wyniku konkretnego FFmpeg na danym pliku nie ustalono.
