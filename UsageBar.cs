using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeUsageBar
{
    public class UsageBar : Control
    {
        private float _percentage;
        private string _valueText = "";
        private string _subLabel = "";
        private float _secondaryPercentage;

        public float Percentage
        {
            get => _percentage;
            set { _percentage = value; Invalidate(); }
        }
        public string ValueText
        {
            get => _valueText;
            set { _valueText = value; Invalidate(); }
        }
        public string SubLabel
        {
            get => _subLabel;
            set { _subLabel = value; Invalidate(); }
        }
        public float SecondaryPercentage
        {
            get => _secondaryPercentage;
            set { _secondaryPercentage = value; Invalidate(); }
        }

        public Color BarColor { get; set; } = Color.Gold;
        public string Label { get; set; } = "";
        public bool LargeValue { get; set; } = false;
        // For split distribution bars (e.g. model distribution)
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
                g.DrawString(ValueText, font, brush, 0, 13);

            // Draw sublabel — sits above the bar, below the value
            if (!string.IsNullOrEmpty(SubLabel))
            {
                using (var font = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                    g.DrawString(SubLabel, font, brush, 0, 36);
            }

            // Draw bar — always at the very bottom
            int barY = this.Height - 5;
            int totalW = this.Width;
            int barW = Math.Clamp((int)(totalW * Percentage), 0, totalW);
            using (var barBrush = new SolidBrush(BarColor))
                g.FillRectangle(barBrush, 0, barY, barW, 4);
            if (SecondaryPercentage > 0f)
            {
                int secW = Math.Clamp((int)(totalW * SecondaryPercentage), 0, totalW - barW);
                using (var secBrush = new SolidBrush(SecondaryBarColor))
                    g.FillRectangle(secBrush, barW, barY, secW, 4);
                int remainder = totalW - barW - secW;
                if (remainder > 0)
                    using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                        g.FillRectangle(bgBrush, barW + secW, barY, remainder, 4);
            }
            else
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                    g.FillRectangle(bgBrush, barW, barY, totalW - barW, 4);
            }
        }
    }
}
