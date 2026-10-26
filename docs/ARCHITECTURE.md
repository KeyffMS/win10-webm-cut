# Architektura aplikacji

[Spis dokumentacji](README.md) · [English](en/ARCHITECTURE.md)

## Stos i przepływ

Win10 WebM Cut jest aplikacją C# / .NET 10 / WPF. Nie korzysta z Electrona ani dodatkowego frameworka MVVM/DI. MainWindow odpowiada za widok i zdarzenia WPF, EditorController za stan, a wyspecjalizowane klasy za media, procesy i eksport.

~~~text
MainWindow.xaml/.cs -> EditorController -> Timeline / CropEditor
                              |               VideoGeometry
                              +-> LatestOperation -> PreviewDecoder
                              |                        |
                              |                 LatestFramePresenter -> WPF
                              +-> ExportValidation / ExportProfiles
                                         |
                                    FfmpegService
                                         |
                                    ToolProcess -> FFmpeg/FFprobe
                                         |
                                   ExportTransaction
~~~

## Właściciele reguł

| Obszar | Moduł |
|---|---|
| Stan edytora, otwieranie, eksport i zamykanie | EditorController |
| Zdarzenia WPF, gesty myszy, focus, dialogi | MainWindow |
| Metadane i strumienie | MediaProbe / VideoInfo / StreamInfo |
| Żądana i przedstawiona pozycja, kroki nawigacji | Timeline / TimePolicy |
| Jeden aktywny worker, nowsze żądania, generacje | LatestOperation |
| Dekodowanie BGRA i PTS | PreviewDecoder |
| Jeden slot oczekującej klatki | LatestFramePresenter |
| Limit pikseli podglądu | PreviewSizing |
| Obrót, SAR, współrzędne obrazu | VideoGeometry |
| Szkic i zatwierdzony crop | CropEditor |
| Stałe profile, lista kontenerów | ExportProfiles / Containers |
| Walidacja czasu, profilu i wymiarów | ExportValidation |
| Argumenty i uruchomienie eksportu | FfmpegService |
| Plik roboczy i jego zatwierdzenie | ExportTransaction |
| Tożsamość plików i rewizje Windows | FileRevision |
| Proces, stdout, stderr, anulowanie, timeout | ToolProcess |
| Narzędzia i dostępne enkodery/muksery | ToolResolver / ToolCapabilities |
| Lokalny log, retencja, komunikaty diagnostyczne | AppLog |

## Stany i operacje

~~~text
Empty -> Loading -> Ready <-> Playing
                   |
                   +-> Exporting -> Ready
Dowolny stan -> Closing
~~~

Nieudane otwarcie daje Empty, a błąd podglądu Ready z komunikatem. Kolejny wybór podczas Loading zastępuje poprzedni. W Exporting zablokowane są edycja, otwieranie i odtwarzanie, pozostaje anulowanie. Closing blokuje nowe operacje. EditorController sprawdza CanOpen, CanEdit, CanPreview, CanMark i CanExport, również gdy GUI wyświetla przycisk jako niedostępny.

## Podgląd, kolejki i wątki

LatestOperation ma jeden worker, jedną aktywną operację i jeden wymienny cel oczekujący. Każdy wniosek otrzymuje generację, aby spóźniony wynik nie zastępował nowego. Dla szybkiego przewijania scala się oczekujące cele; nieruchomy aktywny seek może się zakończyć przed nowszym. Minimalny odstęp startów wynosi 80 ms, bez resetowania go przy każdym zdarzeniu.

PreviewDecoder łączy surową klatkę BGRA z rzeczywistym PTS z showinfo, kontrolując kolejność i kompletność. Wspólna aktualizacja bitmapy i PTS odbywa się na Dispatcher WPF. LatestFramePresenter ma tylko jeden wymienny slot klatki i jeden oczekujący callback; nie utrzymuje kolejki wszystkich obrazów. Rozmiar podglądu jest limitowany do 960 × 540 i 518 400 pikseli. Playback kieruje się PTS, bez stałego timera odświeżania GUI w pauzie.

## Uruchamianie zewnętrznych procesów

ToolResolver znajduje FFmpeg/FFprobe w tools obok EXE lub w bezwzględnych katalogach PATH. ToolCapabilities sprawdza wymagane muksery, enkodery i formaty. ToolProcess używa ścieżki bezwzględnej, ArgumentList i UseShellExecute=false oraz równoczesnego odczytu stdout/stderr.

Limity zwykłych odczytów metadanych/nieruchomych klatek wynoszą 20 s, operacji capabilities 15 s, a oczekiwania cleanup po sygnale zakończenia 5 s. Nie są to gwarantowane limity systemu operacyjnego. Przy niepotwierdzonym zakończeniu procesu kolejne użycie narzędzi może zostać zablokowane w tej instancji.

## Eksport i bezpieczeństwo

Przed dialogiem zapisu tworzony jest niezmienny ExportRequest. Praca kodeka i plików jest poza wątkiem UI. ExportTransaction tworzy tymczasowy plik wynikowy w katalogu celu, weryfikuje jego podstawowe właściwości i kontrolę źródła, a następnie przenosi lub zastępuje cel. Odrzucone albo anulowane operacje nie powinny usuwać poprzedniego pliku docelowego. Granice gwarancji i zasady jakości są w [Kontrakcie eksportu](EXPORT-CONTRACT.md).

## Rozwijanie projektu

Reguły profile/kodeki utrzymuj w ExportProfiles i ExportValidation, geometrię w VideoGeometry, lokalizację narzędzi w ToolResolver, a cykl życia procesów w ToolProcess. Zmieniając zachowanie, aktualizuj równolegle odpowiednie strony dokumentacji PL i EN. Nie dubluj logiki pomiędzy MainWindow a kontrolerem.
