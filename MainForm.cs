using System;
using System.Drawing;
using System.Windows.Forms;
using ClaudeUsageBar.Services;

namespace ClaudeUsageBar
{
    public class MainForm : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int WM_EXITSIZEMOVE = 0x0232;

        private System.Windows.Forms.Timer updateTimer;
        private System.Windows.Forms.Timer refreshBarTimer;
        private System.Windows.Forms.Timer countdownTimer;
        private int refreshIntervalMs = Settings.LoadRefreshIntervalMs();
        private DateTime nextRefreshTime;

        private Panel infoBar;
        private Panel refreshProgressPanel;

        // Top-row bars
        private UsageBar barCost, barToken, barMessage, barModelDist;

        // Top-row labels (time to reset)
        private Label lblTime;

        // Top-row labels (peak status)
        private Label lblPeakStatus;

        // Bottom-row labels
        private Label lblBurnRate;
        private Label lblCostRate;
        private Label lblPredictions;
        private Label lblExtraUsage;
        private Label lblExtraUsageTitle;

        private Button btnConfig;

        private bool _isUpdating = false;
        private NotifyIcon trayIcon;
        private bool _forceClose = false;

        // Burn-rate tracking
        private float _prevTokensUsed = float.NaN;
        private float _prevCostUsed = float.NaN;
        private DateTime _prevReadingTime = DateTime.MinValue;
        private float _tokenBurnRate = float.NaN;
        private float _costBurnRate = float.NaN;
        private DateTime _resetAt = DateTime.MinValue;

        public MainForm()
        {
            this.Text = "Claude Usage Bar";
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(24, 24, 24);
            this.Size = Settings.LoadWindowSize();
            this.StartPosition = FormStartPosition.Manual;
            this.Location = Settings.LoadWindowLocation();

            // ── System tray icon ─────────────────────────────────────────
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Options", null, (s, e) => BtnConfig_Click(s, e));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, (s, e) => { _forceClose = true; this.Close(); });

            trayIcon = new NotifyIcon
            {
                Icon    = CreateRobotIcon(),
                Text    = "Claude Usage",
                Visible = true,
                ContextMenuStrip = trayMenu
            };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.Activate(); };

            infoBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 24, 24) };
            this.Controls.Add(infoBar);

            // ── Layout constants ─────────────────────────────────────────
            const int barTop = 5, barH = 58, barW = 165, gap = 15;
            const int row2Y = 67, row2TitleH = 13, row2ValY = 67 + row2TitleH;
            int x = 10;

            // ── Top row: usage bars ──────────────────────────────────────
            barCost = new UsageBar
            {
                Label = "Cost Usage",
                ValueText = "-",
                Percentage = 0f,
                BarColor = Color.Gold,
                Location = new Point(x, barTop),
                Size = new Size(barW, barH)
            };
            infoBar.Controls.Add(barCost);
            x += barW + gap;

            barToken = new UsageBar
            {
                Label = "5h Session",
                ValueText = "-",
                Percentage = 0f,
                BarColor = Color.Gold,
                Location = new Point(x, barTop),
                Size = new Size(barW, barH)
            };
            infoBar.Controls.Add(barToken);
            x += barW + gap;

            barMessage = new UsageBar
            {
                Label = "7-Day Usage",
                ValueText = "-",
                Percentage = 0f,
                BarColor = Color.Gold,
                Location = new Point(x, barTop),
                Size = new Size(barW, barH)
            };
            infoBar.Controls.Add(barMessage);
            x += barW + gap;

            // ── Top row: time to reset ───────────────────────────────────
            int timeX = x;
            infoBar.Controls.Add(new Label
            {
                Text = "TIME TO RESET",
                Location = new Point(timeX, barTop),
                Size = new Size(130, 14),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            lblTime = new Label
            {
                Text = "--",
                Location = new Point(timeX, barTop + 14),
                Size = new Size(130, 36),
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblTime);
            x += 140 + 10;

            // ── Top row: model distribution bar ─────────────────────────
            barModelDist = new UsageBar
            {
                Label = "Model Distribution",
                ValueText = "--",
                Percentage = 0f,
                BarColor = Color.CornflowerBlue,
                SecondaryBarColor = Color.MediumPurple,
                SecondaryPercentage = 0f,
                Location = new Point(x, barTop),
                Size = new Size(210, barH)
            };
            infoBar.Controls.Add(barModelDist);
            x += 210 + gap;

            // ── Top row: peak / standard / 2x status ────────────────────
            int peakX = x;
            infoBar.Controls.Add(new Label
            {
                Text = "RATE",
                Location = new Point(peakX, barTop),
                Size = new Size(140, 14),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            lblPeakStatus = new Label
            {
                Text = GetRateStatus(),
                Location = new Point(peakX, barTop + 14),
                Size = new Size(140, 36),
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblPeakStatus);

            // ── Buttons (anchored to right edge) ────────────────────────
            btnConfig = new Button
            {
                Text = "⚙",
                Size = new Size(30, 25),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnConfig.FlatAppearance.BorderSize = 0;
            btnConfig.Click += BtnConfig_Click;
            btnConfig.Location = new Point(this.Width - 40, barTop + 30);
            infoBar.Controls.Add(btnConfig);

            var btnExit = new Button
            {
                Text = "✖",
                Size = new Size(30, 25),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (s, e) => this.Hide();
            btnExit.Location = new Point(this.Width - 40, barTop);
            infoBar.Controls.Add(btnExit);

            // ── Bottom row: burn rate ────────────────────────────────────
            infoBar.Controls.Add(new Label
            {
                Text = "BURN RATE",
                Location = new Point(10, row2Y),
                Size = new Size(210, row2TitleH),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            lblBurnRate = new Label
            {
                Text = "-- tok/min",
                Location = new Point(10, row2ValY),
                Size = new Size(220, 22),
                ForeColor = Color.FromArgb(255, 200, 80),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblBurnRate);

            // ── Bottom row: cost rate ────────────────────────────────────
            infoBar.Controls.Add(new Label
            {
                Text = "COST RATE",
                Location = new Point(240, row2Y),
                Size = new Size(200, row2TitleH),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            lblCostRate = new Label
            {
                Text = "-- $/min",
                Location = new Point(240, row2ValY),
                Size = new Size(200, 22),
                ForeColor = Color.FromArgb(100, 220, 100),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblCostRate);

            // ── Bottom row: predictions ──────────────────────────────────
            infoBar.Controls.Add(new Label
            {
                Text = "PREDICTIONS",
                Location = new Point(450, row2Y),
                Size = new Size(200, row2TitleH),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            lblPredictions = new Label
            {
                Text = "Tokens: --  |  Resets: --",
                Location = new Point(450, row2ValY),
                Size = new Size(600, 22),
                ForeColor = Color.FromArgb(130, 180, 255),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblPredictions);

            // ── Bottom row: extra usage ──────────────────────────────────
            lblExtraUsageTitle = new Label
            {
                Text = "EXTRA USAGE",
                Location = new Point(830, row2Y),
                Size = new Size(300, row2TitleH),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblExtraUsageTitle);
            lblExtraUsage = new Label
            {
                Text = "--",
                Location = new Point(830, row2ValY),
                Size = new Size(300, 22),
                ForeColor = Color.FromArgb(200, 160, 255),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            infoBar.Controls.Add(lblExtraUsage);

            // ── Thin refresh-progress bar at bottom edge ─────────────────
            refreshProgressPanel = new Panel
            {
                Location = new Point(0, 112),
                Size = new Size(1200, 3),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            refreshProgressPanel.Paint += RefreshProgressPanel_Paint;
            infoBar.Controls.Add(refreshProgressPanel);

            // Make all controls draggable
            AttachDragHandler(infoBar);

            // Resize: reposition anchored controls
            this.Resize += (s, e) =>
            {
                btnConfig.Location = new Point(this.Width - 40, barTop + 30);
                btnExit.Location = new Point(this.Width - 40, barTop);
                refreshProgressPanel.Width = this.Width;
            };

            // ── Timers ───────────────────────────────────────────────────
            refreshBarTimer = new System.Windows.Forms.Timer { Interval = 50 };
            refreshBarTimer.Tick += RefreshBarTimer_Tick;
            refreshBarTimer.Start();

            countdownTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            countdownTimer.Tick += (s, e) => UpdateCountdown();
            countdownTimer.Start();

            updateTimer = new System.Windows.Forms.Timer { Interval = refreshIntervalMs };
            updateTimer.Tick += async (s, e) =>
            {
                if (_isUpdating) return;
                _isUpdating = true;
                try { await UpdateUsageAsync(); }
                finally { _isUpdating = false; }
            };
            updateTimer.Start();

            nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
            // Delay first load until after Application.Run() installs the WinForms
            // SynchronizationContext — otherwise the await continuation runs on a
            // thread-pool thread and cross-thread UI updates are silently swallowed.
            this.Shown += async (s, e) => await UpdateUsageAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_forceClose)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }

        private static Icon CreateRobotIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Antenna ball
                using var antBrush = new SolidBrush(Color.Gold);
                g.FillEllipse(antBrush, 13, 0, 6, 6);

                // Antenna stem
                using var antPen = new Pen(Color.FromArgb(160, 200, 255), 2);
                g.DrawLine(antPen, 16, 5, 16, 8);

                // Head
                using var headBrush = new SolidBrush(Color.FromArgb(70, 140, 220));
                g.FillRectangle(headBrush, 5, 8, 22, 18);

                // Head border
                using var borderPen = new Pen(Color.FromArgb(120, 180, 255), 1.5f);
                g.DrawRectangle(borderPen, 5, 8, 22, 18);

                // Left eye
                using var eyeBrush = new SolidBrush(Color.Gold);
                g.FillEllipse(eyeBrush, 9, 12, 5, 5);

                // Right eye
                g.FillEllipse(eyeBrush, 18, 12, 5, 5);

                // Mouth (horizontal bar)
                using var mouthBrush = new SolidBrush(Color.FromArgb(200, 240, 255));
                g.FillRectangle(mouthBrush, 10, 21, 12, 3);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void AttachDragHandler(Control parent)
        {
            if (parent is Button) return;
            parent.MouseDown += MainForm_MouseDown;
            foreach (Control c in parent.Controls)
                AttachDragHandler(c);
        }

        private void MainForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void RefreshBarTimer_Tick(object? sender, EventArgs e)
        {
            refreshProgressPanel?.Invalidate();
        }

        private void RefreshProgressPanel_Paint(object? sender, PaintEventArgs e)
        {
            double msLeft = (nextRefreshTime - DateTime.Now).TotalMilliseconds;
            if (msLeft < 0) msLeft = 0;
            double pct = 1.0 - (msLeft / refreshIntervalMs);
            int fillW = (int)(refreshProgressPanel.Width * pct);
            if (fillW > 0)
            {
                using var brush = new SolidBrush(Color.Gold);
                e.Graphics.FillRectangle(brush, 0, 0, fillW, refreshProgressPanel.Height);
            }
        }

        private void UpdateCountdown()
        {
            if (_resetAt == DateTime.MinValue) return;
            var remaining = _resetAt - DateTime.UtcNow;
            if (remaining.TotalSeconds > 0)
                lblTime.Text = remaining.Hours > 0 ? $"{remaining.Hours}h {remaining.Minutes}m" : $"{remaining.Minutes}m";
            else
                lblTime.Text = "Now";
        }

        private static string GetRateStatus()
        {
            try
            {
                // Peak hours: Mon–Fri 9 AM – 5 PM US Eastern (Anthropic HQ timezone)
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var etNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                bool isWeekday = etNow.DayOfWeek >= DayOfWeek.Monday && etNow.DayOfWeek <= DayOfWeek.Friday;
                bool isPeakHour = etNow.Hour >= 9 && etNow.Hour < 17;
                return (isWeekday && isPeakHour) ? "Peak (2x)" : "Standard";
            }
            catch
            {
                return "Standard";
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_EXITSIZEMOVE)
                Settings.SaveWindowBounds(this.Location, this.Size);
        }

        private void BtnConfig_Click(object? sender, EventArgs e)
        {
            using (var dlg = new ConfigDialog(this))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    int newInterval = dlg.RefreshIntervalMs;
                    if (newInterval != refreshIntervalMs)
                    {
                        refreshIntervalMs = newInterval;
                        Settings.SaveRefreshIntervalMs(refreshIntervalMs);
                        updateTimer.Interval = refreshIntervalMs;
                        nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                    }

                    _ = UpdateUsageAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task UpdateUsageAsync()
        {
            if (!CredentialService.CredentialsExist())
            {
                barCost.ValueText = barToken.ValueText = barMessage.ValueText = "-";
                barCost.Percentage = barToken.Percentage = barMessage.Percentage = 0f;
                lblTime.Text = "No Auth";
                nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                Invalidate(true);
                return;
            }

            try
            {
                var usage = await UsageApiService.GetUsageAsync();
                if (usage == null)
                {
                    barCost.ValueText = barToken.ValueText = barMessage.ValueText = "ERR";
                    barCost.Percentage = barToken.Percentage = barMessage.Percentage = 0f;
                    lblTime.Text = "API Err";
                    nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                    Invalidate(true);
                    return;
                }

                // ── Cost (extra_usage) ───────────────────────────────────
                float costUsedAbs = 0, costPct = 0;
                if (usage.ExtraUsage != null && usage.ExtraUsage.IsEnabled)
                {
                    costUsedAbs = (float)usage.ExtraUsage.UsedDollars;
                    costPct     = usage.ExtraUsage.MonthlyLimit > 0
                        ? (float)(usage.ExtraUsage.UsedCredits ?? 0) / (float)usage.ExtraUsage.MonthlyLimit.Value
                        : 0f;
                    barCost.ValueText  = $"{costPct * 100:F1}%";
                    barCost.SubLabel   = usage.ExtraUsage.MonthlyLimit > 0
                        ? $"${costUsedAbs:F2} / ${usage.ExtraUsage.LimitDollars:F2}"
                        : $"${costUsedAbs:F2} used";
                    barCost.Percentage = costPct;
                }

                // ── Extra usage label ────────────────────────────────────
                if (usage.ExtraUsage != null)
                {
                    bool enabled = usage.ExtraUsage.IsEnabled;
                    string enabledStr = enabled ? "Enabled" : "Disabled";
                    lblExtraUsageTitle.Text = $"EXTRA USAGE: {enabledStr.ToUpper()}";
                    string spent = $"${usage.ExtraUsage.UsedDollars:F2} spent";
                    string limit = usage.ExtraUsage.MonthlyLimit > 0
                        ? $" / ${usage.ExtraUsage.LimitDollars:F2} limit"
                        : "";
                    lblExtraUsage.Text      = $"{spent}{limit}";
                    lblExtraUsage.ForeColor = enabled
                        ? Color.FromArgb(200, 160, 255)
                        : Color.FromArgb(140, 140, 140);
                }
                else
                {
                    lblExtraUsageTitle.Text = "EXTRA USAGE: N/A";
                    lblExtraUsage.Text      = "--";
                    lblExtraUsage.ForeColor = Color.FromArgb(120, 120, 120);
                }

                // ── 5-hour session token usage ───────────────────────────
                float tokenPct = 0;
                if (usage.FiveHour != null)
                {
                    tokenPct = (float)(usage.FiveHour.Utilization / 100.0);
                    barToken.ValueText  = $"{tokenPct * 100:F1}%";
                    barToken.SubLabel   = "5h session";
                    barToken.Percentage = tokenPct;
                }

                // ── 7-day usage ──────────────────────────────────────────
                float msgPct = 0;
                if (usage.SevenDay != null)
                {
                    msgPct = (float)(usage.SevenDay.Utilization / 100.0);
                    barMessage.ValueText  = $"{msgPct * 100:F1}%";
                    barMessage.SubLabel   = "7-day";
                    barMessage.Percentage = msgPct;
                }

                // ── Reset time ───────────────────────────────────────────
                _resetAt = DateTime.MinValue;
                if (usage.FiveHour?.ResetsAt != null)
                    _resetAt = usage.FiveHour.ResetsAt.Value.UtcDateTime;
                UpdateCountdown();

                // ── Model distribution (sonnet vs opus) ──────────────────
                if (usage.Sonnet != null)
                {
                    float su = (float)usage.Sonnet.Utilization;
                    float ou = Math.Max(0f, (float)(usage.SevenDay?.Utilization ?? 0) - su);
                    float total = su + ou;
                    if (total > 0)
                    {
                        barModelDist.Percentage          = su / total;
                        barModelDist.SecondaryPercentage = ou / total;
                        barModelDist.ValueText = $"S:{su:F0}% O:{ou:F0}%";
                        barModelDist.SubLabel  = "of 7-day quota";
                    }
                }

                // ── Burn rate ────────────────────────────────────────────
                var now = DateTime.Now;
                if (!float.IsNaN(_prevCostUsed) && _prevReadingTime != DateTime.MinValue)
                {
                    double mins = (now - _prevReadingTime).TotalMinutes;
                    if (mins >= 0.5)
                    {
                        _costBurnRate  = (costUsedAbs - _prevCostUsed) / (float)mins;
                        _tokenBurnRate = (tokenPct * 100f - _prevTokensUsed) / (float)mins;
                    }
                }
                _prevCostUsed    = costUsedAbs;
                _prevTokensUsed  = tokenPct * 100f;
                _prevReadingTime = now;

                lblBurnRate.Text = !float.IsNaN(_tokenBurnRate)
                    ? $"{_tokenBurnRate:F3} %pts/min"
                    : "-- %pts/min";

                lblCostRate.Text = (!float.IsNaN(_costBurnRate) && _costBurnRate >= 0)
                    ? $"${_costBurnRate:F4}/min"
                    : "-- $/min";

                // ── Predictions ──────────────────────────────────────────
                string tokensOutStr = "--";
                if (!float.IsNaN(_tokenBurnRate) && _tokenBurnRate > 0)
                {
                    float pctLeft  = 100f - tokenPct * 100f;
                    float minsLeft = pctLeft / _tokenBurnRate;
                    tokensOutStr = DateTime.Now.AddMinutes(minsLeft).ToString("HH:mm");
                }
                string resetsAtStr = _resetAt != DateTime.MinValue
                    ? _resetAt.ToLocalTime().ToString("HH:mm")
                    : "--";
                lblPredictions.Text = $"Session out: {tokensOutStr}  |  Resets: {resetsAtStr}";

                // ── Peak/standard ────────────────────────────────────────
                string rateStatus = GetRateStatus();
                lblPeakStatus.Text      = rateStatus;
                lblPeakStatus.ForeColor = rateStatus.StartsWith("Peak") ? Color.Orange : Color.LightGreen;

                nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                barCost.Invalidate();
                barToken.Invalidate();
                barMessage.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Exception in UpdateUsageAsync: {ex}");
                barCost.ValueText = barToken.ValueText = barMessage.ValueText = "ERR";
                barCost.Percentage = barToken.Percentage = barMessage.Percentage = 0f;
                lblTime.Text = "Error";
                nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                Invalidate(true);
            }
        }
    }
}
