# Składniki i dystrybucja

Kod Win10 WebM Cut: MIT, Copyright (c) 2026 KeyffMS — pełny tekst w LICENSE.
Paleta nawiązuje do projektu SightAdapt tego samego właściciela. Nowa implementacja
nie kopiuje kodu LosslessCut i nie opisuje tej aplikacji jako jego binarnego forka.

Projekt korzysta z .NET / WPF. Nie deklaruje dodatkowych zależności NuGet, lecz
publikacja self-contained zawiera składniki runtime i jego zależności. Dokładne
wersje, licencje i notices muszą odpowiadać użytemu runtime oraz Windows Desktop
Runtime, nie dowolnemu tekstowi pobranemu z nowszej wersji projektu.

FFmpeg/FFprobe to zewnętrzne narzędzia, domyślnie niedostarczane z repozytorium.
Ich warianty kompilacji, zewnętrzne biblioteki i opcje mogą zmieniać obowiązki
redistribucji. Nie należy zakładać jednej licencji dla każdego builda FFmpeg.
Do wariantu paczki z narzędziami trzeba dołączyć informacje o pochodzeniu,
konfiguracji, wersjach, notices i sposobie spełnienia wymagań źródłowych
właściwego buildu. Ten dokument nie zastępuje pełnych licencji ani oferty źródeł.

Procedura [PACKAGING](docs/PACKAGING.md) wymaga dostarczenia rzeczywistych danych
i dokumentów przed przygotowaniem folderu paczki. Nie pobiera ich automatycznie,
nie fabrykuje potwierdzeń i nie certyfikuje zgodności prawnej. W tym zakresie nie
zbudowano, nie pobrano ani nie opublikowano żadnego binarium.
