using System;
using System.Drawing;
using System.Windows.Forms;
using ClaudeUsageBar.Services;

namespace ClaudeUsageBar
{
    public class ConfigDialog : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private NumericUpDown numRefresh;
        private Label lblStatus;

        public int RefreshIntervalMs => (int)(numRefresh.Value * 60_000);

        public ConfigDialog(Form owner)
        {
            // ── Form setup ───────────────────────────────────────────────
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor       = Color.FromArgb(24, 24, 24);
            this.Size            = new Size(500, 95);
            this.TopMost         = true;
            this.StartPosition   = FormStartPosition.Manual;
            this.Location = new Point(owner.Left, owner.Bottom + 4);

            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 24, 24) };
            this.Controls.Add(panel);

            // ── Row 1: column titles ─────────────────────────────────────
            int y1 = 8;
            panel.Controls.Add(MakeTitle("CREDENTIALS", 10, y1));
            panel.Controls.Add(MakeTitle("REFRESH INTERVAL", 260, y1));

            // ── Row 2: inputs ────────────────────────────────────────────
            int y2 = y1 + 14;

            // Credential status (read-only)
            var credPath = CredentialService.GetCredentialsPath();
            bool credFound = CredentialService.CredentialsExist();
            var lblCred = new Label
            {
                Location  = new Point(10, y2 + 3),
                Size      = new Size(235, 20),
                ForeColor = credFound ? Color.LightGreen : Color.Tomato,
                Font      = new Font("Segoe UI", 8.5f),
                BackColor = Color.Transparent,
                Text      = credFound ? "OAuth credentials found" : "No credentials found"
            };
            panel.Controls.Add(lblCred);

            numRefresh = new NumericUpDown
            {
                Location    = new Point(260, y2),
                Size        = new Size(60, 24),
                Minimum     = 1,
                Maximum     = 60,
                Value       = Math.Max(1, Settings.LoadRefreshIntervalMs() / 60_000),
                BackColor   = Color.FromArgb(40, 40, 40),
                ForeColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Segoe UI", 9f),
                TextAlign   = HorizontalAlignment.Center
            };
            panel.Controls.Add(numRefresh);

            panel.Controls.Add(new Label
            {
                Text      = "min",
                Location  = new Point(326, y2 + 4),
                Size      = new Size(30, 18),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font      = new Font("Segoe UI", 8f),
                BackColor = Color.Transparent
            });

            // ── Buttons ──────────────────────────────────────────────────
            var btnSave = MakeButton("SAVE", 370, y2 - 2);
            btnSave.Click += BtnSave_Click;
            panel.Controls.Add(btnSave);

            var btnClose = MakeButton("✖", 464, y2 - 2);
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            btnClose.Size = new Size(28, 28);
            panel.Controls.Add(btnClose);

            // ── Status label ─────────────────────────────────────────────
            lblStatus = new Label
            {
                Text      = credFound
                    ? $"Using: {credPath ?? "~/.claude/.credentials.json"}"
                    : "Run 'claude' CLI and log in to generate credentials",
                Location  = new Point(10, y2 + 30),
                Size      = new Size(480, 18),
                ForeColor = Color.FromArgb(120, 120, 120),
                Font      = new Font("Segoe UI", 7.5f),
                BackColor = Color.Transparent
            };
            panel.Controls.Add(lblStatus);

            // ── Make draggable (skip buttons) ────────────────────────────
            AttachDrag(panel);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void AttachDrag(Control ctrl)
        {
            if (ctrl is Button) return;
            ctrl.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
            foreach (Control c in ctrl.Controls)
                AttachDrag(c);
        }

        private static Label MakeTitle(string text, int x, int y) => new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(200, 14),
            ForeColor = Color.FromArgb(160, 160, 160),
            Font      = new Font("Segoe UI", 7f, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        private static Button MakeButton(string text, int x, int y) => new Button
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(88, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
    }
}
