# Licencje i składniki zewnętrzne / Third-party notices

## Polski

**Win10 WebM Cut:** kod źródłowy jest udostępniony na warunkach licencji [MIT](LICENSE), Copyright (c) 2026 KeyffMS.

**.NET / WPF:** aplikacja korzysta z Microsoft .NET i Windows Presentation Foundation. Publikacja self-contained dołącza składniki runtime .NET i Windows Desktop. Dystrybucja musi zawierać licencje/notices odpowiadające **faktycznie użytej** wersji runtime i jego zależnościom.

**FFmpeg/FFprobe:** to zewnętrzne narzędzia, standardowo niedołączane do źródeł. Licencje konkretnej paczki FFmpeg zależą od konfiguracji buildu, skompilowanych bibliotek i zasad redystrybucji. Nie wszystkie buildy FFmpeg mają identyczną licencję. Jeśli dołączasz binaria do paczki, zadbaj o rzeczywiste dane pochodzenia, wersję, konfigurację, notices i wymagane źródła/ofertę źródłową dla tego buildu.

Repozytorium nie pobiera automatycznie licencji ani nie deklaruje, że sama obecność tego pliku rozstrzyga zgodność prawną. Instrukcja: [Pakowanie](docs/PACKAGING.md).

## English

**Win10 WebM Cut:** source code is licensed under [MIT](LICENSE), Copyright (c) 2026 KeyffMS.

**.NET / WPF:** the application uses Microsoft .NET and Windows Presentation Foundation. Self-contained publishing bundles .NET and Windows Desktop runtime components. Redistributed packages must include legal notices and licenses matching the **actual runtime build** and dependencies.

**FFmpeg/FFprobe:** external tools are not bundled with the source by default. Licensing depends on each FFmpeg distribution's build configuration, linked libraries and applicable redistribution obligations. Not all FFmpeg builds have the same license. When distributing these executables, supply actual provenance, versions, configuration, required notices and source compliance information for the specific build.

The repository does not automatically download license texts and this document alone is **not** a legal compliance certification. See [Packaging](docs/en/PACKAGING.md).
