# FAQ — pytania i odpowiedzi

[Spis dokumentacji](README.md) · [English](en/FAQ.md)

### Czy potrzebuję internetu lub konta?

Nie. Program pracuje lokalnie na wskazanych plikach. Zewnętrzne FFmpeg i FFprobe użytkownik instaluje sam.

### Jak uruchomić aplikację bez SDK?

Wydanie zależne od .NET wymaga **.NET 10 Desktop Runtime**; wydanie self-contained zawiera runtime. W obu wariantach wymagane są FFmpeg i FFprobe.

### Dlaczego program nie widzi ffmpeg.exe z repo/tools?

Program szuka tools **obok uruchomionego EXE** lub w bezwzględnych katalogach PATH. Folder repo/tools nie jest kopiowany automatycznie.

### Czy Copy jest klatkowo dokładne?

Nie gwarantuje dowolnej granicy klatki. Copy nie rekompresuje strumieni, ale granice mogą zależeć od klatek kluczowych i timestampów. Nie jest to również binarna kopia całego pliku.

### Czy FFV1 zachowuje wszystkie dane?

FFV1 zachowuje obraz bezstratnie w obsługiwanym formacie **po cropie i obrocie**. Dźwięk jest kopiowany, jeśli kodek jest dopuszczony; dodatkowe wideo, napisy i część metadanych nie są zachowywane.

### Czy 1/2 szerokości i wysokości daje plik 4× mniejszy?

Nie ma takiej gwarancji. To około 1/4 liczby pikseli, ale finalny rozmiar zależy od zawartości, jakości i audio.

### Dlaczego numer klatki ma znak ≈?

Jest wyliczony z FPS, a nie z pełnego indeksu wszystkich klatek. W źródłach VFR takie liczby są jedynie przybliżeniem.

### Czy podgląd ma audio lub tone mapping HDR?

Nie. Podgląd pokazuje wideo bez dźwięku. HDR jest wyłączony w podglądzie, a nie automatycznie zamieniany na SDR.

### Co dzieje się przy anulowaniu?

Program próbuje bezpiecznie zatrzymać eksport i zachować istniejący cel. Zamiana już zatwierdzona nie jest cofana. Po awarii systemu może pozostać plik roboczy.

### Czy jest automatyczny aktualizator lub instalator?

Aplikacja nie pobiera aktualizacji. Repozytorium zawiera źródła i instrukcje własnej publikacji; dostępność gotowej paczki należy sprawdzić osobno w zakładce Releases.
