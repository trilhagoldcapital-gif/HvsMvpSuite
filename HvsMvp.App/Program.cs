using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HvsMvp.App
{
    internal static class Program
    {
        // Splash screen display time in milliseconds (10 seconds)
        private const int SplashDisplayTimeMs = 10000;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Show splash screen
            SplashScreen? splash = null;
            var splashStartTime = DateTime.UtcNow;

            try
            {
                splash = new SplashScreen();
                splash.ShowSplash();
                splash.SetMaxTimeout(12000); // Safety timeout: 12 seconds max
            }
            catch
            {
                // If splash fails, continue without it
            }

            // Create main form (but don't show yet)
            var mainForm = new MainForm();

            // When main form is shown, ensure splash stays for the required time
            mainForm.Shown += async (s, e) =>
            {
                try
                {
                    // Calculate remaining time to keep splash visible
                    var elapsedMs = (long)(DateTime.UtcNow - splashStartTime).TotalMilliseconds;
                    var remainingMs = (int)Math.Max(0, SplashDisplayTimeMs - elapsedMs);

                    if (remainingMs > 0)
                    {
                        // Wait the remaining time before closing splash
                        await Task.Delay(remainingMs);
                    }

                    splash?.CloseSplash();
                }
                catch
                {
                    // Ignore splash close errors
                }
            };

            Application.Run(mainForm);
        }
    }
}
