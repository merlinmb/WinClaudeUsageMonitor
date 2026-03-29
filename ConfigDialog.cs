using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeUsageBar
{
    public class ConfigDialog : Form
    {
        public string ApiKey { get; private set; } = "";
        private TextBox txtApiKey;
        private Button btnSave;

        public ConfigDialog()
        {
            this.Text = "Configure API Key";
            this.Size = new Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 32, 32);

            var lbl = new Label
            {
                Text = "Anthropic API Key:",
                Location = new Point(20, 20),
                Size = new Size(120, 20),
                ForeColor = Color.White
            };
            this.Controls.Add(lbl);

            txtApiKey = new TextBox
            {
                Location = new Point(150, 20),
                Size = new Size(200, 20),
                UseSystemPasswordChar = true
            };
            this.Controls.Add(txtApiKey);

            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(150, 60),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => { ApiKey = txtApiKey.Text; this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.Add(btnSave);
        }
    }
}
