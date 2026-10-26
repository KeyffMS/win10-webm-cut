# Prywatność / Privacy

## Polski

Win10 WebM Cut przetwarza **wskazane lokalne pliki**. Odczytuje metadane przez FFprobe, wyświetla podgląd przez FFmpeg i zapisuje wyniki przez lokalny proces FFmpeg. Nie posiada kont użytkownika, telemetrii, analityki, zdalnego raportowania awarii, automatycznych aktualizacji ani funkcji przesyłania filmów do serwera.

**Dane na dysku:** log jest zapisywany w %LOCALAPPDATA%\Win10WebmCut\Logs\app.log i może zawierać ścieżki, metadane, nazwy plików, fragmenty błędów i stack trace. Pliki robocze eksportu tworzone są w katalogu docelowym i po awarii mogą pozostać. Nie usuwaj pliku źródłowego ani prawidłowego wyniku jako elementu sprzątania.

**Udostępnianie diagnostyki:** funkcja „Kopiuj bez ścieżek” tylko częściowo redaguje dane; nie gwarantuje anonimizacji. Ręczne kopiowanie zaznaczonego tekstu nie redaguje go. Przed publikacją issue sprawdź każdą informację i usuń dane osobowe, sekrety, nazwy prywatnych plików oraz materiały wideo.

Program uruchamia **zewnętrzne binaria** FFmpeg/FFprobe z zainstalowanej lokalizacji. Używaj wyłącznie zaufanych plików. [Diagnostyka](docs/DIAGNOSTICS.md) · [Wymagania](docs/REQUIREMENTS.md).

## English

Win10 WebM Cut processes **user-selected local files**. It reads metadata using FFprobe, provides FFmpeg-based preview and exports using a local FFmpeg process. It has no user accounts, telemetry, analytics, remote crash reporting, automatic updates or built-in video upload.

**On-disk data:** logs are written to %LOCALAPPDATA%\Win10WebmCut\Logs\app.log and may include paths, metadata, filenames, error excerpts and stack traces. Export working files are created in the destination directory and may survive system failure. Never delete source media or a valid destination as a cleanup shortcut.

**Sharing diagnostics:** the “Kopiuj bez ścieżek” (Copy without paths) feature performs limited redaction and **does not guarantee anonymization**. Manual copying is verbatim. Review every excerpt before posting an issue; remove personal information, secrets, private filenames and media.

The application executes **external FFmpeg/FFprobe binaries** from the user's installation; use trusted versions only. [Diagnostics](docs/en/DIAGNOSTICS.md) · [Requirements](docs/en/REQUIREMENTS.md).
