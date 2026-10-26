# Rozliczenie implementacji F01–F22

**Gałąź:** `fix/audit-remediation-2026-10-26`. **Baza:**
`51b014ed9810e7d9e44b39322377bdc581be7be5`.
**Zakres:** issues [#2–#23](https://github.com/KeyffMS/win10-webm-cut/issues/1),
po jednym właścicielu każdego ustalenia, według kolejności roadmapy.
**Wersja źródeł:** `0.2.0-audit.1`.

Kod przygotowano w sandboxie. Poniżej rozlicza się implementację i jej analizę,
**nie pomyślne działanie aplikacji**. Nie wykonano kompilacji C#/XAML, testów,
uruchomienia WPF, FFmpeg/FFprobe, dekodowania filmu, benchmarku, renderowania GUI,
PowerShella ani `dotnet publish`. Nie ma zweryfikowanego binarium lub archiwum
użytkowego. Źródła i dokumentacja nie zostały scalone do `main`.

Workflow usunięto tylko na gałęzi naprawczej. Commity zawierają `[skip ci]
[skip actions]`. Nie otwierano PR ani nie używano Actions lub usług płatnych.
Historyczny [audyt](../../audits/2026-10-26/README.md) oraz jego S01–S12 pozostają
niezmienione. Nowe modele M01–M17 nie zastępują ich wyników.

## Dowody i znaczenie zakończenia

- [simulate.py](simulate.py) i [simulation-results.json](simulation-results.json):
  17 deterministycznych modeli na danych syntetycznych. Brak uruchamiania/importu
  aplikacji, połączeń sieciowych, mutacji mediów i werdyktu „testy przeszły”.
- [inspect_source.py](inspect_source.py) i [static-inspection.json](static-inspection.json):
  rzeczywisty odczyt źródeł, poprawność XML, tekstowe powiązania handlerów,
  odwołania do zasobów, pliki publikacji/linki oraz SHA256/git-blob kodu.
  To **nie rozwiązuje typów C#, API platformy ani semantyki bindingów XAML**.
- Każde issue otrzymuje odnośnik do konkretnego commitu implementacji i tej sekcji.
  Historyczne kryteria oraz status pozostają w issue jako SPOT, nie w drugim
  edytowalnym backlogu. Zakończenie w tym zleceniu oznacza zakres źródłowo-modelowy
  z jawnymi ograniczeniami poniżej, nie certyfikację produkcyjną.

## E1 — bezpieczeństwo operacji

<a id="issue-2"></a>

### Issue #2 / F01 — transakcyjny eksport

Właściciel: [ExportTransaction](../../../src/Win10WebmCut/ExportTransaction.cs),
[FileRevision](../../../src/Win10WebmCut/FileRevision.cs). Integracja w FfmpegService.

1. Unikalny plik roboczy ma prawdziwe rozszerzenie i jest rezerwowany przez
   CreateNew w katalogu celu; encoder otrzymuje również jawny mukser.
2. Istniejący cel nie jest otwierany do zapisu podczas kodowania/preflight.
   Sukces procesu, metadane i niezmieniona rewizja poprzedzają Move/Replace.
3. Jedynym `File.Delete` kodu jest cleanup własnego pliku roboczego. UI nie usuwa
   celu; zamknięcie przechodzi przez kontroler anulowania.
4. Normalizacja ścieżki i identyfikacja pliku po uchwycie odrzucają wejście jako
   wynik, także aliasy/hard linki. Brak zgody na istniejący cel zatrzymuje start.
5. Ostatnia kontrola tokenu przed zamianą definiuje zwycięzcę cancel/commit.
   Nie ma kasowania celu ani fallbacku delete/copy po błędzie zamiany.
6. Kontrakt dokumentuje TOCTOU, tożsamość plików, sieć/nośniki i brak gwarancji
   power-loss durability. Nie deklaruje systemowej atomowości na każdym nośniku.

Dowód modelowy: **M01**, stary/nowy cel, odmowa, cancel, błąd encoder/verify/replace,
zmiana celu i anulowanie po commit. Model słownika nie wykonuje operacji dyskowych.

<a id="issue-3"></a>

### Issue #3 / F02 — właściciel sesji i CTS

Właściciel: [LatestOperation](../../../src/Win10WebmCut/LatestOperation.cs),
[EditorController](../../../src/Win10WebmCut/EditorController.cs).

1. Jeden przechowywany worker, aktywny CTS i zastępowalny slot żądania; nie ma
   porzuconego Task.Run dla każdej klatki/kroku.
2. Stop unieważnia generację przed Cancel. Await pracy/cleanup poprzedza Dispose;
   rejestracja sygnału procesu nie wykonuje blokującego Kill w wątku UI.
3. Stop jest powtarzalne, a DisposeAsync idempotentne. Cancel i zwolnienie aktywnego
   CTS są serializowane pod jednym gate.
4. Błędy pracy, w tym przestarzałej generacji, są obserwowane i logowane. Nie są
   globalnie tłumione przez `Handled=true`.
5. Closing blokuje nowe operacje, zatrzymuje obu właścicieli i eksport, a callbacki
   sprawdzają generację/closing/shutdown Dispatchera.
6. UI nie używa `.Wait()` ani blokującego `.Result`. Oczekiwanie jest asynchroniczne.

**M02/M03/M04** pokazują porządek i stany, nie implementację synchronizacji .NET.
Po timeout cleanup nadal pracujące czytniki mają odrębnego właściciela; zasoby
nie są zwalniane w ciemno. Ostrzeżenie runtime pozostaje możliwe i jest opisane.

<a id="issue-4"></a>

### Issue #4 / F03 — ostatnie otwarcie i snapshot

Właściciel: loader kontrolera i niezmienne VideoInfo/ExportRequest.

1. Nowy wybór zastępuje oczekujący, anuluje aktywny probe i unieważnia jego wynik.
2. Kontynuacje sukcesu/błędu w UI sprawdzają generację i closing.
3. A→B→C nie przywraca A po C; źródło, crop, zakres i komunikat mają jeden owner.
4. Błąd aktualnego probe pozostawia Empty; anulowany wynik nie ustawia Ready.
5. Snapshot eksportu powstaje przed modalnymi dialogami. Zawiera source revision,
   zakres, zatwierdzony kadr i kanoniczny profil, niezależne od kontrolek.
6. Zamknięcie nie dopuszcza późnego dialogu na zamkniętym właścicielu.

Dowód: **M03** oraz odczyt call flow Open/CreateExportSnapshot/ExportAsync.

<a id="issue-5"></a>

### Issue #5 / F13 — proces i czytniki

Właściciel: [ToolProcess](../../../src/Win10WebmCut/ToolProcess.cs).

1. Sukces, anulowanie, timeout, start/exit/cleanup mają odrębne wyniki/wyjątki.
2. Preview sprawdza pełną klatkę, PTS, EOF i kod wyjścia; zero klatek jest błędem.
3. Stderr jest równoczesny/ograniczony; raw stdout pozostaje binarny. Odczyt
   tekstowy ma limit. Pełne dane media nie trafiają do StringBuilder.
4. Failure/stop/deadline wygrywa WhenAny przed potencjalnie niekończącym się WhenAll.
   Po Kill są anulowanie czytników, oczekiwanie i kontrolowane zwalnianie.
5. Nazwane polityki: probe/still 20 s, capabilities 15 s, cleanup wait 5 s.
   Timeout cleanup zatrzymuje dalsze starty i pozostawia właściciela obserwującego
   zasoby. To nie twardy limit wywołania jądra Windows.
6. Jest jeden ProcessStartInfo/ArgumentList/UseShellExecute=false. Brak sukcesu
   przed zakończeniem wszystkich zadań. PID/błędy cleanup są logowane.

Dowód: **M02** i statyczny owner procesów. Brak uruchomienia procesu w sandboxie.

## E2 — czas i klatki

<a id="issue-6"></a>

### Issue #6 / F04 — obraz i pozycja

Właściciele: [PreviewDecoder](../../../src/Win10WebmCut/PreviewDecoder.cs), Timeline,
LatestFramePresenter. Wybrano **rzeczywisty PTS z showinfo**, nie nowy zegar UI.

1. Raw frame jest parowany z indeksem i PTS showinfo; ma generację publikacji.
2. Obraz oraz czas prezentacji są zatwierdzane w jednej operacji Dispatchera.
   Czas żądany jest osobnym polem widocznym podczas oczekiwania.
3. Oba znaczniki biorą czas Presented. Są blokowane podczas oczekiwania na nowszy cel.
4. Stop/zmiana źródła/closing odrzucają callbacki starej prezentacji. EOF/error
   kończy Playing; przy zmianie seek nie działa niezależny licznik czasu.
5. Interpretacja copyts/start_at_zero i showinfo jest jawna; indeksy obliczane z
   FPS nadal są szacunkami, nie PTS ani prawdziwym indeksem VFR.
6. Ostrzeżenie keyframe Copy nie jest usuwane przez poprawę nawigacji.

Dowód: **M04/M05**, statyczne parowanie. Poprawność współpracy konkretnego FFmpeg
z showinfo/seek wymaga osobnej późniejszej weryfikacji runtime.

<a id="issue-7"></a>

### Issue #7 / F12 — klatki, zakres i klawisze

Właściciel: [TimePolicy/Timeline](../../../src/Win10WebmCut/Timeline.cs).

1. Indeksy obrazu 0-based są ograniczone do N−1; granica końca może wynosić N.
2. Zakres ma `[start,end)`; przycisk nazywa się „Koniec przed tą klatką”.
3. Ctrl przesuwa o sekundę przed sprawdzeniem FPS. Zwykłe/Shift używają 1/FPS,
   10/FPS, a brak FPS daje komunikat bez fikcyjnej wartości.
4. Etykiety i opis skrótów mają `≈` oraz ostrzeżenie VFR.
5. Sekundy i estymacje są przy pozycji, początku i końcu.
6. Focus pól/list, Alt/Windows, zakres i Copy są opisane; sam kod nie przechwytuje
   standardowych strzałek edytora tekstu.

Dowód: **M05**: 30 i 30000/1001 FPS, brak FPS, zero i EOF. Seek jest żądaniem
czasowym, nie obietnicą wyboru dokładnie sąsiedniej klatki zmiennego FPS.

## E3 — ograniczenia pracy i pamięci

<a id="issue-8"></a>

### Issue #8 / F05 — ograniczona publikacja

[LatestFramePresenter](../../../src/Win10WebmCut/LatestFramePresenter.cs) utrzymuje
jeden slot obrazu i jeden oczekujący callback; nowy obraz zastępuje slot. Clear,
Dispose i generacja odrzucają stary stan. Producent przekazuje nową tablicę BGRA,
której później nie nadpisuje. MainWindow ponownie wykorzystuje WriteableBitmap
przy tym samym rozmiarze. Nie zastosowano niebezpiecznej puli bez umowy zwrotu.
Przy odrzuceniu obrazu nie aktualizuje się jego czasu. **M06** pokazuje 36 publikacji
wstrzymanego UI, jeden slot i 2 073 600 bajtów maksymalnego surowego payloadu;
nie jest to rozmiar całego procesu ani zmierzona oszczędność RAM. Osobny model
kolejki 64 znaczników PTS uwzględnia back-pressure producenta bez odrzucania
czasów; anulowanie kończy także oczekujący zapis do kanału.

<a id="issue-9"></a>

### Issue #9 / F06 — scalanie seek

LatestOperation ma jeden zastępowalny cel i jeden aktywny request, nie listę Tasków.
Co najmniej 80 ms między startami jest stałym limitem, nie przesuwanym debounce.
Still może się skończyć mimo napływu nowszych celów, aby nie głodzić dekodera;
przyjęty obraz nie kasuje nowszego celu. Playback i zmiana pliku anulują od razu.
Przed Start jest kontrola tokenu/generacji, cleanup pracuje poza UI.
**M07**: 41 syntetycznych żądań co 30 ms, założone 130 ms dekodowania, 11 startów,
11 zakończeń, maksymalnie 1 aktywne + 1 oczekujące, ostatni cel 40 zaprezentowany.
Te liczby należą do modelu, nie pomiaru FFmpeg.

<a id="issue-10"></a>

### Issue #10 / F07 — geometria budżetu

[PreviewSizing](../../../src/Win10WebmCut/PreviewSizing.cs) ma limit 960×540,
518 400 pikseli i 2 073 600 bajtów BGRA. Walidacja poprzedza alokację; stride/bytes
są checked. Używa proporcji/SAR i viewport DIP×DPI z bezpiecznym fallbackiem
przed layoutem. Wynik jest rzeczywistym rozmiarem przekazywanym do mapowania myszy.
Resize aktualizuje tylko politykę następnego żądania, nie uruchamia dekodera.
**M08** obejmuje panoramę, 4K, pion, 960×10000, 1×1, SAR i DPI. Zaokrąglenie może
zmienić proporcję poniżej jednego piksela; wynik/błąd są zapisane w JSON.

<a id="issue-11"></a>

### Issue #11 / F08 — pauza bez timera

Nie ma DispatcherTimer w nowych źródłach. Puste okno/pauza otrzymują aktualizacje
wyłącznie z działań/stanu. Odtwarzanie jest taktowane PTS na workerze i kończy się
na EOF/error/Stop. Subskrypcje zakładane są raz na instancję kontrolera/okna.
**M14** plus inspekcja odwołań potwierdzają konstrukcję kodu; nie przeliczamy tego
na zmierzony CPU, temperaturę ani energię.

## E4 — kontrakty profili i kadru

<a id="issue-12"></a>

### Issue #12 / F15 — profile bez indeksów

[ExportProfiles](../../../src/Win10WebmCut/ExportProfiles.cs) definiuje pięć
stabilnych ID, nazwy, rozszerzenia/muksery, skale, crop i argumenty kodeków.
UI wybiera obiekt, nie SelectedIndex. Copy wyznacza rozszerzenie z wejścia.
Definicję konsumują plan, dialog i preflight. Podstawowe WebM CRF/cpu-used nie
zostały zmienione samym refaktorem; jawne formaty i audio są decyzją F09 poniżej.
**M11** pokazuje odwrócenie listy bez zmiany semantyki.

<a id="issue-13"></a>

### Issue #13 / F14 — walidator domenowy

[ExportValidation](../../../src/Win10WebmCut/ExportValidation.cs) jest wywoływany
przy snapshocie i na granicy usługi. Sprawdza finite time, `[start,end)`, profil,
rozmiar, crop i zgodność Copy. Sumy/budżety używają long/checked. X/Y są
nieujemne, nie „dodatnie”; W/H rzeczywistego cropu ≥2 i parzyste. Pełny obraz
jest stanem bez cropu także dla 1px/odd. Redukcja z wynikiem <2 jest odrzucana
zamiast przekazywać zero do scale. **M12** obejmuje brzegi, nieparzystość, wielkie
wartości, 2/6px, skale i niepoprawny czas. To model decyzji, nie walidacja enkodera.

<a id="issue-14"></a>

### Issue #14 / F09 — jakość i strumienie

Autorytatywny [EXPORT-CONTRACT](../../EXPORT-CONTRACT.md) opisuje wszystkie pięć
profili. Copy mapuje wszystkie dopuszczone strumienie; transcode pierwsze
niebędące okładką wideo i całe obsługiwane audio, jawnie pomijając inne składniki.
Znane tagi koloru są przekazywane, nieznane nie są zgadywane. HDR i BT.2020 nie
są niejawnie konwertowane. WebM przyjmuje określony SDR YUV 8-bit i zapisuje
YUV420P/Opus; FFV1 wymaga dokładnego pixel format i kopiuje audio. Brak formatu,
enkodera/muksera lub reguły strumienia przerywa przed zatwierdzeniem.

**Decyzja świadomie zmienia zachowanie:** audio FFV1 nie jest konwertowane do FLAC.
Uzasadnienie: bezstratność wszystkich rodzajów próbek audio nie była wcześniej
ustalona. Skala oznacza wymiary po cropie, nie objętość pliku. Kontrakt nie obiecuje
identyczności całego pliku/kolorów ani obsługi wszystkich wariantów kontenera.
**M11/M12/M16** oraz statyczny preflight; brak negocjacji kodeków lub pomiaru jakości.

<a id="issue-15"></a>

### Issue #15 / F10 — geometria wspólna

[VideoGeometry](../../../src/Win10WebmCut/VideoGeometry.cs) opisuje współrzędne po
jawnej rotacji, przed rozciągnięciem SAR. Probe odczytuje i waliduje macierz/SAR;
nieobsługiwane transformacje są odrzucane zamiast zgadywane. Preview i eksport
mają wyłączoną autorotację i te same filtry obrotu. Myszy służy rzeczywisty rozmiar
bitmapy oraz ViewRect, nie odtworzony niezależnie wzór 960px. Pola/narożniki trafiają
do jednego walidatora. **M09** obejmuje 0/90/180/270°, SAR 1/4:3, DPI 1/2 i czarne
pasy, pokazując punkty przed zaokrągleniem. Nie jest to rendering WPF.

<a id="issue-16"></a>

### Issue #16 / F11 — draft/applied

[CropEditor](../../../src/Win10WebmCut/CropEditor.cs) ma jeden draft i Applied.
UI pokazuje dirty; ramka pozostaje zatwierdzonym obszarem. Eksport i zmiana trybu
wymagają Apply/Discard, a nie użycia starego kadru po cichu. Walidacja błędnego
tekstu nie zmienia Applied. Rysowanie/narożniki korzystają z tego samego SetApplied.
FullImage jawnie usuwa draft i crop; CentralArea to oddzielna akcja. Źródło zmienia
model, a tryb zachowuje applied (pomijany w trim). X/Y=0 i odd full image są zgodne
z F14. **M10/M12** pokazują te stany.

## E5 — pozostałe SPOT/DRY i interfejs

<a id="issue-17"></a>

### Issue #17 / F17 — jeden resolver i reguły

[ToolResolver](../../../src/Win10WebmCut/ToolResolver.cs) jest jedynym miejscem
wyboru tools obok EXE/PATH. Ignoruje CWD, względne elementy i aliasy powłoki.
Wspólny ToolProcess posiada start/kill/stderr; TimePolicy posiada czas, a
ExportValidation/PreviewSizing geometrię walidacji/budżetu. Mapa właścicieli w
[ARCHITECTURE](../../ARCHITECTURE.md) obejmuje wszystkich konsumentów. **M13**
pokazuje niezmienność wyboru przy zmianie CWD. Brak automatycznego instalatora.

<a id="issue-18"></a>

### Issue #18 / F16 — kontroler zamiast reguł w oknie

[EditorController](../../../src/Win10WebmCut/EditorController.cs) definiuje
Empty/Loading/Ready/Playing/Exporting/Closing, dopuszczalne czynności i snapshot.
MainWindow adapteruje eventy, dialogi i gesty, nie startuje/ubija procesów ani
nie usuwa wyników. UI i handlery kontrolera konsumują te same właściwości Can… .
Eksport kończy preview i blokuje dalsze odtwarzanie aż do zakończenia. Usunięto
stare pole-timery, Player/MediaElement i SelectedIndex. Nie dodano DI/MVVM/NuGet.
**M14** i statyczny call flow/owner inventory dokumentują tę granicę.

<a id="issue-19"></a>

### Issue #19 / F18 — ciemny motyw i gesty

[App.xaml](../../../src/Win10WebmCut/App.xaml) jest właścicielem kolorów i stanów
Button/TextBox/ComboBox/Radio/Slider/ScrollBar/ToolTip/ProgressBar. Pozostałe XAML
odwołują się do tokenów. DarkDialog używa tych samych semantic brushes; ma
zaznaczalny tekst, kopię z pomocniczą redakcją, nazwy dostępności, Enter/Esc i close.
Utrata capture kończy/anuluje gest, zmiana pliku/trybu/resize usuwa rubberband,
CropHint wynika ze stanu po każdym otwarciu. WrapPanels, dzielone przestrzenie
oraz przewijana sekcja kontrolek ograniczają wyjście poza obszar małego okna.

**M17**, odwołania XML/handlerów/tokenów. Przy skrajnej skali, gdy obszar roboczy
ma mniej niż 640×480 DIP, minimum okna nie mieści się w modelu: nie deklarujemy
pełnej dostępności każdego ekranu. Nie wykonano kontroli wizualnej, klawiatury
z czytnikiem ekranu ani per-monitor DPI. Systemowe file dialogs pozostają systemowe.

## E6 — dokumentacja i paczka

<a id="issue-20"></a>

### Issue #20 / F19 — tools i start

[BUILD](../../BUILD.md) jest jedynym opisem resolvera. tools/README odsyła do niego.
Osobne przykłady repo/output/publish nie zakładają kopiowania repo/tools.
Rozdzielono SDK, runtime i self-contained; podano prawidłowe --project, kolejność
ścieżek, preflight i brak kwalifikowanej listy wersji narzędzi. Nie umieszczono
prywatnej ścieżki użytkownika. **M13** + statyczne odnośniki i csproj.

<a id="issue-21"></a>

### Issue #21 / F22 — diagnostyka i wersjonowanie

[AppLog](../../../src/Win10WebmCut/AppLog.cs): ID operacji z AsyncLocal, UTC/PID,
wyjątki z ToString do limitu wpisu, rotacja 1 MiB +3, brak rekurencji przy błędzie
zapisu. Diagnostyka używa rzeczywistych metadanych assembly/runtime i odczytanych
wersji narzędzi; brak informacji jest jawny. Redakcja jest pomocnicza, bez uploadu.
.gitignore obejmuje logi/dumpy/media/binaria bez wykluczenia audytu/modeli.
CHANGELOG, BUILD, PRIVACY i DIAGNOSTICS rozdzielają obowiązki. global.json stosuje
stabilny SDK 10.0 z latestFeature, bez deklarowania testowanej zgodności.
**M15** i statyczny przepływ logowania; nie zapisano logu prawdziwej aplikacji.

<a id="issue-22"></a>

### Issue #22 / F20 — pełna mapa dokumentacji

| Wiersz audytu | Kanoniczne miejsce / właściciel kontraktu |
|---|---|
| Cel, zakres, stabilność | README + ten raport |
| SDK/runtime, wymagania, start | BUILD / #20 |
| Publikacja i paczka | PACKAGING / #23 |
| Trim, granice, klatki, skróty | USER-GUIDE; TimePolicy / #6–#7 |
| Crop, narożniki, draft/reset | USER-GUIDE; CropEditor / #15–#16 |
| Podgląd bez audio, PTS, budżet | USER-GUIDE + ARCHITECTURE / #6, #8–#11 |
| Profile, formaty, kolor, audio | EXPORT-CONTRACT / #12–#14 |
| Nadpisanie/anulowanie | EXPORT-CONTRACT + USER-GUIDE / #2 |
| Stan, odpowiedzialności, procesy | ARCHITECTURE / #3–#5, #17–#18 |
| Diagnostyka, wersje, logi | DIAGNOSTICS + BUILD + CHANGELOG / #21 |
| Prywatność i publikowanie danych | PRIVACY / #21 |
| Licencje/proweniencja | THIRD-PARTY-NOTICES, LICENSE, PACKAGING / #23 |
| Weryfikacja/ograniczenia | ten raport + README; jawne runtime-unverified |

Instrukcje opisują faktyczny kod, w tym odmowy i zmianę audio, nie samą roadmapę.
Historyczny audyt pozostaje niezmieniony. Inspekcja odnośników obejmuje dokumenty
nowej wersji; pliki historyczne są zachowane w bazowym drzewie Git.

<a id="issue-23"></a>

### Issue #23 / F21 — manifest i lokalna procedura

[package-manifest.json](../../package-manifest.json) rozdziela EXE, dokumenty,
notices runtime i opcjonalne narzędzia. `.csproj` ma jawne CopyToOutput/Publish
oraz ExcludeFromSingleFile. [Prepare-Package.ps1](../../../scripts/Prepare-Package.ps1)
nie wykonuje builda, narzędzi, pobierania ani GitHuba; przyjmuje gotowy output oraz
rzeczywiste wersje/pochodzenie/notices. Odrzuca nieznane pliki, istniejący cel,
reparse points, brak proweniencji i kompletów dokumentów. Nowy folder roboczy
zawiera hash/rozmiar każdego dołączonego pliku i PACKAGE-CONTENTS.json; cleanup
nie usuwa istniejącej paczki. Nie kopiuje prywatnych logów/mediów ani symboli.

**M16** oraz statyczne reguły projektu/PowerShell. Skrypt nie był wykonywany;
nie istnieje zweryfikowana paczka. Walidacja obecności notices nie certyfikuje
licencji ani zgodności prawnej wszystkich zależności.

## Wyniki inspekcji i ograniczenia wspólne

Po przygotowaniu raportu uruchomiono ponownie modele i skrypt inspekcji. Liczby,
hashe, referencje i rezultaty są w JSON, nie są przepisywane ręcznie jako druga
baza statusów. W źródłach nie ma MediaElement, SelectedIndex ani DispatcherTimer;
File.Delete ma wyłącznie właściciela ExportTransaction, Process.Start wyłącznie
ToolProcess. Poprawny XML i znaleziony handler **nie oznaczają pomyślnej kompilacji**.

Pozostają nieweryfikowane: typowanie/wiązania C#/XAML, faktyczne PTS/seek oraz
preflight w konkretnym FFmpeg, native file identity/Replace na konkretnym nośniku,
porządek callbacków Windows, jakość i synchronizacja pliku, DPI/czytnik ekranu,
rzeczywista zawartość publikacji i wydajność. Te granice nie są ukryte jako
„testy do zrobienia, które już przeszły”. Ukończono zakres implementacji i
weryfikacji **uzgodniony dla sandboxa**, nie kwalifikację wydania.

## Odtworzenie samych modeli

Z katalogu tego raportu:

```powershell
python .\simulate.py > .\simulation-results.local.json
python .\inspect_source.py > .\static-inspection.local.json
```

Skrypty nie wymagają .NET, FFmpeg, pakietów Pythona ani sieci. Nie uruchamiają
aplikacji. Wymagają bieżącego drzewa źródeł do odczytu manifestu i inspekcji.
Wyniki `.local.json` są lokalne i ignorowane w Git.

## Pomocnicza dokumentacja kontraktu FFmpeg

Przy statycznym przeglądzie argumentów odczytano [oficjalne doc/ffmpeg.texi](https://github.com/FFmpeg/FFmpeg/blob/ceb466cbcd7f2366d2dc738cda02e47f0ac59f7d/doc/ffmpeg.texi#L2410-L2548).
Reguły `map_metadata` rozdzielają metadane globalne i strumieniowe; kodowanie
wyłącza oba automatyczne mapowania (`-map_metadata -1`, `-map_metadata:s -1`).
Opis `copyts/start_at_zero` stanowi podstawę wspólnej osi PTS po seek. Odczyt
dokumentacji nie jest odtworzeniem zachowania konkretnego zainstalowanego FFmpeg.
