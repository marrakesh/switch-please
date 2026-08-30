# Switch Please

[![CI](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml/badge.svg)](https://github.com/marrakesh/switch-please/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A keyboard layout switcher for Windows, in the spirit of Punto Switcher. Fixes text you
typed before noticing the layout was wrong — by hotkey, or automatically.

**[Русская](README.ru.md)** · **[Українська](README.uk.md)** · **[Deutsch](README.de.md)** · **[Čeština](README.cs.md)**

![Typing "ghbdtn rfr ltkf", pressing Shift twice, and getting "привет как дела"](docs/demo.svg)

## What it does

| | |
|---|---|
| **Shift ×2** | Fix the last word and switch the layout |
| **Ctrl ×2** | Fix the selection, or the whole line if nothing is selected |
| Undo | Put the last correction back. Unbound by default |
| Automatic | Off by default; switch it on in the tray menu |

All three hotkeys are reassignable from the tray menu, and the dialog captures whatever you
actually press — an ordinary chord like `Ctrl+Shift+L` works just as well. Backspace clears
a binding.

Interface in English, Russian, Ukrainian, German and Czech, following the Windows display
language and the light or dark setting unless you pick otherwise.

## Examples

What lands when the layout was wrong, and what the hotkey turns it into:

| You get | You meant | |
|---|---|---|
| `ghbdtn rfr ltkf` | привет как дела | Russian typed on the US layout |
| `cgfcb,j` | спасибо | Russian typed on the US layout |
| `руддщ` | hello | English typed on the Russian layout |
| `ерфтлы` | thanks | English typed on the Russian layout |
| `привыт` | привіт | Ukrainian typed on the Russian layout |
| `мысто` | місто | Ukrainian typed on the Russian layout |

The last two are the hard case. Russian and Ukrainian differ by three keys, so the result is
ordinary Cyrillic either way and letter statistics cannot tell it apart. The Windows
dictionary is what does: no such Russian word exists.

And what it leaves alone on purpose, in either mode:

| Left alone | Why |
|---|---|
| `ok`, `hi` | Shorter than three characters |
| `test123` | Contains digits |
| `C:\Windows\System32` | Looks like a path |
| `user@example.com` | Looks like an address |
| `camelCase`, `getUserName` | Mixed case inside a word |
| `příliš`, `Grüße` | No model and no dictionary for that language, so it abstains |

## Install

Download from [Releases](https://github.com/marrakesh/switch-please/releases) and run it.
No installer, nothing to configure.

| File | Size | Requires |
|---|---|---|
| `SwitchPlease.exe` | ~52 MB | nothing |
| `SwitchPlease-runtime-required.exe` | ~0.4 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| `SwitchPlease-arm64.exe` | ~50 MB | nothing, on an ARM machine |
| `SwitchPlease-arm64-runtime-required.exe` | ~0.4 MB | the ARM64 desktop runtime |

The binaries are **not code-signed**, so Windows SmartScreen will warn about them the first
time. Every release ships a `SHA256SUMS.txt` produced by the same GitHub Actions run that
built the files, so you can check what you downloaded:

```bash
certutil -hashfile SwitchPlease.exe SHA256
```

Run it **without administrator rights** — an elevated Switch Please cannot send input to
ordinary windows, so it would stop correcting text everywhere it matters.

Requires Windows 10 or later.

## Two modes

**Shift ×2 — the last word.** Works from what was recorded as you typed. Fast, and nothing
needs selecting. The recording is dropped whenever the caret moves somewhere it cannot
follow: Enter, Tab, arrows, Home/End, Esc, and any mouse click. Acting on a stale recording
would delete text you never typed.

**Ctrl ×2 — the selection, or the whole line.** Depends on no recording at all. Select
anything and press; with nothing selected it takes the whole line. This is the mode for "I
already moved the cursor".

Reading a selection means borrowing the clipboard. Everything that was on it — text,
formatting, an image, a list of files — is put back afterwards.

## Where it stays out of the way

Four guards, all on by default:

- **Password fields.** Detected through two independent probes, and never read.
- **Games and presentations.** Nothing happens while a full-screen application has the
  screen, so double-tapping Shift to sprint stays double-tapping Shift to sprint.
- **Excluded applications.** Password managers, terminals and development environments are
  excluded out of the box. The tray menu adds whatever you are in with one click.
- **Chinese, Japanese and Korean input.** Composition is left alone — there is no wrong
  layout to undo there.

Excluding is blunt, and the common case is not all-or-nothing: automatic correction wanted
in the browser, unwanted in the editor, with the hotkey still one keypress away. The tray
menu carries *Correct automatically in this application* for exactly that.

## Privacy

**No network requests at all.** No telemetry, no analytics, no crash reporting, no licence
check. The only code that opens a socket is the update check: it asks GitHub for the latest
release tag, sends nothing about you, and is **off by default**.

Nothing you type leaves the machine, and by default nothing you type is written to disk. The
diagnostic log records decisions with the text reduced to its length:

```
14:22:07 auto: <6 chars> -> <6 chars> [ru=0.94 en=0.11 margin=0.83 after=ru]
```

The words themselves appear only if you tick *Write the typed text to the log*. The log is
capped and rolled over.

## Detection quality

Measured on 427 words deliberately absent from the model's word lists, roughly half Russian
and half English:

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

Detection uses the **Windows spell-check dictionaries** where they exist. Missing ones
install with the language: Settings → Time & language → Language & region → Add a language,
with "Basic typing" ticked. Which ones you have is shown in *Status and latency...*.

Languages come from the **keyboard layouts installed in Windows**, not from a list in the
source, so adding a layout is all it takes. A language with neither a built-in model
(Russian, Ukrainian, English) nor a Windows dictionary abstains rather than guessing.

## Settings

Most of it is in the tray menu, and the numbers are in *Settings...*. Everything is also in
`%APPDATA%\SwitchPlease\settings.json`:

| Setting | Meaning |
|---|---|
| `AutoDetectSensitivity` | How much better the alternative must read, 0..1. Higher is more cautious. |
| `CorrectionDelayMilliseconds` | Pause before an automatic rewrite. |
| `MinimumAutoWordLength` | Shortest word automatic correction will touch. |
| `DoubleTapWindowMilliseconds` | Longest gap between the two presses of a double tap. |
| `DoubleTapHoldMilliseconds` | Longest either press may last. |
| `RespectPasswordFields` | Stay out of password fields. |
| `PauseInFullscreenApps` | Stand aside while a game or a presentation has the screen. |
| `ExcludedProcesses` | Applications the switcher stays out of entirely. |
| `AutoDetectPerApplication` | Applications where automatic correction differs from the setting above. |
| `LogTextContent` | Whether the log may contain what was typed. Off. |
| `LogMaximumBytes` | Size at which the log rolls over. |
| `TypewriterMillisecondsPerCharacter` | Type corrections one character at a time. Zero, i.e. off. |
| `CheckForUpdates` | Ask GitHub for a newer release at startup. Off. |
| `ConvertWordHotkey`, `ConvertSelectionHotkey`, `UndoHotkey` | Key code, modifiers, and `Kind`: `0` chord, `1` double tap. |
| `Language` | `auto`, or `en` / `ru` / `uk` / `de` / `cs`. |

## Limitations

- Applications running as administrator do not accept input from a normal process, so
  corrections will not work there. The tray says so once rather than failing silently.
- Automatic correction relies on a short pause before rewriting. Very slow or heavily loaded
  applications may need a longer one.
- The hotkey leaves alone any text that already reads far better than every alternative.
  This does not apply to the Russian/Ukrainian pair, where the hotkey simply toggles.
- Layouts that put diacritics on the number row — Czech, Slovak, Hungarian — land a digit
  where the accented letter was meant, and words containing digits are never touched. The
  `y`/`z` half of such a layout is corrected; `děkuji` arriving as `d2kuji` is not.
- Password-field detection is best-effort. Applications that draw their own controls and
  expose no accessibility information cannot be asked.
- Layout names come from the input language as Windows reports it, which does not always
  match what the layout types. Colliding names get their digit row appended.

## Support

Free, and staying that way. If it saves you enough retyping to be worth something:
[Ko-fi](https://ko-fi.com/marrakeshgtp) or
[Buy Me a Coffee](https://www.buymeacoffee.com/marrakesh).

## Contributing

Bug reports and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Adding
an interface language is one file and no code. How it is built, and why, is in
[docs/internals.md](docs/internals.md).

## License

[MIT](LICENSE) © Oleksii Ozerov
