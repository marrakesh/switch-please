namespace SwitchPlease.Probe;

/// <summary>
/// The text box everything is typed into.
///
/// Deliberately this program's own window: whatever the switcher does, it happens here and
/// not in something of the user's, and the result is read straight off the control instead
/// of out of another process.
/// </summary>
internal sealed class ProbeWindow : IDisposable
{
    private readonly Form _form;
    private readonly TextBox _box;

    public ProbeWindow()
    {
        _box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Font = new Font("Consolas", 18f),
        };

        _form = new Form
        {
            Text = "Switch Please probe",
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(560, 150),
            TopMost = true,
        };

        _form.Controls.Add(_box);
    }

    public nint Handle => _form.Handle;

    public string Text => _box.Text;

    public void Open()
    {
        _form.Show();
        Wait.For(500);
    }

    public void Close() => Dispose();

    public void Dispose()
    {
        _box.Dispose();
        _form.Dispose();
    }

    /// <summary>
    /// Brings this window to the front and waits until Windows agrees that it is there.
    ///
    /// Retried rather than assumed. Windows refuses to hand the foreground to a process that
    /// has not been interacted with recently, and losing that race once is normal; giving up
    /// on the first attempt made this abort about half the time for no reason.
    /// </summary>
    public bool TakeForeground()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            _form.Activate();

            if (Input.BringToFront(_form.Handle))
            {
                _box.Focus();
                Wait.For(80);

                if (Input.ForegroundWindow == _form.Handle)
                {
                    return true;
                }
            }

            Wait.For(120);
        }

        return false;
    }

    public void Clear() => Set(string.Empty);

    /// <summary>
    /// Replaces what is on screen, and tells the switcher that it happened.
    ///
    /// The second half is the part that is easy to forget and produced a confusing failure
    /// the first time: emptying the text box in code is invisible to a keyboard hook, so the
    /// switcher went on believing the previous word was still in front of the caret and
    /// corrected twelve characters instead of six. Escape is what it treats as "the caret
    /// went somewhere I cannot account for", which is exactly what happened.
    /// </summary>
    public void Set(string text)
    {
        Guard();
        Input.Press(Input.Escape);
        Wait.For(80);

        _box.Text = text;
        _box.SelectionStart = _box.TextLength;
        _box.Focus();
        Wait.For(120);
    }

    public void SelectAll()
    {
        Guard();
        _box.SelectAll();
        _box.Focus();
        Wait.For(150);
    }

    /// <summary>Types <paramref name="text"/> one key at a time, as a person would.</summary>
    public void Type(string text)
    {
        foreach (char c in text)
        {
            Guard();
            Input.Press(c == ' ' ? Input.Space : (ushort)char.ToUpperInvariant(c));
            Wait.For(45);
        }

        Wait.For(250);
    }

    public void DoubleTap(ushort modifier)
    {
        Guard();
        Input.DoubleTap(modifier);
    }

    public void Press(Binding binding)
    {
        Guard();
        Input.Press(binding);
    }

    /// <summary>Re-takes the foreground after something outside the run has stolen it.</summary>
    public void Settle() => Guard();

    /// <summary>
    /// Waits for the box to hold <paramref name="expected"/>, then waits a little longer to
    /// be sure nothing else arrives.
    ///
    /// The second half matters: a correction that overshoots and deletes one character too
    /// many passes through the right answer on its way to the wrong one.
    /// </summary>
    public bool WaitForText(string expected, int settleMilliseconds = 900)
    {
        for (int waited = 0; waited < settleMilliseconds; waited += 50)
        {
            Wait.For(50);

            if (_box.Text == expected)
            {
                break;
            }
        }

        Wait.For(250);
        return _box.Text == expected;
    }

    /// <summary>
    /// Refuses to type unless this window really has the foreground, taking it back once if
    /// it has slipped.
    ///
    /// It slips for an innocent reason: switching the keyboard layout makes Windows show its
    /// own language indicator, which briefly becomes the foreground window. Failing the run
    /// over that would be reporting a fault in the test harness as a fault in the
    /// application.
    /// </summary>
    private void Guard()
    {
        if (Input.ForegroundWindow == _form.Handle || TakeForeground())
        {
            return;
        }

        throw new InvalidOperationException("focus moved away, so nothing further was typed");
    }
}

/// <summary>Sleeps while keeping the window painting, which a plain Thread.Sleep would not.</summary>
internal static class Wait
{
    public static void For(int milliseconds)
    {
        int until = Environment.TickCount + milliseconds;

        while (Environment.TickCount < until)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
