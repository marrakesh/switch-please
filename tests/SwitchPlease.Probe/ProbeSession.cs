using System.Text;

namespace SwitchPlease.Probe;

/// <summary>
/// One run of the checks, and the report it produces.
///
/// Everything the user had is put back before it finishes: the keyboard layout it switched
/// away from, and the clipboard it borrowed. A diagnostic that leaves the machine different
/// from how it found it is one nobody runs twice.
/// </summary>
internal sealed class ProbeSession(string? outputDirectory) : IDisposable
{
    private const string Sentinel = "switch-please-probe-clipboard";

    private readonly StringBuilder _report = new();
    private readonly ProbeWindow _window = new();

    private int _failures;

    public string Report => _report.ToString();

    public bool Passed => _failures == 0;

    public void Run()
    {
        nint originalLayout = Input.ActiveLayout(_window.Handle);

        try
        {
            _window.Open();
            Checks();
        }
        catch (Exception ex)
        {
            Fail($"the run stopped: {ex.Message}");
        }
        finally
        {
            Input.UseLayout(_window.Handle, originalLayout);
            _window.Close();
            Save();
        }
    }

    private void Checks()
    {
        if (!_window.TakeForeground())
        {
            Fail("this window never reached the foreground, so nothing was typed");
            return;
        }

        if (!Input.UseLatinLayout(_window.Handle))
        {
            Fail("could not switch this window to a Latin layout");
            return;
        }

        Note($"layout before typing: {Input.DescribeLayout(_window.Handle)}");

        WrongLayoutWordIsCorrected();
        PressingAgainPutsItBack();
        TypingOnEndsTheOfferToUndo();
        CorrectTextIsLeftAlone();
        SelectionIsConverted();
    }

    /// <summary>The whole point of the application: type Russian on a Latin layout, fix it.</summary>
    private void WrongLayoutWordIsCorrected()
    {
        _window.Clear();
        _window.Type("ghbdtn");

        Check("the word hotkey corrects the last word", () =>
        {
            _window.DoubleTap(Input.Shift);
            return _window.WaitForText("привет");
        });

        Note($"layout after correcting: {Input.DescribeLayout(_window.Handle)}");
    }

    /// <summary>
    /// Pressing it again on the same word is an undo.
    ///
    /// This did nothing at all for a long time, and every piece of it was behaving as
    /// designed: the corrected word is a real word, so the refusal margin protected it.
    /// </summary>
    private void PressingAgainPutsItBack()
    {
        Check("pressing the hotkey again puts the word back", () =>
        {
            _window.DoubleTap(Input.Shift);
            return _window.WaitForText("ghbdtn");
        });
    }

    /// <summary>
    /// Once anything else has been typed, the undo hotkey has nothing left to put back.
    ///
    /// Undo works by erasing a known number of characters at the caret, so it only means
    /// anything while the caret is still where the correction left it. An offer that outlives
    /// that erases whatever happens to be under the caret instead -- six characters of a
    /// sentence the user was in the middle of writing.
    ///
    /// Only reachable when an undo hotkey is bound, which it is not by default, so this is
    /// skipped rather than faked when there is none.
    /// </summary>
    private void TypingOnEndsTheOfferToUndo()
    {
        if (Settings.Undo() is not { } undo)
        {
            Note("skipped 'typing on ends the offer to undo': no undo hotkey is bound");
            return;
        }

        if (!Input.UseLatinLayout(_window.Handle))
        {
            Note("skipped 'typing on ends the offer to undo': could not use a Latin layout");
            return;
        }

        _window.Settle();
        _window.Clear();
        _window.Type("ghbdtn");
        _window.DoubleTap(Input.Shift);

        if (!_window.WaitForText("привет"))
        {
            Note("skipped 'typing on ends the offer to undo': the correction did not happen");
            return;
        }

        // Correcting switches the layout, so these keys now type Cyrillic directly. What they
        // spell does not matter; that something was typed at all is the whole point.
        _window.Type(" lf");
        string typedOn = _window.Text;

        Check("typing on withdraws the offer to undo", () =>
        {
            _window.Press(undo);
            return _window.WaitForText(typedOn, settleMilliseconds: 1400);
        });
    }

    /// <summary>
    /// The other direction, and the one that costs the user something when it goes wrong: a
    /// word that is already right must survive a double tap of Shift, which is easy to hit
    /// by accident.
    /// </summary>
    private void CorrectTextIsLeftAlone()
    {
        if (!Input.UseCyrillicLayout(_window.Handle))
        {
            Note("skipped 'correct text is left alone': no Cyrillic layout is installed");
            return;
        }

        // Switching the layout pops up the Windows language indicator, which takes the
        // foreground for a moment.
        _window.Settle();
        _window.Clear();

        // The same keys, with a Cyrillic layout active, produce the correct word directly.
        _window.Type("ghbdtn");

        if (!_window.WaitForText("привет"))
        {
            Note($"skipped 'correct text is left alone': the layout typed {_window.Text} instead");
            return;
        }

        Check("correctly typed text survives a stray double tap", () =>
        {
            _window.DoubleTap(Input.Shift);
            return _window.WaitForText("привет", settleMilliseconds: 1200);
        });
    }

    /// <summary>
    /// The selection hotkey, which borrows the clipboard to do its work. Checked together
    /// with the clipboard being handed back untouched, because that is the half a user
    /// notices only after losing something.
    /// </summary>
    private void SelectionIsConverted()
    {
        if (!Input.UseLatinLayout(_window.Handle))
        {
            Note("skipped the selection check: could not return to a Latin layout");
            return;
        }

        _window.Settle();

        Clipboard.SetText(Sentinel);
        Wait.For(150);

        _window.Set("ghbdtn rfr ltkf");
        _window.SelectAll();

        Check("the selection hotkey converts what is highlighted", () =>
        {
            _window.DoubleTap(Input.Control);
            return _window.WaitForText("привет как дела", settleMilliseconds: 1600);
        });

        Check("the clipboard is put back the way it was found", () =>
        {
            string now = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;

            if (now != Sentinel)
            {
                Note($"  clipboard holds \"{Trim(now)}\"");
            }

            return now == Sentinel;
        });

        Clipboard.Clear();
    }

    private void Check(string what, Func<bool> body)
    {
        bool ok;

        try
        {
            ok = body();
        }
        catch (Exception ex)
        {
            Note($"  {ex.Message}");
            ok = false;
        }

        if (ok)
        {
            _report.AppendLine($"PASS  {what}");
            return;
        }

        _failures++;
        _report.AppendLine($"FAIL  {what}");
        _report.AppendLine($"      the window holds \"{Trim(_window.Text)}\"");
    }

    private void Note(string line) => _report.AppendLine($"      {line}");

    private void Fail(string line)
    {
        _failures++;
        _report.AppendLine($"FAIL  {line}");
    }

    private void Save()
    {
        _report.AppendLine();
        _report.AppendLine(Passed ? "everything passed" : $"{_failures} check(s) failed");

        if (outputDirectory is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "probe.txt"), Report);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"could not write the report: {ex.Message}");
        }
    }

    private static string Trim(string value) =>
        value.Length <= 60 ? value : string.Concat(value.AsSpan(0, 57), "...");

    public void Dispose() => _window.Dispose();
}
