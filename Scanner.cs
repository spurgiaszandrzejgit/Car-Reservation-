using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using vaurioajoneuvo_finder1;
using System.IO;
using System.Security.Policy;

namespace vaurioajoneuvo_finder
{
    public class Scanner : IDisposable
    {
        private static readonly string AutoReservedPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoReserved.txt");
        private static readonly string ExcludePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcludeFromSearch.txt");

        private readonly HashSet<string> _autoReserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly WebView2Automation _wv2;
        private bool _disposed;

        // cashe strony głównej
        private string _lastMainPageHtml = null;
        private int cfFailuresInRow = 0;

        public Scanner(WebView2Automation wv2)
        {
            _wv2 = wv2 ?? throw new ArgumentNullException(nameof(wv2));

            // wczytanie AutoReserved.txt
            if (File.Exists(AutoReservedPath))
            {
                foreach (var ln in File.ReadAllLines(AutoReservedPath))
                    if (!string.IsNullOrWhiteSpace(ln))
                        _autoReserved.Add(ln.Trim());
            }

            // wczytanie ExcludeFromSearch.txt
            if (File.Exists(ExcludePath))
            {
                foreach (var ln in File.ReadAllLines(ExcludePath))
                    if (!string.IsNullOrWhiteSpace(ln))
                        _exclude.Add(ln.Trim());
            }
        }

        public async Task InitAsync(Form hostForm)
        {
            await _wv2.InitializeAsync(hostForm);
        }

        /// <summary>
        /// Pobiera HTML strony głównej, parsuje oferty i filtruje po cenie.
        /// </summary>
        public async Task<List<Oferta>> RunAsync(int minPrice,int maxPrice,int minYear,int maxYear,int maxCount,Form hostForm)
        {
            ReloadLists();
            Logger.Log("[SCAN] Startuję skanowanie strony głównej");

            await _wv2.InitializeAsync(hostForm);
            var url = "https://www.vaurioajoneuvo.fi/?condition=no_demo";

            // ✅ smart fetch
            string html = await GetBodyPageSmartAsync(url, hostForm);

            if (string.IsNullOrEmpty(html) && !string.IsNullOrEmpty(_lastMainPageHtml))
            {
                Logger.Log("[SCAN][FALLBACK] ❗ Nowy fetch się nie udał – używam cache");
                html = _lastMainPageHtml;
            }

            if (string.IsNullOrEmpty(html))
            {
                Logger.Log("[SCAN] ❌ Brak HTML – przerywam");
                return new List<Oferta>();
            }

            Logger.Log($"[SCAN] Pobrano HTML ({html.Length} bajtów)");

            var all = ParseModernHtml(html, maxCount);
            Logger.Log($"[SCAN] Sparsowano {all.Count} ofert");

            // normalizacja granic
            if (minPrice < 0) minPrice = 0;
            if (maxPrice < 0) maxPrice = 0;

            bool useYearFilter = (minYear > 0) || (maxYear > 0);

            var filtered = new List<Oferta>();
            foreach (var o in all)
            {
                if (_autoReserved.Contains(o.Url))
                {
                    Logger.Log($"[SCAN][SKIP] Oferta już była rezerwowana → {o.Url}");
                    continue;
                }

                if (_exclude.Contains(o.Url))
                {
                    Logger.Log($"[SCAN][SKIP] Oferta wykluczona (ExcludeFromSearch) → {o.Url}");
                    continue;
                }

                if (!int.TryParse(o.Price.Replace("€", "").Replace(" ", ""), out int price))
                {
                    Logger.Log($"[SCAN][SKIP] Nie można sparsować ceny: {o.Price} → {o.Url}");
                    continue;
                }

                // ✅ Min/Max price
                if (price < minPrice)
                {
                    Logger.Log($"[SCAN][SKIP] cena {price} < {minPrice}: {o.Url}");
                    continue;
                }

                if (price > maxPrice)
                {
                    Logger.Log($"[SCAN][SKIP] cena {price} > {maxPrice}: {o.Url}");
                    continue;
                }

                // ✅ Min/Max year (0 = brak limitu)
                if (useYearFilter)
                {
                    if (o.Year == 0)
                    {
                        Logger.Log($"[SCAN][SKIP] brak roku (Year=0) przy aktywnym filtrze → {o.Url}");
                        continue;
                    }

                    if (minYear > 0 && o.Year < minYear)
                    {
                        Logger.Log($"[SCAN][SKIP] rok {o.Year} < {minYear}: {o.Url}");
                        continue;
                    }

                    if (maxYear > 0 && o.Year > maxYear)
                    {
                        Logger.Log($"[SCAN][SKIP] rok {o.Year} > {maxYear}: {o.Url}");
                        continue;
                    }
                }

                filtered.Add(o);
                Logger.Log($"[SCAN][ADD] {o.Header} | {o.Price} | rok={o.Year} | {o.Url}");
            }

            var validated = new List<Oferta>();
            foreach (var o in filtered)
            {
                bool ok = await IsProductAvailableAsync(o.Url, hostForm);
                if (!ok)
                {
                    Logger.Log($"[SCAN][SKIP404] Strona 404 (content) → {o.Url}");
                    try
                    {
                        File.AppendAllText(ExcludePath, o.Url + Environment.NewLine);
                        _exclude.Add(o.Url);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogEx("[SCAN][SKIP404] Nie udało się dopisać do ExcludeFromSearch.txt", ex);
                    }
                    continue;
                }
                validated.Add(o);
            }

            Logger.Log($"[SCAN] Wynik końcowy po walidacji 404: {validated.Count} ofert");
            return validated;
        }



