using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using vaurioajoneuvo_finder;
using System.Collections.Concurrent;

namespace vaurioajoneuvo_finder1
{
    /// <summary>
    /// Klasa odpowiedzialna za automatyczną rezerwację ofert.
    /// </summary>
    public class AutoReserver
    {
        private readonly string _profilePath;
        private readonly Queue<Oferta> _queue = new Queue<Oferta>();
        private readonly object _queueLock = new object();
        private volatile bool _workerRunning = false;
        private volatile bool _inProgress = false;
        private readonly HashSet<string> _alreadyReserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly string AutoReservedPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoReserved.txt");

        private static readonly Random _rnd = new Random();

        private const int MaxUnavailableAttempts = 3;

        private readonly ConcurrentDictionary<string, int> _unavailableAttempts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static readonly string ExcludePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcludeFromSearch.txt");

        public bool AutoReserveEnabled { get; set; } = true;
        public bool ShowWindow { get; set; } = true;
        public int TimeoutMs { get; set; } = 50000;
        public bool AlsoOpenChrome { get; set; } = true;
        public bool AutoReserveAllFound { get; set; } = true;

        public AutoReserver(string profilePath)
        {
            _profilePath = profilePath;

            // wczytanie listy już zarezerwowanych
            if (File.Exists(AutoReservedPath))
            {
                foreach (var ln in File.ReadAllLines(AutoReservedPath))
                    if (!string.IsNullOrWhiteSpace(ln))
                        _alreadyReserved.Add(ln.Trim());
            }
        }

