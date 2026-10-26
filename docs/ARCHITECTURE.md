# Architektura i właściciele reguł

Mała aplikacja WPF bez dodatkowego frameworka MVVM/DI. Code-behind jest adapterem
widoku, a nie właścicielem procesów, czasu lub kasowania plików. Podział wynika z
konkretnych napraw, nie z próby budowy uniwersalnego silnika multimedialnego.

## SPOT / DRY / KISS

| Reguła lub zasób | Jedyny właściciel | Konsumenci |
|---|---|---|
| Stany i dopuszczalne działania edytora | EditorController | MainWindow |
| Ostatni wybór pliku / generacja otwarcia | LatestOperation<string> w kontrolerze | MediaProbe, UI |
| Sesja podglądu, aktywny Task/CTS, jedno oczekujące żądanie | LatestOperation<PreviewRequest> | PreviewDecoder, kontroler |
| Proces, exit, czytniki, anulowanie i timeout | ToolProcess | MediaProbe, PreviewDecoder, ToolCapabilities, FfmpegService |
| Wyszukiwanie binariów | ToolResolver | ToolProcess, ToolCapabilities, diagnostyka |
| Wersje/możliwości enkodera i muksera | ToolCapabilities | preflight eksportu |
| Rewizja/tożsamość pliku Windows | FileRevision | probe, transakcja, usługa eksportu |
| Niezmienny opis źródła | VideoInfo + StreamInfo | kontroler, decoder, walidator |
| Żądana i faktycznie pokazana pozycja | Timeline | kontroler i etykiety |
| Sekundy, numeracja szacunkowa, kroki | TimePolicy | kontroler, walidator, GUI |
| Parowanie BGRA z rzeczywistym PTS | PreviewDecoder | LatestFramePresenter |
| Jedna oczekująca klatka/callback | LatestFramePresenter | kontroler i adapter bitmapy |
| Budżet rozmiaru preview | PreviewSizing | kontroler |
| Obrót/SAR i transformacja myszy | VideoGeometry | preview, UI, eksport |
| Draft oraz zatwierdzony crop | CropEditor | kontroler, snapshot |
| Definicje pięciu profili | ExportProfiles / Containers | lista UI, plan, preflight, argumenty |
| Walidacja żądania i rozmiar wynikowy | ExportValidation | snapshot i granica usługi |
| Budowanie argumentów eksportu | FfmpegService | ToolProcess |
| Plik roboczy i rozstrzygnięcie commit/cancel | ExportTransaction | FfmpegService; nigdy UI |
| Kolory/stany kontrolek | zasoby App.xaml | okna/dialogi |
| Dziennik/retencja/redakcja | AppLog | właściciele operacji, dialog diagnostyczny |

SPOOA ma wyłącznie robocze znaczenie przyjęte w audycie: jeden właściciel operacji,
decyzji i cyklu życia. Nie deklarujemy zewnętrznego standardu o tym skrócie.

## Stany edytora

`Empty → Loading → Ready ↔ Playing`; eksport przechodzi `Ready/Playing → Exporting
→ Ready`; zamknięcie z dowolnego stanu przechodzi do terminalnego `Closing`.
Błąd aktywnego otwarcia daje Empty, błąd podglądu Ready z komunikatem. Stary wynik
nie może przejść do żadnego nowego stanu. Loading pozwala wybrać nowszy plik.
Exporting blokuje otwieranie, edycję i odtwarzanie, pozostawiając Cancel.
Closing blokuje wszystkie nowe operacje i callbacki do okna.

`CanOpen/CanEdit/CanPreview/CanMark/CanExport` należą do kontrolera. UI je wyświetla,
a handlery kontrolera sprawdzają ponownie. `MainWindow` obsługuje jedynie dialogi,
focus, gesty myszy, przypisanie wartości kontrolek i WritePixels do powtarzalnej
bitmapy WPF. Szczegóły gestu (punkt zaczepienia/capture) są stanem widoku, nie
konkurencyjną definicją kadru eksportu.

## Generacje, kolejki i kończenie pracy

LatestOperation ma jeden worker, jeden aktywny CTS/Task i bounded channel rozmiaru
1. `RequestLatest` zastępuje oczekujące żądanie. Otwieranie nowego pliku, zatrzymanie
odtwarzania i zamknięcie natychmiast unieważniają prezentację i anulują operację.
CTS jest zwalniany dopiero po `await` pracy oraz cleanup jej procesu/czytników.
Stop można powtórzyć; nie ma .Wait/.Result na wątku UI.

