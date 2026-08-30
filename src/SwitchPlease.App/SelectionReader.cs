using SwitchPlease.Core.Keys;
using SwitchPlease.Win32;

namespace SwitchPlease.App;

/// <summary>
/// Reads whatever the user has highlighted, by borrowing the clipboard and giving it back.
///
/// There is no way to ask an arbitrary Windows application what its selection is. UI
/// Automation can answer for applications that implement its text pattern, but reaching it
/// from a Windows Forms process means taking a dependency on the WPF half of the desktop
/// runtime, which would roughly double a download whose whole appeal is that it is one small
/// file. So the clipboard it is -- carefully.
///
/// Three things that the obvious implementation gets wrong:
///
/// 1. <b>Copying with Ctrl+C.</b> In a terminal that is how you interrupt a running program.
///    Ctrl+Insert means copy everywhere Ctrl+C does and interrupts nothing.
///
/// 2. <b>Restoring only the text.</b> Anyone who had an image, a file or formatted content
///    on the clipboard lost it. Everything that can be put back is put back.
///
/// 3. <b>Polling the content to find out whether the copy landed.</b> An application with
///    nothing selected answers nothing at all, which is indistinguishable from one that is
///    merely slow. The clipboard's sequence number distinguishes them, and lets the wait end
///    the instant the answer arrives instead of running to a fixed timeout.
///
/// 4. <b>Doing any of it on the user interface thread.</b> This ran there at first, because
///    the clipboard needs an STA thread and that is the obvious one. It is also the thread
///    Windows watches: an application whose interface thread stops answering for a few
///    seconds is declared hung and closed, and clipboard calls block for as long as whoever
///    holds the clipboard open cares to hold it. The worker is STA too, so it does this
///    itself and a jammed clipboard costs one correction rather than the process.
/// </summary>
public sealed class SelectionReader(Action<string> log)
{
    /// <summary>
    /// How long to wait for the focused application to answer. Generous, because it is only
    /// ever waited out in full when nothing is selected, and stingy would mean the hotkey
    /// silently doing nothing in a busy editor.
    /// </summary>
    private const int WaitMilliseconds = 400;

    private const int PollIntervalMilliseconds = 10;

    /// <summary>
    /// Returns the highlighted text, or null when there is none.
    ///
    /// Must be called from a single-threaded apartment, which the switcher's worker is.
    /// </summary>
    public string? Read()
    {
        var saved = Capture();

        try
        {
            uint before = ClipboardSequence.Current;

            InputSender.SendChord(VirtualKeys.Control, VirtualKeys.Insert);

            string? selection = WaitForAnswer(before);

            Restore(saved, replaced: selection is not null);

            return string.IsNullOrWhiteSpace(selection) ? null : selection;
        }
        finally
        {
            // Here rather than inside Restore, which is not reached when the copy is refused
            // outright -- and refusal is the ordinary outcome in a window running as
            // administrator, so every press would otherwise leak a full-size bitmap.
            saved?.Image?.Dispose();
        }
    }

    /// <summary>
    /// Waits until the clipboard changes, then reads it. A changed sequence number is proof
    /// the application answered; without one there was nothing selected to copy.
    /// </summary>
    private static string? WaitForAnswer(uint before)
    {
        for (int waited = 0; waited < WaitMilliseconds; waited += PollIntervalMilliseconds)
        {
            Thread.Sleep(PollIntervalMilliseconds);

            if (ClipboardSequence.Current == before)
            {
                continue;
            }

            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (Exception)
            {
                // Another process is holding the clipboard open. It will let go; keep
                // polling until the wait runs out.
            }
        }

        return null;
    }

    /// <summary>
    /// Takes a copy of what is on the clipboard, in every form this can put back afterwards.
    ///
    /// Deliberately limited to the formats with a typed accessor. Round-tripping arbitrary
    /// formats means serialising objects out of another process's data, which is both
    /// unreliable and exactly the sort of thing that should not be done silently in the
    /// background to hold a hotkey's place.
    /// </summary>
    private ClipboardContents? Capture()
    {
        try
        {
            var contents = new ClipboardContents
            {
                Text = Clipboard.ContainsText() ? Clipboard.GetText() : null,
                Html = Clipboard.ContainsText(TextDataFormat.Html)
                    ? Clipboard.GetText(TextDataFormat.Html)
                    : null,
                Rtf = Clipboard.ContainsText(TextDataFormat.Rtf)
                    ? Clipboard.GetText(TextDataFormat.Rtf)
                    : null,
                Image = Clipboard.ContainsImage() ? Clipboard.GetImage() : null,
                Files = Clipboard.ContainsFileDropList() ? Clipboard.GetFileDropList() : null,
            };

            return contents.IsEmpty ? null : contents;
        }
        catch (Exception ex)
        {
            log($"clipboard could not be saved: {ex.Message}");
            return null;
        }
    }

    private void Restore(ClipboardContents? saved, bool replaced)
    {
        if (!replaced)
        {
            // The application never answered, so the clipboard still holds what it held
            // before and putting the copy back would only risk breaking it.
            return;
        }

        try
        {
            if (saved is null)
            {
                Clipboard.Clear();
                return;
            }

            var data = new DataObject();

            if (saved.Text is not null) data.SetText(saved.Text, TextDataFormat.UnicodeText);
            if (saved.Html is not null) data.SetText(saved.Html, TextDataFormat.Html);
            if (saved.Rtf is not null) data.SetText(saved.Rtf, TextDataFormat.Rtf);
            if (saved.Image is not null) data.SetImage(saved.Image);
            if (saved.Files is not null) data.SetFileDropList(saved.Files);

            Clipboard.SetDataObject(data, copy: true);
        }
        catch (Exception ex)
        {
            log($"clipboard could not be restored: {ex.Message}");
        }
    }

    private sealed class ClipboardContents
    {
        public string? Text { get; init; }

        public string? Html { get; init; }

        public string? Rtf { get; init; }

        public Image? Image { get; init; }

        public System.Collections.Specialized.StringCollection? Files { get; init; }

        public bool IsEmpty =>
            Text is null && Html is null && Rtf is null && Image is null && Files is null;
    }
}
