# Contributing

Thanks for taking a look. Bug reports are as useful as code here — this program touches
every keystroke on the machine, so the failure modes that matter are the ones only real use
finds.

Before changing anything, [docs/internals.md](docs/internals.md) explains how the program is
put together and why. Most of the odd-looking decisions in it are the result of a
measurement or a crash, and that document says which.

## Reporting a bug

The single most useful thing you can attach is a diagnostics log:

1. Tray menu → **Measure hook latency** (this also starts writing decisions to disk).
2. Reproduce the problem.
3. Tray menu → **Open settings folder** → attach `switch-please.log`.

By default the log records what was decided and why, with the text itself reduced to its
length — a keyboard hook's log is a transcript of everything typed while it was running, and
that is not something to ask anyone to upload. If the decision cannot be understood without
the words, **Settings... → Write the typed text to the log** turns them on; please reproduce
with something you are happy to publish, and switch it off afterwards.

Also paste the contents of **Status and latency...** — it lists your layouts, which
dictionaries Windows has, and which languages the switcher is modelling. Most misbehaviour
is explained by those three lines. The window has a Copy button.

Please say which application you were typing into. Terminals, Electron apps and remote
desktop clients each mishandle synthesised input in their own way.

## Building

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

The SDK version is pinned in `global.json`, and CI runs all three of those. Two things worth
knowing before the first build surprises you:

- **Warnings are errors**, and analysers and code style run as part of the build. The style
  rules live in `.editorconfig` and describe what the code already does rather than
  introducing anything new.
- **`dotnet format` is checked in CI.** Running it locally before pushing saves a round trip.

There are two test projects.

`SwitchPlease.Core.Tests` covers `SwitchPlease.Core`, which has no Windows dependencies. That
is where the decisions live: what a correction should erase and type, which of several layouts
to switch to, when to refuse, whether pressing the hotkey again means "put that back", whether
the keyboard hook has been dropped, whether automatic correction applies in this application,
and what each keystroke does to the record of what has been typed. `ConversionPlanner` takes an
`ILayoutResolver` and `TypingRecorder` takes an `ICharacterResolver`, so the tests supply a
fixed QWERTY/ЙЦУКЕН table and run anywhere.

`TypingRecorder` is the one to be careful with. Every correction is a count of backspaces
followed by replacement text, and that count comes from the record it keeps: one character
more than the screen holds and the correction deletes something the user typed. Nothing
reports a failure when that happens, because nothing failed.

`SwitchPlease.App.Tests` covers the settings file, the windows and the tray icon. Windows
Forms will not create a control on the runner's own thread, so anything touching a form goes
through the `Sta` helper. The test worth copying when adding a setting is
`EverySettingOnTheFormMakesItBackOut`: it builds the settings window from one set of values
and reads it back into another, which catches the failure nothing else does -- a control added
to the form and forgotten in `ApplyTo`, so the setting silently never saves. It caught exactly
that the day it was written.

The hooks, `SendInput`, dictionaries and the password-field and full-screen probes live in
`SwitchPlease.Win32`. Those are Windows, and none of them can be faked convincingly enough to
be worth faking, so there is a third test project that drives the real thing instead.

## The probe

`SwitchPlease.Probe` types into a text box of its own with `SendInput`, presses the hotkeys,
and checks what comes back. Start Switch Please first, then:

```bash
dotnet run --project tests/SwitchPlease.Probe
```

It prints a report and exits 0 when everything passed, so it can go in a script. It puts back
the keyboard layout and the clipboard it borrowed.

What it covers is the half the unit tests cannot: that a real keystroke reaches the real hook,
that the correction comes back out, that the layout switches, that pressing the hotkey again
undoes it, that correctly typed text survives a stray double tap, and that the clipboard is
handed back untouched. It has already caught two things nothing else did.

Its own window rather than Notepad's, for two reasons: nothing it types can land in anything
of yours, and the result can be read straight back. Every burst of input is preceded by a
check that the window really has the foreground; if focus has moved, the run stops rather than
typing into whatever took it.

It needs a real desktop session, so it is not part of `dotnet test` and CI does not run it.
Run it before releasing, and after touching anything in `SwitchPlease.Win32`.

## Adding an interface language

One file, no code:

1. Open `src/SwitchPlease.Core/Localization/Translations.cs` and copy the `English` block.
2. Translate the values. `LanguageName` should be the language written in itself.
3. Add one line to `Translations.All`: the two-letter tag, the primary part of the Windows
   LANGID for that language, and your block.

That is the whole change. The menu, the setting, the "follow the Windows display language"
check and the translation tests all read that list, so nothing else needs to learn about the
new language.

Every string is `required`, so the compiler will not let you forget one. `TranslationTests`
additionally checks that your placeholders (`{0}`, `{1}`) match the English original —
getting those wrong throws at runtime, in a language you may never open yourself — and that
the registry and the translations agree.

Two of the strings are the first-run demonstration: `WelcomeDemoTyped` is a short phrase as
it lands when the layout was wrong, and `WelcomeDemoFixed` is the same phrase put right.
Make them the same length if you can; the window types one and then the other.

Line breaks are only worth keeping where the original uses one to separate two thoughts. The windows measure and wrap their own text, so a hard break placed to control the width fights that and produces a ragged paragraph in whichever language it was not written for.

## Adding a language model

Detection already works for any language Windows has a spell-check dictionary for. A
hand-written profile is only worth adding for a language *without* one.

See `LanguageProfile.Russian` for the shape: alphabet, vowels, frequent bigrams, impossible
bigrams, a small frequent-word list, and the expected vowel ratio. Then add it to
`LanguageProfile.BuiltIn`.

If you do, please add words **and whole sentences** to `DetectionCorpus`, using words that
are **not** in your own frequent-word list. Numbers measured on the model's own vocabulary
say nothing, and the sentence tests are the ones that catch the failure that matters: correct
prose being rewritten a word at a time as it is typed.

## Code style

Follow what is already there. In particular:

- Comments explain *why*, not *what*. Several of the odder decisions in this codebase are
  the result of a measurement or a crash, and the comment says which — please keep that
  habit, it is the difference between a rule and a superstition.
- Nothing new goes into the hook callback. It has a 300 ms budget enforced by Windows and
  currently runs in microseconds; every addition there is a step towards the hook being
  silently dropped.
- New behaviour that can damage the user's text needs a test. The asymmetry throughout this
  project is deliberate: a missed correction is a minor annoyance, a wrong one is not.
- No network calls. The one that exists is the update check, it is off by default, and it
  asks GitHub for a release tag. Anything else that would send anything anywhere is not a
  feature this program wants.
- A guard that cannot answer must answer "carry on". Every probe here — password field,
  full-screen application, accessibility — returns "no" when it fails, because the
  alternative is the switcher silently disabling itself on machines nobody tested.
