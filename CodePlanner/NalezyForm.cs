using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Linq;
using CodePlanner.Core;

namespace CodePlanner
{
    public class NalezyForm : Form
    {
        private readonly List<ConsistencyFinding> _offlineFindings;
        private string? _apiKey;
        private string? _model;
        private readonly ProjectSpecification _project;
        private readonly Action _onZmena;
        private ListView lvNalezy = default!;
        private Button btnAiCheck = default!;
        private Label lblStatus = default!;
        private CancellationTokenSource? _cts = null;

        public NalezyForm(List<ConsistencyFinding> offlineFindings, string? apiKey, string? model, ProjectSpecification project, Action onZmena)
        {
            _offlineFindings = offlineFindings;
            _apiKey = apiKey;
            _model = model;
            _project = project;
            _onZmena = onZmena;

            Text = LocalizationService.T("Kontrola konzistence specifikace", "Specification Consistency Check");
            Size = new Size(750, 480);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(500, 350);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            this.Resize += (s, e) =>
            {
                if (lvNalezy == null) return;
                int totalWidth = lvNalezy.ClientSize.Width;
                if (totalWidth > 320)
                {
                    lvNalezy.Columns[0].Width = (int)(totalWidth * 0.15);
                    lvNalezy.Columns[1].Width = (int)(totalWidth * 0.25);
                    lvNalezy.Columns[2].Width = totalWidth - lvNalezy.Columns[0].Width - lvNalezy.Columns[1].Width - 4;
                }
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // ListView
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));  // Status label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Buttons

            lvNalezy = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.FixedSingle,
                Font = DesignSystem.Body
            };
            lvNalezy.Columns.Add(LocalizationService.T("Typ", "Type"), 110);
            lvNalezy.Columns.Add(LocalizationService.T("Téma / Oblast", "Topic / Field"), 180);
            lvNalezy.Columns.Add(LocalizationService.T("Detail / Navrhované řešení", "Detail / Proposed Resolution"), 410);

            lblStatus = new Label
            {
                Text = LocalizationService.T("Offline kontrola porovnává klíčová slova. Spusťte hloubkovou AI analýzu pro sémantickou kontrolu.", "The offline check compares keywords. Run deep AI consistency analysis for semantic inspection."),
                Dock = DockStyle.Fill,
                ForeColor = DesignSystem.SedaText,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = DesignSystem.BodyItalic
            };

