using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace vaurioajoneuvo_finder
{
    public class WebView2Automation : IDisposable
    {
        private WebView2 _wv;                 // ukryty kontroler WebView2
        private readonly string _profilePath; // ścieżka do profilu
        private bool _disposed;

        public WebView2Automation(string profilePath)
        {
            _profilePath = profilePath ?? throw new ArgumentNullException(nameof(profilePath));
            _wv = CreateHiddenWebView2();
        }

        private WebView2 CreateHiddenWebView2()
        {
            return new WebView2
            {
                Visible = false,
                Width = 1,
                Height = 1
            };
        }

        /// <summary>
        /// Inicjalizacja WebView2 z podanym folderem profilu
        /// </summary>
        public async Task InitializeAsync(Form hostForm)
        {
            if (_wv.CoreWebView2 != null)
                return;

            if (_wv.Parent == null)
                hostForm.Controls.Add(_wv);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
            await _wv.EnsureCoreWebView2Async(env);

            Logger.Log("[WV2] Inicjalizacja zakończona");
        }

        /// <summary>
        /// Re-inicjalizacja WebView2 (np. po błędzie ArgumentException)
        /// </summary>
        public async Task ReinitAsync(Form hostForm = null)
        {
            try
            {
                Logger.Log("[WV2] Reinit WebView2...");

                try { _wv?.CoreWebView2?.Stop(); } catch { }

                if (_wv != null)
                {
                    try { _wv.Dispose(); } catch { }
                    _wv = null;
                }

                _wv = CreateHiddenWebView2();
                if (hostForm != null && _wv.Parent == null)
                    hostForm.Controls.Add(_wv);

                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
                await _wv.EnsureCoreWebView2Async(env);

                await Task.Delay(2000);

                Logger.Log("[WV2] Reinit OK");
            }
            catch (Exception ex)
            {
                Logger.LogEx("[WV2] ReinitAsync błąd", ex);
            }
        }

        /// <summary>
        /// Ładuje stronę i zwraca jej HTML jako string
        /// </summary>
        public async Task<string> GetHtmlAsync(string url, int timeoutMs = 30000)
        {
            try
            {
                var core = _wv?.CoreWebView2;
                if (core == null)
                {
                    Logger.Log("[WV2] WebView2 nie jest zainicjalizowany");
                    return string.Empty;
                }

                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                ulong currentNavId = 0;

                EventHandler<CoreWebView2NavigationStartingEventArgs> starting = (s, e) =>
                {
                    if (currentNavId == 0)
                        currentNavId = e.NavigationId;
                };

                EventHandler<CoreWebView2NavigationCompletedEventArgs> completed = null;
                completed = async (s, e) =>
                {
                    try
                    {
                        if (currentNavId != 0 && e.NavigationId != currentNavId)
                            return;

                        if (e.IsSuccess)
                        {
                            await Task.Delay(500);

                            int attemptTimeout = timeoutMs;
                            for (int i = 0; i < 3; i++)
                            {
                                string htmlJson = await core.ExecuteScriptAsync("document.documentElement.outerHTML");
                                string html = UnwrapJsString(htmlJson);

                                bool isValid =
                                    !string.IsNullOrEmpty(html)
                                    && html.Length > 20000
                                    && !LooksLikeCloudflare(html)
                                    && (
                                        html.Contains("button-buy")
                                        || html.Contains("item-lift-title")
                                        || html.Contains("<title>")
                                    );

                                if (isValid || html.Length > 100000)
                                {
                                    Logger.Log($"[WV2] Gotowy HTML: {html.Length} bajtów (próba {i + 1})");
                                    tcs.TrySetResult(html);
                                    return;
                                }

                                if (string.IsNullOrEmpty(html) || html.Length < 5000)
                                    Logger.Log("[ERR_CF_EMPTY] HTML pusty / zbyt krótki");
                                else if (html.IndexOf("cf-challenge", StringComparison.OrdinalIgnoreCase) >= 0
                                            || html.IndexOf("Cloudflare Ray ID", StringComparison.OrdinalIgnoreCase) >= 0)
                                        Logger.Log("[ERR_CF_CHALLENGE] Wykryto stronę challenge Cloudflare");
                                else
                                    Logger.Log($"[ERR_CF_UNKNOWN] HTML wygląda źle (len={html.Length})");

                                attemptTimeout = Math.Min(attemptTimeout * 2, 120_000);
                                int delay = new Random().Next(300, 800) + attemptTimeout / 10;
                                Logger.Log($"[WV2] Retry {i + 1} za {delay}ms (timeout={attemptTimeout}ms)");
                                await Task.Delay(delay);
                            }
                        }

                        Logger.Log("[WV2] Nawigacja zakończona, ale HTML nadal wygląda źle");
                        tcs.TrySetResult(string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogEx("[WV2] Błąd podczas pobierania HTML", ex);
                        tcs.TrySetResult(string.Empty);
                    }
                    finally
                    {
                        try { core.NavigationCompleted -= completed; } catch { }
                        try { core.NavigationStarting -= starting; } catch { }
                    }
                };

                core.NavigationStarting += starting;
                core.NavigationCompleted += completed;

                core.Navigate(url);

                using (var cts = new CancellationTokenSource(timeoutMs))
                using (cts.Token.Register(() =>
                {
                    try { core.Stop(); } catch { }
                    tcs.TrySetResult(string.Empty);
                }))
                {
                    return await tcs.Task;
                }
            }
            catch (Exception ex)
            {
                Logger.LogEx("[WV2] Krytyczny błąd GetHtmlAsync", ex);
                return string.Empty;
            }
        }



        private static string UnwrapJsString(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<string>(raw);
            }
            catch
            {
                return raw.Trim('"')
                    .Replace("\\u003C", "<")
                    .Replace("\\u003E", ">")
                    .Replace("\\u0026", "&")
                    .Replace("\\\"", "\"");
            }
        }

        // używane w GetHtmlAsync
        public static bool LooksLikeCloudflare(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                Logger.Log("[CF_CHECK] Pusty HTML - uznaję za Cloudflare");
                return true;
            }

            // ✅ HTML ogromny — uznajemy za poprawny
            if (html.Length > 100000)
            {
                Logger.Log($"[CF_CHECK] Duży HTML ({html.Length} bajtów) - uznaję za poprawny");
                return false;
            }

            bool isCloudflare = html.IndexOf("Just a moment", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("cloudflare", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("cf-", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("challenge", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("Checking your browser", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("DDoS protection", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("Please enable JavaScript", StringComparison.OrdinalIgnoreCase) >= 0
                || html.Length < 1500;


            Logger.Log($"[CF_CHECK] Wynik: {isCloudflare}, długość HTML: {html.Length}");
            return isCloudflare;
        }

        // --- nowa metoda ---
        private void SafeDisposeWebView2(WebView2 webView)
        {
            if (webView == null) return;

            try
            {
                if (webView.CoreWebView2 != null)
                {
                    try { webView.CoreWebView2.Stop(); } catch { }
                }

                webView.Dispose();
                Logger.Log("[WV2] WebView2 poprawnie zwolniony (SafeDispose)");
            }
            catch (Exception ex)
            {
                Logger.LogEx("[WV2] SafeDispose error", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_wv != null)
                {
                    SafeDisposeWebView2(_wv);
                    _wv = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogEx("[WV2] Błąd Dispose", ex);
            }
        }

        // --- Można użyć z zewnątrz ---
        public void SafeDispose()
        {
            SafeDisposeWebView2(_wv);
            _wv = null;
        }

        public WebView2 Control => _wv;
    }
}
