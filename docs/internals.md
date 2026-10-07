# How it works, and why

Background for anyone changing the code. The [README](../README.md) covers what the program
does; this covers the decisions behind it, most of which are the result of a measurement or
a crash rather than a preference.

```
SwitchPlease.Core    layout tables, typing buffer, language models, detection,
                     and what a correction should do. No Windows dependencies.
                     Fully covered by tests.
SwitchPlease.Win32   keyboard and mouse hooks, SendInput, layout, dictionary,
                     password-field and full-screen detection.
SwitchPlease.App     tray icon, settings, and the worker thread tying it together.
```

## Why a double tap instead of Pause/Break

The obvious key is Pause/Break, but laptops and compact keyboards often do not have it, and
nearly every free chord is already taken by some application. Double-tapping a modifier
works everywhere and collides with nothing: Shift on its own does nothing.

Shift and Ctrl, and not Alt. Alt on its own is how Windows opens a window's menu bar, and it
does so on the release — the very event a double tap is recognised on, and one that cannot
be swallowed without leaving the application believing Alt is still held down. So Alt is
offered as part of a chord, where the key pressed with it cancels the menu, and not as a
tap. Undo, which ships unbound, therefore wants a chord: `Ctrl+Shift+Z` and the like.

A press only counts as a tap if the key went down and up with no other key in between and
was not held. Ordinary typing therefore never triggers it: writing "AB" presses Shift twice,
but each press has a letter inside it. The timing is covered by tests — a false trigger here
is the most expensive kind of bug this program can have.

A chord is swallowed and never reaches the application. A double tap is not: it is
recognised on the modifier's **release**, and swallowing that release would leave the
application believing the modifier is still held down.

The one place this breaks down is games, where Shift is sprint and tapping it twice is what
running feels like — hence the full-screen guard below.

## The hook

**It lives on its own thread.** `WH_KEYBOARD_LL` is called on the thread that installed it,
and that thread must pump messages. Giving it a dedicated thread rather than borrowing the
UI thread means a busy settings window can never stall input.

**The callback does nothing.** Windows silently drops a hook whose callback misses
`LowLevelHooksTimeout` (300 ms by default). So the callback only compares a few integers and
writes one struct into a lock-free queue: no allocation, no locks, no I/O, nothing the
garbage collector can pause for long. Everything else happens on a worker thread.

You can measure this: tray menu → *Measure hook latency*, then *Status and latency...*.

**Nothing hides a stopped worker.** *Status and latency...* reports whether the thread that
turns keystrokes into corrections is alive, how many keystrokes are waiting for it, how long
ago it handled one, and the longest a single keystroke has ever taken. That last number is
there because the two things that can stall it — `SendInput` and the clipboard — are both
waits on other processes with no ceiling, and a queue that quietly stops draining looks
exactly like a switcher that has decided not to correct anything.

**The hook puts itself back.** Windows removes a low-level hook whose callback overruns that
budget, and says nothing: no error, no notification, and the handle stays as valid-looking
as before. The switcher then sits in the tray with its icon showing and does nothing, which
is the worst failure it has, because there is no way to tell it apart from "it decided not
to correct that one". There is no call that asks whether a hook is still installed, so the
hook thread compares two clocks instead: when its callback last ran, and when Windows last
saw any input at all. Recent input that never reached the callback means the hook is gone,
and it is installed again. The status window reports it if it ever happened.

**Mouse clicks are tracked with a second hook**, because the keyboard reports nothing when
you click, and without it the switcher would still believe its recorded text sits in front
of the cursor.

## Sending the correction

**Layout tables are not hard-coded.** Character mappings are derived from scan codes through
`ToUnicodeEx` — Windows is asked what each physical key would produce under each layout. Any
pair of installed layouts therefore works, not just RU/EN, and nothing breaks on a Dvorak or
typewriter variant. Adding or removing a layout in Windows is noticed within a second;
there is no broadcast to subscribe to, so the list is polled, which is one cheap call over
two entries.

**Corrected text is sent as `KEYEVENTF_UNICODE`**, so the characters that arrive do not
depend on which layout is active at that instant. Every synthesised event carries a
signature in `dwExtraInfo` so the hook recognises its own echo instead of treating a
correction as fresh typing.

**Switching the layout afterwards is tried twice.** `WM_INPUTLANGCHANGEREQUEST` is the
documented way and is only a request: Electron, UWP and a fair share of what people type
into simply drop it, which leaves the text corrected and the keyboard still wrong.
`ActivateKeyboardLayout` on the target thread, reached through `AttachThreadInput`, does not
depend on the application handling anything — so that is tried first, checked, and the
message used as the fallback.

**Reading a selection** means asking the focused window to copy, which means borrowing the
clipboard. The copy is sent as **Ctrl+Insert**, not Ctrl+C, because in a terminal Ctrl+C
interrupts whatever is running; and everything that was on the clipboard is put back
afterwards, not just the text.

