# Profile eksportu i ograniczenia mediów

[Spis dokumentacji](README.md) · [English](en/EXPORT-CONTRACT.md)

Źródłem faktycznych reguł są klasy ExportProfiles, Containers, ExportValidation, VideoGeometry, FfmpegService i ExportTransaction.

## Profile

| ID | Kontener, wideo | Audio | Rozdzielczość |
|---|---|---|---|
| Copy | Oryginalne rozszerzenie/mukser, kopiowanie strumieni | copy | Bez zmian |
| Ffv1 | MKV, FFV1 level 3, źródłowy obsługiwany pixel format | copy | Po cropie i obrocie, bez skalowania |
| WebmFull | WebM, VP9, CRF 32, good/cpu-used 4, row-mt, yuv420p | Opus 96 kb/s na ścieżkę, 48 kHz | 1:1 |
| WebmHalf | Jak WebmFull | Jak WebmFull | Oba wymiary /2 |
| WebmQuarter | Jak WebmFull | Jak WebmFull | Oba wymiary /4 |

Wymiary WebM liczone są **po** ewentualnym cropie i obrocie oraz zaokrąglane w dół do parzystych. Wynik poniżej 2 × 2 jest odrzucany. Nie ma suwaka CRF; parametry profili są stałe.

**Copy** nie przekodowuje obrazu/audio, ale nie daje bitowo identycznego kontenera i nie gwarantuje granicy wybranej klatki. **FFV1** zachowuje obsługiwane próbki obrazu po transformacji; nie gwarantuje zachowania wszystkich innych strumieni. **WebM** jest stratny; rozmiar i czas kodowania zależą od treści.

## Jak traktowane są strumienie

**Copy:** mapowane są wszystkie strumienie, metadane i rozdziały, jeśli są zgodne z konserwatywnymi regułami kontenera. Nieobsługiwany strumień **blokuje** Copy zamiast być pomijanym. Rozpoznawane rozszerzenia: MKV, WebM, MP4/M4V, MOV, AVI, TS/MTS/M2TS, WMV, MPG/MPEG. Szczegółowa lista dopuszczalnych kombinacji znajduje się w Containers.CanCopyStream.

**Kodowanie FFV1/WebM:** wybierane jest pierwsze wideo niebędące okładką i wszystkie ścieżki audio. Pomijane są dodatkowe wideo, okładki, napisy, dane, załączniki, rozdziały i część metadanych. Komunikat przed eksportem o tym ostrzega. Kolorystyka jest zachowywana tylko w zakresie jawnie obsługiwanych informacji.

## Formaty obrazu i dźwięku

WebM przyjmuje wideo **SDR, 8-bit** o źródłowym pixel format: yuv420p, yuv422p, yuv444p, nv12 lub nv21. Wideo wynikowe jest yuv420p — możliwa jest utrata informacji chrominancji. Profil nie zmienia po cichu większej głębi, RGB i alpha na 8-bit. Nie obsługuje automatycznego tone mappingu HDR.

FFV1 wymaga, by konkretny zainstalowany enkoder deklarował obsługę źródłowego formatu pikseli. Nie następuje niejawna konwersja pixel format. Przy obrotach 90°/270° asymetryczne siatki chromy 422/440/411 są odrzucane, zamiast wymuszać resampling.

Audio WebM jest kodowane do **libopus**, 48 kHz, **96 kb/s na ścieżkę**. Akceptowane jest od 1 do 8 kanałów i rozpoznana wejściowa częstotliwość próbkowania; nietypowy układ kanałów może zostać odrzucony przez FFmpeg. Audio FFV1 jest **kopiowane** i wymaga kodeka dopuszczonego do Matroski przez program (m.in. AAC, Opus, Vorbis, MP3, FLAC, AC3, kompatybilne PCM).

## Obrót, SAR i crop

Program obsługuje obroty 0°, 90°, 180° i 270°, stosowane jawnie przy dekodowaniu/eksporcie. Nieznane macierze, odbicia, kąty pośrednie i inne transformacje nie są zgadywane. Kodowana orientacja wyjściowa jest zerowana, ponieważ obrót jest już wykonany na pikselach.

Współrzędne cropu odnoszą się do pikseli **po obrocie, przed korekcją SAR**. SAR wpływa na proporcje wyświetlania, ale nie na współrzędne kadru. Dopuszczalny SAR to 0,01–100; brak, N/A lub 0:1 oznaczają założenie kwadratowych pikseli.

Górny limit geometrii źródła: **32768 pikseli na wymiar i 128 Mi pikseli łącznie**. Faktyczny crop wymaga parzystych X/Y/W/H i obszaru minimum 2 × 2, mieszczącego się w granicach. **Cały obraz** oznacza brak filtra crop, również przy nieparzystej geometrii źródła.

Znane HDR/Dolby Vision i szeroka przestrzeń BT.2020 są odrzucane dla kodowania. Nieobsługiwane źródła mogą pozostać dostępne dla Copy według zgodności kontenera.

## Czas i dokładność

Zakres czasu to **[start, end)**. Koniec równy długości filmu jest granicą EOF. Tryb Copy używa stream copy, więc rzeczywisty punkt cięcia może zależeć od keyframes i timestampów. Liczniki klatek w UI z symbolem ≈ są szacunkami FPS i nie są indeksami pliku VFR.

## Bezpieczny eksport

Źródło ma rejestrowaną rewizję/tożsamość Windows. Program tworzy unikalny plik roboczy w docelowym katalogu, sprawdza niepustość i podstawowe metadane/wymiary, porównuje rewizję źródła, a potem używa Move lub Replace. Nie wykonuje kasowania wcześniejszego celu jako części anulowania. Anulowanie można skutecznie przyjąć do ostatniego punktu przed zatwierdzeniem, lecz nie cofa zamiany już dokonanej.

Takie kontrole nie gwarantują pełnego dekodowania wszystkich klatek, synchronizacji audio, jakości obrazu, odporności na utratę zasilania ani blokady zewnętrznych procesów modyfikujących pliki. Nietypowe systemy plików/udziały mogą nie obsłużyć wymaganych operacji. Dla ważnych materiałów zachowaj oryginał i sprawdź wynik odtwarzaczem.
