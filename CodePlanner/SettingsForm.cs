using System;
using System.Drawing;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class SettingsForm : Form
    {
        private readonly GeminiSettings _settings;
        private TextBox txtApiKey = default!;
        private ComboBox cbModel = default!;
        private ComboBox cbLanguage = default!;
        private CheckBox chkZobrazitKlic = default!;
        private LinkLabel lnkGetApiKey = default!;
        private Button btnUlozit = default!;
        private Button btnStorno = default!;
        private Button btnTest = default!;

        public SettingsForm()
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            _settings = GeminiSettings.Load();

            ClientSize = new Size(500, 380);
            MinimumSize = new Size(450, 380);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = LocalizationService.T("Nastavení Gemini API", "Gemini API Settings");
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            PostavUI();
            LoadValues();
        }

        private void PostavUI()
        {
            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(16),
                BackColor = Color.Transparent
            };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key textbox
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Checkbox + LinkLabel flow
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model combobox
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Language label
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Language combobox
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Spacer/Buttons

            var lblApiKey = new Label
            {
                Text = LocalizationService.T("Klíč k API Gemini (lze nastavit i přes proměnnou prostředí GEMINI_API_KEY):", "Gemini API Key (can also be set via GEMINI_API_KEY environment variable):"),
                AutoSize = true,
                Font = DesignSystem.BodyBold,
                Margin = new Padding(0, 0, 0, 4)
            };

            txtApiKey = new TextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                Margin = new Padding(0, 0, 0, 4),
                Font = DesignSystem.Body
            };

            var pnlKeyHelper = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12),
                Height = 24
            };

            chkZobrazitKlic = new CheckBox
            {
                Text = LocalizationService.T("Zobrazit klíč", "Show key"),
                AutoSize = true,
                Font = DesignSystem.Small,
                ForeColor = DesignSystem.SedaText,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 12, 0)
            };
            chkZobrazitKlic.CheckedChanged += (s, e) =>
            {
                txtApiKey.UseSystemPasswordChar = !chkZobrazitKlic.Checked;
            };

            lnkGetApiKey = new LinkLabel
            {
                Text = LocalizationService.T("Získat API klíč v Google AI Studio", "Get API key in Google AI Studio"),
                Font = DesignSystem.SmallUnderline,
                LinkColor = DesignSystem.Teal,
                ActiveLinkColor = Color.FromArgb(19, 150, 137),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 3, 0, 0)
            };
            lnkGetApiKey.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://aistudio.google.com/") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, LocalizationService.T("Nepodařilo se otevřít odkaz: ", "Failed to open link: ") + ex.Message, LocalizationService.T("Chyba", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            pnlKeyHelper.Controls.Add(chkZobrazitKlic);
            pnlKeyHelper.Controls.Add(lnkGetApiKey);

            var lblModel = new Label
            {
                Text = LocalizationService.T("Model Gemini:", "Gemini Model:"),
                AutoSize = true,
                Font = DesignSystem.BodyBold,
                Margin = new Padding(0, 0, 0, 4)
            };

            cbModel = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 12),
                Font = DesignSystem.Body
            };
            cbModel.Items.Add("gemini-2.5-flash");
            cbModel.Items.Add("gemini-2.5-pro");
            cbModel.Items.Add("gemini-2.0-flash");
            cbModel.Items.Add("gemini-1.5-flash");
            cbModel.Items.Add("gemini-1.5-pro");

            var lblLanguage = new Label
            {
                Text = LocalizationService.T("Jazyk / Language:", "Language / Jazyk:"),
                AutoSize = true,
                Font = DesignSystem.BodyBold,
                Margin = new Padding(0, 0, 0, 4)
            };

            cbLanguage = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 20),
                Font = DesignSystem.Body
            };
            cbLanguage.Items.Add("Čeština (Czech)");
            cbLanguage.Items.Add("English");

            var pnlTlacitka = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Height = 36,
                Margin = new Padding(0)
            };

            btnStorno = new Button
            {
                Text = LocalizationService.T("Storno", "Cancel"),
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0),
                Font = DesignSystem.Body
            };
            btnStorno.FlatAppearance.BorderColor = DesignSystem.Teal;
            btnStorno.FlatAppearance.MouseOverBackColor = DesignSystem.TealSvetla;

            btnUlozit = new Button
            {
                Text = LocalizationService.T("Uložit", "Save"),
                Size = new Size(90, 30),
                BackColor = DesignSystem.Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0),
                Font = DesignSystem.BodyBold
            };
            btnUlozit.FlatAppearance.BorderSize = 0;
            btnUlozit.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnUlozit.Click += BtnUlozit_Click;

            btnTest = new Button
            {
                Text = LocalizationService.T("🧪 Testovat připojení", "🧪 Test Connection"),
                Size = new Size(130, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
                Font = DesignSystem.Body
            };
            btnTest.FlatAppearance.BorderColor = Color.Silver;
            btnTest.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 238, 242);
            btnTest.Click += BtnTest_Click;

            pnlTlacitka.Controls.Add(btnStorno);
            pnlTlacitka.Controls.Add(btnUlozit);
            pnlTlacitka.Controls.Add(btnTest);

            pnlMain.Controls.Add(lblApiKey, 0, 0);
            pnlMain.Controls.Add(txtApiKey, 0, 1);
            pnlMain.Controls.Add(pnlKeyHelper, 0, 2);
            pnlMain.Controls.Add(lblModel, 0, 3);
            pnlMain.Controls.Add(cbModel, 0, 4);
            pnlMain.Controls.Add(lblLanguage, 0, 5);
            pnlMain.Controls.Add(cbLanguage, 0, 6);

            var wrapper = new Panel { Dock = DockStyle.Fill };
            wrapper.Controls.Add(pnlTlacitka); // Bottom dock first
            wrapper.Controls.Add(pnlMain);      // Fill dock second

            Controls.Add(wrapper);

            AcceptButton = btnUlozit;
            CancelButton = btnStorno;
        }

        private void LoadValues()
        {
            txtApiKey.Text = _settings.GeminiApiKey;
            
            int idx = cbModel.Items.IndexOf(_settings.GeminiModel);
            if (idx >= 0)
            {
                cbModel.SelectedIndex = idx;
            }
            else
            {
                cbModel.Items.Add(_settings.GeminiModel);
                cbModel.SelectedIndex = cbModel.Items.Count - 1;
            }

            cbLanguage.SelectedIndex = string.Equals(_settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private void BtnUlozit_Click(object? sender, EventArgs e)
        {
            _settings.GeminiApiKey = txtApiKey.Text.Trim();
            _settings.GeminiModel = cbModel.SelectedItem?.ToString() ?? "gemini-2.5-flash";
            _settings.Language = cbLanguage.SelectedIndex == 1 ? "en" : "cs";

            try
            {
                _settings.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, LocalizationService.T("Chyba při ukládání nastavení:\n\n", "Error saving settings:\n\n") + ex.Message,
                    LocalizationService.T("Chyba", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnTest_Click(object? sender, EventArgs e)
        {
            string testKey = txtApiKey.Text.Trim();
            string testModel = cbModel.SelectedItem?.ToString() ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(testKey))
            {
                MessageBox.Show(this, LocalizationService.T("Zadejte prosím platný API klíč pro testování.", "Please enter a valid API key to test."), LocalizationService.T("Test připojení", "Test Connection"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTest.Enabled = false;
            btnTest.Text = LocalizationService.T("🧪 Testování...", "🧪 Testing...");
            Cursor = Cursors.WaitCursor;

            try
            {
                await GeminiService.TestConnectionAsync(testKey, testModel);
                MessageBox.Show(this, LocalizationService.T("Připojení k Gemini API bylo úspěšně ověřeno. Váš API klíč je platný!", "Connection to Gemini API verified successfully. Your API key is valid!"), LocalizationService.T("Test úspěšný", "Test Successful"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, LocalizationService.T("Test připojení selhal:\n\n", "Test connection failed:\n\n") + ex.Message, LocalizationService.T("Chyba připojení", "Connection Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTest.Enabled = true;
                btnTest.Text = LocalizationService.T("🧪 Testovat připojení", "🧪 Test Connection");
                Cursor = Cursors.Default;
            }
        }
    }
}
