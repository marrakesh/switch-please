# Switch Please

[![CI](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml/badge.svg)](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Ein Tastaturlayout-Umschalter für Windows, im Geiste von Punto Switcher. Korrigiert Text,
den Sie im falschen Layout getippt haben — per Tastenkürzel oder automatisch.

**[English](README.md)** · **[Русская](README.ru.md)** · **[Українська](README.uk.md)** · **[Čeština](README.cs.md)**

## Was es tut

| | |
|---|---|
| **Shift ×2** | Korrigiert das letzte Wort und wechselt das Layout |
| **Strg ×2** | Korrigiert die Auswahl, oder die ganze Zeile, wenn nichts ausgewählt ist |
| Rückgängig | Stellt die letzte Korrektur zurück. Standardmäßig nicht belegt |
| Automatisch | Standardmäßig aus, einzuschalten im Tray-Menü |

Alle drei Tastenkürzel sind frei belegbar; der Dialog nimmt auf, was Sie tatsächlich
drücken. Rücktaste löscht eine Belegung. Oberfläche auf Englisch, Russisch und Ukrainisch,
Hell oder Dunkel nach der Windows-Einstellung.

## Beispiele

| Herausgekommen | Gemeint | |
|---|---|---|
| `Yeit` | Zeit | Deutsch, US-Layout aktiv |
| `Ywiebel` | Zwiebel | Deutsch, US-Layout aktiv |
| `tzpisch` | typisch | Deutsch, US-Layout aktiv |
| `sch;n` | schön | Deutsch, US-Layout aktiv |
| `Gr;-e` | Größe | Deutsch, US-Layout aktiv |
| `zes` | yes | Englisch, deutsches Layout aktiv |
| `siye` | size | Englisch, deutsches Layout aktiv |

Für Deutsch gibt es kein eingebautes Sprachmodell — es gibt eines nur für Russisch,
Ukrainisch und Englisch. Deutsch wird stattdessen anhand des **Windows-Wörterbuchs**
beurteilt, und ohne dieses Wörterbuch enthält sich der Umschalter, statt zu raten. Zu
installieren über Einstellungen → Zeit und Sprache → Sprache und Region → Sprache
hinzufügen, mit angehaktem „Grundlegende Eingabe". Welche Wörterbücher vorhanden sind, zeigt
*Status and latency...*.

Absichtlich unangetastet bleiben: Wörter unter drei Zeichen, Wörter mit Ziffern, Pfade
(`C:\Windows`), Adressen (`user@example.com`) und `camelCase`.

## Installation

Von den [Releases](https://github.com/marrakesh/switch-please/releases) herunterladen und
starten. Kein Installer, nichts einzurichten.

| Datei | Größe | Benötigt |
|---|---|---|
| `SwitchPlease.exe` | ~52 MB | nichts |
| `SwitchPlease-runtime-required.exe` | ~0,4 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| `SwitchPlease-arm64.exe` | ~50 MB | nichts, auf einem ARM-Rechner |
| `SwitchPlease-arm64-runtime-required.exe` | ~0,4 MB | die ARM64-Desktop-Runtime |

Die Dateien sind **nicht signiert**, daher warnt SmartScreen beim ersten Start. Jedes Release
enthält eine `SHA256SUMS.txt`, erzeugt vom selben Lauf, der auch die Dateien gebaut hat:
`certutil -hashfile SwitchPlease.exe SHA256`.

**Ohne Administratorrechte** starten, sonst erreichen die Korrekturen gewöhnliche Fenster
nicht mehr. Erfordert Windows 10 oder neuer.

## Datenschutz

**Keinerlei Netzwerkanfragen.** Keine Telemetrie, keine Analyse, keine Absturzberichte. Die
einzige Stelle, die einen Socket öffnet, ist die Update-Prüfung: Sie fragt GitHub nach der
neuesten Release-Nummer, übermittelt nichts über Sie und ist **standardmäßig aus**.

Getipptes verlässt den Rechner nicht und wird standardmäßig nicht auf die Festplatte
geschrieben: Das Protokoll hält Entscheidungen fest, der Text ist auf seine Länge reduziert.
Die Wörter selbst erscheinen nur, wenn Sie *Write the typed text to the log* einschalten.

## Einstellungen

Das Wesentliche steht im Tray-Menü, die Zahlen unter *Settings...*, alles zusammen in
`%APPDATA%\SwitchPlease\settings.json`. Die vollständige Beschreibung jedes Parameters steht
[in der englischen Fassung](README.md#settings).

## Unterstützen

Das Programm ist kostenlos und bleibt es. Wenn es Ihnen genug Tipparbeit erspart hat:
[Ko-fi](https://ko-fi.com/marrakeshgtp) oder
[Buy Me a Coffee](https://www.buymeacoffee.com/marrakesh).

## Weiter

Die vollständige Dokumentation ist das [englische README](README.md): die beiden Modi, wo
sich der Umschalter heraushält, die gemessene Erkennungsqualität, alle Einstellungen und
Einschränkungen. Wie es innen aufgebaut ist und warum, steht in
[docs/internals.md](docs/internals.md). Fehlerberichte und Pull Requests:
[CONTRIBUTING.md](CONTRIBUTING.md).

## Lizenz

[MIT](LICENSE) © Oleksii Ozerov
