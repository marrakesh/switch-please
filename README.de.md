# Switch Please

[![CI](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml/badge.svg)](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/marrakesh/switch-please)](https://github.com/marrakesh/switch-please/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-support-FF5E5B?logo=kofi&logoColor=white)](https://ko-fi.com/marrakeshgtp)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy_Me_a_Coffee-support-FFDD00?logo=buymeacoffee&logoColor=black)](https://www.buymeacoffee.com/marrakesh)

Korrigiert Text, den Sie im falschen Tastaturlayout getippt haben. Aus `Ywiebel` wird
`Zwiebel`, aus `zes` wird `yes`, und das Layout wird umgeschaltet, damit Sie gleich
weitertippen können.

Ein kostenloses Open-Source-Programm für Windows 10 und 11, das im Infobereich sitzt.
Deutsch und jede andere Sprache, für die Windows ein Rechtschreibwörterbuch hat, werden
anhand dieses Wörterbuchs beurteilt; Russisch, Ukrainisch und Englisch haben ein eigenes
Modell.

**[English](README.md)** · **[Русская](README.ru.md)** · **[Українська](README.uk.md)** · **[Čeština](README.cs.md)**

![Getippt wird „kurye Yeit“, zweimal Strg, und es wird „kurze Zeit“](docs/demo.de.svg)

- **Zwei Tastenkürzel.** Zweimal Shift korrigiert das letzte Wort, zweimal Strg die Auswahl
  oder die ganze Zeile. Gleich danach noch einmal zweimal Shift, und das Wort ist zurück.
- **Vorsichtig von Haus aus.** Die automatische Korrektur ist aus, bis Sie sie wollen, und so
  abgestimmt, dass sie korrekten Text in Ruhe lässt.
- **Hält sich heraus** aus Passwortfeldern, Spielen, Terminals und Code-Editoren.
- **Privat.** Keine Telemetrie, kein Netzwerkzugriff, solange Sie die Update-Prüfung nicht
  einschalten, und nichts Getipptes landet auf der Festplatte.
- **Keine Administratorrechte.** Installiert sich nur für Sie oder läuft als einzelne Datei.

## Was es tut

| Tasten | Was passiert |
|---|---|
| **Shift ×2** | Korrigiert das letzte Wort und wechselt das Layout. Sofort noch einmal gedrückt, stellt es das Wort wieder her |
| **Strg ×2** | Korrigiert die Auswahl, oder die ganze Zeile, wenn nichts ausgewählt ist, und wechselt das Layout |
| Rückgängig | Stellt die letzte Korrektur zurück, auch eine umgewandelte Auswahl. Standardmäßig nicht belegt |
| Automatisch | Korrigiert jedes Wort, sobald es fertig getippt ist. Standardmäßig aus |

Shift ×2 arbeitet mit der Aufzeichnung dessen, was Sie getippt haben, und verwirft sie,
sobald der Cursor dorthin springt, wohin sie ihm nicht folgen kann: Enter, Tab, Pfeiltasten,
Pos1/Ende, Esc und jeder Mausklick. Ist der Cursor schon woanders, markieren Sie den Text und
drücken Sie zweimal Strg.

Vergessenes Caps Lock wird gleich mit korrigiert: Aus `hALLO` wird `Hallo`, und Caps Lock
wird ausgeschaltet. Erkannt wird das an Shift: Bei eingeschaltetem Caps Lock hält Shift nur,
wer nicht weiß, dass es an ist — absichtlich ohne Shift getippte Großbuchstaben bleiben also
stehen. Die automatische Korrektur tut das ebenfalls, wenn sie eingeschaltet ist.

*Layout am Textcursor anzeigen* im Tray-Menü blendet bei jedem Layoutwechsel — ob von Ihnen
oder durch eine Korrektur — für eine Sekunde ein Kürzel wie `DE` oder `EN` unter dem Cursor
ein. Es verschwindet beim ersten Tastendruck oder Klick; wie lange es sonst bleibt, steht unter
*Einstellungen...*. Dafür muss die Anwendung verraten, wo
ihr Cursor steht: gewöhnliche Windows-Programme, Office und die Browser tun das; Anwendungen,
die ihren Text selbst zeichnen — manche Electron-Editoren, Terminals —, nicht, und dort
erscheint nichts. Standardmäßig aus.

Eine **automatische** Korrektur rückgängig zu machen, lehrt sie außerdem etwas: Das Wort
kommt auf eine Liste, die die automatische Korrektur fortan in Ruhe lässt — ein Nachname, ein
Login, ein Wort in einer Sprache ohne Modell. Eine Benachrichtigung nennt das Wort, und unter
*Einstellungen...* → *Diese Wörter nie automatisch korrigieren* lässt sich die Liste lesen und
bearbeiten. Die Tastenkürzel wirken weiterhin auf diese Wörter.

Alle drei Tastenkürzel sind unter *Tastenkürzel...* frei belegbar; der Dialog nimmt auf, was
Sie tatsächlich drücken — zweimal Shift, Strg oder Alt oder eine gewöhnliche Kombination wie
`Strg+Shift+L`. Rücktaste löscht eine Belegung. Die automatische Korrektur schaltet *Falsches
Layout automatisch erkennen* im Tray-Menü ein, und *In … automatisch korrigieren* schaltet sie
für die aktuelle Anwendung ein oder aus.

Oberfläche auf Deutsch, Englisch, Russisch, Ukrainisch und Tschechisch — nach der
Windows-Anzeigesprache oder wie unter *Sprache* gewählt. Hell oder dunkel nach der
Windows-Einstellung.

## Beispiele

| Herausgekommen | Gemeint | |
|---|---|---|
| `Yeit` | Zeit | Deutsch, US-Layout aktiv |
| `Ywiebel` | Zwiebel | Deutsch, US-Layout aktiv |
| `tzpisch` | typisch | Deutsch, US-Layout aktiv |
| `sch;n` | schön | Deutsch, US-Layout aktiv |
| `Gr;-e` | Größe | Deutsch, US-Layout aktiv |
| `yEIT` | Zeit | Deutsch, US-Layout aktiv, Caps Lock vergessen |
| `hALLO` | Hallo | Caps Lock vergessen, das Layout stimmt |
| `zes` | yes | Englisch, deutsches Layout aktiv |
| `siye` | size | Englisch, deutsches Layout aktiv |

Für Deutsch gibt es kein eingebautes Sprachmodell — es gibt eines nur für Russisch,
Ukrainisch und Englisch. Deutsch wird stattdessen anhand des **Windows-Wörterbuchs**
beurteilt, und ohne dieses Wörterbuch enthält sich der Umschalter, statt zu raten. Zu
installieren über Einstellungen → Zeit und Sprache → Sprache und Region → Sprache
hinzufügen, mit angehaktem „Grundlegende Eingabe". Welche Wörterbücher vorhanden sind, zeigt
*Status und Latenz...*.

Die automatische Korrektur lässt absichtlich in Ruhe: Wörter unter drei Zeichen, Wörter mit
Ziffern, Pfade (`C:\Windows`), Adressen (`user@example.com`) und `camelCase`. Die
Tastenkürzel sind an diese Regeln nicht gebunden — ein Druck darauf ist eine Bitte —, aber
auch sie lassen Text stehen, der sich schon deutlich besser liest als jede Alternative. Ein
versehentliches doppeltes Shift verdirbt deshalb kein richtig getipptes Wort.

## Installation

Aus dem [neuesten Release](https://github.com/marrakesh/switch-please/releases/latest)
herunterladen und den Installer starten:

| Installer | Größe | |
|---|---|---|
| **`SwitchPlease-Setup.exe`** | ~47 MB | die meisten Rechner |
| `SwitchPlease-Setup-arm64.exe` | ~45 MB | ARM-Rechner |

Er installiert nur für Sie und verlangt keine Administratorrechte, bietet den Start mit
Windows an und fragt beim Deinstallieren, ob Ihre Einstellungen bleiben sollen.

Oder nehmen Sie die Anwendung für sich allein: nichts wird installiert, und außerhalb von
`%APPDATA%\SwitchPlease` wird nichts geschrieben:

| Portabel | Größe | Benötigt |
|---|---|---|
| `SwitchPlease.exe` | ~52 MB | nichts |
| `SwitchPlease-runtime-required.exe` | ~0,4 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| `SwitchPlease-arm64.exe` | ~50 MB | nichts, auf einem ARM-Rechner |
| `SwitchPlease-arm64-runtime-required.exe` | ~0,4 MB | die ARM64-Desktop-Runtime |

Erfordert Windows 10 oder neuer. **Ohne Administratorrechte** starten, sonst erreichen die
Korrekturen gewöhnliche Fenster nicht mehr.

Die Dateien sind **nicht signiert**, daher warnt SmartScreen beim ersten Start — *Weitere
Informationen → Trotzdem ausführen*. Jedes Release enthält eine `SHA256SUMS.txt`, erzeugt vom
selben Lauf, der auch die Dateien gebaut hat:

```powershell
Get-FileHash .\SwitchPlease-Setup.exe -Algorithm SHA256
```

Die Signierung über die SignPath Foundation wird gerade eingerichtet; was, von wem und wie
signiert wird, steht in der [Code-Signing-Richtlinie](docs/code-signing.md) (auf Englisch).

Nach dem Start sitzt das Programm im Infobereich, und sein Symbol zeigt das aktuelle Layout.
Ist es nicht zu sehen, liegt es unter dem Pfeil ^ und lässt sich von dort auf die Taskleiste
ziehen.

## Datenschutz

**Keine Netzwerkanfragen, solange Sie keine wollen.** Keine Telemetrie, keine Analyse, keine
Absturzberichte. Die einzige Stelle, die einen Socket öffnet, ist die Update-Prüfung: Sie
fragt GitHub nach der neuesten Release-Nummer, übermittelt nichts über Sie und ist
**standardmäßig aus**.

Getipptes verlässt den Rechner nicht, und standardmäßig wird außer den Einstellungen nichts
auf die Festplatte geschrieben. Der einzige getippte Text, der in den Einstellungen landen
kann, ist ein Wort, auf das Sie selbst gezeigt haben: Wer eine automatische Korrektur
rückgängig macht, setzt das Wort auf die Liste „nie korrigieren“; eine Benachrichtigung sagt
das, und unter *Einstellungen...* lässt sich die Liste ansehen und bereinigen. Ein Protokoll entsteht nur, solange *Hook-Latenz messen*
eingeschaltet ist; es hält Entscheidungen fest, der Text ist auf seine Länge reduziert. Die
Wörter selbst erscheinen nur, wenn Sie *Getippten Text ins Protokoll schreiben* einschalten.

## Einstellungen

Das Wesentliche steht im Tray-Menü, die Zahlen unter *Einstellungen...*, alles zusammen in
`%APPDATA%\SwitchPlease\settings.json`; vor dem Bearbeiten von Hand das Programm beenden. Die
vollständige Beschreibung jedes Parameters samt Standardwert steht
[in der englischen Fassung](README.md#settings).

## Unterstützen

Das Programm ist kostenlos und bleibt es. Wenn es Ihnen genug Tipparbeit erspart hat:

<a href="https://ko-fi.com/marrakeshgtp"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Ko-fi" height="40"></a>
<a href="https://www.buymeacoffee.com/marrakesh"><img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&amp;emoji=&amp;slug=marrakesh&amp;button_colour=FFDD00&amp;font_colour=000000&amp;font_family=Cookie&amp;outline_colour=000000&amp;coffee_colour=ffffff" alt="Buy Me a Coffee" height="40"></a>

Ko-fi behält bei einer einmaligen Spende nichts ein, Buy Me a Coffee fünf Prozent.

## Weiter

Die vollständige Dokumentation ist das [englische README](README.md): die beiden Modi, wo
sich der Umschalter heraushält, die gemessene Erkennungsqualität, alle Einstellungen, was zu
tun ist, wenn es nicht funktioniert, und die Einschränkungen. Wie es innen aufgebaut ist und
warum, steht in [docs/internals.md](docs/internals.md). Fehlerberichte und Pull Requests:
[CONTRIBUTING.md](CONTRIBUTING.md).

## Lizenz

[MIT](LICENSE) © Oleksii Ozerov
