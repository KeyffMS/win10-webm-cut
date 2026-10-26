# Roadmapa napraw po audycie — Win10 WebM Cut

**Data planu:** 2026-10-26. **Epik:** [#1 — naprawy po audycie](https://github.com/KeyffMS/win10-webm-cut/issues/1).

**Podstawa:** [audyt F01–F22](https://github.com/KeyffMS/win10-webm-cut/blob/feaad3b50ef494786801160ac456c2f8d0bafe40/docs/audits/2026-10-26/README.md), opublikowany w `feaad3b50ef494786801160ac456c2f8d0bafe40`, dla kodu `a124e98ea9171b2f24e2ea1555801fffd5634580`.

Plan zawiera **1 epik i 22 issues naprawcze**, po jednym na każde ustalenie audytu. Zachowuje priorytety: **4 × P1, 14 × P2, 4 × P3**. Opisy problemów i rozróżnienie KOD / MODEL / RYZYKO / LUKA pochodzą z audytu. Etapy, zależności, granice odpowiedzialności i kryteria odbioru są propozycją wykonawczą opracowaną na jego podstawie, nie nowymi wynikami badań aplikacji.

Utworzenie planu nie oznacza wdrożenia napraw. Nie ustalono terminów ani czasochłonności i nie przypisano wykonawców. Przed rozpoczęciem konkretnego zadania należy wskazać jednego odpowiedzialnego w jego issue.

## 1. SPOT: gdzie jest aktualna informacja

| Informacja | Autorytatywne miejsce |
|---|---|
| Historyczne ustalenie i dowód | Niezmienny audyt wskazany powyżej |
| Zakres naprawy, kryteria, zależności, decyzje, dowody i aktualny stan | Jedno issue Fxx z indeksu poniżej |
| Wspólne zasady wykonania i zakończenie całego przedsięwzięcia | Epik #1 |
| Zalecana kolejność, cel etapów i indeks powiązań | Ten dokument |
| Faktycznie wdrożone zachowanie | Kod wskazany commitem w issue oraz dokumentacja po aktualizacji |

Roadmapa **nie utrzymuje drugiej listy statusów ani kopii kryteriów odbioru**. Przy zmianie szczegółowej zależności najpierw aktualizować właściwe issue; tutaj zmieniać jedynie układ etapów/odnośniki, jeśli przestały odpowiadać planowi. Nie tworzyć równoległych zadań dla tej samej Fxx. Jeżeli konieczny będzie podział, zachować jedno nadrzędne issue jako właściciela ustalenia i jawnie wskazać zakres nowych zadań.

SPOT oznacza jedno źródło reguły/stanu. DRY dotyczy niepowielania wiedzy. SPOOA zachowuje wyłącznie robocze znaczenie z audytu: jeden właściciel operacji, decyzji i cyklu życia; nie deklarujemy zewnętrznego standardu.

## 2. Roadmapa — sześć etapów

Etapy grupują cel prac, a nie tworzą sztywnego wodospadu. Zależności w issues są warunkami integracji i zakończenia. Projekt rozwiązania, modele i szkielety dokumentacji można przygotowywać równolegle. P3 nie powinno opóźniać niezależnego P1/P2.

| Etap | Cel | Zalecana kolejność w obszarze | Warunek zakończenia etapu |
|---|---|---|---|
| **E1 — bezpieczeństwo operacji** | Ochrona istniejącego wyniku, jednoznaczne zakończenie zadań, brak przywracania starego pliku | #2; #3 i #4 mogą powstawać równolegle; następnie integracja #5 | Rozliczone kryteria F01/F02/F03/F13: transakcja wyniku, właściciele operacji, generacje i cleanup. Wspierający F13 pozostaje P2, mimo że jest potrzebny do stabilizacji P1. |
| **E2 — czas i klatki** | Zgodność pozycji, obrazu, etykiet i znaczników | #6 → #7 | Rozliczone F04/F12: jeden kontrakt pozycji, numeracji i zakresu; estymacje są jawne. Wraz z E1 domyka zakres czterech P1. |
| **E3 — wydajność podglądu** | Ograniczenie kolejek, pracy przy seek i rozmiaru klatek | #8 → #9 → #11; #10 niezależnie | Rozliczone F05–F08: ograniczona publikacja klatek, latest-wins nawigacji, budżet rozmiaru i zdarzeniowa pauza. Brak deklaracji pomiarów RAM/CPU. |
| **E4 — profile i crop** | Spójne profile, walidacja i przewidywalny obszar eksportu | #12 → #13; dalej #14 oraz #15; następnie #16. #15 korzysta również z #10 | Rozliczone F09/F10/F11/F14/F15: wspólne profile, kontrakt jakości/strumieni, walidacja, geometria oraz draft/applied crop. |
| **E5 — SPOT, DRY i prostota** | Usunięcie pozostałych duplikacji i nadmiernych odpowiedzialności okna | #17 → #18 → #19, po odpowiednich kontraktach E1/E2/E4 | Rozliczone F16/F17/F18: mapa właścicieli, lekki code-behind, spójny motyw i kończenie gestów. Nie wymaga rewrite ani MVVM/DI. |
| **E6 — dokumentacja i pakowanie** | Instrukcje zgodne z kodem, diagnostyka i jawna zawartość paczki | #20 i #21; finalizacja #22; następnie #23 | Rozliczone F19–F22: pełna macierz dokumentacji, zasady logów/wersji i manifest dystrybucji. To nie jest deklaracja zbudowania lub zweryfikowania binarium. |

### Pierwsze zadania do podjęcia

Zacząć od **#2 (F01)**, ponieważ zapis docelowy wymaga ochrony istniejącego wyniku. **#3 (F02)** i **#4 (F03)** mają niezależne zakresy startowe i mogą być przygotowywane równolegle. Nie odkładać ich do refaktoru #18.

**#10 (F07)** i **#12 (F15)** również nie mają poprzedników w grafie, lecz ich rozpoczęcie nie powinno wypierać napraw P1. **#5 (F13)** scala mechanikę procesów z właścicielami operacji, zamiast tworzyć konkurencyjną obsługę zasobów.

Kolejność numerów **#2–#23** jest jedną poprawną kolejnością sekwencyjnego wykonania początkowego planu. Nie jest jedyną; bieżące zależności należy odczytywać z issues. Nie wyznaczono ścieżki krytycznej w dniach — brak estymat czasu.

## 3. Pełny indeks issues — jedno ustalenie, jeden właściciel

Indeks jest uporządkowany według Fxx, nie kolejności utworzenia. Każde issue zawiera odnośnik do właściwego fragmentu audytu, rodzaj dowodu, zakres i właściciela SPOT, zależności, kryteria implementacji, plan dowodów z sandboxa oraz wyłączenia z zakresu.

| Ustalenie | Priorytet audytu | Etap | Issue — autorytatywny zakres naprawy |
|---|---|---|---|
| F01 | P1 | E1 | [#2 — transakcyjny eksport i ochrona poprzedniego wyniku](https://github.com/KeyffMS/win10-webm-cut/issues/2) |
| F02 | P1 | E1 | [#3 — właściciel sesji podglądu, Task i CTS](https://github.com/KeyffMS/win10-webm-cut/issues/3) |
| F03 | P1 | E1 | [#4 — otwieranie latest-wins i snapshot eksportu](https://github.com/KeyffMS/win10-webm-cut/issues/4) |
| F04 | P1 | E2 | [#6 — wspólna oś czasu obrazu, etykiet i znaczników](https://github.com/KeyffMS/win10-webm-cut/issues/6) |
| F05 | P2 | E3 | [#8 — ograniczona publikacja klatek i własność buforów](https://github.com/KeyffMS/win10-webm-cut/issues/8) |
| F06 | P2 | E3 | [#9 — scalanie żądań przewijania](https://github.com/KeyffMS/win10-webm-cut/issues/9) |
| F07 | P2 | E3 | [#10 — limit obu wymiarów i budżet pikseli podglądu](https://github.com/KeyffMS/win10-webm-cut/issues/10) |
| F08 | P3 | E3 | [#11 — aktualizacja w pauzie bez stałego timera](https://github.com/KeyffMS/win10-webm-cut/issues/11) |
| F09 | P2 | E4 | [#14 — kontrakt jakości, strumieni i zgodności profili](https://github.com/KeyffMS/win10-webm-cut/issues/14) |
| F10 | P2 | E4 | [#15 — wspólna geometria podglądu i cropu](https://github.com/KeyffMS/win10-webm-cut/issues/15) |
| F11 | P2 | E4 | [#16 — draft/applied crop, zatwierdzanie i reset](https://github.com/KeyffMS/win10-webm-cut/issues/16) |
| F12 | P2 | E2 | [#7 — klatki, zakres i skróty klawiatury](https://github.com/KeyffMS/win10-webm-cut/issues/7) |
| F13 | P2 | E1 | [#5 — kontrakt zakończenia procesów](https://github.com/KeyffMS/win10-webm-cut/issues/5) |
| F14 | P2 | E4 | [#13 — walidacja żądania i minimalne wymiary](https://github.com/KeyffMS/win10-webm-cut/issues/13) |
| F15 | P2 | E4 | [#12 — wspólna definicja profili zamiast SelectedIndex](https://github.com/KeyffMS/win10-webm-cut/issues/12) |
| F16 | P2 | E5 | [#18 — MainWindow i niezmienniki stanu](https://github.com/KeyffMS/win10-webm-cut/issues/18) |
| F17 | P3 | E5 | [#17 — resolver narzędzi i usunięcie powielonych reguł](https://github.com/KeyffMS/win10-webm-cut/issues/17) |
| F18 | P3 | E5 | [#19 — motyw, dostępność i cykl interakcji](https://github.com/KeyffMS/win10-webm-cut/issues/19) |
| F19 | P2 | E6 | [#20 — instrukcja tools zgodna z resolverem](https://github.com/KeyffMS/win10-webm-cut/issues/20) |
| F20 | P2 | E6 | [#22 — pełna dokumentacja użytkownika i architektury](https://github.com/KeyffMS/win10-webm-cut/issues/22) |
| F21 | P2 | E6 | [#23 — manifest paczki i dokumenty dystrybucyjne](https://github.com/KeyffMS/win10-webm-cut/issues/23) |
| F22 | P3 | E6 | [#21 — lokalna diagnostyka, wersje i higiena danych](https://github.com/KeyffMS/win10-webm-cut/issues/21) |

## 4. Granice odpowiedzialności między zadaniami

Aby nie naprawiać tego samego na kilka sposobów:

- **Operacje a proces:** #2 posiada transakcję wyniku, #3 sesję podglądu, #4 wybór pliku; #5 jedynie wspólny mechanizm procesu. #17 konsoliduje jego użycie i resolver, nie tworzy kolejnego runnera.
- **Czas a kolejki:** #6 posiada pozycję obrazu, #7 semantykę nawigacji; #8 posiada kolejkę obrazów do UI, a #9 kolejkę żądań seek. To różne zasoby.
- **Profile i crop:** #12 posiada definicje profili, #14 ich kontrakt jakości/strumieni, #13 reguły walidacji, #15 transformację współrzędnych, #16 stan edycji. #18 porządkuje adapter UI dopiero po ustanowieniu tych granic.
- **Dokumentacja:** #20 posiada instrukcję narzędzi/startu, #21 logi/prywatność/wersje, #22 indeks i przewodnik użytkownika/architektury, #23 specyfikację paczki. Rozdziały odsyłają do siebie; nie ma kilku instrukcji z rozbieżnymi regułami.

To mapa powiązań. Szczegółowy zakres i zmiany kontraktu należy prowadzić w odpowiednim issue, nie dopisywać równoległych kryteriów tutaj.

## 5. Decyzje do zamknięcia podczas implementacji

| Decyzja | Issue-właściciel | Ograniczenie wynikające z audytu |
|---|---|---|
| Rzeczywisty PTS/indeks czy jawnie przybliżony protokół podglądu | #6, z nawigacją w #7 | Nie obiecywać precyzji klatek, której źródło pozycji nie zapewnia |
| Układ współrzędnych cropu przed/po rotacji | #15 | Ta sama geometria musi obowiązywać w podglądzie, UI i eksporcie |
| Obsługiwany zakres „bezstratnego” profilu i zachowywane strumienie | #14 | Nie utożsamiać kodeka bezstratnego z zachowaniem całego pliku |
| Minimalne rozmiary i zachowanie zbyt małego cropu | #13 | Zero we wzorze filtra nie może przypadkowo decydować o wyniku |
| tools obok EXE/PATH czy świadome kopiowanie z repo | #17, opis w #20 | Instrukcja musi odpowiadać resolverowi i regułom projektu |
| Paczka z narzędziami czy narzędzia zewnętrzne | #23 | Manifest, pochodzenie i dokumenty muszą odpowiadać realnemu wariantowi |

Są to decyzje wykonawcze do zapisania w issues, nie nowe potwierdzone fakty o aplikacji. Nie narzuca się z góry dodatkowego frameworka ani bibliotek.

## 6. Dowody, zakres weryfikacji i zakończenie

Wspólna definicja zakończenia jest utrzymywana w **epiku #1**. Kryteria w issues są wymaganiami wobec przyszłego kodu, a nie opisem wykonanych napraw. Zamknięcie etapu wymaga rozliczenia właściwych issues z commitem i jawnie ograniczonym materiałem dowodowym.

W tym planie dozwolone są przeglądy statyczne i modele w sandboxie. Nie uruchamiano ani nie planuje się tu GitHub Actions, płatnych usług GitHuba, testów aplikacji, kompilacji lub benchmarków. Commity dokumentacyjne mają `[skip ci] [skip actions]`. Nie zmieniono istniejącego workflow; znaczniki nie są jego trwałym wyłączeniem.

Modele S01–S12 z audytu są punktami wyjścia wskazanymi w issues. Ich rozszerzenia należy wersjonować osobno, z commitem naprawy i założeniami; nie nadpisywać historycznych wyników audytu. Materiał z modeli nie potwierdza działania Windows, WPF, FFmpeg, rzeczywistych systemów plików, jakości obrazu ani zawartości zbudowanej paczki. Takie potwierdzenie pozostaje poza tym zakresem.

### Kontrola kompletności planu

Lokalny model grafu przygotowany 2026-10-26 na podstawie nagłówków utworzonych issues wykazał:

- 22 unikalne Fxx i 22 różne issues naprawcze; brak pominiętych lub nadmiarowych ustaleń względem audytu.
- Zachowane wszystkie priorytety; E1/E2/E3/E4/E5/E6 obejmują odpowiednio 4/2/4/5/3/4 zadania.
- Brak brakujących poprzedników i cykli zależności. Początkowe zadania bez poprzedników: #2, #3, #4, #10 i #12.

To kontrola planu, **nie test aplikacji ani automatyczna synchronizacja z przyszłymi zmianami issues**. Przy zmianach zależności należy ponownie przeanalizować graf. Samo przygotowanie tej roadmapy nie zmieniło kodu C# ani XAML.
