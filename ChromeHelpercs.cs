using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace vaurioajoneuvo_finder1
{
    /// <summary>
    /// Pomocnicza klasa do otwierania linków w Chrome z opcją "rozgrzewki domeny".
    /// </summary>
    public static class ChromeHelper
    {
        private static DateTime _lastChromeWarmupUtc = DateTime.MinValue;
        private static readonly object _lock = new object();

        /// <summary>
        /// Szuka ścieżki do chrome.exe w rejestrze.
        /// </summary>
        private static string FindChromePath()
        {
            string[] regPaths =
            {
                @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
                @"HKEY_CLASSES_ROOT\ChromeHTML\shell\open\command"
            };

            foreach (var p in regPaths)
            {
                var val = Registry.GetValue(p, null, null) as string;
                if (string.IsNullOrEmpty(val)) continue;

                var parts = val.Split('"');
                string candidate = null;

                if (val.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(val))
                    candidate = val;
                else if (parts.Length >= 2 && File.Exists(parts[1]))
                    candidate = parts[1];

                if (!string.IsNullOrEmpty(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Otwiera stronę produktu w Chrome, a raz na N minut najpierw "rozgrzewa" domenę główną.
        /// </summary>
        public static async Task OpenInChromeWithWarmupAsync(string productUrl, int warmupMinutes = 10, int delayMs = 3000)
        {
            if (string.IsNullOrWhiteSpace(productUrl)) return;

            string chrome = FindChromePath();

            if (string.IsNullOrEmpty(chrome) || !File.Exists(chrome))
            {
                // fallback do przeglądarki domyślnej
                var psi = new ProcessStartInfo { FileName = productUrl, UseShellExecute = true };
                Process.Start(psi);
                return;
            }

            const string home = "https://www.vaurioajoneuvo.fi/";

            var now = DateTime.UtcNow;
            lock (_lock)
            {
                if (_lastChromeWarmupUtc < now.AddMinutes(-warmupMinutes))
                {
                    try { Process.Start(chrome, home); } catch { }
                    _lastChromeWarmupUtc = now;
                }
            }

            // krótka przerwa na clearance Cloudflare
            await Task.Delay(delayMs);

            try { Process.Start(chrome, productUrl); }
            catch
            {
                var psi = new ProcessStartInfo { FileName = productUrl, UseShellExecute = true };
                Process.Start(psi);
            }
        }
    }
}