        /// <summary>
        /// Dodaje ofertę do kolejki rezerwacji.
        /// </summary>
        public void Enqueue(Oferta oferta)
        {
            if (!AutoReserveEnabled)
            {
                Logger.Log($"[QUEUE][SKIP] AutoReserve wyłączony → {oferta.Url}");
                return;
            }

            lock (_queueLock)
            {
                if (_queue.Any(q => q.Url.Equals(oferta.Url, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Log($"[QUEUE][SKIP] Oferta już w kolejce → {oferta.Url}");
                    return;
                }

                if (_alreadyReserved.Contains(oferta.Url))
                {
                    Logger.Log($"[QUEUE][SKIP] Oferta już była rezerwowana → {oferta.Url}");
                    return;
                }

                _queue.Enqueue(oferta);
                Logger.Log($"[QUEUE] Dodano ofertę do kolejki: {oferta.Url}");
            }

            EnsureWorker();
        }

        /// Uruchamia worker obsługujący kolejkę.
        public void EnsureWorker()
        {
            if (!AutoReserveEnabled) return;

            lock (_queueLock)
            {
                if (_workerRunning || _queue.Count == 0) return;
                _workerRunning = true;
            }

            Logger.Log("[QUEUE] Uruchamiam worker do obsługi kolejki");
            _ = Task.Run(ProcessQueueAsync);
        }

        public void ClearQueue()
        {
            lock (_queueLock)
            {
                _queue.Clear();
                _workerRunning = false;
                _inProgress = false;
            }
            Logger.Log("[QUEUE] Kolejka wyczyszczona");
        }

        /// Obsługuje kolejkę rezerwacji.
        private async Task ProcessQueueAsync()
        {
            try
            {
                while (true)
                {
                    Oferta item;
                    lock (_queueLock)
                    {
                        if (_queue.Count == 0) break;
                        item = _queue.Dequeue();
                    }

                    Logger.Log($"[AUTO] Rozpoczynam rezerwację: {item.Url}");
                    _inProgress = true;

                    try
                    {
                        if (AlsoOpenChrome)
                        {
                            Logger.Log("[AUTO] Otwieram Chrome w tle (najpierw główna)...");
                            try
                            {
                                // otwórz główną stronę
                                await ChromeHelper.OpenInChromeWithWarmupAsync("https://www.vaurioajoneuvo.fi/?condition=no_demo", 10, 3000);

                                int delayHome = _rnd.Next(2000, 4000);
                                Logger.Log($"[AUTO] Pauza po otwarciu strony głównej: {delayHome}ms");
                                await Task.Delay(delayHome);

                                // otwórz produkt
                                await ChromeHelper.OpenInChromeWithWarmupAsync(item.Url, 10, 3000);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogEx("[AUTO] Błąd podczas otwierania w Chrome", ex);
                            }
                        }

                        //bool reserved = await TryReserveAsync(item.Url);

                        //if (reserved)
                        //{
                        //    Logger.Log($"[AUTO] ✅ Rezerwacja zakończona sukcesem: {item.Url}");
                        //    if (_alreadyReserved.Add(item.Url))
                        //        File.AppendAllText(AutoReservedPath, item.Url + Environment.NewLine);

                        //    // usuń z grida (UI w Form1)
                        //    try
                        //    {
                        //        Form activeForm = Application.OpenForms["Form1"];
                        //        if (activeForm is Form1 form)
                        //        {
                        //            form.Invoke(new Action(() =>
                        //            {
                        //                if (form.Controls["dataGridView1"] is DataGridView grid)
                        //                {
                        //                    foreach (DataGridViewRow row in grid.Rows)
                        //                    {
                        //                        if (row.Cells["Link"].Value?.ToString() == item.Url)
                        //                        {
                        //                            grid.Rows.Remove(row);
                        //                            Logger.Log($"[UI] Usunięto z grida: {item.Url}");
                        //                            break;
                        //                        }
                        //                    }
                        //                }
                        //            }));
                        //        }
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Logger.LogEx("[UI] Błąd podczas usuwania z grida", ex);
                        //    }

                        //    try
                        //    {
                        //        await Notifier.SendEmailAsync(
                        //            "🎉 SUKCES: Produkt zarezerwowany!",
                        //            $"<b>{DateTime.Now:dd.MM.yyyy HH:mm}</b><br/>{item.Header}<br/>Cena - {item.Price}<br/><br/>Masz 3 min aby zapłacić!!",
                        //            item.Url,
                        //            item.ImgUrl
                        //        );
                        //        Logger.Log("[NOTIFY] Email wysłany");
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Logger.LogEx("[NOTIFY] Błąd wysyłania emaila", ex);
                        //    }

                        //    await Notifier.SendTelegramPhotoAsync(
                        //        $"🎉 SUKCES: Produkt zarezerwowany!\n{DateTime.Now:dd.MM.yyyy HH:mm}\n{item.Header}\nCena - {item.Price}\nMasz 3 min aby zapłacić!!",
                        //        item.Url,
                        //        item.ImgUrl
                        //    );
                        //    Logger.Log("[NOTIFY] Telegram wysłany");
                        //}
                        //else
                        //{
                        //    Logger.Log($"[AUTO] ❌ Nie udało się zarezerwować: {item.Url}");
                        //}

                        var result = await TryReserveAsync(item.Url);

                        if (result == ReserveResult.Success)
                        {
                            Logger.Log($"[AUTO] ✅ Rezerwacja zakończona sukcesem: {item.Url}");
                            if (_alreadyReserved.Add(item.Url))
                                File.AppendAllText(AutoReservedPath, item.Url + Environment.NewLine);

                            // usuń z grida (UI w Form1)
                            try
                            {
                                Form activeForm = Application.OpenForms["Form1"];
                                if (activeForm is Form1 form)
                                {
                                    form.Invoke(new Action(() =>
                                    {
                                        if (form.Controls["dataGridView1"] is DataGridView grid)
                                        {
                                            foreach (DataGridViewRow row in grid.Rows)
                                            {
                                                if (row.Cells["Link"].Value?.ToString() == item.Url)
                                                {
                                                    grid.Rows.Remove(row);
                                                    Logger.Log($"[UI] Usunięto z grida: {item.Url}");
                                                    break;
                                                }
                                            }
                                        }
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogEx("[UI] Błąd podczas usuwania z grida", ex);
                            }

                            //try
                            //{
                            //    await Notifier.SendEmailAsync(
                            //        "🎉 SUKCES: Produkt zarezerwowany!",
                            //        $"<b>{DateTime.Now:dd.MM.yyyy HH:mm}</b><br/>{item.Header}<br/>Cena - {item.Price}<br/><br/>Masz 3 min aby zapłacić!!",
                            //        item.Url,
                            //        item.ImgUrl
                            //    );
                            //    Logger.Log("[NOTIFY] Email wysłany");
                            //}
                            //catch (Exception ex)
                            //{
                            //    Logger.LogEx("[NOTIFY] Błąd wysyłania emaila", ex);
                            //}

                            try
                            {
                                await Notifier.SendTelegramPhotoAsync(
                                $"🎉 SUKCES: Produkt zarezerwowany!\n{DateTime.Now:dd.MM.yyyy HH:mm}\n{item.Header}\nCena - {item.Price}\nMasz 3 min aby zapłacić!!",
                                item.Url,
                                item.ImgUrl
                                );
                                Logger.Log("[NOTIFY] Telegram wysłany");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogEx("[NOTIFY] Błąd wysyłania Telegram", ex);
                            }
                            
                        }
                        else if (result == ReserveResult.ButtonUnavailable)
                        {
                            // 1)
                            int cnt = _unavailableAttempts.AddOrUpdate(item.Url, 1, (_, old) => old + 1);

                            Logger.Log($"[AUTO] ⏸️ Przycisk niedostępny → {item.Url} (attempt {cnt}/{MaxUnavailableAttempts})");

                            // 2)
                            if (cnt >= MaxUnavailableAttempts)
                            {
                                Logger.Log($"[AUTO] 🚫 Osiągnięto {MaxUnavailableAttempts} prób → dodaję do ExcludeFromSearch: {item.Url}");
                                try
                                {
                                    File.AppendAllText(ExcludePath, item.Url + Environment.NewLine);
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogEx("[AUTO] Nie udało się dopisać do ExcludeFromSearch.txt", ex);
                                }
                            }

                        }
                        else
                        {
                            Logger.Log($"[AUTO] ❌ Nie udało się zarezerwować: {item.Url} (result={result})");
                        }


                        if (!AutoReserveAllFound && result == ReserveResult.Success)
                            break;

                    }
                    finally
                    {
                        _inProgress = false;
                    }

                    int delayBetween = _rnd.Next(5000, 10000);
                    Logger.Log($"[AUTO] Pauza {delayBetween}ms przed kolejną rezerwacją...");
                    await Task.Delay(delayBetween);
                }
            }
            finally
            {
                lock (_queueLock) { _workerRunning = false; }
                Logger.Log("[QUEUE] Obsługa kolejki zakończona");
            }
        }


        /// Przeprowadza próbę rezerwacji przez WebView2.
        //    private async Task<bool> TryReserveAsync(string productUrl, int maxRetries = 3)
        //    {
        //        if (string.IsNullOrWhiteSpace(productUrl)) return false;

        //        for (int attempt = 1; attempt <= maxRetries; attempt++)
        //        {
        //            Logger.Log($"[AUTO] Próba {attempt}/{maxRetries}: {productUrl}");
        //            await Task.Delay(_rnd.Next(3000, 6000)); // mała pauza

        //            var tcs = new TaskCompletionSource<bool>();
        //            bool success = false;

        //            var thread = new Thread(() =>
        //            {
        //                using (var frm = new Form
        //                {
        //                    Text = "Auto-rezerwacja",
        //                    Width = 1100,
        //                    Height = 800,
        //                    StartPosition = FormStartPosition.CenterScreen,
        //                    ShowInTaskbar = ShowWindow
        //                })
        //                using (var wv = new WebView2 { Dock = DockStyle.Fill })
        //                {
        //                    frm.Controls.Add(wv);

        //                    EventHandler<CoreWebView2NavigationStartingEventArgs> onStarting = null;
        //                    EventHandler<CoreWebView2NavigationCompletedEventArgs> onCompleted = null;

        //                    frm.Load += async (s, e) =>
        //                    {
        //                        try
        //                        {
        //                            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
        //                            await wv.EnsureCoreWebView2Async(env);

        //                            // detekcja /tilaus/ = sukces
        //                            onStarting = (s, e) =>
        //                            {
        //                                var uri = e.Uri ?? "";
        //                                Logger.Log($"[AUTO] Nawigacja: {uri}");

        //                                if (uri.Contains("/tilaus/"))
        //                                {
        //                                    Logger.Log("✅ Strona potwierdzenia rezerwacji wykryta!");
        //                                    success = true;
        //                                    tcs.TrySetResult(true);
        //                                    frm.Close();
        //                                }
        //                                if (uri.Contains("/captcha"))
        //                                {
        //                                    Logger.Log("⚠️ CAPTCHA wykryta");
        //                                    tcs.TrySetResult(false);
        //                                    frm.Close();
        //                                }
        //                            };
        //                            wv.CoreWebView2.NavigationStarting += onStarting;

        //                            // po załadowaniu strony produktu kliknij „Osta”
        //                            onCompleted = async (_, args2) =>
        //                            {
        //                                var currentUrl = wv.CoreWebView2.Source;
        //                                Logger.Log($"[AUTO] Załadowano stronę: {currentUrl}");

        //                                if (!currentUrl.Contains("tuote")) return;

        //                                await Task.Delay(_rnd.Next(3000, 5000));
        //                                const string jsClickOsta =
        //                                    @"(function(){ 
        //                                        let btn = document.querySelector('button.button.button-buy[type=""submit""]');
        //                                        if(btn){ btn.click(); return 'ok'; }
        //                                        return 'brak';
        //                                      })();";
        //                                var result = await wv.CoreWebView2.ExecuteScriptAsync(jsClickOsta);
        //                                Logger.Log($"[AUTO] Wynik kliknięcia: {result}");
        //                            };
        //                            wv.CoreWebView2.NavigationCompleted += onCompleted;

        //                            wv.Source = new Uri(productUrl);
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Logger.Log($"[AUTO] Błąd: {ex.Message}");
        //                            tcs.TrySetResult(false);
        //                            frm.Close();
        //                        }
        //                    };

        //                    frm.FormClosed += (s, e) =>
        //                    {
        //                        if (!tcs.Task.IsCompleted)
        //                            tcs.TrySetResult(success);
        //                    };

        //                    if (ShowWindow) frm.ShowDialog(); else Application.Run(frm);
        //                }
        //            });

        //            thread.SetApartmentState(ApartmentState.STA);
        //            thread.Start();

        //            var finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMs));
        //            if (finished == tcs.Task && tcs.Task.Result)
        //                return true;

        //            Logger.Log($"❌ Próba {attempt} nieudana.");
        //            if (attempt < maxRetries)
        //            {
        //                int delay = _rnd.Next(10000, 20000);
        //                Logger.Log($"⏳ Odczekam {delay / 1000}s przed kolejną próbą...");
        //                await Task.Delay(delay);
        //            }
        //        }

        //        return false;
        //    }
        //}

        private async Task<ReserveResult> TryReserveAsync(string productUrl, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(productUrl)) return ReserveResult.OtherFail;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                Logger.Log($"[AUTO] Próba {attempt}/{maxRetries}: {productUrl}");
                await Task.Delay(_rnd.Next(2000, 4000));

                var tcs = new TaskCompletionSource<ReserveResult>(TaskCreationOptions.RunContinuationsAsynchronously);

                var thread = new Thread(() =>
                {
                    using (var frm = new Form
                    {
                        Text = "Auto-rezerwacja",
                        Width = 1100,
                        Height = 800,
                        StartPosition = FormStartPosition.CenterScreen,
                        ShowInTaskbar = ShowWindow
                    })
                    using (var wv = new WebView2 { Dock = DockStyle.Fill })
                    {
                        frm.Controls.Add(wv);

                        EventHandler<CoreWebView2NavigationStartingEventArgs> onStarting = null;
                        EventHandler<CoreWebView2NavigationCompletedEventArgs> onCompleted = null;

                        frm.Load += async (s, e) =>
                        {
                            try
                            {
                                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
                                await wv.EnsureCoreWebView2Async(env);

                                onStarting = (s2, e2) =>
                                {
                                    var uri = e2.Uri ?? "";
                                    Logger.Log($"[AUTO] Nawigacja: {uri}");

                                    if (uri.Contains("/tilaus/"))
                                    {
                                        Logger.Log("✅ Strona potwierdzenia rezerwacji wykryta!");
                                        tcs.TrySetResult(ReserveResult.Success);
                                        frm.Close();
                                    }
                                    else if (uri.Contains("/captcha"))
                                    {
                                        Logger.Log("⚠️ CAPTCHA wykryta");
                                        tcs.TrySetResult(ReserveResult.Captcha);
                                        frm.Close();
                                    }
                                };
                                wv.CoreWebView2.NavigationStarting += onStarting;

                                onCompleted = async (_, args2) =>
                                {
                                    try
                                    {
                                        var currentUrl = wv.CoreWebView2.Source;
                                        Logger.Log($"[AUTO] Załadowano stronę: {currentUrl}");

                                        if (!currentUrl.Contains("tuote")) return;

                                        await Task.Delay(_rnd.Next(1200, 2000));

                                        // 1) Проверяем: есть ли кнопка и не disabled ли она
                                        const string jsCheckButton =
                                            @"(function(){
                                        let btn = document.querySelector('button.button.button-buy[type=""submit""]');
                                        if(!btn) return 'missing';
                                        if(btn.disabled) return 'disabled';
                                        let aria = (btn.getAttribute('aria-disabled')||'').toLowerCase();
                                        if(aria === 'true') return 'disabled';
                                        let cls = (btn.className||'').toLowerCase();
                                        if(cls.includes('disabled')) return 'disabled';
                                        return 'enabled';
                                      })();";

                                        var check = await wv.CoreWebView2.ExecuteScriptAsync(jsCheckButton);
                                        var status = UnwrapJs(check);
                                        Logger.Log($"[AUTO] Status button-buy: {status}");

                                        if (status == "missing" || status == "disabled")
                                        {
                                            // ВАЖНО: закрываем окно и НЕ добавляем в списки.
                                            tcs.TrySetResult(ReserveResult.ButtonUnavailable);
                                            frm.Close();
                                            return;
                                        }

                                        // 2) Кнопка доступна — кликаем
                                        const string jsClickOsta =
                                            @"(function(){ 
                                        let btn = document.querySelector('button.button.button-buy[type=""submit""]');
                                        if(btn){ btn.click(); return 'clicked'; }
                                        return 'missing';
                                      })();";

                                        var clickRes = await wv.CoreWebView2.ExecuteScriptAsync(jsClickOsta);
                                        Logger.Log($"[AUTO] Wynik kliknięcia: {clickRes}");
                                    }
                                    catch (Exception ex2)
                                    {
                                        Logger.LogEx("[AUTO] onCompleted error", ex2);
                                        tcs.TrySetResult(ReserveResult.OtherFail);
                                        frm.Close();
                                    }
                                };
                                wv.CoreWebView2.NavigationCompleted += onCompleted;

                                wv.Source = new Uri(productUrl);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogEx("[AUTO] Błąd w frm.Load", ex);
                                tcs.TrySetResult(ReserveResult.OtherFail);
                                frm.Close();
                            }
                        };

                        frm.FormClosed += (s, e) =>
                        {
                            if (!tcs.Task.IsCompleted)
                                tcs.TrySetResult(ReserveResult.OtherFail);
                        };

                        if (ShowWindow) frm.ShowDialog();
                        else Application.Run(frm);
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                var finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMs));
                if (finished != tcs.Task)
                {
                    Logger.Log("[AUTO] Timeout rezerwacji (okno nie zakończyło na czas)");
                    return ReserveResult.Timeout;
                }

                var result = await tcs.Task;

                // успех — сразу выходим
                if (result == ReserveResult.Success)
                    return ReserveResult.Success;

                // Кнопка недоступна — смысла делать ретраи внутри TryReserve нет.
                if (result == ReserveResult.ButtonUnavailable)
                    return ReserveResult.ButtonUnavailable;

                Logger.Log($"❌ Próba {attempt} nieudana (result={result}).");

                if (attempt < maxRetries)
                {
                    int delay = _rnd.Next(10000, 20000);
                    Logger.Log($"⏳ Odczekam {delay / 1000}s przed kolejną próbą...");
                    await Task.Delay(delay);
                }
            }

            return ReserveResult.OtherFail;
        }

        private static string UnwrapJs(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            try { return System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? ""; }
            catch { return raw.Trim('"'); }
        }
    }

    public enum ReserveResult
    {
        Success,
        ButtonUnavailable,
        Captcha,
        Timeout,
        OtherFail
    }
}



