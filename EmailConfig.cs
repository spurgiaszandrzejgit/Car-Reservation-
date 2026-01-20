using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vaurioajoneuvo_finder
{
    public static class EmailConfig
    {
        public static string SmtpHost => ConfigurationManager.AppSettings["SmtpHost"];
        public static int SmtpPort => int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
        public static bool EnableSsl => bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");
        public static string FromEmail => ConfigurationManager.AppSettings["SmtpFromEmail"];
        public static string FromPassword => ConfigurationManager.AppSettings["SmtpPassword"];
        public static string ToEmail => ConfigurationManager.AppSettings["SmtpToEmail"];
    }
}
