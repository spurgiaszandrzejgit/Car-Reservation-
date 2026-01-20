using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using vaurioajoneuvo_finder1;

namespace vaurioajoneuvo_finder
{
    public class Form1 : Form
    {
        private IContainer components = null;
        private DataGridView dataGridView1;
        private Button btnExcludeAll;
        private Button btnOptions;
        private Button btnStop;
        private Button btnScan;
        private Button btnLogin;
        private DataGridViewImageColumn Img;
        private Label lblStatus;
        private Label lblLastRun;
        private Label lblNextRunTitle;
        private Label lblNextRun;
        private System.Windows.Forms.Timer timer1;
        private DateTime _startTime = DateTime.Now;
        private System.Windows.Forms.Timer _heartbeatTimer;
        private bool isPaused = false;
        private static readonly HttpClient _http = new HttpClient();

        private readonly Scanner _scanner;
        private readonly AutoReserver _autoReserver;

        private DateTime _nextRunAt = DateTime.MinValue;
        private bool _isScanning;

        public int MaxPriceScan { get; set; }
        public int MinPriceScan { get; set; }
        public int MaxScanPerRun { get; set; }
        public int RetryAfterMin { get; set; }
        public bool AutoReserveEnabled { get; set; }
        public int MinYear { get; set; }
        public int MaxYear { get; set; }

        private static readonly string Wv2ProfilePath = Path.Combine(Application.StartupPath, "wv2_profile");

        public Form1()
        {
            InitializeComponent();
            var wv2 = new WebView2Automation(Wv2ProfilePath);
            _scanner = new Scanner(wv2);
            _autoReserver = new AutoReserver(Wv2ProfilePath);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            Logger.Init();
            Logger.Log("[APP] Uruchamianie aplikacji...");

            try
            {
                var path = Path.Combine(Application.StartupPath, "Opcje.txt");
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);

                    // helper
                    int ReadInt(string key, int def)
                    {
                        foreach (var ln in lines)
                        {
                            if (ln.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                            {
                                var v = ln.Split('=')[1].Trim();
                                if (int.TryParse(v, out var n)) return n;
                            }
                        }
                        return def;
                    }

                    bool ReadBool01(string key, bool def)
                    {
                        foreach (var ln in lines)
                        {
                            if (ln.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                            {
                                var v = ln.Split('=')[1].Trim();
                                return v == "1";
                            }
                        }
                        return def;
                    }

                    MaxPriceScan = ReadInt("MaxPriceScan", 5000);
                    MinPriceScan = ReadInt("MinPriceScan", 0);
                    MaxScanPerRun = ReadInt("MaxScanPerRun", 10);
                    RetryAfterMin = ReadInt("RetryAfterMin", 5);
                    AutoReserveEnabled = ReadBool01("AutoReserveEnabled", false);
                    MinYear = ReadInt("MinYear", 0);
                    MaxYear = ReadInt("MaxYear", 0);

                    Logger.Log("[APP] Opcje wczytane z pliku");
                }
                else
                {
                    // defaults
                    MaxPriceScan = 5000;
                    MinPriceScan = 0;
                    MaxScanPerRun = 10;
                    RetryAfterMin = 5;
                    AutoReserveEnabled = false;
                    MinYear = 0;
                    MaxYear = 0;

                    Logger.Log("[APP] Brak pliku Opcje.txt – ustawienia domyślne");
                }
            }
            catch (Exception ex)
            {
                Logger.LogEx("[APP] Błąd wczytywania opcji", ex);

                MaxPriceScan = 50000;
                MinPriceScan = 0;
                MaxScanPerRun = 10;
                RetryAfterMin = 5;
                AutoReserveEnabled = false;
                MinYear = 2000;
                MaxYear = 2050;
            }

            await _scanner.InitAsync(this);
            Logger.Log("[APP] Inicjalizacja WebView2 zakończona");

            ScheduleNextRun();

            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            Logger.Log("[APP] Gotowe, czekam na następne uruchomienie");

            _heartbeatTimer = new System.Windows.Forms.Timer();
            _heartbeatTimer.Interval = 10 * 60 * 1000; // 10 minut
            _heartbeatTimer.Tick += HeartbeatTimer_Tick;
            _heartbeatTimer.Start();
        }

