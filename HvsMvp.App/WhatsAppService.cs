using System;
using System.Diagnostics;
using System.IO;
using System.Web;

namespace HvsMvp.App
{
    /// <summary>
    /// Service for sharing reports via WhatsApp Web.
    /// </summary>
    public static class WhatsAppService
    {
        private const string WhatsAppWebUrl = "https://web.whatsapp.com/send";

        /// <summary>
        /// Share a report via WhatsApp by opening WhatsApp Web and the report folder.
        /// </summary>
        /// <param name="sampleName">Name of the sample/project</param>
        /// <param name="reportFilePath">Path to the report file</param>
        /// <param name="settings">App settings for contact info</param>
        public static void ShareReport(string sampleName, string reportFilePath, AppSettings settings)
        {
            // Build message
            string message = BuildShareMessage(sampleName, reportFilePath, settings);

            // Open WhatsApp Web with message
            OpenWhatsAppWeb(message, settings.WhatsAppContact);

            // Open folder containing the report
            if (!string.IsNullOrWhiteSpace(reportFilePath) && File.Exists(reportFilePath))
            {
                OpenFolder(Path.GetDirectoryName(reportFilePath) ?? "");
            }
        }

        /// <summary>
        /// Build the share message for WhatsApp.
        /// </summary>
        private static string BuildShareMessage(string sampleName, string reportFilePath, AppSettings settings)
        {
            var message = new System.Text.StringBuilder();

            message.AppendLine("🔬 *Laudo de Análise MicroLab HVS*");
            message.AppendLine();

            if (!string.IsNullOrWhiteSpace(settings.LabName))
            {
                message.AppendLine($"📍 *Laboratório:* {settings.LabName}");
            }

            if (!string.IsNullOrWhiteSpace(sampleName))
            {
                message.AppendLine($"📋 *Amostra:* {sampleName}");
            }

            message.AppendLine($"📅 *Data:* {DateTime.Now:dd/MM/yyyy HH:mm}");

            if (!string.IsNullOrWhiteSpace(settings.DefaultOperator))
            {
                message.AppendLine($"👤 *Operador:* {settings.DefaultOperator}");
            }

            message.AppendLine();

            if (!string.IsNullOrWhiteSpace(reportFilePath))
            {
                string fileName = Path.GetFileName(reportFilePath);
                message.AppendLine($"📎 *Arquivo:* {fileName}");
                message.AppendLine();
                message.AppendLine("_O laudo está salvo localmente. Arraste o arquivo para esta conversa ou compartilhe manualmente._");
            }
            else
            {
                message.AppendLine("_Laudo gerado pelo MicroLab HVS-MVP._");
            }

            message.AppendLine();
            message.AppendLine("---");
            message.AppendLine("_Gerado por MicroLab HVS-MVP - Trilha Gold Capital_");

            return message.ToString();
        }

        /// <summary>
        /// Open WhatsApp Web with a pre-filled message.
        /// </summary>
        private static void OpenWhatsAppWeb(string message, string? contact = null)
        {
            try
            {
                string encodedMessage = HttpUtility.UrlEncode(message);
                string url = $"{WhatsAppWebUrl}?text={encodedMessage}";

                // If contact is provided and looks like a phone number, add it
                if (!string.IsNullOrWhiteSpace(contact))
                {
                    // Clean phone number (remove spaces, dashes, etc.)
                    string cleanPhone = contact.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
                    if (cleanPhone.Length > 0 && (cleanPhone.StartsWith("+") || char.IsDigit(cleanPhone[0])))
                    {
                        url = $"{WhatsAppWebUrl}?phone={cleanPhone}&text={encodedMessage}";
                    }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Erro ao abrir WhatsApp Web:\n\n{ex.Message}",
                    "Erro",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Open a folder in Windows Explorer.
        /// </summary>
        private static void OpenFolder(string folderPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                // Silent fail - folder opening is secondary
            }
        }

        /// <summary>
        /// Check if a report path is valid for sharing.
        /// </summary>
        public static bool IsValidReportPath(string? reportFilePath)
        {
            if (string.IsNullOrWhiteSpace(reportFilePath))
                return false;

            return File.Exists(reportFilePath);
        }
    }
}
