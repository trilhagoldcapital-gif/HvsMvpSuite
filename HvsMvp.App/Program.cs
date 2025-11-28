using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HvsMvp.App
{
    internal static class Program
    {
        // PR10: Splash display time
        private const int SplashDisplayTimeMs = 2000;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Load app settings
            var appSettings = AppSettings.Load();

            // Show splash screen
            SplashScreen? splash = null;
            var splashStartTime = DateTime.UtcNow;

            try
            {
                splash = new SplashScreen();
                splash.ShowSplash();
                splash.SetMaxTimeout(4000); // Safety timeout: 4 seconds max
            }
            catch
            {
                // If splash fails, continue without it
            }

            // PR10: Determine welcome action
            WelcomeScreen.WelcomeAction welcomeAction = WelcomeScreen.WelcomeAction.GoToMainDirect;
            string? selectedImagePath = null;

            if (!appSettings.SkipWelcomeScreen)
            {
                // Close splash before showing welcome (use synchronous approach)
                // This is on the main thread before the message loop starts, so it's safe
                Thread.Sleep(Math.Max(0, SplashDisplayTimeMs - (int)(DateTime.UtcNow - splashStartTime).TotalMilliseconds + 200));
                
                try { splash?.CloseSplash(); } catch { }
                
                // Small delay to let splash close animation complete
                Thread.Sleep(400);

                using var welcomeScreen = new WelcomeScreen(appSettings);
                var result = welcomeScreen.ShowDialog();

                if (result == DialogResult.OK)
                {
                    welcomeAction = welcomeScreen.SelectedAction;
                    selectedImagePath = welcomeScreen.SelectedImagePath;

                    // Save skip preference if changed
                    if (welcomeScreen.SkipWelcomeOnStartup != appSettings.SkipWelcomeScreen)
                    {
                        appSettings.SkipWelcomeScreen = welcomeScreen.SkipWelcomeOnStartup;
                        appSettings.Save();
                    }
                }
                else
                {
                    // User closed welcome without selecting action - exit app
                    return;
                }
            }
            else
            {
                // Skipping welcome - just let splash complete and close
                Task.Run(async () =>
                {
                    await Task.Delay(SplashDisplayTimeMs);
                    splash?.BeginInvoke(new Action(() =>
                    {
                        try { splash?.CloseSplash(); } catch { }
                    }));
                });
            }

            // Handle explore action - open folder
            if (welcomeAction == WelcomeScreen.WelcomeAction.ExploreSamplesReports)
            {
                try
                {
                    string exploreDir = !string.IsNullOrWhiteSpace(appSettings.ReportsDirectory)
                        ? appSettings.ReportsDirectory
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
                    Directory.CreateDirectory(exploreDir);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exploreDir,
                        UseShellExecute = true
                    });
                }
                catch { }

                // After opening folder, show main form anyway
                welcomeAction = WelcomeScreen.WelcomeAction.GoToMainDirect;
            }

            // Create main form with the selected action
            var mainForm = new MainForm();

            // PR10: Apply welcome action to main form
            mainForm.Shown += async (s, e) =>
            {
                // Ensure splash is closed
                try { splash?.CloseSplash(); } catch { }

                // Apply selected action
                await Task.Delay(100); // Small delay for form to be fully ready

                switch (welcomeAction)
                {
                    case WelcomeScreen.WelcomeAction.NewImageAnalysis:
                        if (!string.IsNullOrWhiteSpace(selectedImagePath))
                        {
                            mainForm.LoadImageFromWelcome(selectedImagePath);
                        }
                        break;

                    case WelcomeScreen.WelcomeAction.LiveCamera:
                        mainForm.StartLiveFromWelcome();
                        break;
                }
            };

            Application.Run(mainForm);
        }
    }
}