Dla kolejnych **nieruchomych** klatek tej samej sesji nie przerywa się bez końca
aktywnego dekodera podczas autorepeat. Pozwala się zakończyć aktualnemu krótkiemu
żądaniu, a jedno oczekujące przechowuje najnowszy cel. Generacja prezentacji i
najnowsza generacja żądania są rozróżnione. Obraz aktywnej generacji ma prawdziwy
PTS, ale nie kasuje napisu „Żądano” dla nowszego celu. Po zakończeniu aktywnego
żądania startuje najnowsze, nie cała historia ruchów. Maksymalna częstotliwość
startów wynika z nazwanej przerwy 80 ms; przerwa nie jest przesuwana przy każdym
nowym zdarzeniu. Limit pojedynczego still/probe to 20 s, nie obietnica responsywności.

Publikacja obrazu ma inny zasób: jeden wymienialny slot klatki i jeden oczekujący
callback Dispatchera. Starszy slot jest odrzucany. BGRA jest nową tablicą na klatkę,
która po przekazaniu nigdy nie jest modyfikowana przez producenta. UI kopiuje ją do
WriteableBitmap ponownie używanej do czasu zmiany rozmiaru. Świadomie nie wdrożono
puli tablic bez pełnej umowy zwrotu. Nie ma nieograniczonej kolejki obrazów ani
obietnicy zerowych alokacji. Przepustowość/model bajtów nie oznaczają working setu.

PreviewDecoder łączy n-ty raw frame z n-tym wpisem showinfo. Weryfikuje indeks,
monotoniczny czas i skończony zakres. Brak znacznika, ucięta/zerowa klatka lub niezerowy exit są błędem, nie sukcesem.
Kolejka PTS ma pojemność 64; zapełnienie wstrzymuje czytnik stderr (back-pressure),
a anulowanie przerywa także ten zapis. Nie gubimy czasów i nie przerywamy
podglądu wyłącznie dlatego, że bardzo małe klatki wyprzedziły prezentację. Playback jest taktowany
różnicami tych PTS na workerze, a nie osobnym zegarem w GUI. Przyjęcie obrazu i
jego pozycji odbywa się w tej samej operacji Dispatchera. W pauzie nie ma timera.

## Procesy i awarie cleanup

ToolProcess używa bezwzględnej ścieżki, ArgumentList i `UseShellExecute=false`.
Stdout jest binarny w preview lub ograniczony do 2 Mi znaków w trybie tekstowym.
Stderr: 48 końcowych wierszy, po maksymalnie 4096 znaków; jest czytany równocześnie.
Anulowanie tylko sygnalizuje workerowi — nie wykonuje Kill synchronicznie w UI.
Błąd któregokolwiek czytnika, token lub deadline rozpoczyna zatrzymanie niezależnie
od tego, czy Task.WhenAll już się skończył. Sukces wymaga zakończenia obu odczytów,
procesu i kodu wyjścia 0.

Probe/still: 20 s; capability call: 15 s; oczekiwanie cleanup po żądaniu Kill: 5 s.
Te liczby są polityką kodu, nie pomiarem lub twardym limitem wywołań systemowych.
Po przekroczeniu cleanup nie zwalnia się zasobów nadal używanych przez czytniki.
Przejmuje je obserwator końcowy, a dalsze uruchamianie narzędzi zostaje zablokowane
w tej instancji aplikacji. Błędy są logowane, przy zamknięciu pojawia się ostrzeżenie.
Nie gwarantujemy, że Windows zawsze pozwoli zakończyć zewnętrzny proces.

## Eksport i ograniczenia

Snapshot tworzy się **przed** modalnym wyborem wyniku. Zawiera niezmienne źródło
z rewizją, zakres, zatwierdzony crop i kanoniczny profil. Kontroler kończy preview
przed uruchomieniem eksportu. Dysk/probe/encoder pracują poza UI. Preflight,
argumenty, kontrola metadanych i rewizji są częścią callbacku transakcji.
Zamiana wyniku ma ostatni punkt anulowania przed Move/Replace; nie ma kasowania
celu w obsłudze wyjątku lub Cancel. Szczegóły: [EXPORT-CONTRACT](EXPORT-CONTRACT.md).

## Dowody i nieweryfikowane granice

[Raport napraw](https://github.com/KeyffMS/win10-webm-cut/blob/fix/audit-remediation-2026-10-26/docs/remediation/2026-10-26/README.md) wiąże kryteria issues z symbolami
i wynikami modeli. Modele nie implementują WPF, .NET CancellationTokenSource,
FFmpeg, systemu plików ani schedulera Windows. Sprawdzenie składni XML nie jest
kompilacją XAML/C#; nie stwierdzamy działania wiązań, PTS, dostępności, DPI lub
jakości wyjścia bez osobnej weryfikacji runtime.