        // --- Smart Fetch jak w Form1 ---
        public async Task<string> GetBodyPageSmartAsync(string url, Form hostForm = null)
        {
            //Logger.Log($"[LOG][MAIN] Pobieram stronę główną : {url}");
            try
            {
                // --- MAIN PAGE ---
                if (url.Equals("https://www.vaurioajoneuvo.fi/?condition=no_demo", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log($"[FETCH][MAIN] Pobieram stronę główną (bez cache): {url}");

                    string viaWebView = await _wv2.GetHtmlAsync(url, 60000);

                    // 🔁 Retry logic
                    int attempts = 0;
                    while ((string.IsNullOrEmpty(viaWebView) || viaWebView.Length < 1000) && attempts < 3)
                    {
                        attempts++;
                        Logger.Log($"[FETCH][MAIN] Retry #{attempts} po Reinit...");

                        await _wv2.ReinitAsync(hostForm);

                        // mała pauza żeby WebView2 się ustabilizował
                        await Task.Delay(2000);

                        viaWebView = await _wv2.GetHtmlAsync(url, 60000);
                    }

                    if (!string.IsNullOrEmpty(viaWebView) && viaWebView.Length > 1000 &&
                        !WebView2Automation.LooksLikeCloudflare(viaWebView))
                    {
                        Logger.Log($"[FETCH][MAIN] WebView2 sukces: {viaWebView.Length} bajtów");
                        _lastMainPageHtml = viaWebView;
                        return viaWebView;
                    }
                    else
                    {
                        string preview = viaWebView?.Substring(0, Math.Min(200, viaWebView.Length))
                                                   .Replace("\n", " ")
                                                   .Replace("\r", " ");
                        Logger.Log($"[FETCH][DEBUG][MAIN] Odrzucony HTML (len={viaWebView?.Length ?? 0}): {preview}");
                    }

                    Logger.Log("[FETCH][MAIN] ❌ Nie udało się pobrać strony głównej");
                    return string.Empty;
                }

                // --- PRODUCTS ---
                Logger.Log($"[FETCH] Próba HTTP: {url}");

                if (cfFailuresInRow >= 3)
                {
                    Logger.Log($"[CF_OPTIMIZE] Od razu WebView2 dla {url}");
                    return await _wv2.GetHtmlAsync(url, 60000);
                }

                var html = new vaurioajoneuvo_finder1.Req().GetBodyPage(url);

                if (!WebView2Automation.LooksLikeCloudflare(html) && !string.IsNullOrEmpty(html) && html.Length > 1000)
                {
                    Logger.Log($"[FETCH] HTTP sukces: {html.Length} bajtów");
                    cfFailuresInRow = 0;
                    return html;
                }

                cfFailuresInRow++;
                Logger.Log($"[FETCH] Wykryto Cloudflare dla {url} (kolejno {cfFailuresInRow}) → fallback do WebView2");

                string viaWebViewProd = await _wv2.GetHtmlAsync(url, 60000);

                // 🔁 Retry logic dla produktów
                int prodAttempts = 0;
                while ((string.IsNullOrEmpty(viaWebViewProd) || viaWebViewProd.Length < 1000) && prodAttempts < 3)
                {
                    prodAttempts++;
                    Logger.Log($"[FETCH][PROD] Retry #{prodAttempts} po Reinit...");

                    await _wv2.ReinitAsync(hostForm);
                    await Task.Delay(2000);

                    viaWebViewProd = await _wv2.GetHtmlAsync(url, 60000);
                }

                if (!string.IsNullOrEmpty(viaWebViewProd) && viaWebViewProd.Length > 1000 &&
                    !WebView2Automation.LooksLikeCloudflare(viaWebViewProd))
                {
                    Logger.Log($"[FETCH] WebView2 sukces: {viaWebViewProd.Length} bajtów");
                    cfFailuresInRow = 0;
                    return viaWebViewProd;
                }
                else
                {
                    string preview = viaWebViewProd?.Substring(0, Math.Min(200, viaWebViewProd.Length))
                                                    .Replace("\n", " ")
                                                    .Replace("\r", " ");
                    Logger.Log($"[FETCH][DEBUG][PROD] Odrzucony HTML (len={viaWebViewProd?.Length ?? 0}): {preview}");
                }

                Logger.Log("[FETCH] WebView2 również nie powiódł się");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogEx($"[FETCH] Błąd w GetBodyPageSmartAsync dla {url}", ex);
                return string.Empty;
            }
        }


        /// <summary>
        /// Parsuje HTML i zwraca listę ofert
        /// </summary>
        public List<Oferta> ParseModernHtml(string html, int maxCount = 0)
        {
            var items = new List<Oferta>();
            if (string.IsNullOrEmpty(html))
            {
                Logger.Log("[PARSE] Pusty HTML");
                return items;
            }

            try
            {
                var itemContainers = html.Split(new[] { "item-lift-container" }, StringSplitOptions.None);
                Logger.Log($"[PARSE] Znaleziono {itemContainers.Length - 1} kontenerów produktów");

                int parsed = 0;
                for (int i = 1; i < itemContainers.Length; i++)
                {
                    if (maxCount > 0 && parsed >= maxCount)
                    {
                        Logger.Log($"[PARSE] Osiągnięto limit {maxCount}");
                        break;
                    }

                    parsed++;

                    var container = itemContainers[i];
                    string url = ExtractValue(container, "href=\"", "\"");
                    if (string.IsNullOrEmpty(url) || !url.Contains("/tuote/"))
                        continue;
                    if (!url.StartsWith("http"))
                        url = "https://www.vaurioajoneuvo.fi" + url;

                    string title = ExtractTitle(container);
                    int priceVal = ExtractPriceUnified(container);
                    if (priceVal == 0) continue;

                    int year = ExtractYearFromContainer(container);

                    string imgUrl = ExtractValue(container, "data-lazy=\"", "\"");
                    if (string.IsNullOrEmpty(imgUrl))
                        imgUrl = ExtractValue(container, "src=\"", "\"");

                    items.Add(new Oferta
                    {
                        Header = title ?? "Brak tytułu",
                        Url = url,
                        Price = priceVal + " €",
                        ImgUrl = imgUrl,
                        Year = year
                    });

                    Logger.Log($"[PARSE] Dodano ofertę: {title} | {priceVal}€ | {year} Year | {url}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogEx("[PARSE] Błąd parsowania HTML", ex);
            }

            return items;
        }

        // ---------------- POMOCNICZE ----------------
        private string ExtractValue(string text, string startMarker, string endMarker)
        {
            try
            {
                int start = text.IndexOf(startMarker);
                if (start < 0) return string.Empty;
                start += startMarker.Length;
                int end = text.IndexOf(endMarker, start);
                if (end < 0) return string.Empty;
                return text.Substring(start, end - start).Trim();
            }
            catch { return string.Empty; }
        }

        private string ExtractTitle(string html)
        {
            try
            {
                var block = ExtractValue(html, "item-lift-title", "</div>");
                if (string.IsNullOrEmpty(block)) return string.Empty;

                var h2 = Regex.Match(block, @"<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline);
                var part1 = h2.Success ? h2.Groups[1].Value.Trim() : "";

                var strong = Regex.Match(block, @"<strong[^>]*>(.*?)</strong>", RegexOptions.Singleline);
                var part2 = strong.Success ? strong.Groups[1].Value.Trim() : "";

                return HttpUtility.HtmlDecode((part1 + " " + part2).Trim());
            }
            catch { return string.Empty; }
        }

        private int ExtractPriceUnified(string html)
        {
            try
            {
                var match1 = Regex.Match(
                    html,
                    @"<strong[^>]*class\s*=\s*""[^""]*item-lift-price-now[^""]*""[^>]*>\s*([\d\s]+)€",
                    RegexOptions.IgnoreCase
                );
                if (match1.Success)
                {
                    var text = match1.Groups[1].Value.Replace(" ", "").Trim();
                    if (int.TryParse(text, out int price)) return price;
                }

                var match2 = Regex.Match(
                    html,
                    @"<p[^>]*class\s*=\s*""[^""]*price[^""]*""[^>]*>\s*([\d\s]+)€",
                    RegexOptions.IgnoreCase
                );
                if (match2.Success)
                {
                    var text = match2.Groups[1].Value.Replace(" ", "").Trim();
                    if (int.TryParse(text, out int price)) return price;
                }
            }
            catch { }
            return 0;
        }

        private static int ExtractYearFromContainer(string container)
        {
            if (string.IsNullOrWhiteSpace(container)) return 0;

            var m = Regex.Match(
                container,
                @"item-lift-details.*?<span[^>]*>\s*(19\d{2}|20\d{2})\s*\*?\s*,?\s*</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (m.Success && int.TryParse(m.Groups[1].Value, out int year))
                return year;

            var m2 = Regex.Match(
                container,
                @"item-lift-details[\s\S]{0,300}?\b(19\d{2}|20\d{2})\b",
                RegexOptions.IgnoreCase
            );

            if (m2.Success && int.TryParse(m2.Groups[1].Value, out year))
                return year;

            return 0;
        }


        public void ReloadLists()
        {
            _autoReserved.Clear();
            if (File.Exists(AutoReservedPath))
            {
                foreach (var ln in File.ReadAllLines(AutoReservedPath))
                    if (!string.IsNullOrWhiteSpace(ln))
                        _autoReserved.Add(ln.Trim());
                Logger.Log($"[SCAN] AutoReserved lists reloaded");
            }

            _exclude.Clear();
            if (File.Exists(ExcludePath))
            {
                foreach (var ln in File.ReadAllLines(ExcludePath))
                    if (!string.IsNullOrWhiteSpace(ln))
                        _exclude.Add(ln.Trim());
                Logger.Log($"[SCAN] Exclude lists reloaded");
            }
        }

        public bool IsExcluded(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            url = url.Trim();

            return _autoReserved.Contains(url) || _exclude.Contains(url);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _wv2?.SafeDispose();   // ✅ teraz używa bezpiecznego zwolnienia
            }
            catch (Exception ex)
            {
                Logger.LogEx("[SCAN] Błąd podczas zwalniania WebView2Automation", ex);
            }

            Logger.Log("[SCAN] Zwolniono zasoby (WebView2Automation)");
        }

        private static bool LooksLike404(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return true;

            if (html.IndexOf(">404<", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (html.IndexOf("Sivua ei löytynyt", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (html.IndexOf("class=\"breadtext\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (html.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 html.IndexOf("Sivua ei löytynyt", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            return false;
        }

        public async Task<bool> IsProductAvailableAsync(string url, Form hostForm = null)
        {
            var html = await GetBodyPageSmartAsync(url, hostForm);
            if (string.IsNullOrEmpty(html)) return false;
            return !LooksLike404(html);           
        }

    }
}
