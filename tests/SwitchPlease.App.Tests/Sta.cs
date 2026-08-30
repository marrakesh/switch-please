namespace SwitchPlease.App.Tests;

/// <summary>
/// Runs a test body on a single-threaded apartment.
///
/// Every window in this application is Windows Forms, and a Windows Forms control cannot be
/// created on a multi-threaded apartment thread -- which is what the test runner gives us.
/// Without this the form tests fail with an apartment error that says nothing about the
/// thing being tested.
///
/// The exception is rethrown rather than wrapped, so a failed assertion inside still reads
/// as a failed assertion.
/// </summary>
internal static class Sta
{
    public static void Run(Action body)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Generous, and a deadline rather than an indefinite wait: a hung form test should
        // fail the run rather than hold the build open until someone notices.
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The test body did not finish on its apartment thread.");
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
