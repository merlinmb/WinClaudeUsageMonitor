using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeUsageBar
{
    public class UsageBar : Control
    {
        public float Percentage { get; set; } = 0f; // 0.0 - 1.0
        public Color BarColor { get; set; } = Color.Gold;
        public string Label { get; set; } = "";
        public string ValueText { get; set; } = "";
        public string SubLabel { get; set; } = "";
        public bool LargeValue { get; set; } = false;
        // For split distribution bars (e.g. model distribution)
        public float SecondaryPercentage { get; set; } = 0f;
        public Color SecondaryBarColor { get; set; } = Color.MediumPurple;

        public UsageBar()
        {
            this.DoubleBuffered = true;
            this.Size = new Size(180, 60);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(Color.FromArgb(24, 24, 24));

            // Draw label
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                g.DrawString(Label.ToUpper(), font, brush, 0, 0);

            // Draw value
            using (var font = new Font("Segoe UI", LargeValue ? 18f : 13f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
                g.DrawString(ValueText, font, brush, 0, 15);

            // Draw sublabel
            if (!string.IsNullOrEmpty(SubLabel))
            {
                using (var font = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                    g.DrawString(SubLabel, font, brush, 0, 40);
            }

            // Draw bar
            int barY = this.Height - 8;
            int barW = (int)(this.Width * Percentage);
            using (var barBrush = new SolidBrush(BarColor))
                g.FillRectangle(barBrush, 0, barY, barW, 4);
            if (SecondaryPercentage > 0f)
            {
                int secW = (int)(this.Width * SecondaryPercentage);
                using (var secBrush = new SolidBrush(SecondaryBarColor))
                    g.FillRectangle(secBrush, barW, barY, secW, 4);
                int remainder = this.Width - barW - secW;
                if (remainder > 0)
                    using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                        g.FillRectangle(bgBrush, barW + secW, barY, remainder, 4);
            }
            else
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                    g.FillRectangle(bgBrush, barW, barY, this.Width - barW, 4);
            }
        }
    }
}
