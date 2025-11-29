using System;
using System.Diagnostics;
using System.Drawing;
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
        
        // PR14: Splash timing constants
        private const int SplashCloseAnimationDelayMs = 400;
        private const int SplashMaxWaitMs = 2000;
        
        // PR14: Startup log file path
        private static readonly string StartupLogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "logs", "startup.log");

        [STAThread]
        static void Main()
        {
            // PR11: Enhanced startup - always show SOMETHING to the user
            // Even if everything fails, we must show either the app or an error message
            
            // PR14: Set up global exception handling for unhandled exceptions
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => 
            {
                LogStartupError("ThreadException", e.Exception);
                ShowCriticalError("Erro de thread não tratado", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => 
            {
                var ex = e.ExceptionObject as Exception;
                LogStartupError("UnhandledException", ex);
                if (e.IsTerminating)
                {
                    ShowCriticalError("Erro crítico não tratado", ex);
                }
            };

            try
            {
                ApplicationConfiguration.Initialize();
                LogStartupInfo("ApplicationConfiguration initialized.");
                
                RunApplicationWithFallback();
            }
            catch (Exception ex)
            {
                // PR11: Ultimate fallback - if everything fails, show clear error message
                LogStartupError("Critical startup error", ex);
                ShowCriticalError("Erro crítico ao iniciar o aplicativo", ex);
            }
        }
        
        /// <summary>
        /// PR11: Shows a critical error message to the user via MessageBox.
        /// This is the last resort when the application cannot start normally.
        /// </summary>
        /// <param name="title">The error title to display</param>
        /// <param name="ex">The exception that caused the error, or null</param>
        /// <remarks>
        /// If MessageBox itself fails, this method silently fails.
        /// The error is also logged to the startup log file.
        /// </remarks>
        private static void ShowCriticalError(string title, Exception? ex)
        {
            try
            {
                string message = $"{title}\n\n";
                if (ex != null)
                {
                    message += $"Detalhes: {ex.Message}\n\n";
                }
                message += $"O aplicativo será encerrado.\nVerifique o log em:\n{StartupLogPath}";
                
                MessageBox.Show(
                    message,
                    "HVS-MVP - Erro Crítico",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // If even MessageBox fails, we can't do anything
            }
        }

        /// <summary>
        /// PR14/PR15: Main application startup logic with fail-safe fallback.
        /// PR15: Enhanced robustness - always show either WelcomeScreen, MainForm, or error message.
        /// </summary>
        private static void RunApplicationWithFallback()
        {
            AppSettings? appSettings = null;
            SplashScreen? splash = null;
            
            // Load app settings (with fallback to defaults)
            try
            {
                appSettings = AppSettings.Load();
                LogStartupInfo("AppSettings loaded successfully.");
            }
            catch (Exception ex)
            {
                LogStartupError("Failed to load AppSettings, using defaults", ex);
                appSettings = AppSettings.CreateDefault();
            }

            // Show splash screen (non-critical, can fail)
            var splashStartTime = DateTime.UtcNow;
            try
            {
                splash = new SplashScreen();
                splash.ShowSplash();
                splash.SetMaxTimeout(4000); // Safety timeout: 4 seconds max
                LogStartupInfo("SplashScreen shown.");
            }
            catch (Exception ex)
            {
                LogStartupError("Failed to show SplashScreen, continuing without it", ex);
                splash = null;
            }

            // PR14/PR15: Determine welcome action with fallback
            WelcomeScreen.WelcomeAction welcomeAction = WelcomeScreen.WelcomeAction.GoToMainDirect;
            string? selectedImagePath = null;

            if (!appSettings.SkipWelcomeScreen)
            {
                try
                {
                    // Close splash before showing welcome
                    CloseSplashSafely(splash, splashStartTime);
                    splash = null; // Mark as closed

                    // PR14: Show welcome screen in try-catch with fallback
                    LogStartupInfo("Creating WelcomeScreen...");
                    using var welcomeScreen = new WelcomeScreen(appSettings);
                    
                    // PR15: Ensure welcome screen is visible (in case fade-in fails)
                    welcomeScreen.Load += (s, e) =>
                    {
                        // Safety: ensure form becomes visible after a delay even if animation fails
                        // Use a timer instead of Task.Delay for better control and cleanup
                        var visibilityTimer = new System.Windows.Forms.Timer { Interval = 500 };
                        visibilityTimer.Tick += (ts, te) =>
                        {
                            visibilityTimer.Stop();
                            visibilityTimer.Dispose();
                            try
                            {
                                if (!welcomeScreen.IsDisposed && welcomeScreen.Opacity < 0.5)
                                {
                                    welcomeScreen.Opacity = 1.0;
                                    LogStartupInfo("WelcomeScreen forced visible (animation fallback).");
                                }
                            }
                            catch { } // Ignore errors if form is disposed
                        };
                        visibilityTimer.Start();
                    };
                    
                    // PR14: Ensure welcome screen is positioned on-screen
                    EnsureFormIsOnScreen(welcomeScreen);
                    
                    LogStartupInfo("Showing WelcomeScreen dialog...");
                    var result = welcomeScreen.ShowDialog();
                    LogStartupInfo($"WelcomeScreen dialog returned: {result}");

                    if (result == DialogResult.OK)
                    {
                        welcomeAction = welcomeScreen.SelectedAction;
                        selectedImagePath = welcomeScreen.SelectedImagePath;

                        // Save skip preference if changed
                        if (welcomeScreen.SkipWelcomeOnStartup != appSettings.SkipWelcomeScreen)
                        {
                            appSettings.SkipWelcomeScreen = welcomeScreen.SkipWelcomeOnStartup;
                            try { appSettings.Save(); } catch { }
                        }
                        
                        LogStartupInfo($"WelcomeScreen completed with action: {welcomeAction}");
                    }
                    else
                    {
                        // PR15: User closed welcome without selecting action
                        // Instead of exiting silently, show MainForm anyway (user can close it if they want)
                        LogStartupInfo("User closed WelcomeScreen without action, proceeding to MainForm.");
                        welcomeAction = WelcomeScreen.WelcomeAction.GoToMainDirect;
                    }
                }
                catch (Exception ex)
                {
                    // PR14: Welcome screen failed - fall back to MainForm
                    LogStartupError("WelcomeScreen failed, falling back to MainForm", ex);
                    CloseSplashSafely(splash, DateTime.UtcNow);
                    splash = null;
                    welcomeAction = WelcomeScreen.WelcomeAction.GoToMainDirect;
                    
                    // PR15: Show error message to user
                    try
                    {
                        MessageBox.Show(
                            $"A tela de boas-vindas falhou ao carregar.\nO aplicativo iniciará em modo direto.\n\nDetalhes: {ex.Message}",
                            "HVS-MVP - Aviso de Inicialização",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    catch { }
                }
            }
            else
            {
                // Skipping welcome - just let splash complete and close asynchronously
                LogStartupInfo("Skipping WelcomeScreen (user preference).");
                Task.Run(async () =>
                {
                    await Task.Delay(SplashDisplayTimeMs);
                    try
                    {
                        splash?.BeginInvoke(new Action(() =>
                        {
                            try { splash?.CloseSplash(); } catch { }
                        }));
                    }
                    catch { }
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
                catch (Exception ex)
                {
                    LogStartupError("Failed to open explore folder", ex);
                }

                // After opening folder, show main form anyway
                welcomeAction = WelcomeScreen.WelcomeAction.GoToMainDirect;
            }

            // PR15: Create and run main form with fallback
            try
            {
                LogStartupInfo("Creating MainForm...");
                var mainForm = new MainForm();
                
                // PR14: Ensure main form is positioned on-screen
                EnsureFormIsOnScreen(mainForm);
                
                // PR15: Ensure main form is visible (safety)
                mainForm.WindowState = FormWindowState.Normal;
                mainForm.Visible = true;

                // PR10/PR15: Apply welcome action to main form
                mainForm.Shown += async (s, e) =>
                {
                    // Ensure splash is closed
                    try { splash?.CloseSplash(); } catch { }

                    // Apply selected action
                    await Task.Delay(100); // Small delay for form to be fully ready

                    try
                    {
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
                    }
                    catch (Exception ex)
                    {
                        LogStartupError("Failed to apply welcome action", ex);
                    }
                };

                LogStartupInfo("Starting Application.Run(MainForm)...");
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                LogStartupError("MainForm failed", ex);
                TryShowMainFormAsFallback(appSettings, "An error occurred. Starting in safe mode.");
            }
        }

        /// <summary>
        /// PR14: Safely close splash screen with proper timing.
        /// </summary>
        private static void CloseSplashSafely(SplashScreen? splash, DateTime splashStartTime)
        {
            if (splash == null) return;
            
            try
            {
                // Wait for minimum display time
                int elapsed = (int)(DateTime.UtcNow - splashStartTime).TotalMilliseconds;
                int remaining = SplashDisplayTimeMs - elapsed + 200;
                if (remaining > 0)
                {
                    Thread.Sleep(Math.Min(remaining, SplashMaxWaitMs));
                }
                
                splash.CloseSplash();
                Thread.Sleep(SplashCloseAnimationDelayMs); // Let close animation complete
            }
            catch
            {
                // Ignore splash close errors
            }
        }

        /// <summary>
        /// PR14: Ensure a form is positioned within visible screen bounds.
        /// </summary>
        private static void EnsureFormIsOnScreen(Form form)
        {
            try
            {
                // Get the working area of all screens combined
                // Use SystemInformation.PrimaryMonitorSize as fallback if screen info unavailable
                Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? 
                    new Rectangle(0, 0, SystemInformation.PrimaryMonitorSize.Width, SystemInformation.PrimaryMonitorSize.Height);
                
                // For multi-monitor setups, get the combined working area
                foreach (var screen in Screen.AllScreens)
                {
                    workingArea = Rectangle.Union(workingArea, screen.WorkingArea);
                }

                // If form starts centered on screen, no adjustment needed for most cases
                if (form.StartPosition == FormStartPosition.CenterScreen)
                {
                    return;
                }

                // Check if form's location is within bounds
                Point location = form.Location;
                Size size = form.Size;

                // Ensure form is not completely off-screen
                if (location.X + size.Width < workingArea.Left || 
                    location.X > workingArea.Right ||
                    location.Y + size.Height < workingArea.Top || 
                    location.Y > workingArea.Bottom)
                {
                    // Reset to center of primary screen
                    form.StartPosition = FormStartPosition.CenterScreen;
                    LogStartupInfo($"Form {form.Name} position was off-screen, reset to CenterScreen.");
                }

                // Ensure form is not minimized at startup
                if (form.WindowState == FormWindowState.Minimized)
                {
                    form.WindowState = FormWindowState.Normal;
                    LogStartupInfo($"Form {form.Name} was minimized, set to Normal.");
                }
            }
            catch (Exception ex)
            {
                LogStartupError("Error ensuring form is on screen", ex);
                // Default to center screen as fallback
                form.StartPosition = FormStartPosition.CenterScreen;
                form.WindowState = FormWindowState.Normal;
            }
        }

        /// <summary>
        /// PR14: Ultimate fallback - try to show a basic MainForm.
        /// </summary>
        private static void TryShowMainFormAsFallback(AppSettings? settings, string errorMessage)
        {
            try
            {
                LogStartupInfo($"Attempting fallback MainForm. Message: {errorMessage}");
                
                var mainForm = new MainForm();
                mainForm.StartPosition = FormStartPosition.CenterScreen;
                mainForm.WindowState = FormWindowState.Normal;
                
                // Show error message after form loads
                mainForm.Shown += (s, e) =>
                {
                    MessageBox.Show(
                        mainForm,
                        $"{errorMessage}\n\nO aplicativo iniciou em modo seguro.\nVerifique o log em: {StartupLogPath}",
                        "HVS-MVP - Modo Seguro",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                };
                
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                LogStartupError("Ultimate fallback failed", ex);
                MessageBox.Show(
                    $"Erro crítico ao iniciar o aplicativo:\n\n{ex.Message}\n\nVerifique o log em: {StartupLogPath}",
                    "HVS-MVP - Erro Crítico",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// PR14: Log informational startup messages.
        /// </summary>
        private static void LogStartupInfo(string message)
        {
            try
            {
                EnsureLogDirectoryExists();
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO: {message}";
                File.AppendAllText(StartupLogPath, logLine + Environment.NewLine);
            }
            catch
            {
                // Silent fail - logging should never crash the app
            }
        }

        /// <summary>
        /// PR14: Log startup errors with exception details.
        /// </summary>
        private static void LogStartupError(string context, Exception? ex)
        {
            try
            {
                EnsureLogDirectoryExists();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logLine = $"[{timestamp}] ERROR: {context}";
                if (ex != null)
                {
                    logLine += $"\n  Exception: {ex.GetType().Name}: {ex.Message}";
                    logLine += $"\n  StackTrace: {ex.StackTrace}";
                    if (ex.InnerException != null)
                    {
                        logLine += $"\n  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                    }
                }
                File.AppendAllText(StartupLogPath, logLine + Environment.NewLine);
            }
            catch
            {
                // Silent fail - logging should never crash the app
            }
        }

        /// <summary>
        /// PR14: Ensure the log directory exists.
        /// </summary>
        private static void EnsureLogDirectoryExists()
        {
            try
            {
                string? logDir = Path.GetDirectoryName(StartupLogPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch
            {
                // Silent fail
            }
        }
    }
}
