# Kontrakt eksportu i geometrii

Właściciele w kodzie: `ExportProfiles` (stałe profile), `ExportValidation`
(walidacja/plany), `VideoGeometry` (współrzędne), `FfmpegService` (argumenty),
`ExportTransaction` (zatwierdzenie). Treść tego dokumentu nie zastępuje tych
reguł; opisuje ich wersję `0.2.0-audit.1`, bez wyników testów mediów.

## Pięć stabilnych identyfikatorów

| ID | Kontener / obraz | Audio | Rozmiar |
|---|---|---|---|
| Copy | Oryginalne rozszerzenie, jawny mukser, `-c copy` | Copy | Bez zmiany wymiarów |
| Ffv1 | MKV, FFV1 level 3, źródłowy obsługiwany pixel format | **Copy** | Po obrocie/cropie, bez skalowania |
| WebmFull | WebM, libvpx-vp9 CRF 32, good/cpu-used 4/row-mt | libopus 96 kb/s na ścieżkę, 48 kHz | 1:1 po cropie |
| WebmHalf | Jak WebmFull | Jak WebmFull | Oba wymiary /2, w dół do parzystych |
| WebmQuarter | Jak WebmFull | Jak WebmFull | Oba wymiary /4, w dół do parzystych |

Identyfikatory nie są pozycjami ComboBox. Parametry WebM mają jednego właściciela.
Zmiana FFV1 z FLAC na audio copy jest celowa: nie deklarujemy zachowania próbek
float audio po niejawnej konwersji do formatu całkowitoliczbowego. Gdy kopiowany
kodek audio nie jest na dopuszczonej liście MKV, eksport odmawia; nie przechodzi
sam na stratne lub inne bezstratne kodowanie.

## Mapowanie i metadane

**Copy:** wszystkie strumienie `-map 0`, metadane i rozdziały wejścia. Przed startem
każdy strumień musi pasować do konserwatywnej tabeli kontenera w `Containers`.
Dotyczy to również napisów i załączników. Nieznany typ/kodek blokuje Copy zamiast
być pomijany. Obsługiwane rozszerzenia tabeli: MKV, WebM, MP4/M4V, MOV, AVI,
TS/MTS/M2TS, WMV i MPG/MPEG. Lista kodeków w źródle jest autorytatywna;
nie jest to uniwersalna macierz zgodności wszystkich buildów FFmpeg.
Copy nie jest bitowo identycznym plikiem: zmienia kontener/timestamps przy muxowaniu.
Zachowanie wszystkich prywatnych metadanych danego kontenera nie jest gwarantowane.

**Kodowanie:** pierwsze wideo niebędące okładką oraz wszystkie ścieżki audio.
Dodatkowe wideo, okładki, napisy, dane, załączniki, rozdziały i dowolne tagi są
pomijane świadomie; komunikat przed eksportem to wymienia. Obsługiwane wartości
primaries/transfer/colorspace/range są przekazywane jawnie. Nieznanym wartościom
nie nadaje się domyślnego profilu kolorów. Dodatkowe metadane przestrzenne/audio
nieobjęte tym kontraktem nie mają obietnicy zachowania.

WebM koduje do YUV420P 8-bit. Przyjmowane do tego profilu źródła SDR mają formaty
`yuv420p`, `yuv422p`, `yuv444p`, `nv12` albo `nv21`. Nie przyjmuje po cichu RGB,
alpha ani większej głębi; wybór FFV1 nadal wymaga obsługi dokładnego formatu przez
enkoder. Opus wymaga rozpoznanej częstotliwości i od 1 do 8 kanałów. Mapowanie
specjalnych układów kanałów rozstrzyga FFmpeg; błąd przerywa transakcję, nie zmienia
profilu automatycznie.

FFV1 wymusza `+pix_fmt` źródła, blokując automatyczne konwersje formatu. Preflight
czyta deklarowane formaty enkodera. Bezstratność dotyczy obsługiwanych próbek obrazu
**po** cropie/obrocie, nie całego pliku, kolorów odtwarzacza ani odrzuconych pikseli.
Znane HDR/Dolby Vision i gamut BT.2020 są odrzucane przy kodowaniu. FFV1 z obrotem
90/270° odrzuca asymetryczne siatki chromy 422/440/411, zamiast obiecywać ich
bezstratną resamplację. Copy może obsługiwać takie źródła według tabeli kontenera.

## Współrzędne, SAR i wymiary

Podgląd i kodowanie wyłączają autorotację oraz stosują tę samą jawną transformację:
0°, 90°, 180° albo 270° zgodnie z odczytaną orientacją. Dowolne obroty, odbicia,
shear, translation/perspective i nieobsługiwana macierz wyłączają crop/podgląd,
nie są zgadywane. Przy kodowaniu wyjściowa orientacja metadanych jest zerowana,
ponieważ obrót jest już wykonany na pikselach.

X/Y/W/H odnoszą się do wymiarów po obrocie. SAR jest uwzględniany przy proporcjach
podglądu, a nie jako dodatkowe piksele cropu. Dopuszczony SAR mieści się w zakresie
0.01–100. Brak SAR, `N/A` i `0:1` oznaczają przyjęcie kwadratowych pikseli; błędnych
metadanych obrazu nie da się tym naprawić. Rzeczywisty rozmiar bitmapy po
zaokrągleniu jest używany do letterboxingu i mapowania myszy.

Źródło ma limit 32768 pikseli na wymiar i 128 Mi pikseli łącznie. To kontrola
rozmiarów, nie obietnica zmieszczenia dekodera w określonym RAM. Pełny obraz może
mieć 1 piksel lub nieparzysty wymiar, lecz rzeczywisty crop wymaga co najmniej 2×2
oraz parzystych współrzędnych/wymiarów. WebM 1:1 wymaga parzystych wymiarów;
redukcje wyliczają oba wymiary w dół do parzystych. Wynik mniejszy niż 2×2 jest
odrzucany przed FFmpeg — zero nie trafia przypadkowo do filtra scale.

## Bezpieczeństwo i granice gwarancji

Transakcja korzysta ze zwykłego lokalnego pliku docelowego, unikalnego pliku
roboczego w tym samym katalogu, `File.Move(overwrite:false)` lub `File.Replace`.
Nie ma fallbacku „usuń stary i skopiuj nowy”. Reparse target jest odrzucany.
Tożsamość plików jest odczytywana z uchwytu Windows (volume/file ID), uzupełniona
rozmiarem i czasem zapisu; aliasy tego samego wejścia są odrzucane. Źródło nie może
zmienić rewizji między probe a eksportem ani w trakcie eksportu.

Nie jest to blokada transakcyjna wszystkich zewnętrznych programów. Istnieje
okno TOCTOU między kontrolą rewizji a operacją systemową. Identyfikatory plików
mogą być ponownie użyte przez system plików; udział sieciowy lub nietypowy nośnik
może nie dostarczyć wiarygodnej tożsamości. Nie deklaruje się odporności na awarie
zasilania, sprzętu, złośliwe zmiany ani atomowości wszystkich udziałów sieciowych.
Przy braku wymaganej operacji systemowej eksport odmawia zamiast kasować cel.

Weryfikacja wyniku przed zatwierdzeniem to niepusty plik i odczyt podstawowych
metadanych/wymiarów oraz niezmienione źródło. **Nie jest to pełne dekodowanie,
porównanie próbek, kontrola synchronizacji audio ani audyt jakości.** W tej gałęzi
również samego tego kodu nie uruchamiano na rzeczywistych plikach.
