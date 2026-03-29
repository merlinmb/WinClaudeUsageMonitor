using System;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

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

        private System.Windows.Forms.Timer updateTimer;
        private System.Windows.Forms.Timer refreshBarTimer;
        private System.Windows.Forms.Timer countdownTimer;
        private int refreshIntervalMs = 2 * 60 * 1000;
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

        private Button btnConfig;
        private ClaudeApiClient? apiClient;

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
            this.BackColor = Color.FromArgb(24, 24, 24);
            this.Size = new Size(1200, 115);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(100, 100);

            infoBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 24, 24) };
            this.Controls.Add(infoBar);

            // ── Layout constants ─────────────────────────────────────────
            const int barTop = 5, barH = 55, barW = 165, gap = 15;
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
                Label = "Token Usage",
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
                Label = "Messages Usage",
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
            btnExit.Click += (s, e) => this.Close();
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

            // ── Thin refresh-progress bar at bottom edge ─────────────────
            refreshProgressPanel = new Panel
            {
                Location = new Point(0, 109),
                Size = new Size(1200, 6),
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
            updateTimer.Tick += async (s, e) => await UpdateUsageAsync();
            updateTimer.Start();

            nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
            _ = UpdateUsageAsync();
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
            var now = DateTime.Now;
            double msLeft = (nextRefreshTime - now).TotalMilliseconds;
            if (msLeft < 0) msLeft = 0;
            refreshProgressPanel?.Invalidate();
        }

        private void RefreshProgressPanel_Paint(object? sender, PaintEventArgs e)
        {
            double msLeft = (nextRefreshTime - DateTime.Now).TotalMilliseconds;
            if (msLeft < 0) msLeft = 0;
            double pct = 1.0 - (msLeft / refreshIntervalMs);
            int fillW = (int)(refreshProgressPanel.Width * pct);
            if (fillW > 0)
                e.Graphics.FillRectangle(new SolidBrush(Color.Gold), 0, 0, fillW, refreshProgressPanel.Height);
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

        private void BtnConfig_Click(object? sender, EventArgs e)
        {
            using (var dlg = new ConfigDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Settings.SaveApiKey(dlg.ApiKey);
                    _ = UpdateUsageAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task UpdateUsageAsync()
        {
            string apiKey = Settings.LoadApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                barCost.ValueText = barToken.ValueText = barMessage.ValueText = "-";
                barCost.Percentage = barToken.Percentage = barMessage.Percentage = 0f;
                lblTime.Text = "No Key";
                nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                return;
            }

            apiClient = new ClaudeApiClient(apiKey);
            try
            {
                var usage = await apiClient.GetUsageAsync();
                if (usage == null)
                {
                    barCost.ValueText = barToken.ValueText = barMessage.ValueText = "ERR";
                    barCost.Percentage = barToken.Percentage = barMessage.Percentage = 0f;
                    lblTime.Text = "API Err";
                    nextRefreshTime = DateTime.Now.AddMilliseconds(refreshIntervalMs);
                    return;
                }

                float costUsedAbs = 0, costLimit = 0;
                float tokenUsedAbs = 0, tokenLimit = 0;
                float msgUsedAbs = 0, msgLimit = 0;

                // ── Cost ────────────────────────────────────────────────
                var costNode = usage["cost"];
                if (costNode != null)
                {
                    costUsedAbs = costNode.Value<float>("used");
                    costLimit = costNode.Value<float>("limit");
                    float pct = costLimit > 0 ? costUsedAbs / costLimit : 0f;
                    barCost.ValueText = costLimit > 0 ? $"{pct * 100:F1}%" : "-";
                    barCost.SubLabel = costLimit > 0 ? $"${costUsedAbs:F2} / ${costLimit:F2}" : "";
                    barCost.Percentage = pct;
                }

                // ── Tokens (try "tokens" then "token") ──────────────────
                var tokenNode = usage["tokens"] ?? usage["token"];
                if (tokenNode != null)
                {
                    // Some responses nest totals under a "total" sub-key
                    var totalNode = tokenNode["total"] ?? tokenNode;
                    tokenUsedAbs = totalNode.Value<float>("used");
                    tokenLimit = totalNode.Value<float>("limit");
                    float pct = tokenLimit > 0 ? tokenUsedAbs / tokenLimit : 0f;
                    barToken.ValueText = tokenLimit > 0 ? $"{pct * 100:F1}%" : "-";
                    barToken.SubLabel = tokenLimit > 0 ? $"{tokenUsedAbs:N0} / {tokenLimit:N0}" : "";
                    barToken.Percentage = pct;
                }

                // ── Messages (try "messages" then "message") ─────────────
                var msgNode = usage["messages"] ?? usage["message"];
                if (msgNode != null)
                {
                    msgUsedAbs = msgNode.Value<float>("used");
                    msgLimit = msgNode.Value<float>("limit");
                    float pct = msgLimit > 0 ? msgUsedAbs / msgLimit : 0f;
                    barMessage.ValueText = msgLimit > 0 ? $"{pct * 100:F1}%" : "-";
                    barMessage.SubLabel = msgLimit > 0 ? $"{msgUsedAbs:N0} / {msgLimit:N0}" : "";
                    barMessage.Percentage = pct;
                }

                // ── Reset time: try "reset_at" (ISO) then "reset_seconds" ─
                _resetAt = DateTime.MinValue;
                var resetAtToken = usage["reset_at"];
                var resetSecToken = usage["reset_seconds"];
                if (resetAtToken != null && resetAtToken.Type != JTokenType.Null)
                {
                    string resetAtStr = resetAtToken.Value<string>() ?? "";
                    if (DateTime.TryParse(resetAtStr, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
                        _resetAt = parsed.ToUniversalTime();
                }
                else if (resetSecToken != null && resetSecToken.Type != JTokenType.Null)
                {
                    int sec = resetSecToken.Value<int>();
                    _resetAt = DateTime.UtcNow.AddSeconds(sec);
                }
                UpdateCountdown();

                // ── Model distribution ───────────────────────────────────
                if (usage["model_distribution"] is JObject mdist)
                {
                    float sonnet = mdist["claude-sonnet-4-5"]?.Value<float>()
                        ?? mdist["claude-sonnet"]?.Value<float>()
                        ?? mdist["sonnet"]?.Value<float>() ?? 0f;
                    float opus = mdist["claude-opus-4"]?.Value<float>()
                        ?? mdist["claude-opus"]?.Value<float>()
                        ?? mdist["opus"]?.Value<float>() ?? 0f;
                    float haiku = mdist["claude-haiku"]?.Value<float>()
                        ?? mdist["haiku"]?.Value<float>() ?? 0f;
                    float total = sonnet + opus + haiku;
                    if (total > 0) { sonnet /= total; opus /= total; haiku /= total; }
                    barModelDist.Percentage = sonnet;
                    barModelDist.SecondaryPercentage = opus;
                    barModelDist.ValueText = $"S:{sonnet * 100:F0}% O:{opus * 100:F0}%";
                    barModelDist.SubLabel = haiku > 0 ? $"Haiku: {haiku * 100:F0}%" : "";
                    barModelDist.Invalidate();
                }

                // ── Burn rate (computed from successive readings) ─────────
                var now = DateTime.Now;
                if (!float.IsNaN(_prevTokensUsed) && _prevReadingTime != DateTime.MinValue && tokenUsedAbs > 0)
                {
                    double mins = (now - _prevReadingTime).TotalMinutes;
                    if (mins >= 0.5)
                    {
                        _tokenBurnRate = (tokenUsedAbs - _prevTokensUsed) / (float)mins;
                        _costBurnRate = (costUsedAbs - _prevCostUsed) / (float)mins;
                    }
                }
                if (tokenUsedAbs > 0)
                {
                    _prevTokensUsed = tokenUsedAbs;
                    _prevCostUsed = costUsedAbs;
                    _prevReadingTime = now;
                }

                lblBurnRate.Text = (!float.IsNaN(_tokenBurnRate) && _tokenBurnRate >= 0)
                    ? $"{_tokenBurnRate:N1} tok/min"
                    : "-- tok/min";

                lblCostRate.Text = (!float.IsNaN(_costBurnRate) && _costBurnRate >= 0)
                    ? $"${_costBurnRate:F4}/min"
                    : "-- $/min";

                // ── Predictions ──────────────────────────────────────────
                string tokensOutStr = "--";
                if (!float.IsNaN(_tokenBurnRate) && _tokenBurnRate > 0 && tokenLimit > 0)
                {
                    float minsLeft = (tokenLimit - tokenUsedAbs) / _tokenBurnRate;
                    tokensOutStr = DateTime.Now.AddMinutes(minsLeft).ToString("HH:mm");
                }
                string resetsAtStr = _resetAt != DateTime.MinValue
                    ? _resetAt.ToLocalTime().ToString("HH:mm")
                    : "--";
                lblPredictions.Text = $"Tokens: {tokensOutStr}  |  Resets: {resetsAtStr}";

                // ── Peak/standard ────────────────────────────────────────
                string rateStatus = GetRateStatus();
                lblPeakStatus.Text = rateStatus;
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
            }
        }
    }
}
