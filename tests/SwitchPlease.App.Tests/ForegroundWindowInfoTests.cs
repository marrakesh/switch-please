using System.Runtime.InteropServices;
using SwitchPlease.Win32;
using Xunit;

namespace SwitchPlease.App.Tests;

/// <summary>
/// The process name behind a window, asked for from more than one thread.
///
/// It is asked from more than one thread: the worker looks it up on every keystroke to decide
/// whether the application is excluded, and the tray looks it up on its tick to name the
/// application in its menu. The answer is cached, and the cache used to be two fields written
/// one after the other -- so a reader arriving between the two writes got one thread's window
/// paired with the other thread's process name.
///
/// That is not a cosmetic mix-up. The pair is what the exclusion list is checked against, so
/// the wrong answer means the switcher recording keystrokes inside a password manager.
///
/// Two windows from the same process cannot show the fault, because both answers are the same
/// string. This uses the shell's window, which belongs to explorer.exe, against a window of
/// its own.
/// </summary>
public class ForegroundWindowInfoTests
{
    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [Fact]
    public void TwoThreadsAskingAboutDifferentWindowsNeverGetEachOthersAnswer()
    {
        nint shell = GetShellWindow();

        if (shell == 0)
        {
            // No desktop shell, which is the case on a bare build agent. There is nothing to
            // compare against, and a test that quietly passes is better than one that fails
            // for a reason unrelated to the code.
            return;
        }

        string shellProcess = ForegroundWindowInfo.GetProcessName(shell);
        Assert.False(string.IsNullOrEmpty(shellProcess));

        nint own = 0;
        string ownProcess = string.Empty;

        Sta.Run(() =>
        {
            using var form = new Form();
            own = form.Handle;
            ownProcess = ForegroundWindowInfo.GetProcessName(own);

            Assert.False(string.IsNullOrEmpty(ownProcess));
            Assert.NotEqual(shellProcess, ownProcess);

            var wrong = new List<string>();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            void Hammer(nint window, string expected)
            {
                while (!stop.IsCancellationRequested)
                {
                    string answer = ForegroundWindowInfo.GetProcessName(window);

                    if (!string.Equals(answer, expected, StringComparison.Ordinal))
                    {
                        lock (wrong)
                        {
                            wrong.Add($"{window:X} answered {answer}, expected {expected}");
                        }

                        return;
                    }
                }
            }

            // Two threads alternating between two windows is what the real arrangement looks
            // like, and what made the old cache hand out mismatched pairs within a second.
            var one = new Thread(() => Hammer(shell, shellProcess));
            var two = new Thread(() => Hammer(own, ownProcess));

            one.Start();
            two.Start();
            one.Join();
            two.Join();

            Assert.True(wrong.Count == 0, string.Join("; ", wrong));
        });
    }

    [Fact]
    public void AWindowThatIsGoneAnswersEmptyRatherThanThrowing()
    {
        // Windows close while the switcher is looking at them, and the process behind an
        // elevated one cannot be opened at all.
        Assert.Equal(string.Empty, ForegroundWindowInfo.GetProcessName(0));
        Assert.Equal(string.Empty, ForegroundWindowInfo.GetProcessName(0x7FFF_FFFF));
    }

    [Fact]
    public void TheShellsOwnSurfacesAreRecognisedAsTheShell()
    {
        nint shell = GetShellWindow();

        if (shell != 0)
        {
            Assert.True(ForegroundWindowInfo.IsShellSurface(shell));
        }

        Assert.True(ForegroundWindowInfo.IsShellSurface(0));

        Sta.Run(() =>
        {
            using var form = new Form();

            // An ordinary window is not the shell, or the tray menu would refuse to offer it.
            Assert.False(ForegroundWindowInfo.IsShellSurface(form.Handle));
        });
    }
}
