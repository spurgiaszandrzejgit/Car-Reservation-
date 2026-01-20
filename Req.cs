using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using vaurioajoneuvo_finder;

namespace vaurioajoneuvo_finder1
{
    /// <summary>
    /// Lightweight HTTP client based on HttpWebRequest that mimics a real browser.
    /// Designed to work with .NET Framework / C# 7.3.
    /// </summary>
    public class Req
    {
        // Keep cookies across requests so the site can set/see them (often required)
        private static readonly CookieContainer Cookies = new CookieContainer();

        /// <summary>
        /// Builds a pre-configured HttpWebRequest that looks like Chrome.
        /// </summary>
        private static HttpWebRequest CreateRequest(string uri, string referer)
        {
            var req = (HttpWebRequest)WebRequest.Create(uri);
            req.Method = "GET";
            req.AllowAutoRedirect = true;
            req.CookieContainer = Cookies;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            // Browser-like headers
            var userAgents = new[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/120.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0 Safari/537.36"
            };

            req.UserAgent = userAgents[new Random().Next(userAgents.Length)];

            req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
            req.Headers.Add(HttpRequestHeader.AcceptLanguage, "fi-FI,fi;q=0.9,en-US;q=0.8,en;q=0.7,ru;q=0.6");
            req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            req.Referer = string.IsNullOrEmpty(referer) ? "https://www.vaurioajoneuvo.fi/" : referer;

            req.Headers.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
            req.Headers.Add("sec-ch-ua-mobile", "?0");
            req.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            req.Headers.Add("sec-fetch-site", "same-origin");
            req.Headers.Add("sec-fetch-mode", "navigate");
            req.Headers.Add("sec-fetch-user", "?1");
            req.Headers.Add("sec-fetch-dest", "document");
            req.Headers.Add("upgrade-insecure-requests", "1");
            req.Headers.Add("cache-control", "max-age=0");

            req.KeepAlive = true;
            req.Timeout = 25000;        // 25s
            req.ReadWriteTimeout = 25000;
            req.ServicePoint.ConnectionLimit = 10;
            req.ServicePoint.Expect100Continue = false;

            return req;
        }

        /// <summary>
        /// Downloads page HTML as a string. Returns empty string on failure.
        /// Use this first; if the result looks like a Cloudflare challenge,
        /// your caller should fallback to WebView2.
        /// </summary>
        public string GetBodyPage(string uri, string referer = "https://www.vaurioajoneuvo.fi/")
        {
            Logger.Log($"[HTTP] Request: {uri}, Referer: {referer}");

            try
            {
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                var req = CreateRequest(uri, referer);

                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;

                Logger.Log($"[HTTP] Sending request to: {uri}");
                var stopwatch = Stopwatch.StartNew();

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                {
                    if (stream == null)
                    {
                        Logger.Log($"[HTTP] Empty response stream for: {uri}");
                        return string.Empty;
                    }

                    Encoding enc = Encoding.UTF8;
                    try
                    {
                        if (!string.IsNullOrEmpty(resp.CharacterSet))
                            enc = Encoding.GetEncoding(resp.CharacterSet);
                    }
                    catch
                    {
                        enc = Encoding.UTF8;
                    }

                    using (var reader = new StreamReader(stream, enc))
                    {
                        var html = reader.ReadToEnd();
                        stopwatch.Stop();

                        Logger.Log($"[HTTP] Response received: {uri}, Status: {resp.StatusCode}, " +
                            $"Size: {html.Length} bytes, Time: {stopwatch.ElapsedMilliseconds}ms");

                        return html;
                    }
                }
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
            {
                Logger.Log($"[HTTP] Timeout for: {uri}");
                return string.Empty;
            }
            catch (WebException ex)
            {
                var status = ex.Status;
                var response = ex.Response as HttpWebResponse;
                var statusCode = response?.StatusCode.ToString() ?? "No response";

                Logger.Log($"[HTTP] WebException for {uri}: Status={status}, HTTP={statusCode}, Message={ex.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Log($"[HTTP] Exception for {uri}: {ex.GetType().Name} - {ex.Message}");
                return string.Empty;
            }
        }

    }
}
