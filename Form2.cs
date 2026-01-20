using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace vaurioajoneuvo_finder
{
    public class Form2 : Form
    {
        private IContainer components = null;
        private Label lblMaxPrice;
        private Label lblMaxScan;
        private Label lblRetry;
        private TextBox txtMaxPrice;
        private TextBox txtMaxScan;
        private TextBox txtRetry;
        private CheckBox chkAutoReserve;
        private Button btnSave;
        private Button btnCancel;
        private Label lblMinPrice;
        private TextBox txtMinPrice;

        private Label lblMinYear;
        private TextBox txtMinYear;

        private Label lblMaxYear;
        private TextBox txtMaxYear;


        // --- Właściwości do odczytu w Form1 ---
        public int MaxPriceScan { get; set; }
        public int MinPriceScan { get; set; }
        public int MinYear { get; set; }
        public int MaxYear { get; set; }
        public int MaxScanPerRun { get; set; }
        public int RetryAfterMin { get; set; }
        public bool AutoReserveEnabled { get; set; }
        


        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            // ustaw wartości w kontrolkach
            txtMaxPrice.Text = MaxPriceScan.ToString();
            txtMinPrice.Text = MinPriceScan.ToString();
            txtMinYear.Text = MinYear.ToString();
            txtMaxYear.Text = MaxYear.ToString();
            txtMaxScan.Text = MaxScanPerRun.ToString();
            txtRetry.Text = RetryAfterMin.ToString();
            chkAutoReserve.Checked = AutoReserveEnabled;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // walidacja prostych wartości liczbowych
            if (!int.TryParse(txtMaxPrice.Text, out int maxPrice) || maxPrice <= 100)
            {
                MessageBox.Show("❌ MaxPriceScan musi być > 100", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(txtMinPrice.Text, out int minPrice) || minPrice <= 0)
            {
                MessageBox.Show("❌ MinPriceScan musi być ≥ 0");
                return;
            }

            if (!int.TryParse(txtMinYear.Text, out int minYear) || minYear < 0 || minYear > 2100)
            {
                MessageBox.Show("❌ MinYear musi być 0–2100");
                return;
            }

            if (!int.TryParse(txtMaxYear.Text, out int maxYear) || maxYear < 0 || maxYear > 2100)
            {
                MessageBox.Show("❌ MaxYear musi być 0–2100");
                return;
            }

            if (minYear > 0 && maxYear > 0 && minYear > maxYear)
            {
                MessageBox.Show("❌ MinYear nie może być > MaxYear");
                return;
            }

            if (!int.TryParse(txtMaxScan.Text, out int maxScan) || maxScan < 1 || maxScan >= 150)
            {
                MessageBox.Show("❌ MaxScanPerRun musi być od 1 do 149", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(txtRetry.Text, out int retry) || retry < 1 || retry > 60)
            {
                MessageBox.Show("❌ RetryAfterMin musi być od 1 do 60", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // przypisz do właściwości
            MaxPriceScan = maxPrice;
            MinPriceScan = minPrice;
            MinYear = minYear;
            MaxYear = maxYear;
            MaxScanPerRun = maxScan;
            RetryAfterMin = retry;
            AutoReserveEnabled = chkAutoReserve.Checked;

            this.DialogResult = DialogResult.OK; // sygnalizujemy sukces
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblMaxPrice = new Label();
            this.lblMinPrice = new Label();
            this.lblMinYear = new Label();
            this.lblMaxYear = new Label();
            this.lblMaxScan = new Label();
            this.lblRetry = new Label();
            this.txtMinPrice = new TextBox();
            this.txtMinYear = new TextBox();
            this.txtMaxYear = new TextBox();
            this.txtMaxPrice = new TextBox();
            this.txtMaxScan = new TextBox();
            this.txtRetry = new TextBox();
            this.chkAutoReserve = new CheckBox();
            this.btnSave = new Button();
            this.btnCancel = new Button();
            this.SuspendLayout();

            // lblMaxPrice
            this.lblMaxPrice.AutoSize = true;
            this.lblMaxPrice.Location = new Point(12, 15);
            this.lblMaxPrice.Text = "MaxPriceScan:";

            // lblMinPrice
            this.lblMinPrice.AutoSize = true;
            this.lblMinPrice.Location = new Point(12, 45);
            this.lblMinPrice.Text = "MinPriceScan:";

            // lblMaxScan
            this.lblMaxScan.AutoSize = true;
            this.lblMaxScan.Location = new Point(12, 75);
            this.lblMaxScan.Text = "MaxScanPerRun:";

            // lblRetry
            this.lblRetry.AutoSize = true;
            this.lblRetry.Location = new Point(12, 105);
            this.lblRetry.Text = "RetryAfterMin:";

            // lblMinYear
            this.lblMinYear.AutoSize = true;
            this.lblMinYear.Location = new Point(12, 135);
            this.lblMinYear.Text = "MinYear:";

            // lblMaxYear
            this.lblMaxYear.AutoSize = true;
            this.lblMaxYear.Location = new Point(12, 165);
            this.lblMaxYear.Text = "MaxYear:";

            // txtMaxPrice
            this.txtMaxPrice.Location = new Point(120, 12);
            this.txtMaxPrice.Size = new Size(100, 20);

            // txtMinPrice
            this.txtMinPrice.Location = new Point(120, 42);
            this.txtMinPrice.Size = new Size(100, 20);

            // txtMaxScan
            this.txtMaxScan.Location = new Point(120, 72);
            this.txtMaxScan.Size = new Size(100, 20);

            // txtRetry
            this.txtRetry.Location = new Point(120, 102);
            this.txtRetry.Size = new Size(100, 20);

            // txtMinYear
            this.txtMinYear.Location = new Point(120, 132);
            this.txtMinYear.Size = new Size(100, 20);

            // txtMaxYear
            this.txtMaxYear.Location = new Point(120, 162);
            this.txtMaxYear.Size = new Size(100, 20);

            // chkAutoReserve
            this.chkAutoReserve.AutoSize = true;
            this.chkAutoReserve.Location = new Point(15, 200);
            this.chkAutoReserve.Text = "Auto-rezerwacja";
            this.chkAutoReserve.Checked = false;

            // btnSave
            this.btnSave.Location = new Point(60, 230);
            this.btnSave.Size = new Size(75, 25);
            this.btnSave.Text = "Save";
            this.btnSave.Click += new EventHandler(this.btnSave_Click);

            // btnCancel
            this.btnCancel.Location = new Point(145, 230);
            this.btnCancel.Size = new Size(75, 25);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // Form2
            this.ClientSize = new Size(250, 270);

            this.Controls.Add(this.lblMaxPrice);
            this.Controls.Add(this.lblMaxScan);
            this.Controls.Add(this.lblRetry);
            this.Controls.Add(this.txtMaxPrice);
            this.Controls.Add(this.lblMinPrice);
            this.Controls.Add(this.txtMinPrice);
            this.Controls.Add(this.lblMinYear);
            this.Controls.Add(this.txtMinYear);
            this.Controls.Add(this.lblMaxYear);
            this.Controls.Add(this.txtMaxYear);
            this.Controls.Add(this.txtMaxScan);
            this.Controls.Add(this.txtRetry);
            this.Controls.Add(this.chkAutoReserve);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Opcje";
            this.Load += new EventHandler(this.Form2_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
