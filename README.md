# Win10 WebM Cut

Lokalny edytor dwóch operacji: przycięcie początku/końca i prostokątny crop.
C# / .NET 10 / WPF, docelowo Windows 10/11 x64. Bez Electrona, kont, telemetrii
ani dodatkowych pakietów NuGet. FFmpeg i FFprobe są zewnętrznymi programami.

## Status tej gałęzi

`fix/audit-remediation-2026-10-26`, wersja źródeł `0.2.0-audit.1`.
Implementacja napraw z [epika #1](https://github.com/KeyffMS/win10-webm-cut/issues/1).
W tym zakresie wykonano wyłącznie przegląd statyczny i modele w sandboxie.
**Nie kompilowano ani nie uruchamiano aplikacji, FFmpeg, testów lub benchmarków.**
Nie jest to potwierdzenie zgodności binarnej, poprawności obrazu, wydajności ani
oficjalnego wsparcia Microsoft dla każdej edycji Windows. Nie opublikowano EXE.
`main` nie jest zmieniany ani scalany przez te naprawy.

## Funkcje i ograniczenia

- Trim używa półotwartego zakresu `[początek, koniec)`. Copy nie koduje ponownie;
  granice zależą od klatek kluczowych i znaczników czasu źródła.
- Crop: ramka/narożniki lub X/Y/W/H, jawne **Zastosuj/Odrzuć**, osobne **Cały obraz**
  i **Obszar środkowy**. Niezastosowane pola blokują eksport.
- Podgląd FFmpeg bez dźwięku: obraz i jego PTS są przyjmowane razem. Numery klatek
  z FPS są zawsze oznaczone `≈`; nie jest to indeks klatek VFR.
- Profile: Copy; FFV1/MKV z **audio copy**; stratny WebM VP9/Opus w wymiarach 1:1,
  1/2 lub 1/4 po cropie. Nie ma gwarancji proporcjonalnie mniejszego pliku.
- Wynik powstaje w pliku roboczym; dopiero udany eksport i kontrola metadanych
  pozwalają zastąpić cel. To nie jest gwarancja odporności każdego systemu plików
  na awarię zasilania, sprzętu lub zewnętrzne zmiany plików.
- HDR/Dolby Vision, nieznana geometria i formaty spoza jawnego kontraktu nie są
  niejawnie konwertowane. Część przypadków pozostaje dostępna tylko w Copy.

## Dokumentacja — jedno miejsce dla każdej reguły

| Temat | Dokument |
|---|---|
| SDK, uruchomienie, FFmpeg/FFprobe, PATH i tools | [BUILD](docs/BUILD.md) |
| Trim, klatki, klawiatura, crop i anulowanie | [USER-GUIDE](docs/USER-GUIDE.md) |
| Kontenery, jakość, ścieżki audio i ograniczenia | [EXPORT-CONTRACT](docs/EXPORT-CONTRACT.md) |
| Właściciele stanu, zasobów i procesów | [ARCHITECTURE](docs/ARCHITECTURE.md) |
| Lokalny log, wersje i zgłaszanie błędów | [DIAGNOSTICS](docs/DIAGNOSTICS.md) |
| Dane lokalne i udostępnianie diagnostyki | [PRIVACY](PRIVACY.md) |
| Przygotowanie paczki i pochodzenie składników | [PACKAGING](docs/PACKAGING.md) |
| Zmiany tej gałęzi | [CHANGELOG](CHANGELOG.md) |
| Licencje składników | [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md), [MIT](LICENSE) |

## Audyt i naprawy — materiały repozytorium

[Historyczny audyt](https://github.com/KeyffMS/win10-webm-cut/blob/feaad3b50ef494786801160ac456c2f8d0bafe40/docs/audits/2026-10-26/README.md) jest niezmiennym opisem starszego
kodu. [Roadmapa](https://github.com/KeyffMS/win10-webm-cut/blob/main/docs/ROADMAP.md) odsyła do autorytatywnych issues F01–F22.
[Raport implementacji i modeli](https://github.com/KeyffMS/win10-webm-cut/blob/fix/audit-remediation-2026-10-26/docs/remediation/2026-10-26/README.md) rozlicza
kryteria zadań i podaje nieweryfikowane aspekty runtime. Te materiały nie wchodzą
do minimalnej paczki użytkownika. Gałąź naprawcza nie zawiera workflow Actions.