            btnAiCheck = new Button
            {
                Text = LocalizationService.T("🧠 Spustit hloubkovou AI analýzu", "🧠 Run Deep AI Analysis"),
                Height = 32,
                AutoSize = true,
                BackColor = DesignSystem.Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.BodyBold
            };
            btnAiCheck.FlatAppearance.BorderSize = 0;
            btnAiCheck.Click += BtnAiCheck_Click;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                btnAiCheck.Text = LocalizationService.T("🔑 Konfigurovat API klíč", "🔑 Configure API Key");
            }

            var btnZavrit = new Button
            {
                Text = LocalizationService.T("Zavřít", "Close"),
                Height = 32,
                Width = 100,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnZavrit.FlatAppearance.BorderColor = Color.Silver;
            btnZavrit.Click += (s, e) => Close();

            this.CancelButton = btnZavrit;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            flow.Controls.Add(btnAiCheck);
            flow.Controls.Add(btnZavrit);

            layout.Controls.Add(lvNalezy, 0, 0);
            layout.Controls.Add(lblStatus, 0, 1);
            layout.Controls.Add(flow, 0, 2);

            Controls.Add(layout);

            NaplnNalezy(_offlineFindings, isAi: false);

            if (_project.AiFindings != null && _project.AiFindings.Count > 0)
            {
                var converted = _project.AiFindings.Select(f => new ConsistencyFinding
                {
                    Severity = string.Equals(f.Severity, "Rozpor", StringComparison.OrdinalIgnoreCase) || string.Equals(f.Severity, "Conflict", StringComparison.OrdinalIgnoreCase) ? Severity.Conflict : Severity.Warning,
                    Title = f.Title,
                    Detail = f.Detail
                }).ToList();
                NaplnNalezy(converted, isAi: true);
                lblStatus.Text = LocalizationService.T($"Načteny AI nálezy z poslední kontroly ({_project.AiCheckTimestamp:g}).", $"Loaded AI findings from the last check ({_project.AiCheckTimestamp:g}).");
                lblStatus.ForeColor = DesignSystem.Zelena;
            }

            FormClosing += (s, e) =>
            {
                try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
            };
        }


        private void NaplnNalezy(List<ConsistencyFinding> list, bool isAi)
        {
            lvNalezy.BeginUpdate();
            if (!isAi) lvNalezy.Items.Clear();

            foreach (var n in list)
            {
                bool isConflict = n.Severity == Severity.Conflict;
                var it = new ListViewItem(isConflict ? LocalizationService.T("❗ ROZPOR", "❗ CONFLICT") : LocalizationService.T("⚠ Varování", "⚠ Warning"));
                it.ForeColor = isConflict ? DesignSystem.Cervena : DesignSystem.Oranzova;
                
                if (isAi)
                {
                    it.Text = isConflict ? LocalizationService.T("🧠 ROZPOR (AI)", "🧠 CONFLICT (AI)") : LocalizationService.T("🧠 Varování (AI)", "🧠 Warning (AI)");
                }

                it.SubItems.Add(n.Title);
                it.SubItems.Add(n.Detail);
                lvNalezy.Items.Add(it);
            }
            lvNalezy.EndUpdate();
        }

        private async void BtnAiCheck_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                using (var settingsDlg = new SettingsForm())
                {
                    if (settingsDlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var nastaveni = GeminiSettings.Load();
                        _apiKey = nastaveni.EffectiveApiKey;
                        _model = nastaveni.GeminiModel;
                        if (!string.IsNullOrWhiteSpace(_apiKey))
                        {
                            btnAiCheck.Text = LocalizationService.T("🧠 Spustit hloubkovou AI analýzu", "🧠 Run Deep AI Analysis");
                        }
                    }
                }
                return;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            btnAiCheck.Text = LocalizationService.T("❌ Zrušit analýzu", "❌ Cancel Analysis");
            btnAiCheck.Enabled = true;
            lblStatus.Text = LocalizationService.T("Volám Gemini API pro hloubkovou kontrolu konzistence, čekejte prosím...", "Calling Gemini API for deep consistency check, please wait...");
            lblStatus.ForeColor = DesignSystem.Navy;

            // Vyčistíme staré nálezy a naplníme seznam pouze výchozími offline nálezy před novou AI kontrolou
            NaplnNalezy(_offlineFindings, isAi: false);
            _cts = new CancellationTokenSource();

            try
            {
                var aiFindings = await GeminiService.AnalyzeConsistencyAsync(_apiKey, _model ?? "gemini-2.5-flash", _project, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                
                // Uložíme do projektu a spustíme callback změn
                _project.AiFindings = aiFindings.Select(AiFinding.FromFinding).ToList();
                _project.AiCheckTimestamp = DateTime.Now;
                _project.ChangeLog.Add(new DecisionLogEntry { Timestamp = DateTime.Now, Action = LocalizationService.T("AI Analýza", "AI Analysis"), Detail = LocalizationService.T($"Byla spuštěna hloubková AI kontrola konzistence. Nalezeno {aiFindings.Count} nálezů.", $"Deep AI consistency check executed. Found {aiFindings.Count} findings.") });
                _onZmena?.Invoke();

                if (aiFindings.Count == 0)
                {
                    lblStatus.Text = LocalizationService.T("Gemini AI nenalezla žádné logické rozpory ani bezpečnostní slabiny. Skvělá práce!", "Gemini AI found no logical conflicts or security vulnerabilities. Great job!");
                    lblStatus.ForeColor = DesignSystem.Zelena;
                }
                else
                {
                    NaplnNalezy(aiFindings, isAi: true);
                    lblStatus.Text = LocalizationService.T($"Analýza dokončena. Nalezeno {aiFindings.Count} nových AI nálezů.", $"Analysis complete. Found {aiFindings.Count} new AI findings.");
                    lblStatus.ForeColor = DesignSystem.Zelena;
                }
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = LocalizationService.T("Analýza zrušena uživatelem.", "Analysis cancelled by user.");
                    lblStatus.ForeColor = DesignSystem.Navy;
                    return;
                }
                MessageBox.Show(this, LocalizationService.T("AI analýza selhala:\n\n", "AI analysis failed:\n\n") + ex.Message, LocalizationService.T("Chyba AI", "AI Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = LocalizationService.T("Během AI analýzy došlo k chybě.", "An error occurred during AI analysis.");
                lblStatus.ForeColor = DesignSystem.Cervena;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (!this.IsDisposed && this.Created)
                {
                    btnAiCheck.Text = LocalizationService.T("🧠 Spustit hloubkovou AI analýzu", "🧠 Run Deep AI Analysis");
                    btnAiCheck.Enabled = true;
                }
            }

        }
    }
}
