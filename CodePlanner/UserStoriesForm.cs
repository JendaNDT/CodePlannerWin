using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public class UserStoriesForm : Form
    {
        private readonly List<UserStory> _stories;
        private string? _apiKey;
        private string? _model;
        private readonly ProjectSpecification _project;
        private readonly Action _onZmena;

        private ListBox lstStories = default!;
        private RichTextBox rtbDetail = default!;
        private Label lblStatus = default!;
        private Button btnAiStories = default!;
        private Button btnExportMd = default!;
        private Button btnExportCsv = default!;
        private CancellationTokenSource? _cts = null;

        public UserStoriesForm(List<UserStory> stories, string? apiKey, string? model, ProjectSpecification project, Action onZmena)
        {
            _stories = stories;
            _apiKey = apiKey;
            _model = model;
            _project = project;
            _onZmena = onZmena;

            Text = "User Stories";
            Size = new Size(850, 580);
            MinimumSize = new Size(750, 450);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;
            Font = DesignSystem.Body;
            BackColor = DesignSystem.SvetlePozadi;
            ForeColor = DesignSystem.Navy;

            PostavUI();
            NaplnStories();
        }

        private void PostavUI()
        {
            // 1. Header
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = DesignSystem.Navy
            };
            var lblTitle = new Label
            {
                Text = "💡 User Stories / Backlog",
                Font = DesignSystem.HeaderLarge,
                ForeColor = Color.White,
                Location = new Point(16, 16),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);

            // 2. Footer with buttons
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.White
            };

            lblStatus = new Label
            {
                Text = "Loaded",
                ForeColor = DesignSystem.SedaText,
                Location = new Point(16, 18),
                AutoSize = true,
                Font = DesignSystem.Body
            };

            btnAiStories = new Button
            {
                Text = "🤖 Generate with Gemini",
                Height = 32,
                AutoSize = true,
                BackColor = DesignSystem.Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.BodyBold
            };
            btnAiStories.FlatAppearance.BorderSize = 0;
            btnAiStories.Click += BtnAiStories_Click;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                btnAiStories.Text = "🔑 Configure API Key";
            }

            btnExportMd = new Button
            {
                Text = "⬇ Export Markdown...",
                Height = 32,
                Width = 135,
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnExportMd.FlatAppearance.BorderColor = DesignSystem.Teal;
            btnExportMd.Click += BtnExportMd_Click;

            btnExportCsv = new Button
            {
                Text = "⬇ Export CSV (Jira/Trello)...",
                Height = 32,
                Width = 175,
                FlatStyle = FlatStyle.Flat,
                ForeColor = DesignSystem.Navy,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnExportCsv.FlatAppearance.BorderColor = DesignSystem.Teal;
            btnExportCsv.Click += BtnExportCsv_Click;

            var btnZavrit = new Button
            {
                Text = "Close",
                Height = 32,
                Width = 80,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = DesignSystem.Body
            };
            btnZavrit.FlatAppearance.BorderColor = Color.Silver;
            btnZavrit.Click += (s, e) => Close();

            // ESC key to close
            this.CancelButton = btnZavrit;

            var flowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Width = 580,
                Padding = new Padding(0, 10, 10, 0)
            };
            flowButtons.Controls.Add(btnAiStories);
            flowButtons.Controls.Add(btnExportMd);
            flowButtons.Controls.Add(btnExportCsv);
            flowButtons.Controls.Add(btnZavrit);

            pnlFooter.Controls.Add(flowButtons);
            pnlFooter.Controls.Add(lblStatus);

            // 3. Center - SplitContainer
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 250,
                BackColor = DesignSystem.SvetlePozadi
            };

            lstStories = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = DesignSystem.Body,
                IntegralHeight = false
            };
            lstStories.SelectedIndexChanged += LstStories_SelectedIndexChanged;
            split.Panel1.Controls.Add(lstStories);

            rtbDetail = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                BackColor = Color.White,
                Font = DesignSystem.Body,
                Padding = new Padding(12)
            };
            split.Panel2.Controls.Add(rtbDetail);

            Controls.Add(split);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);

            FormClosing += (s, e) =>
            {
                try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
            };
        }

        private void NaplnStories()
        {
            lstStories.BeginUpdate();
            lstStories.Items.Clear();
            foreach (var s in _stories)
            {
                string prioritaTag = (s.Priority == "High" || s.Priority == "Vysoká") ? "🔴 " : ((s.Priority == "Medium" || s.Priority == "Střední") ? "🟡 " : "🟢 ");
                lstStories.Items.Add($"{prioritaTag}{s.Id}: {s.Title}");
            }
            lstStories.EndUpdate();

            if (_stories.Count > 0)
            {
                lstStories.SelectedIndex = 0;
                lblStatus.Text = $"Loaded {_stories.Count} User Stories.";
                btnExportMd.Enabled = true;
                btnExportCsv.Enabled = true;
            }
            else
            {
                rtbDetail.Text = "No User Stories have been generated yet.\n\nClick the \"🤖 Generate with Gemini\" (or \"🔑 Configure API Key\") button to create an agile backlog based on the current specification.";
                lblStatus.Text = "No User Stories available.";
                btnExportMd.Enabled = false;
                btnExportCsv.Enabled = false;
            }
        }

        private void LstStories_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int idx = lstStories.SelectedIndex;
            if (idx < 0 || idx >= _stories.Count)
            {
                return;
            }

            var s = _stories[idx];
            rtbDetail.Clear();

            // Vykreslíme detail s využitím static fontů z DesignSystem
            rtbDetail.SelectionFont = DesignSystem.HeaderMedium;
            rtbDetail.SelectionColor = DesignSystem.Navy;
            rtbDetail.AppendText($"{s.Id}: {s.Title}\n\n");

            rtbDetail.SelectionFont = DesignSystem.BodyBold;
            rtbDetail.SelectionColor = DesignSystem.SedaText;
            rtbDetail.AppendText("Priority: ");
            rtbDetail.SelectionFont = DesignSystem.BodyBold;
            rtbDetail.SelectionColor = s.Priority == "High" ? DesignSystem.Cervena : (s.Priority == "Medium" ? DesignSystem.Oranzova : DesignSystem.Zelena);
            rtbDetail.AppendText($"{s.Priority}\n\n");

            rtbDetail.SelectionFont = DesignSystem.CardHeader;
            rtbDetail.SelectionColor = DesignSystem.Navy;
            rtbDetail.AppendText("User Story\n");
            
            rtbDetail.SelectionFont = DesignSystem.BodyItalic;
            rtbDetail.SelectionColor = Color.FromArgb(50, 50, 50);
            rtbDetail.AppendText($"> {s.Description}\n\n");

            rtbDetail.SelectionFont = DesignSystem.CardHeader;
            rtbDetail.SelectionColor = DesignSystem.Navy;
            rtbDetail.AppendText("Acceptance Criteria\n");

            rtbDetail.SelectionFont = DesignSystem.Body;
            rtbDetail.SelectionColor = Color.Black;
            foreach (var k in s.Criteria)
            {
                rtbDetail.AppendText($"• {k}\n");
            }
        }

        private async void BtnAiStories_Click(object? sender, EventArgs e)
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
                            btnAiStories.Text = "🤖 Generovat přes Gemini";
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
            btnAiStories.Text = "❌ Cancel Generation";
            btnAiStories.Enabled = true;
            lblStatus.Text = "Calling Gemini API, please wait...";
            _cts = new CancellationTokenSource();

            try
            {
                var noveStories = await GeminiService.GenerateUserStoriesAsync(_apiKey, _model ?? "gemini-2.5-flash", _project, _cts.Token);
                if (this.IsDisposed || !this.Created) return;
                _stories.Clear();
                _stories.AddRange(noveStories);

                // Set stories generation timestamp
                _project.StoriesGenerationTimestamp = DateTime.Now;

                // Log the action
                _project.ChangeLog.Add(new DecisionLogEntry
                {
                    Timestamp = DateTime.Now,
                    Action = "User Stories",
                    Detail = $"Generated {_stories.Count} user stories via Gemini."
                });

                _onZmena(); // Save project change (mark dirty)
                NaplnStories();
                MessageBox.Show(this, $"Successfully generated {_stories.Count} User Stories.", "Generation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    lblStatus.Text = "Generation cancelled by user.";
                    return;
                }
                MessageBox.Show(this, "Error generating User Stories:\n\n" + ex.Message, "AI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Generation failed.";
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                if (!this.IsDisposed && this.Created)
                {
                    btnAiStories.Text = "🤖 Generate with Gemini";
                    btnAiStories.Enabled = true;
                    Cursor = Cursors.Default;
                }
            }

        }

        private void BtnExportMd_Click(object? sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export User Stories to Markdown",
                Filter = "Markdown (*.md)|*.md",
                FileName = "user_stories_" + MainForm.GetSafeFilename(_project.Name, "project") + ".md"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ExportMarkdown(dlg.FileName);
                        MessageBox.Show(this, "Markdown export completed:\n\n" + dlg.FileName, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Export failed:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export User Stories to CSV",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "user_stories_" + MainForm.GetSafeFilename(_project.Name, "project") + ".csv"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ExportCsv(dlg.FileName);
                        MessageBox.Show(this, "CSV export completed (file can be imported to Jira/Trello):\n\n" + dlg.FileName, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Export failed:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportCsv(string soubor)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Issue ID,Summary,Description,Priority");
            foreach (var s in _stories)
            {
                string id = EscapeCsv(s.Id);
                string sum = EscapeCsv(s.Title);
                
                var descBuilder = new StringBuilder();
                descBuilder.AppendLine(s.Description);
                descBuilder.AppendLine();
                descBuilder.AppendLine("Acceptance Criteria:");
                foreach (var k in s.Criteria)
                {
                    descBuilder.AppendLine($"- {k}");
                }
                string desc = EscapeCsv(descBuilder.ToString());
                string prio = EscapeCsv(s.Priority);

                sb.AppendLine($"{id},{sum},{desc},{prio}");
            }
            File.WriteAllText(soubor, sb.ToString(), Encoding.UTF8);
        }

        private void ExportMarkdown(string soubor)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# User Stories for Project: {_project.Name}");
            sb.AppendLine($"Generation Date: {DateTime.Now:yyyy-MM-dd}");
            sb.AppendLine();
            foreach (var s in _stories)
            {
                sb.AppendLine($"## {s.Id}: {s.Title}");
                sb.AppendLine($"**Priority:** {s.Priority}");
                sb.AppendLine();
                sb.AppendLine($"> {s.Description}");
                sb.AppendLine();
                sb.AppendLine("### Acceptance Criteria:");
                foreach (var k in s.Criteria)
                {
                    sb.AppendLine($"- [ ] {k}");
                }
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
            File.WriteAllText(soubor, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string safeText = text;
            if (safeText.StartsWith("=") || safeText.StartsWith("+") || safeText.StartsWith("-") || safeText.StartsWith("@"))
            {
                safeText = "'" + safeText;
            }
            if (safeText.Contains("\"") || safeText.Contains(",") || safeText.Contains("\n") || safeText.Contains("\r"))
            {
                return "\"" + safeText.Replace("\"", "\"\"") + "\"";
            }
            return safeText;
        }
    }
}
