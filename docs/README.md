# Dokumentacja — Win10 WebM Cut

**Polski** · [English](en/README.md) · [Repozytorium](../README.md)

Win10 WebM Cut jest lokalnym edytorem wideo dla Windows 10/11 x64, napisanym w C# i WPF (.NET 10). Obsługuje przycinanie początku i końca filmu, prostokątny crop oraz pięć profili eksportu. Interfejs programu jest obecnie po polsku.

## Dokumenty

| Temat | Dokument |
|---|---|
| System, .NET, FFmpeg/FFprobe i zgodność | [Wymagania](REQUIREMENTS.md) |
| Kompilacja, uruchomienie i publikacja | [Budowanie](BUILD.md) |
| Podgląd, nawigacja, crop i eksport | [Instrukcja użytkownika](USER-GUIDE.md) |
| Profile, kontenery, audio, geometria i ograniczenia | [Kontrakt eksportu](EXPORT-CONTRACT.md) |
| Najczęstsze problemy | [FAQ](FAQ.md) |
| Logi i zgłaszanie błędów | [Diagnostyka](DIAGNOSTICS.md) |
| Struktura kodu i cykl życia operacji | [Architektura](ARCHITECTURE.md) |
| Lokalna dystrybucja i licencje runtime | [Pakowanie](PACKAGING.md) |
| Dane lokalne i logi | [Prywatność](../PRIVACY.md) |
| Licencje projektu i narzędzi | [Informacje o składnikach](../THIRD-PARTY-NOTICES.md) |

## Jak rozpocząć

Zainstaluj .NET 10 SDK oraz FFmpeg i FFprobe zgodnie z wymaganiami, zbuduj projekt według instrukcji budowania, a następnie w programie wybierz **Otwórz wideo…**, ustaw czas/crop i profil oraz użyj **Eksportuj**.