## The guards

**Password fields.** Two probes: `EM_GETPASSWORDCHAR`, which every native edit control
answers and which is cheap enough to ask on every keystroke, and MSAA's
`STATE_SYSTEM_PROTECTED`, which is how a browser reports a masked field and which is only
asked before anything is actually rewritten. Both are best-effort, and a probe that cannot
answer says "not a password field" — the alternative would be the switcher silently
switching itself off in applications that answer neither question.

**Games and presentations.** `SHQueryUserNotificationState` knows about exclusive-fullscreen
Direct3D and presentation mode; a window that covers its monitor exactly is the
borderless-windowed case that Windows does not report at all.

**Input method editors.** Chinese, Japanese and Korean are typed by composing characters
from latin letters, so there is no wrong layout to undo, and interfering with a composition
in progress would destroy it. `ImmGetCompositionString` says when one is under way.

A guard that cannot answer must answer "carry on". Every probe here returns "no" when it
fails, because the alternative is the switcher silently disabling itself on machines nobody
tested.

## Detection

Isolated words are the easy case. What automatic correction actually does is fire at the end
of every word in a line, so the corpus tests run over whole sentences too. Of 191 eligible
words typed entirely in the wrong layout, 90.1 % are recovered — up from 77.5 % before
context was taken into account.

A word does not arrive alone, and the text before it is evidence about it: after three
Russian words the fourth is far more likely to be Russian too. The interesting case is the
one everybody actually hits — noticing halfway through a sentence that the whole thing went
in wrong. There the preceding text is gibberish as it stands and perfectly ordinary once
converted, and that says a great deal about the word that follows. The as-typed reading is
always tried first, so ordinary prose is never read as though it were about to be converted.
The nudge is deliberately small, far too small to overrule the letters themselves: an
English word in the middle of a Russian sentence still survives.

### Windows dictionaries

Windows has shipped a spell-checking service since Windows 8, and it answers the one
question statistics cannot: **is that actually a word?**

The Russian and Ukrainian layouts differ by three keys, so "привіт" typed on the Russian one
comes out as "привыт" — a sequence no bigram model will reject, because every letter pair in
it is common Russian. The model scored it 0.94. The dictionary says no such word exists.

| typed | statistics | with dictionary |
|---|---|---|
| `привыт` | 0.938 | **0.600** |
| `мысто` | 0.986 | **0.600** |
| `привет`, `спасибо` | 1.000 | 1.000 |

A dictionary can only ever *lower* a score. It confirms a word but cannot vouch for one it
does not contain, so an unknown word — a name, a piece of jargon — is capped rather than
condemned.

### Levels of evidence

The alphabet comes from the layout itself — the letters it can type. Each language then gets
one of three levels:

| Evidence | Source | What it can do |
|---|---|---|
| Built-in model | Russian, Ukrainian, English | judges on its own, dictionary refines |
| Windows dictionary | whatever is installed | judges by dictionary |
| Alphabet only | any layout | **abstains** |

That last row matters most. Without a model or a dictionary, a profile knows *whose* letters
these are but not whether the word reads naturally. This is not hypothetical: before that
check existed, Czech `příliš` scored 0.05 under the English profile and German `Grüße`
scored 0.245. Correct words looked like noise worth rewriting.

With three or more layouts installed the switcher picks the one the text reads best in
rather than the next one in the list — cycling would land on Ukrainian for a Russian word,
which shares almost every key with it and would change nothing while silently switching the
keyboard to a language nobody asked for.

### Caps Lock left on

`пРИВЕТ` is a slip of a different kind, and every guard above reads it as an identifier:
mixed case inside a word. The capitals themselves cannot tell it from `mRNA` or `iOS`. The
keys can. Every recorded stroke carries the Caps Lock state, and nobody holds Shift with Caps
Lock on unless they did not know it was on — someone who switched it on for capitals has no
reason to. So a stretch typed with Caps Lock on counts as a slip only if Shift was held for a
letter somewhere inside it, and then the whole stretch is replayed as if Caps Lock had been
off, through `ToUnicodeEx` like any other conversion. The evidence reaches the words in the
stretch that had no Shift, which is most of them: in `пРИВЕТ КАК ДЕЛА` only the first did.

The replayed reading is what gets judged, so `gHBDTN` competes as `Ghbdtn` against `Привет`.
When no other layout wins, the case is still put right on its own and the layout is left
where it is. Either way Caps Lock is then switched off, by pressing it, because otherwise the
next word would need the same correction. A selection has no recorded keys, so there the
characters have to do, and only while Caps Lock is actually on.

### What the models are

Hand-built lists rather than a corpus: alphabet, vowels, frequent bigrams, impossible
bigrams, a small frequent-word list, and the expected vowel ratio. That is what keeps the
assembly small and dependency-free. A model derived from real text would do better on rare
words, and is the obvious thing to try next.
