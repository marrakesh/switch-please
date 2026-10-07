# Switch Please

[![CI](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml/badge.svg)](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/marrakesh/switch-please)](https://github.com/marrakesh/switch-please/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-support-FF5E5B?logo=kofi&logoColor=white)](https://ko-fi.com/marrakeshgtp)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy_Me_a_Coffee-support-FFDD00?logo=buymeacoffee&logoColor=black)](https://www.buymeacoffee.com/marrakesh)

Opraví text napsaný ve špatném rozložení klávesnice. Z `yima` se stane `zima`, ze `zes`
`yes`, a rozložení se přepne, takže můžete psát dál.

Bezplatný program s otevřeným kódem pro Windows 10 a 11, který sedí v oznamovací oblasti.
Čeština a každý další jazyk, pro který má Windows slovník kontroly pravopisu, se posuzuje
podle tohoto slovníku; ruština, ukrajinština a angličtina mají vlastní model.

**[English](README.md)** · **[Русская](README.ru.md)** · **[Українська](README.uk.md)** · **[Deutsch](README.de.md)**

![Napíše se „ykusme yase“, dvakrát Ctrl, a vyjde „zkusme zase“](docs/demo.cs.svg)

- **Dvě klávesové zkratky.** Dvakrát Shift opraví poslední slovo, dvakrát Ctrl výběr nebo
  celý řádek. Hned poté znovu dvakrát Shift a slovo je zpátky.
- **Opatrný od základu.** Automatická oprava je vypnutá, dokud ji nechcete, a nastavená tak,
  aby správný text nechala být.
- **Drží se stranou** od polí pro hesla, her, terminálů a editorů kódu.
- **Soukromí.** Žádná telemetrie, žádná síť, dokud sami nezapnete kontrolu aktualizací, a nic
  z napsaného se nezapisuje na disk.
- **Bez práv správce.** Instaluje se jen pro vás, nebo běží jako jediný soubor.

## Nejdřív to podstatné pro češtinu

Česká diakritika sedí na číselné řadě: `ě` je tam, kde má americké rozložení `2`, `š` na
`3`, `č` na `4` a tak dál. Slovo s diakritikou tedy dopadne s číslicí uvnitř — a slova
obsahující číslice automatická oprava **zásadně nechává být**, protože jinak by přepisovala
hesla, verze a cesty k souborům. Toto pravidlo běží dřív než jakékoli vyhodnocování.
Klávesová zkratka jím vázaná není, ale ani na ni se u takových slov spolehnout nedá.

Prakticky to znamená:

| Napsané | Zamýšlené | |
|---|---|---|
| `yima` | zima | **opraví** — jen prohozené y/z |
| `jayzk` | jazyk | **opraví** |
| `ynovu` | znovu | **opraví** |
| `d2kuji` | děkuji | automaticky neopraví — obsahuje číslici |
| `m2sto` | město | automaticky neopraví — obsahuje číslici |
| `p59li3` | příliš | automaticky neopraví — obsahuje číslici |

Čeština má z toho tedy užitek jen zčásti: pomůže tam, kde jde o prohození `y` a `z` mezi
QWERTZ a americkým rozložením, ale ne u slov s diakritikou. Totéž platí pro slovenštinu a
maďarštinu, které mají diakritiku na číselné řadě také.

Opačný směr funguje bez výhrad:

| Napsané | Zamýšlené | |
|---|---|---|
| `zes` | yes | angličtina, aktivní české rozložení |
| `siye` | size | angličtina, aktivní české rozložení |

## Co to dělá

| Klávesy | Co se stane |
|---|---|
| **Shift ×2** | Opraví poslední slovo a přepne rozložení. Stisknuto hned znovu, vrátí slovo zpět |
| **Ctrl ×2** | Opraví výběr, a když není nic vybráno, celý řádek, a přepne rozložení |
| Zpět | Vrátí poslední opravu, i převedený výběr. Ve výchozím stavu nepřiřazeno |
| Automaticky | Opraví každé slovo, jakmile je dopsané. Ve výchozím stavu vypnuto |

Shift ×2 pracuje se záznamem toho, co jste psali, a zahodí ho, jakmile kurzor skočí tam,
kam ho záznam nemůže sledovat: Enter, Tab, šipky, Home/End, Esc a jakékoli kliknutí myší.
Pokud už je kurzor jinde, označte text a stiskněte dvakrát Ctrl.

Zapomenutý Caps Lock se opraví spolu s rozložením: z `aHOJ` bude `Ahoj` a Caps Lock se
vypne. Pozná se to podle Shiftu: se zapnutým Caps Lockem ho drží jen ten, kdo neví, že je
zapnutý — velká písmena napsaná záměrně, bez Shiftu, proto zůstanou. Automatická oprava to
dělá také, pokud je zapnutá.

Všechny tři zkratky lze přeřadit v *Klávesové zkratky...*; dialog zachytí to, co skutečně
stisknete — dvojí stisk Shift, Ctrl nebo Alt, nebo běžnou kombinaci jako `Ctrl+Shift+L`.
Backspace přiřazení zruší. Automatickou opravu zapíná *Rozpoznávat špatné rozložení
automaticky* v nabídce v oznamovací oblasti a *Automaticky opravovat v …* ji zapne nebo
vypne pro aktuální aplikaci.

Automatická oprava záměrně nechává být slova kratší než tři znaky, slova s číslicemi, cesty
(`C:\Windows`), adresy (`user@example.com`) a `camelCase`. Klávesové zkratky tato pravidla
nemají — stisk je žádost —, ale ani ony nesáhnou na text, který se už čte výrazně lépe než
jakákoli alternativa. Náhodný dvojí Shift proto správně napsané slovo nepokazí.

Rozhraní česky, anglicky, rusky, ukrajinsky a německy — podle jazyka Windows, nebo jak
zvolíte v nabídce *Jazyk*. Světlé nebo tmavé podle nastavení Windows.

Pro češtinu neexistuje vestavěný jazykový model — ten je jen pro ruštinu, ukrajinštinu a
angličtinu. Čeština se posuzuje podle **slovníku Windows**, a bez něj se přepínač zdrží
místo hádání. Instaluje se v Nastavení → Čas a jazyk → Jazyk a oblast → Přidat jazyk, se
zaškrtnutým „Základní psaní". Které slovníky máte, ukáže *Stav a latence...*.

## Instalace

Stáhněte z [posledního vydání](https://github.com/marrakesh/switch-please/releases/latest) a
spusťte instalátor:

| Instalátor | Velikost | |
|---|---|---|
| **`SwitchPlease-Setup.exe`** | ~47 MB | většina strojů |
| `SwitchPlease-Setup-arm64.exe` | ~45 MB | stroje s ARM |

Instaluje se jen pro vás, práva správce nevyžaduje, nabídne spouštění se systémem Windows a
při odinstalaci se zeptá, zda ponechat vaše nastavení.

Nebo si vezměte samotný spustitelný soubor: nic se neinstaluje a mimo
`%APPDATA%\SwitchPlease` se nic nezapisuje:

| Přenosně | Velikost | Vyžaduje |
|---|---|---|
| `SwitchPlease.exe` | ~52 MB | nic |
| `SwitchPlease-runtime-required.exe` | ~0,4 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| `SwitchPlease-arm64.exe` | ~50 MB | nic, na stroji s ARM |
| `SwitchPlease-arm64-runtime-required.exe` | ~0,4 MB | ARM64 verzi Desktop Runtime |

Vyžaduje Windows 10 nebo novější. Spouštějte **bez práv správce**, jinak opravy přestanou
docházet do běžných oken.

Soubory **nejsou podepsané**, takže SmartScreen při prvním spuštění varuje — *Další
informace → Přesto spustit*. Každé vydání obsahuje `SHA256SUMS.txt` vytvořený týmž během,
který sestavil i samotné soubory:

```powershell
Get-FileHash .\SwitchPlease-Setup.exe -Algorithm SHA256
```

Po spuštění program sedí v oznamovací oblasti a jeho ikona ukazuje aktuální rozložení. Pokud
ji nevidíte, je pod šipkou ^ a odtud ji lze přetáhnout na hlavní panel.

## Soukromí

**Žádné síťové požadavky, dokud o ně nepožádáte.** Žádná telemetrie, žádná analytika, žádná
hlášení o pádech. Jediné místo, kde se otevírá socket, je kontrola aktualizací: zeptá se
GitHubu na číslo posledního vydání, nesděluje o vás nic a je **ve výchozím stavu vypnutá**.

Napsané neopouští počítač a ve výchozím stavu se na disk nezapisuje nic kromě nastavení.
Protokol vzniká jen, dokud je zapnuté *Měřit latenci hooku*; zaznamenává rozhodnutí a text
zkracuje na jeho délku. Samotná slova se objeví, jen když zapnete *Zapisovat napsaný text do
protokolu*.

## Nastavení

To podstatné je v nabídce v oznamovací oblasti, čísla v *Nastavení...*, všechno dohromady v
`%APPDATA%\SwitchPlease\settings.json`; před ruční úpravou program ukončete. Úplný popis
každého parametru i s výchozími hodnotami je [v anglické verzi](README.md#settings).

## Podpořit

Program je zdarma a zůstane. Pokud vám ušetřil dost přepisování:

<a href="https://ko-fi.com/marrakeshgtp"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Ko-fi" height="40"></a>
<a href="https://www.buymeacoffee.com/marrakesh"><img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&amp;emoji=&amp;slug=marrakesh&amp;button_colour=FFDD00&amp;font_colour=000000&amp;font_family=Cookie&amp;outline_colour=000000&amp;coffee_colour=ffffff" alt="Buy Me a Coffee" height="40"></a>

Ko-fi si z jednorázového příspěvku nebere nic, Buy Me a Coffee pět procent.

## Dál

Úplná dokumentace je [anglické README](README.md): oba režimy, kam přepínač neleze, změřená
kvalita rozpoznávání, všechna nastavení, co dělat, když to nefunguje, a omezení. Jak je to
uvnitř postavené a proč, je v [docs/internals.md](docs/internals.md). Hlášení chyb a pull
requesty: [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[MIT](LICENSE) © Oleksii Ozerov