        private async Task RunScanAsync()
        {
            if (_isScanning || isPaused) return;

            _isScanning = true;
            timer1.Stop();

            Logger.Log("[SCAN] Rozpoczynam nowe skanowanie ofert");

            try
            {
                lblStatus.Text = "Skanowanie...";
                ClearGridWithImages();

                var oferty = await _scanner.RunAsync(MinPriceScan,MaxPriceScan,MinYear,MaxYear,MaxScanPerRun,this);
                Logger.Log($"[SCAN] Znaleziono {oferty.Count} ofert");

                if (AutoReserveEnabled)
                    _autoReserver.ClearQueue();

                foreach (var o in oferty)
                {
                    if (_scanner.IsExcluded(o.Url))
                    {
                        Logger.Log($"[UI][SKIP] Oferta już wykluczona/rezerwowana → {o.Url}");
                        continue;
                    }

                    Image img = null;

                    if (!string.IsNullOrEmpty(o.ImgUrl))
                    {
                        img = await TryLoadThumbAsync(o.ImgUrl);
                    }

                    dataGridView1.Rows.Add(o.Header, o.Url, o.Price, img, "X", "Go");
                    Logger.Log($"[SCAN][ADD] {o.Header} | {o.Price} | {o.Url}");

                    if (AutoReserveEnabled)
                        _autoReserver.Enqueue(o);
                }

                if (AutoReserveEnabled)
                {
                    Logger.Log("[AUTO] Uruchamiam moduł auto-rezerwacji");
                    _autoReserver.EnsureWorker();
                }
            }
            finally
            {
                _isScanning = false;

                if (isPaused)
                {
                    lblStatus.Text = "Pause";
                    Logger.Log("[SCAN] Program wstrzymany (Pause)");
                }
                else
                {
                    lblStatus.Text = "";
                    lblLastRun.Text = $"Ost. skanowanie: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    lblLastRun.Visible = true;
                    Logger.Log("[SCAN] Koniec skanowania");
                    ScheduleNextRun();
                }
            }
        }

