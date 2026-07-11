using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class MetrikyForm : Form
    {
        private readonly ProjectMetrics _metrics;
        private string? _apiKey;
        private string? _model;
        private readonly ProjectSpecification _project;
        private readonly Action _onZmena;

        private Label lblCasOdhaduVal = default!;
        private Label lblKomplexitaVal = default!;
        private Label lblRozpocetVal = default!;
        private Label lblSlozeniVal = default!;
        private RichTextBox rtbRozbor = default!;
        private RichTextBox rtbRizika = default!;
        private Label lblStatus = default!;
        private Button btnAiMetriky = default!;
        private Button btnCopy = default!;
        private CancellationTokenSource? _cts = null;

        public MetrikyForm(ProjectMetrics metrics, string? apiKey, string? model, ProjectSpecification project, Action onZmena)
        {
            _metrics = metrics;
            _apiKey = apiKey;
            _model = model;
            _project = project;
            _onZmena = onZmena;

            Text = "Project Metrics & Estimates";
            Size = new Size(820, 580);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 450);
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            // Esc closes the form
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content split
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Bottom action bar

            // Header panel (Navy)
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DesignSystem.Navy
            };
            var lblTitle = new Label
            {
                Text = "📊 AI Estimate & Project Metrics",
                ForeColor = Color.White,
                Font = DesignSystem.HeaderMedium,
                Dock = DockStyle.Left,
                Width = 400,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            pnlHeader.Controls.Add(lblTitle);

            // Split Container
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 320,
                BorderStyle = BorderStyle.None
            };

            // LEVÝ PANEL: vizuální vizitka a hlavní metriky
            var pnlLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            float scale = 1.0f;
            int cardHeight = (int)(65 * scale);
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel CreateCard(string label, out Label valLabel)
            {
                var card = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 0, 0, 8),
                    Padding = new Padding(8)
                };

                var lblName = new Label
                {
                    Text = label,
                    ForeColor = DesignSystem.SedaText,
                    Font = DesignSystem.SmallBold,
                    Dock = DockStyle.Top,
                    Height = (int)(18 * scale)
                };

                valLabel = new Label
                {
                    ForeColor = DesignSystem.Navy,
                    Font = DesignSystem.BodyBold,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                card.Controls.Add(valLabel);
                card.Controls.Add(lblName);
                return card;
            }

            pnlLeft.Controls.Add(CreateCard("ESTIMATED DEVELOPMENT TIME", out lblCasOdhaduVal), 0, 0);
            pnlLeft.Controls.Add(CreateCard("PROJECT COMPLEXITY", out lblKomplexitaVal), 0, 1);
            pnlLeft.Controls.Add(CreateCard("RECOMMENDED BUDGET", out lblRozpocetVal), 0, 2);
            pnlLeft.Controls.Add(CreateCard("TEAM COMPOSITION", out lblSlozeniVal), 0, 3);

            split.Panel1.Controls.Add(pnlLeft);

            // PRAVÝ PANEL: technický rozbor a rizika
            var tabContent = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = DesignSystem.Body
            };

            var tabRozbor = new TabPage("🔧 Technical Breakdown & Architecture");
            rtbRozbor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = DesignSystem.Body,
                Padding = new Padding(8)
            };
            tabRozbor.Controls.Add(rtbRozbor);

            var tabRizika = new TabPage("⚠ Estimated Project Risks");
            rtbRizika = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = DesignSystem.Body,
                Padding = new Padding(8)
            };
            tabRizika.Controls.Add(rtbRizika);

            tabContent.TabPages.Add(tabRozbor);
            tabContent.TabPages.Add(tabRizika);

            split.Panel2.Controls.Add(tabContent);

            // Bottom bar
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Padding = new Padding(6)
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Left,
                Width = 350,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DesignSystem.SedaText,
                Font = DesignSystem.BodyItalic
            };

            btnAiMetriky = new Button
            {
                Text = "🤖 Calculate Estimate via AI",
                Width = 200,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = DesignSystem.Navy,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = DesignSystem.BodyBold
            };
            btnAiMetriky.FlatAppearance.BorderSize = 0;
            btnAiMetriky.Click += BtnAiMetriky_Click;

            btnCopy = new Button
            {
                Text = "📋 Copy text",
                Width = 140,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0),
                Font = DesignSystem.Body
            };
            btnCopy.FlatAppearance.BorderColor = Color.Silver;
            btnCopy.Click += BtnCopy_Click;

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(btnCopy);
            pnlBottom.Controls.Add(btnAiMetriky);

            mainLayout.Controls.Add(pnlHeader, 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            Controls.Add(mainLayout);

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                btnAiMetriky.Text = "🔑 Configure API Key";
            }

            NaplnMetriky();

            FormClosing += (s, e) =>
            {
                try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
            };
        }

        private void NaplnMetriky()
        {
            if (_metrics == null || _metrics.CalculationTimestamp == default)
            {
                lblCasOdhaduVal.Text = "Zatím nespočítáno";
                lblKomplexitaVal.Text = "Zatím nespočítáno";
                lblRozpocetVal.Text = "Zatím nespočítáno";
                lblSlozeniVal.Text = "Zatím nespočítáno";
                rtbRozbor.Text = "Klikněte na tlačítko 'Spočítat odhad přes AI' pro asynchronní analýzu specifikace a backlogu pomocí Gemini API.";
                rtbRizika.Text = "Seznam rizik bude vygenerován spolu s odhadem.";
                lblStatus.Text = "Odhad dosud nebyl vygenerován.";
                btnCopy.Enabled = false;
                return;
            }

            btnCopy.Enabled = true;
            lblCasOdhaduVal.Text = $"{_metrics.TimeEstimateMin} to {_metrics.TimeEstimateMax}";
            lblKomplexitaVal.Text = _metrics.Complexity;
            
            // Barevné odlišení komplexity
            if (_metrics.Complexity.Contains("High"))
                lblKomplexitaVal.ForeColor = DesignSystem.Cervena;
            else if (_metrics.Complexity.Contains("Medium"))
                lblKomplexitaVal.ForeColor = DesignSystem.Oranzova;
            else
                lblKomplexitaVal.ForeColor = DesignSystem.Zelena;

            lblRozpocetVal.Text = _metrics.RecommendedBudget;
            lblSlozeniVal.Text = _metrics.TeamComposition;

            rtbRozbor.Text = _metrics.TechnicalAnalysis;
            
            rtbRizika.Clear();
            if (_metrics.MetricRisks != null && _metrics.MetricRisks.Count > 0)
            {
                foreach (var r in _metrics.MetricRisks)
                {
                    rtbRizika.AppendText($"• {r}\n\n");
                }
            }
            else
            {
                rtbRizika.Text = "No specific threats or risks defined yet.";
            }

            lblStatus.Text = $"Estimate updated: {_metrics.CalculationTimestamp:yyyy-MM-dd H:mm}";
        }

        private async void BtnAiMetriky_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                using (var settingsDlg = new SettingsForm())
                {
                    if (settingsDlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var nast = GeminiSettings.Load();
                        _apiKey = nast.EffectiveApiKey;
                        _model = nast.GeminiModel;
                        if (!string.IsNullOrWhiteSpace(_apiKey))
                        {
                            btnAiMetriky.Text = "🤖 Calculate Estimate via AI";
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

            Cursor = Cursors.WaitCursor;
            btnAiMetriky.Text = "❌ Cancel Estimate";
            btnAiMetriky.Enabled = true;
            lblStatus.Text = "Calculating estimate using Gemini API...";
            _cts = new CancellationTokenSource();

            try
            {
                var noveMetriky = await GeminiService.GenerateMetricsAsync(_apiKey, _model ?? "gemini-2.5-flash", _project, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                
                _metrics.TimeEstimateMin = noveMetriky.TimeEstimateMin;
                _metrics.TimeEstimateMax = noveMetriky.TimeEstimateMax;
                _metrics.Complexity = noveMetriky.Complexity;
                _metrics.RecommendedBudget = noveMetriky.RecommendedBudget;
                _metrics.TeamComposition = noveMetriky.TeamComposition;
                _metrics.TechnicalAnalysis = noveMetriky.TechnicalAnalysis;
                _metrics.MetricRisks = noveMetriky.MetricRisks;
                _metrics.CalculationTimestamp = DateTime.Now;

                _project.ChangeLog.Add(new DecisionLogEntry
                {
                    Timestamp = DateTime.Now,
                    Action = "Project Metrics",
                    Detail = $"AI effort estimate: {_metrics.TimeEstimateMin} to {_metrics.TimeEstimateMax} (complexity: {_metrics.Complexity})."
                });

                _onZmena();
                NaplnMetriky();
                MessageBox.Show(this, "AI analysis and project estimate completed successfully.", "Estimate Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = "Calculation cancelled by user.";
                    return;
                }
                MessageBox.Show(this, "Estimate calculation failed:\n\n" + ex.Message, "AI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Calculation failed.";
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (!this.IsDisposed && this.Created)
                {
                    btnAiMetriky.Enabled = true;
                    btnAiMetriky.Text = "🤖 Calculate Estimate via AI";
                    Cursor = Cursors.Default;
                }
            }

        }

        private void BtnCopy_Click(object? sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PROJECT METRICS AND ESTIMATE ===");
            sb.AppendLine($"Project: {_project.Name}");
            sb.AppendLine($"Time estimate: {_metrics.TimeEstimateMin} to {_metrics.TimeEstimateMax}");
            sb.AppendLine($"Complexity: {_metrics.Complexity}");
            sb.AppendLine($"Recommended budget: {_metrics.RecommendedBudget}");
            sb.AppendLine($"Recommended team composition: {_metrics.TeamComposition}");
            sb.AppendLine();
            sb.AppendLine("--- Technical analysis ---");
            sb.AppendLine(_metrics.TechnicalAnalysis);
            sb.AppendLine();
            sb.AppendLine("--- Estimated risks ---");
            foreach (var r in _metrics.MetricRisks)
            {
                sb.AppendLine($"- {r}");
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show(this, "The complete estimate text has been copied to the clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to copy to clipboard:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
