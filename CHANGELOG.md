# Historia zmian

## 0.2.0-audit.1 — gałąź naprawcza, 2026-10-26

Stan: implementacja źródłowa oraz analiza statyczna/modele, **bez kompilacji,
testów aplikacji, uruchomienia na Windows i kwalifikowanego binarnego wydania**.

- Eksport do unikalnego pliku roboczego, kontrola rewizji/tożsamości, zatwierdzenie
  Move/Replace zamiast kasowania celu przy anulowaniu.
- Właściciele otwierania i preview: generacje, obserwowane zadania, CTS zwalniane
  po zakończeniu, wspólny runner, jawne błędy/timeout/cleanup.
- Obraz parowany z PTS; osobny cel nawigacji, szacunkowe indeksy ≈, koniec wyłączny,
  Ctrl+strzałka bez wymagania FPS i etykiety przy obu granicach.
- Jedna oczekująca klatka i żądanie; stały limit startów seek, brak głodzenia
  krótkiej operacji przez autorepeat, budżet obu wymiarów, bez idle timera.
- Profile o stabilnych identyfikatorach, wspólna walidacja, jawne reguły formatów,
  geometrii i metadanych. Nieobsługiwane przypadki są odrzucane, nie zgadywane.
- **FFV1 zmienia audio z FLAC na copy**, aby nie obiecywać bezstratnej niejawnej
  konwersji próbek. WebM pozostaje stratny; jest jawnie ograniczony do SDR YUV 8-bit.
- Crop ma draft/applied, blokadę eksportu niezastosowanych zmian oraz osobne
  akcje pełnego obrazu i środka. Mapowanie uwzględnia rotację/SAR/bitmapę/DPI.
- Kontroler stanu odciąża MainWindow. Jedna paleta obejmuje komunikaty, focus,
  disabled, suwaki, scrollbar i tooltip; gesty mają ścieżki utraty capture.
- Lokalna diagnostyka z retencją i korelacją, instrukcje prywatności, mapowanie
  dokumentacji oraz jawne reguły przygotowania paczki i pochodzenia składników.
- Workflow Actions usunięty **tylko z gałęzi naprawczej**. `main` zachowany.

Szczegółowe dowody: [raport napraw](https://github.com/KeyffMS/win10-webm-cut/blob/fix/audit-remediation-2026-10-26/docs/remediation/2026-10-26/README.md).
Historyczny audyt i jego modele nie zostały przepisane jako wyniki nowej wersji.
