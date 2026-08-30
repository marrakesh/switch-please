# Switch Please

[![CI](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml/badge.svg)](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Přepínač klávesnicového rozložení pro Windows v duchu Punto Switcheru. Opraví text napsaný
ve špatném rozložení — klávesovou zkratkou nebo automaticky.

**[English](README.md)** · **[Русская](README.ru.md)** · **[Українська](README.uk.md)** · **[Deutsch](README.de.md)**

## Nejdřív to podstatné pro češtinu

Česká diakritika sedí na číselné řadě: `ě` je tam, kde má americké rozložení `2`, `š` na
`3`, `č` na `4` a tak dál. Slovo s diakritikou tedy dopadne s číslicí uvnitř — a slova
obsahující číslice přepínač **zásadně nechává být**, protože jinak by přepisoval hesla,
verze a cesty k souborům. Toto pravidlo běží dřív než jakékoli vyhodnocování a nedá se
obejít.

Prakticky to znamená:

| Napsané | Zamýšlené | |
|---|---|---|
| `yima` | zima | **opraví** — jen prohozené y/z |
| `jayzk` | jazyk | **opraví** |
| `ynovu` | znovu | **opraví** |
| `d2kuji` | děkuji | neopraví — obsahuje číslici |
| `m2sto` | město | neopraví — obsahuje číslici |
| `p59li3` | příliš | neopraví — obsahuje číslici |

Čeština má z toho tedy užitek jen zčásti: pomůže tam, kde jde o prohození `y` a `z` mezi
QWERTZ a americkým rozložením, ale ne u slov s diakritikou. Totéž platí pro slovenštinu a
maďarštinu, které mají diakritiku na číselné řadě také.

Opačný směr funguje bez výhrad:

| Napsané | Zamýšlené | |
|---|---|---|
| `zes` | yes | angličtina, aktivní české rozložení |
| `siye` | size | angličtina, aktivní české rozložení |

## Co to dělá

| | |
|---|---|
| **Shift ×2** | Opraví poslední slovo a přepne rozložení |
| **Ctrl ×2** | Opraví výběr, a když není nic vybráno, celý řádek |
| Zpět | Vrátí poslední opravu. Ve výchozím stavu nepřiřazeno |
| Automaticky | Ve výchozím stavu vypnuto, zapíná se v nabídce v oznamovací oblasti |

Všechny tři zkratky lze přeřadit, dialog zachytí to, co skutečně stisknete. Backspace
přiřazení zruší. Rozhraní česky, anglicky, rusky, ukrajinsky a německy, světlé nebo tmavé
podle nastavení Windows.

Pro češtinu neexistuje vestavěný jazykový model — ten je jen pro ruštinu, ukrajinštinu a
angličtinu. Čeština se posuzuje podle **slovníku Windows**, a bez něj se přepínač zdrží
místo hádání. Instaluje se v Nastavení → Čas a jazyk → Jazyk a oblast → Přidat jazyk, se
zaškrtnutým „Základní psaní". Které slovníky máte, ukáže *Status and latency...*.

## Instalace

Stáhněte z [Releases](https://github.com/marrakesh/switch-please/releases) a spusťte.
Instalátor není potřeba.

| Soubor | Velikost | Vyžaduje |
|---|---|---|
| `SwitchPlease.exe` | ~52 MB | nic |
| `SwitchPlease-runtime-required.exe` | ~0,4 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| `SwitchPlease-arm64.exe` | ~50 MB | nic, na stroji s ARM |
| `SwitchPlease-arm64-runtime-required.exe` | ~0,4 MB | ARM64 verzi Desktop Runtime |

Soubory **nejsou podepsané**, takže SmartScreen při prvním spuštění varuje. Každé vydání
obsahuje `SHA256SUMS.txt` vytvořený týmž během, který sestavil i samotné soubory:
`certutil -hashfile SwitchPlease.exe SHA256`.

Spouštějte **bez práv správce**, jinak opravy přestanou docházet do běžných oken. Vyžaduje
Windows 10 nebo novější.

## Soukromí

**Žádné síťové požadavky.** Žádná telemetrie, žádná analytika, žádná hlášení o pádech.
Jediné místo, kde se otevírá socket, je kontrola aktualizací: zeptá se GitHubu na číslo
posledního vydání, nesděluje o vás nic a je **ve výchozím stavu vypnutá**.

Napsané neopouští počítač a ve výchozím stavu se nezapisuje na disk: do protokolu jdou
rozhodnutí, text je zkrácen na svou délku. Samotná slova se objeví, jen když zapnete *Write
the typed text to the log*.

## Nastavení

To podstatné je v nabídce v oznamovací oblasti, čísla v *Settings...*, všechno dohromady v
`%APPDATA%\SwitchPlease\settings.json`. Úplný popis každého parametru je
[v anglické verzi](README.md#settings).

## Podpořit

Program je zdarma a zůstane. Pokud vám ušetřil dost přepisování:
[Ko-fi](https://ko-fi.com/marrakeshgtp) nebo
[Buy Me a Coffee](https://www.buymeacoffee.com/marrakesh).

## Dál

Úplná dokumentace je [anglické README](README.md): oba režimy, kam přepínač neleze, změřená
kvalita rozpoznávání, všechna nastavení a omezení. Jak je to uvnitř postavené a proč, je v
[docs/internals.md](docs/internals.md). Hlášení chyb a pull requesty:
[CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[MIT](LICENSE) © Oleksii Ozerov
