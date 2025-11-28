using System;
using System.Threading;
using System.Windows.Forms;

namespace HvsMvp.App
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Show splash screen
            SplashScreen? splash = null;
            try
            {
                splash = new SplashScreen();
                splash.ShowSplash();
                splash.SetMaxTimeout(5000); // Safety timeout

                // Update splash status
                splash.UpdateStatus("Carregando configurações...");
                Thread.Sleep(300);

                splash.UpdateStatus("Inicializando serviços...");
                Thread.Sleep(300);

                splash.UpdateStatus("Preparando interface...");
                Thread.Sleep(400);

                splash.UpdateStatus("Pronto!");
            }
            catch
            {
                // If splash fails, continue without it
            }

            // Create and show main form
            var mainForm = new MainForm();

            // Close splash after main form is ready
            mainForm.Shown += (s, e) =>
            {
                try
                {
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
