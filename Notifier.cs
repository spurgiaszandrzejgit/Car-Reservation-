using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Http;
using System.Configuration;
using System.Threading.Tasks;
using vaurioajoneuvo_finder;

namespace vaurioajoneuvo_finder1
{
    /// <summary>
    /// Klasa pomocnicza do wysyłania powiadomień (email + Telegram).
    /// </summary>
    public class Notifier
    {

        // === EMAIL ===
        public static async Task SendEmailAsync(string subject, string message, string productUrl, string imgUrl = null)
        {
            try
            {
                using (var smtpClient = new SmtpClient(EmailConfig.SmtpHost, EmailConfig.SmtpPort))
                {
                    smtpClient.Credentials = new NetworkCredential(
                        EmailConfig.FromEmail, EmailConfig.FromPassword);
                    smtpClient.EnableSsl = EmailConfig.EnableSsl;

                    string bodyHtml;

                    if (!string.IsNullOrWhiteSpace(imgUrl))
                    {
                        bodyHtml = $@"
                    <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='text-align: center;'>
                                <img src='{imgUrl}' alt='Product' style='max-width: 400px; border-radius: 10px;'/><br/><br/>
                                <p style='font-size: 16px; color: #333;'>{message}</p>
                                <p style='font-size: 14px;'>
                                    <a href='{productUrl}' target='_blank'>{productUrl}</a>
                                </p>
                            </div>
                        </body>
                    </html>";
                    }
                    else
                    {
                        bodyHtml = $@"
                    <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <p style='font-size: 16px; color: #333;'>{message}</p>
                            <p style='font-size: 14px;'>
                                <a href='{productUrl}' target='_blank'>{productUrl}</a>
                            </p>
                        </body>
                    </html>";
                    }

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(EmailConfig.FromEmail),
                        Subject = subject,
                        Body = bodyHtml,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(EmailConfig.ToEmail);

                    await smtpClient.SendMailAsync(mailMessage);
                    Logger.Log("✅ Email wysłany pomyślnie");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Błąd wysyłки emaila: {ex.Message}");
            }
        }

        // === TELEGRAM ===
        public static async Task SendTelegramPhotoAsync(string message, string productUrl, string imgUrl = null)
        {
            try
            {
                var botToken = ConfigurationManager.AppSettings["TelegramBotToken"];
                var chatId = ConfigurationManager.AppSettings["TelegramChatId"];

                if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
                {
                    Logger.Log("⚠️ Brak konfiguracji Telegrama (token/chatId)");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    HttpContent content;

                    if (!string.IsNullOrWhiteSpace(imgUrl))
                    {
                        var imgData = await httpClient.GetByteArrayAsync(imgUrl);

                        var multipart = new MultipartFormDataContent
                        {
                            { new StringContent(chatId), "chat_id" },
                            { new StringContent($"{message}\n\n{productUrl}"), "caption" },
                            { new StringContent("HTML"), "parse_mode" }
                        };
                        multipart.Add(new ByteArrayContent(imgData), "photo", "product.jpg");
                        content = multipart;
                    }
                    else
                    {
                        var multipart = new MultipartFormDataContent
                        {
                            { new StringContent(chatId), "chat_id" },
                            { new StringContent($"{message}\n\n{productUrl}"), "caption" },
                            { new StringContent("HTML"), "parse_mode" }
                        };
                        content = multipart;
                    }

                    var response = await httpClient.PostAsync(
                        $"https://api.telegram.org/bot{botToken}/sendPhoto", content);

                    if (response.IsSuccessStatusCode)
                        Logger.Log("✅ Telegram OK");
                    else
                        Logger.Log($"❌ Błąd Telegram API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Błąd wysyłania Telegrama: {ex.Message}");
            }
        }
    }
}
