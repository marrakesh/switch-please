# Switch Please

[![CI](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml/badge.svg)](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/marrakesh/switch-please)](https://github.com/marrakesh/switch-please/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-support-FF5E5B?logo=kofi&logoColor=white)](https://ko-fi.com/marrakeshgtp)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy_Me_a_Coffee-support-FFDD00?logo=buymeacoffee&logoColor=black)](https://www.buymeacoffee.com/marrakesh)

Fixes text you typed before noticing the keyboard layout was wrong. `ghbdtn` becomes
`привет`, `руддщ` becomes `hello`, and the layout is switched so you can carry on typing.

A free, open-source tray utility for Windows 10 and 11. Russian, Ukrainian and English work
out of the box, and so does any other language Windows has a spell-check dictionary for.

**[Русская](README.ru.md)** · **[Українська](README.uk.md)** · **[Deutsch](README.de.md)** · **[Čeština](README.cs.md)**

![Typing "ghbdsn zr cghfdb", pressing Ctrl twice, and getting "привіт як справи"](docs/demo.uk.svg)

- **Two hotkeys.** Shift ×2 fixes the last word, Ctrl ×2 the selection or the whole line.
  Press Shift ×2 again straight away and the word goes back.
- **Careful by design.** Automatic correction is off until you want it, and tuned to leave
  correct text alone: over ordinary prose it rewrote none of 233 correctly typed words.
- **Tells Russian from Ukrainian.** The two layouts differ by three keys, and only a
  dictionary can tell `привыт` from `привіт`.
- **Stays out of the way** of password fields, games, terminals and code editors.
- **Private.** No telemetry, no network access unless you switch on the update check, and
  nothing you type is written to disk.
- **No administrator rights.** Installs for you alone, or runs as a single executable.

## Install

Download from the [latest release](https://github.com/marrakesh/switch-please/releases/latest)
and run the installer:

| Installer | Size | |
|---|---|---|
| **`SwitchPlease-Setup.exe`** | ~47 MB | most machines |
| `SwitchPlease-Setup-arm64.exe` | ~45 MB | ARM machines |

It installs for you alone and never asks for administrator rights, offers to start Switch
Please with Windows, and asks on uninstall whether to keep your settings.

Or take the executable on its own. Nothing is installed, and nothing is written outside
`%APPDATA%\SwitchPlease`:

| Portable | Size | Requires |
|---|---|---|
| `SwitchPlease.exe` | ~52 MB | nothing |
| `SwitchPlease-runtime-required.exe` | ~0.4 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| `SwitchPlease-arm64.exe` | ~50 MB | nothing, on an ARM machine |
| `SwitchPlease-arm64-runtime-required.exe` | ~0.4 MB | the ARM64 desktop runtime |

Requires Windows 10 or later. Run it **without administrator rights**: an elevated Switch
Please cannot send input to ordinary windows, so it would stop correcting text everywhere it
matters.

The binaries are **not code-signed**, so Windows SmartScreen will warn about them the first
time; *More info → Run anyway* gets past it. Every release ships a `SHA256SUMS.txt` produced
by the same GitHub Actions run that built the files, so you can check what you downloaded:

```powershell
Get-FileHash .\SwitchPlease-Setup.exe -Algorithm SHA256
```

To remove it, uninstall *Switch Please* from Settings → Apps. The portable executable leaves
nothing behind but itself and `%APPDATA%\SwitchPlease`.

## First run

Switch Please lives in the notification area, and its icon shows the current layout. The
first time it starts, a small window shows the two hotkeys and a short demonstration;
after that it stays quiet. If the icon is not visible, look under the ^ arrow, and drag it
out onto the taskbar to keep it in sight.

Everything else is in the icon's menu: automatic correction, the hotkeys, the sound,
starting with Windows, the applications to stay out of, and *Settings...*.

## Using it

| Keys | What it does |
|---|---|
| **Shift ×2** | Fixes the last word and switches the layout. Pressed again straight away, puts the word back. |
| **Ctrl ×2** | Fixes the selection, or the whole line if nothing is selected, and switches the layout. |
| Undo | Puts back the last correction, a converted selection included. Unbound by default. |
| Automatic | Fixes each word as you finish it. Off by default. |

**Shift ×2 — the last word.** Works from what was recorded as you typed, so nothing needs
selecting. The recording is dropped whenever the caret moves somewhere it cannot follow:
Enter, Tab, arrows, Home/End, Esc, and any mouse click. Acting on a stale recording would
delete text you never typed.

**Ctrl ×2 — the selection, or the whole line.** With text selected, it converts exactly
that and needs no recording at all; this is the mode for "I already moved the cursor". With
nothing selected, it takes everything typed since the caret last moved, which is usually
the whole line. Reading a selection means borrowing the clipboard. Everything that was on
it — text, formatting, an image, a list of files — is put back afterwards.

**Automatic correction** is switched on with *Detect wrong layout automatically* in the tray
menu. Each word is judged when you press Space after it, and rewritten only when another
layout reads clearly better; how much better is *Caution when correcting automatically* in
*Settings...*. It need not be all or nothing: *Correct automatically in …* in the tray menu turns it on or off
for the application you are in, so it can work in the browser and leave the editor alone,
with the hotkeys still one keypress away there.

**Undo** puts back the last correction, as long as nothing has been typed since. For a word,
pressing Shift ×2 again does the same; the undo hotkey also covers a converted selection,
which Shift ×2 cannot reverse.

**Caps Lock left on** is put right along with the layout: `пРИВЕТ` becomes `Привет`, and
Caps Lock is switched off. Shift is how it tells: nobody holds Shift with Caps Lock on unless
they did not know it was on, so capitals typed on purpose, with no Shift, are left as they
are. Automatic correction does this too, when it is on.

All three hotkeys are reassignable from *Hotkeys...* in the tray menu. The dialog captures
whatever you actually press — a double tap of Shift, Ctrl or Alt, or an ordinary chord like
`Ctrl+Shift+L`. Backspace clears a binding.

## Examples

What lands when the layout was wrong, and what the hotkey turns it into:

| You get | You meant | |
|---|---|---|
| `ghbdtn rfr ltkf` | привет как дела | Russian typed on the US layout |
| `cgfcb,j` | спасибо | Russian typed on the US layout |
| `руддщ` | hello | English typed on the Russian layout |
| `ерфтлы` | thanks | English typed on the Russian layout |
| `gHBDTN` | Привет | Russian typed on the US layout, with Caps Lock left on |
| `пРИВЕТ` | Привет | Caps Lock left on; the layout was right |
| `привыт` | привіт | Ukrainian typed on the Russian layout |
| `мысто` | місто | Ukrainian typed on the Russian layout |

The last two are the hard case. Russian and Ukrainian differ by three keys, so the result is
ordinary Cyrillic either way and letter statistics cannot tell it apart. The Windows
dictionary is what does: no such Russian word exists.

And what automatic correction leaves alone on purpose:

| Left alone | Why |
|---|---|
| `ok`, `hi` | Shorter than three characters |
| `test123` | Contains digits |
| `C:\Windows\System32` | Looks like a path |
| `user@example.com` | Looks like an address |
| `camelCase`, `getUserName` | Mixed case inside a word |
| `NASA` with Caps Lock on | Capitals on purpose: no Shift was held |
| `příliš`, `Grüße` | No model and no dictionary for that language, so it abstains |

The hotkeys are not bound by these rules: pressing one is a request, so they convert. They
still decline text that already reads clearly better than every alternative, which is what
keeps a stray double tap from mangling a correctly typed word.

## Where it stays out of the way

Four guards, all on by default:

- **Password fields.** Detected through two independent probes, and never read.
- **Games and presentations.** Nothing happens while a full-screen application has the
  screen, so double-tapping Shift to sprint stays double-tapping Shift to sprint.
- **Excluded applications.** Password managers, terminals and code editors — Visual Studio,
  VS Code, Cursor, the JetBrains IDEs and a few more — are excluded out of the box, because
  what gets typed there is mostly passwords, commands and identifiers. *Never run in …* in
  the tray menu adds the application you are in; *Settings...* has the whole list.
- **Chinese, Japanese and Korean input.** Composition is left alone — there is no wrong
  layout to undo there.

## Languages

The languages it corrects between come from the **keyboard layouts installed in Windows**,
not from a list in the source. Adding a layout is all it takes, and Switch Please notices
within a second.

Russian, Ukrainian and English have built-in models. Every other language is judged with the
**Windows spell-check dictionary**, which installs with the language: Settings → Time &
language → Language & region → Add a language, with "Basic typing" ticked. A language with
neither abstains rather than guessing. *Status and latency...* in the tray menu shows which
dictionaries you have.

The interface is in English, Russian, Ukrainian, German and Czech. It follows the Windows
display language unless you pick one under *Language* in the tray menu, and follows the
Windows light or dark setting.

## Privacy

**No network requests unless you ask for them.** No telemetry, no analytics, no crash
reporting, no licence check. The only code that opens a socket is the update check: it asks
GitHub for the latest release tag, sends nothing about you, and is **off by default**.

Nothing you type leaves the machine, and by default nothing at all is written to disk but
the settings. The diagnostic log exists only while *Measure hook latency* is on, and records
decisions with the text reduced to its length:

```
14:22:07 auto: <6 chars> -> <6 chars> [ru=0.94 en=0.11 margin=0.83 after=ru]
```

The words themselves appear only if you tick *Write the typed text to the log*. The log is
capped and rolled over.

## Detection quality

How automatic correction trades caught mistakes against damaged words, measured on 427
words deliberately absent from the model's word lists, roughly half Russian and half
English:

| Sensitivity | Mistakes caught | Correct words mangled |
|---|---|---|
| 0.15 | 95.6 % | 0 |
| 0.20 | 93.9 % | 0 |
| **0.25** (default) | **88.8 %** | **0** |
| 0.30 | 83.4 % | 0 |
| 0.35 | 78.2 % | 0 |
| 0.45 | 68.9 % | 0 |

Over whole sentences rather than isolated words: of 233 words of ordinary prose, judged one
at a time as they would be while being typed, **none** would have been rewritten; of 191
eligible words typed entirely in the wrong layout, **90.1 %** were recovered.

The asymmetry is intentional. A missed correction costs one keypress; mangling a correctly
typed word costs far more.

## Settings

Most of it is in the tray menu, and the numbers are in *Settings...*. Everything is also in
`%APPDATA%\SwitchPlease\settings.json`. Close Switch Please before editing it by hand: the
file is read at startup and rewritten whenever something changes in the menu.

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch, also in the tray menu. |
| `AutoDetectEnabled` | `false` | Automatic correction. |
| `AutoDetectPerApplication` | none | Applications where automatic correction differs from the above, e.g. `{"chrome.exe": true}`. |
| `AutoDetectSensitivity` | `0.25` | How much better the alternative must read, 0..1. Higher is more cautious. |
| `MinimumAutoWordLength` | `3` | Shortest word automatic correction will touch. |
| `CorrectionDelayMilliseconds` | `15` | Pause before an automatic rewrite, so the space that ended the word lands first. |
| `ConvertWordHotkey`, `ConvertSelectionHotkey`, `UndoHotkey` | Shift ×2, Ctrl ×2, none | Key code, modifiers, and `Kind`: `0` chord, `1` double tap. |
| `DoubleTapWindowMilliseconds` | `500` | Longest gap between the two presses of a double tap. |
| `DoubleTapHoldMilliseconds` | `400` | Longest either press may last. |
| `PlaySoundOnConvert` | `true` | A sound on every correction. |
| `TypewriterMillisecondsPerCharacter` | `0` | Type corrections one character at a time. Zero is off. |
| `RespectPasswordFields` | `true` | Stay out of password fields. |
| `PauseInFullscreenApps` | `true` | Stand aside while a game or a presentation has the screen. |
| `ExcludedProcesses` | see above | Applications the switcher stays out of entirely. |
| `DiagnosticsEnabled` | `false` | Measure hook latency and keep the diagnostic log. |
| `LogTextContent` | `false` | Whether the log may contain what was typed. |
| `LogMaximumBytes` | `1048576` | Size at which the log rolls over. |
| `CheckForUpdates` | `false` | Ask GitHub for a newer release at startup. |
| `Language` | `auto` | `auto`, or `en` / `ru` / `uk` / `de` / `cs`. |

## When it does not work

**Nothing happens on Shift ×2.** Usually one of these:

- The application is excluded. Code editors and terminals are out of the box; the tray menu
  offers *Run in … again*.
- The caret moved after the word was typed — a click, an arrow key, Enter. Select the text
  and use Ctrl ×2.
- The window belongs to a program running as administrator. Windows will not accept input
  from Switch Please there, and the tray says so once.
- The word already reads better as it stands than in any other layout, so the hotkey
  declines to touch it.

**It fires by accident.** Shorten *Double tap - longest gap* in *Settings...*, or move the
command to a chord.

**It picks the wrong one of two similar languages.** *Status and latency...* lists the
dictionaries Windows has; a missing one is the usual cause.

Anything else is worth a [bug report](https://github.com/marrakesh/switch-please/issues/new?template=bug_report.md).
[CONTRIBUTING.md](CONTRIBUTING.md) says what to attach.

## Limitations

- Applications running as administrator do not accept input from a normal process, so
  corrections will not work there.
- Automatic correction relies on a short pause before rewriting. Very slow or heavily loaded
  applications may need a longer one.
- The hotkey leaves alone any text that already reads far better than every alternative.
  This does not apply to the Russian/Ukrainian pair, where the hotkey simply toggles.
- Layouts that put diacritics on the number row — Czech, Slovak, Hungarian — land a digit
  where the accented letter was meant: `děkuji` arrives as `d2kuji`. Automatic correction
  never touches a word with a digit in it, and the hotkey is not reliable on such words
  either. The `y`/`z` half of such a layout is corrected normally.
- Password-field detection is best-effort. Applications that draw their own controls and
  expose no accessibility information cannot be asked.
- Layout names come from the input language as Windows reports it, which does not always
  match what the layout types. Colliding names get their digit row appended.

## Support

Free, and staying that way. If it saves you enough retyping to be worth something:

<a href="https://ko-fi.com/marrakeshgtp"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Ko-fi" height="40"></a>
<a href="https://www.buymeacoffee.com/marrakesh"><img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&amp;emoji=&amp;slug=marrakesh&amp;button_colour=FFDD00&amp;font_colour=000000&amp;font_family=Cookie&amp;outline_colour=000000&amp;coffee_colour=ffffff" alt="Buy Me a Coffee" height="40"></a>

Ko-fi takes no cut of a one-off donation; Buy Me a Coffee takes five percent.

## Contributing

Bug reports and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Adding
an interface language is one file and no code. How it is built, and why, is in
[docs/internals.md](docs/internals.md).

## License

[MIT](LICENSE) © Oleksii Ozerov