        private void ClearGridWithImages()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Img"]?.Value is Image im) im.Dispose();
            }
            dataGridView1.Rows.Clear();
        }

        private void ScheduleNextRun()
        {
            if (isPaused) return;

            if (!AutoReserveEnabled)
            {
                Logger.Log("[PLANER] Auto-rezerwacja wyłączona → nie planuję następnego uruchomienia");
                return;
            }

            var minutes = RetryAfterMin > 0 ? RetryAfterMin : 2;
            timer1.Interval = minutes * 60 * 1000;
            _nextRunAt = DateTime.Now.AddMinutes(minutes);

            lblNextRun.Text = _nextRunAt.ToString("yyyy-MM-dd HH:mm:ss");
            lblNextRun.Visible = AutoReserveEnabled;
            lblNextRunTitle.Visible = AutoReserveEnabled;

            Logger.Log($"[PLANER] Zaplanowano kolejne uruchomienie za {minutes} minut, o {_nextRunAt:yyyy-MM-dd HH:mm:ss}");

            timer1.Start();
            btnStop.Enabled = true;
        }


        // === UI eventy ===
        private async void BtnScan_Click(object sender, EventArgs e)
        {
            if (isPaused)
            {
                Logger.Log("[UI] ▶ Wznowienie autoskanningu");
                isPaused = false;
                btnStop.Enabled = true;
                ScheduleNextRun();
                lblStatus.Text = "Gotowy.";
            }
            else
            {
                Logger.Log("[UI] Ręczne uruchomienie skanowania");
                this.WindowState = FormWindowState.Normal;
                await RunScanAsync();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_isScanning || timer1.Enabled)
            {
                isPaused = true;
                timer1.Stop();
                _nextRunAt = DateTime.MinValue; // wyczyszczenie czasu planowania
                lblNextRun.Text = "-";
                lblStatus.Text = "Pause";

                Logger.Log("[APP] ⏸ Autoskanning zatrzymany ręcznie");

                btnStop.Enabled = false;
                btnScan.Enabled = true;
            }
        }

        private void BtnOptions_Click(object sender, EventArgs e)
        {
            Logger.Log("[UI] Otwieranie okna opcji");

            var f = new Form2
            {
                MaxPriceScan = MaxPriceScan,
                MinPriceScan = MinPriceScan,
                MaxScanPerRun = MaxScanPerRun,
                RetryAfterMin = RetryAfterMin,
                AutoReserveEnabled = AutoReserveEnabled,
                MinYear = MinYear,
                MaxYear = MaxYear
            };

            if (f.ShowDialog() == DialogResult.OK)
            {
                MaxPriceScan = f.MaxPriceScan;
                MinPriceScan = f.MinPriceScan;
                MaxScanPerRun = f.MaxScanPerRun;
                RetryAfterMin = f.RetryAfterMin;
                AutoReserveEnabled = f.AutoReserveEnabled;
                MinYear = f.MinYear;
                MaxYear = f.MaxYear;

                Logger.Log("[UI] Zapisano nowe ustawienia");
                File.WriteAllLines(Path.Combine(Application.StartupPath, "Opcje.txt"),
                new[]
                {
                    "MaxPriceScan=" + MaxPriceScan,
                    "MinPriceScan=" + MinPriceScan,
                    "MaxScanPerRun=" + MaxScanPerRun,
                    "RetryAfterMin=" + RetryAfterMin,
                    "AutoReserveEnabled=" + (AutoReserveEnabled ? "1" : "0"),
                    "MinYear=" + MinYear,
                    "MaxYear=" + MaxYear
                });

                ScheduleNextRun();
            }
        }

        private void BtnExcludeAll_Click(object sender, EventArgs e)
        {
            try
            {
                string excludePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcludeFromSearch.txt");

                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    var url = row.Cells["Link"].Value?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        File.AppendAllText(excludePath, url + Environment.NewLine);
                        Logger.Log($"[UI] Dodano do ExcludeFromSearch.txt (mass): {url}");
                    }
                }
                _scanner.ReloadLists();
            }
            catch (Exception ex)
            {
                Logger.LogEx("[UI] Błąd przy masowym dodawaniu do ExcludeFromSearch.txt", ex);
            }

            ClearGridWithImages();
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            var t = new System.Threading.Thread(() =>
            {
                using (var frm = new Form
                {
                    Text = "Login – WebView2",
                    Width = 1100,
                    Height = 800,
                    StartPosition = FormStartPosition.CenterScreen
                })
                using (var wv = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill })
                {
                    frm.Controls.Add(wv);

                    frm.Load += async (_, __) =>
                    {
                        try
                        {
                            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: Wv2ProfilePath);
                            await wv.EnsureCoreWebView2Async(env);
                            wv.Source = new Uri("https://www.vaurioajoneuvo.fi/?condition=no_demo");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            frm.Close();
                        }
                    };

                    Application.Run(frm);
                }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        private async void Timer1_Tick(object sender, EventArgs e)
        {
            await RunScanAsync();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == "BtnClose")
            {
                var url = dataGridView1.Rows[e.RowIndex].Cells["Link"].Value?.ToString();
                var cellImg = dataGridView1.Rows[e.RowIndex].Cells["Img"]?.Value as Image;
                cellImg?.Dispose();

                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        string excludePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcludeFromSearch.txt");
                        File.AppendAllText(excludePath, url + Environment.NewLine);

                        Logger.Log($"[UI] Dodano do ExcludeFromSearch.txt: {url}");

                        _scanner.ReloadLists();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogEx("[UI] Błąd przy dodawaniu do ExcludeFromSearch.txt", ex);
                    }
                }

                dataGridView1.Rows.RemoveAt(e.RowIndex);
            }
            else if (dataGridView1.Columns[e.ColumnIndex].Name == "BtnGo")
            {
                var url = dataGridView1.Rows[e.RowIndex].Cells["Link"].Value?.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
        }

        private void HeartbeatTimer_Tick(object sender, EventArgs e)
        {
            var uptime = DateTime.Now - _startTime;
            var uptimeText = $"{(int)uptime.TotalHours}h{uptime.Minutes}m";
            Logger.Log($"[APP] Wciąż żyję, uptime={uptimeText}");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { timer1?.Stop(); timer1?.Dispose(); } catch { }
                try { _heartbeatTimer?.Stop(); _heartbeatTimer?.Dispose(); } catch { }
                try { ClearGridWithImages(); } catch { }

                components?.Dispose();
                _scanner?.Dispose();
                _autoReserver?.ClearQueue();
            }
            base.Dispose(disposing);
        }

        private async Task<Image> TryLoadThumbAsync(string url)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                using (var ms = new MemoryStream(bytes))
                using (var original = Image.FromStream(ms))
                {
                    var thumb = new Bitmap(100, 100);
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, new Rectangle(0, 0, 100, 100));
                    }
                    return thumb; // caller disposes!
                }
            }
            catch
            {
                var bmp = new Bitmap(100, 100);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.LightGray);
                    g.DrawString("Brak zdjęcia", SystemFonts.DefaultFont, Brushes.Black, new PointF(10, 40));
                }
                return bmp; // caller disposes!
            }
        }

        private void InitializeComponent()
        {
            this.components = new Container();
            this.dataGridView1 = new DataGridView();
            this.btnExcludeAll = new Button();
            this.btnOptions = new Button();
            this.btnStop = new Button();
            this.btnScan = new Button();
            this.btnLogin = new Button();
            this.lblStatus = new Label();
            this.lblLastRun = new Label();
            this.lblNextRunTitle = new Label();
            this.lblNextRun = new Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);

            ((ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();

            // dataGridView1
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new Point(12, 25);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowTemplate.Height = 100;
            this.dataGridView1.Size = new Size(667, 303);

            var colNaglowek = new DataGridViewTextBoxColumn { HeaderText = "Naglowek", Name = "Naglowek", Width = 300, ReadOnly = true };
            var colLink = new DataGridViewTextBoxColumn { HeaderText = "Link", Name = "Link", Visible = false, ReadOnly = true };
            var colCena = new DataGridViewTextBoxColumn { HeaderText = "Cena", Name = "Cena", ReadOnly = true };
            var colBtnClose = new DataGridViewButtonColumn { HeaderText = "", Name = "BtnClose", Text = "X", UseColumnTextForButtonValue = true, Width = 35 };
            var colBtnGo = new DataGridViewButtonColumn { HeaderText = "Go", Name = "BtnGo", Text = "Go", UseColumnTextForButtonValue = true, Width = 35 };

            this.Img = new DataGridViewImageColumn();
            this.Img.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.Img.HeaderText = "Img";
            this.Img.Name = "Img";
            this.Img.ReadOnly = true;
            this.Img.ImageLayout = DataGridViewImageCellLayout.Zoom;

            this.dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colNaglowek, colLink, colCena, this.Img, colBtnClose, colBtnGo });
            this.dataGridView1.CellContentClick += new DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);

            // btnScan
            this.btnScan.Location = new Point(100, 334);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new Size(75, 23);
            this.btnScan.Text = "Scan";
            this.btnScan.Click += new EventHandler(this.BtnScan_Click);

            // btnOptions
            this.btnOptions.Location = new Point(13, 334);
            this.btnOptions.Name = "btnOptions";
            this.btnOptions.Size = new Size(75, 23);
            this.btnOptions.Text = "Opcje";
            this.btnOptions.Click += new EventHandler(this.BtnOptions_Click);

            // btnExcludeAll
            this.btnExcludeAll.Location = new Point(469, 334);
            this.btnExcludeAll.Name = "btnExcludeAll";
            this.btnExcludeAll.Size = new Size(114, 23);
            this.btnExcludeAll.Text = "Wyklucz wszystkie";
            this.btnExcludeAll.Click += new EventHandler(this.BtnExcludeAll_Click);

            // btnStop
            this.btnStop.Location = new Point(592, 334);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new Size(87, 23);
            this.btnStop.Text = "Stop";
            this.btnStop.Click += new EventHandler(this.BtnStop_Click);
            this.btnStop.Enabled = false;

            // btnLogin
            this.btnLogin.Location = new Point(180, 334);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new Size(75, 23);
            this.btnLogin.Text = "Login";
            this.btnLogin.Click += new EventHandler(this.BtnLogin_Click);

            // lblStatus
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(12, 6);
            this.lblStatus.Text = "Gotowy.";

            // lblLastRun
            this.lblLastRun.AutoSize = true;
            this.lblLastRun.Location = new Point(489, 6);

            // lblNextRunTitle
            this.lblNextRunTitle.AutoSize = true;
            this.lblNextRunTitle.Location = new Point(230, 6);
            this.lblNextRunTitle.Text = "Nast. uruchomienie :";

            // lblNextRun
            this.lblNextRun.AutoSize = true;
            this.lblNextRun.Location = new Point(340, 6);

            // timer1
            this.timer1.Tick += new EventHandler(this.Timer1_Tick);

            this.Icon = new Icon(Path.Combine(Application.StartupPath, "icons8_car.ico"));

            this.ClientSize = new Size(691, 360);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.btnExcludeAll);
            this.Controls.Add(this.btnOptions);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblLastRun);
            this.Controls.Add(this.lblNextRunTitle);
            this.Controls.Add(this.lblNextRun);
            this.Name = "Form1";
            this.Text = "vaurioajoneuvo finder";
            this.Load += new EventHandler(this.Form1_Load);

            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimumSize = this.Size;
            this.MaximumSize = this.Size;

            ((ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
