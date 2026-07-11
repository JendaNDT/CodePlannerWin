using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public partial class MainForm : Form
    {
        private void NewProject(bool prvniSpusteni)
        {
            if (!prvniSpusteni && !ConfirmUnsavedChanges()) return;

            _projekt = new ProjectSpecification();
            _projekt.ChangeLog.Add(new DecisionLogEntry { Timestamp = DateTime.Now, Action = "Project", Detail = "New project created." });
            _cestaSouboru = null;
            _dirty = false;
            _snapshotAnalyzyExistuje = false;
            RefreshRevertAnalysisButton();

            _nacitani = true;
            txtNazev.Text = "";
            txtNapad.Text = "";
            SetTypeCombo("General");
            txtOdpoved.Text = "";
            _nacitani = false;

            RefreshAll();
            SelectQuestion(SpecificationService.GetNextUnansweredQuestion(_projekt));

            if (!prvniSpusteni)
            {
                SetStatus("New project created.");
            }
        }

        private void OpenProject()
        {
            if (!ConfirmUnsavedChanges()) return;

            using var dlg = new OpenFileDialog
            {
                Title = "Open Project",
                Filter = "CodePlanner Project (*.vcbrief)|*.vcbrief"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                OpenProjectFromPath(dlg.FileName);
            }
        }

        private void OpenProjectFromPath(string cesta)
        {
            if (!File.Exists(cesta))
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T($"Soubor nebyl nalezen:\n\n{cesta}\n\nOdstraňuji ze seznamu nedávných souborů.", $"File not found:\n\n{cesta}\n\nRemoving from recent files list."),
                    CodePlanner.Core.LocalizationService.T("Soubor nenalezen", "File Not Found"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                var nast = GeminiSettings.Load();
                nast.RemoveRecentProject(cesta);
                RefreshRecentMenu();
                return;
            }

            try
            {
                var nacteny = SpecificationService.LoadProject(cesta);
                var dalsiOtazka = SpecificationService.GetNextUnansweredQuestion(nacteny);

                // Safe swap: Teprve při úspěšném načtení a validaci přepíšeme aktivní model
                _projekt = nacteny;
                _cestaSouboru = cesta;
                _dirty = false;

                // záloha před analýzou patřila k předchozímu projektu – tlačítko „↩ Vrátit analýzu“ skryjeme
                _snapshotAnalyzyExistuje = false;
                RefreshRevertAnalysisButton();

                _nacitani = true;
                txtNazev.Text = _projekt.Name ?? "";
                txtNapad.Text = _projekt.Idea ?? "";
                SetTypeCombo(_projekt.ProjectTypeKey);
                txtOdpoved.Text = "";
                _nacitani = false;

                RefreshAll();
                SelectQuestion(dalsiOtazka);
                SetStatus(CodePlanner.Core.LocalizationService.T("Otevřeno: ", "Opened: ") + Path.GetFileName(cesta));

                var nast = GeminiSettings.Load();
                nast.AddRecentProject(cesta);
                RefreshRecentMenu();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Nepodařilo se načíst projektový soubor.\n\n", "Failed to load project file.\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba při otevírání", "Open Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CommitActiveTextFields()
        {
            if (_nacitani) return;

            if (txtNazev != null && (txtNazev.Focused || ActiveControl == txtNazev))
            {
                if ((_projekt.Name ?? "") != _nazevSnapshot)
                {
                    SpecificationService.LogChange(_projekt, "Name", $"Project name changed to '{_projekt.Name}'.");
                    _nazevSnapshot = _projekt.Name ?? "";
                    RefreshLog();
                    RefreshStatus();
                }
            }

            if (txtNapad != null && (txtNapad.Focused || ActiveControl == txtNapad))
            {
                if ((_projekt.Idea ?? "") != _napadSnapshot)
                {
                    SpecificationService.LogChange(_projekt, "Idea", "Project idea text modified.");
                    _napadSnapshot = _projekt.Idea ?? "";
                    RefreshLog();
                    RefreshStatus();
                    RenderSpecification();
                }
            }
        }

        private bool SaveProject()
        {
            CommitActiveTextFields();
            if (_cestaSouboru == null)
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Save Project",
                    Filter = "CodePlanner Project (*.vcbrief)|*.vcbrief",
                    FileName = GetSafeFilename(_projekt.Name, "project") + ".vcbrief"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                _cestaSouboru = dlg.FileName;
            }

            try
            {
                SpecificationService.SaveProject(_projekt, _cestaSouboru);
                _dirty = false;
                DeleteAutosave();   // po ručním uložení už automatická záloha není potřeba
                RefreshTitle();
                SetStatus(CodePlanner.Core.LocalizationService.T("Uloženo: ", "Saved: ") + Path.GetFileName(_cestaSouboru));

                var nast = GeminiSettings.Load();
                nast.AddRecentProject(_cestaSouboru);
                RefreshRecentMenu();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Nepodařilo se uložit projekt.\n\n", "Failed to save project.\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba při ukládání", "Save Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void Export(bool markdown)
        {
            if (!ConfirmExportWithSummary()) return;

            using var dlg = new SaveFileDialog
            {
                Title = markdown 
                    ? CodePlanner.Core.LocalizationService.T("Exportovat specifikaci (Markdown)", "Export Specification (Markdown)") 
                    : CodePlanner.Core.LocalizationService.T("Exportovat specifikaci (JSON)", "Export Specification (JSON)"),
                Filter = markdown 
                    ? CodePlanner.Core.LocalizationService.T("Markdown (*.md)|*.md", "Markdown (*.md)|*.md") 
                    : CodePlanner.Core.LocalizationService.T("JSON (*.json)|*.json", "JSON (*.json)|*.json"),
                FileName = GetSafeFilename(_projekt.Name, "specification") + (markdown ? ".md" : ".json")
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var obsah = markdown ? SpecificationService.RenderMarkdown(_projekt) : SpecificationService.RenderJson(_projekt);
                File.WriteAllText(dlg.FileName, obsah, new UTF8Encoding(true));
                SetStatus(CodePlanner.Core.LocalizationService.T("Export dokončen: ", "Export completed: ") + Path.GetFileName(dlg.FileName));

                var res = MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T(
                        "Specifikace byla úspěšně exportována:\n\n" + dlg.FileName + "\n\nChcete exportovaný soubor nyní otevřít?",
                        "Specification exported successfully:\n\n" + dlg.FileName + "\n\nDo you want to open the exported file now?"
                    ),
                    CodePlanner.Core.LocalizationService.T("Export dokončen", "Export Completed"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (res == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Export selhal.\n\n", "Export failed.\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba exportu", "Export Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportHtml()
        {
            if (!ConfirmExportWithSummary()) return;

            using var dlg = new SaveFileDialog
            {
                Title = CodePlanner.Core.LocalizationService.T("Exportovat specifikaci (interaktivní HTML web)", "Export Specification (Interactive HTML Web)"),
                Filter = CodePlanner.Core.LocalizationService.T("HTML soubory (*.html;*.htm)|*.html;*.htm", "HTML Files (*.html;*.htm)|*.html;*.htm"),
                FileName = GetSafeFilename(_projekt.Name, "specification") + ".html"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var obsah = SpecificationService.RenderHtml(_projekt);
                File.WriteAllText(dlg.FileName, obsah, Encoding.UTF8);
                SetStatus(CodePlanner.Core.LocalizationService.T("HTML export dokončen: ", "HTML export completed: ") + Path.GetFileName(dlg.FileName));
                
                var res = MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T(
                        "Interaktivní HTML specifikace byla úspěšně exportována.\n\nChcete ji nyní otevřít v prohlížeči?",
                        "Interactive HTML specification exported successfully.\n\nDo you want to open it in your browser now?"
                    ),
                    CodePlanner.Core.LocalizationService.T("Export dokončen", "Export Completed"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (res == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Export selhal.\n\n", "Export failed.\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba exportu", "Export Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportPdf()
        {
            if (!ConfirmExportWithSummary()) return;

            using (var dlg = new SaveFileDialog
            {
                Title = CodePlanner.Core.LocalizationService.T("Exportovat specifikaci do PDF", "Export Specification to PDF"),
                Filter = CodePlanner.Core.LocalizationService.T("PDF soubory (*.pdf)|*.pdf", "PDF Files (*.pdf)|*.pdf"),
                FileName = GetSafeFilename(_projekt.Name, "specification") + ".pdf"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                SetStatus(CodePlanner.Core.LocalizationService.T("Exportuji do PDF...", "Exporting to PDF..."));
                try
                {
                    var exp = new PdfExporter(_projekt);
                    exp.Export(this, dlg.FileName);

                    if (!File.Exists(dlg.FileName))
                    {
                        throw new FileNotFoundException(CodePlanner.Core.LocalizationService.T("Soubor PDF nebyl vytvořen. Zkontrolujte prosím konfiguraci své tiskárny PDF.", "PDF file was not created. Please check your PDF printer configuration."));
                    }

                    SetStatus(CodePlanner.Core.LocalizationService.T("PDF export dokončen.", "PDF export completed."));
                    var res = MessageBox.Show(this,
                        CodePlanner.Core.LocalizationService.T(
                            "Specifikace byla úspěšně exportována do PDF.\n\nChcete exportovaný soubor nyní otevřít?",
                            "Specification exported to PDF successfully.\n\nDo you want to open the exported file now?"
                        ),
                        CodePlanner.Core.LocalizationService.T("Export dokončen", "Export Completed"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (res == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = dlg.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Chyba při exportu do PDF:\n\n", "Error exporting to PDF:\n\n") + ex.Message,
                        CodePlanner.Core.LocalizationService.T("Chyba exportu", "Export Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private void RefreshRecentMenu()
        {
            if (btnOtevritSplit == null) return;

            btnOtevritSplit.DropDownItems.Clear();

            var nastaveni = GeminiSettings.Load();
            if (nastaveni.RecentProjects == null || nastaveni.RecentProjects.Count == 0)
            {
                var emptyItem = new ToolStripMenuItem(CodePlanner.Core.LocalizationService.T("Žádné nedávné projekty", "No recent projects")) { Enabled = false };
                btnOtevritSplit.DropDownItems.Add(emptyItem);
                return;
            }

            foreach (var cesta in nastaveni.RecentProjects)
            {
                if (string.IsNullOrWhiteSpace(cesta)) continue;

                string nazevSouboru = Path.GetFileName(cesta);
                var item = new ToolStripMenuItem(nazevSouboru)
                {
                    ToolTipText = cesta,
                    Tag = cesta
                };
                item.Click += (s, e) =>
                {
                    if (!ConfirmUnsavedChanges()) return;
                    var menuIt = (ToolStripMenuItem)s!;
                    string path = menuIt.Tag!.ToString()!;
                    OpenProjectFromPath(path);
                };
                btnOtevritSplit.DropDownItems.Add(item);
            }

            btnOtevritSplit.DropDownItems.Add(new ToolStripSeparator());
            var clearItem = new ToolStripMenuItem(CodePlanner.Core.LocalizationService.T("Vymazat historii...", "Clear history..."), null, (s, e) => 
            {
                var confirm = MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T("Opravdu chcete vymazat historii nedávných projektů?", "Are you sure you want to clear the history of recent projects?"),
                    CodePlanner.Core.LocalizationService.T("Vymazat historii", "Clear History"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    var nast = GeminiSettings.Load();
                    nast.RecentProjects.Clear();
                    nast.Save();
                    RefreshRecentMenu();
                }
            });
            btnOtevritSplit.DropDownItems.Add(clearItem);
        }

        private void RefreshReferenceButton()
        {
            if (btnReferencie == null) return;

            if (string.IsNullOrWhiteSpace(_projekt.ReferenceText))
            {
                btnReferencie.Text = "📎 " + CodePlanner.Core.LocalizationService.T("Připojit podklady", "Attach Reference");
                btnReferencie.BackColor = Color.Gainsboro;
                btnReferencie.ForeColor = Navy;
                _tipReference.SetToolTip(btnReferencie, CodePlanner.Core.LocalizationService.T("Připojit textový soubor (TXT, MD, JSON) jako referenční podklady pro AI analýzu.", "Attach a text file (TXT, MD, JSON) as reference documentation for AI analysis."));
            }
            else
            {
                string zkracenyNazev = _projekt.ReferenceName ?? "attachment.txt";
                if (zkracenyNazev.Length > 20)
                {
                    zkracenyNazev = zkracenyNazev.Substring(0, 17) + "...";
                }
                btnReferencie.Text = "📎 " + zkracenyNazev;
                btnReferencie.BackColor = TealSvetla;
                btnReferencie.ForeColor = Navy;
                _tipReference.SetToolTip(btnReferencie, CodePlanner.Core.LocalizationService.T(
                    $"Připojený soubor: {_projekt.ReferenceName}\nObsah: {(_projekt.ReferenceText.Length > 100 ? _projekt.ReferenceText.Substring(0, 100) + "..." : _projekt.ReferenceText)}\n\nKliknutím zobrazíte možnosti.",
                    $"Attached file: {_projekt.ReferenceName}\nContent: {(_projekt.ReferenceText.Length > 100 ? _projekt.ReferenceText.Substring(0, 100) + "..." : _projekt.ReferenceText)}\n\nClick to view options."
                ));
            }
        }

        private void BtnReferences_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_projekt.ReferenceText))
            {
                LoadReference();
            }
            else
            {
                menuReferencie.Show(btnReferencie, new Point(0, btnReferencie.Height));
            }
        }

        private void LoadReference()
        {
            using var dlg = new OpenFileDialog();
            dlg.Filter = CodePlanner.Core.LocalizationService.T(
                "Podporované textové soubory (*.txt;*.md;*.json;*.html)|*.txt;*.md;*.json;*.html|Všechny soubory (*.*)|*.*",
                "Supported text files (*.txt;*.md;*.json;*.html)|*.txt;*.md;*.json;*.html|All files (*.*)|*.*"
            );
            dlg.Title = CodePlanner.Core.LocalizationService.T("Vyberte soubor s referenční dokumentací", "Select reference documentation file");

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var fileInfo = new FileInfo(dlg.FileName);
                    if (fileInfo.Length > 2 * 1024 * 1024)
                    {
                        MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Vybraný soubor je příliš velký (maximální velikost je 2 MB).", "The selected file is too large (maximum size is 2 MB)."),
                            CodePlanner.Core.LocalizationService.T("Příliš velký soubor", "Large File"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string text = File.ReadAllText(dlg.FileName);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Vybraný soubor je prázdný.", "The selected file is empty."),
                            CodePlanner.Core.LocalizationService.T("Prázdný soubor", "Empty File"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _projekt.ReferenceText = text;
                    _projekt.ReferenceName = Path.GetFileName(dlg.FileName);
                    SpecificationService.LogChange(_projekt, CodePlanner.Core.LocalizationService.T("Příloha", "Attachment"), CodePlanner.Core.LocalizationService.T($"Připojen referenční soubor {_projekt.ReferenceName}.", $"Attached reference file {_projekt.ReferenceName}."));

                    MarkChanged();
                    RefreshAll();
                    SetStatus(CodePlanner.Core.LocalizationService.T($"Připojen soubor: {_projekt.ReferenceName}", $"Attached file: {_projekt.ReferenceName}"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error reading file:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowReferenceContent()
        {
            if (string.IsNullOrWhiteSpace(_projekt.ReferenceText)) return;

            using var dlg = new Form
            {
                Text = CodePlanner.Core.LocalizationService.T($"Obsah přílohy: {_projekt.ReferenceName}", $"Attachment content: {_projekt.ReferenceName}"),
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = true,
                ShowIcon = false
            };

            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Text = _projekt.ReferenceText,
                Font = new Font("Consolas", 10f)
            };

            dlg.Controls.Add(txt);
            dlg.ShowDialog(this);
        }

        private void RemoveReference()
        {
            if (string.IsNullOrWhiteSpace(_projekt.ReferenceText)) return;

            string? nazev = _projekt.ReferenceName;
            _projekt.ReferenceText = null;
            _projekt.ReferenceName = null;
            SpecificationService.LogChange(_projekt, CodePlanner.Core.LocalizationService.T("Příloha", "Attachment"), CodePlanner.Core.LocalizationService.T($"Odstraněn referenční soubor {nazev}.", $"Removed reference file {nazev}."));

            MarkChanged();
            RefreshAll();
            SetStatus(CodePlanner.Core.LocalizationService.T("Referenční soubor byl odstraněn.", "Reference file removed."));
        }

        private void RefreshMockupButton()
        {
            if (btnMockup == null) return;

            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64))
            {
                btnMockup.Text = "🖼 " + CodePlanner.Core.LocalizationService.T("Připojit mockup", "Attach Mockup");
                btnMockup.BackColor = Color.Gainsboro;
                btnMockup.ForeColor = Navy;
                if (_tipReference != null)
                {
                    _tipReference.SetToolTip(btnMockup, CodePlanner.Core.LocalizationService.T("Připojit obrázek, snímek obrazovky nebo diagram (PNG/JPG) jako vizuální kontext pro AI analýzu.", "Attach an image, screenshot, or diagram (PNG/JPG) as visual context for AI analysis."));
                }
            }
            else
            {
                string zkracenyNazev = _projekt.MockupName ?? "mockup.png";
                if (zkracenyNazev.Length > 20)
                {
                    zkracenyNazev = zkracenyNazev.Substring(0, 17) + "...";
                }
                btnMockup.Text = "🖼 " + zkracenyNazev;
                btnMockup.BackColor = TealSvetla;
                btnMockup.ForeColor = Navy;
                if (_tipReference != null)
                {
                    _tipReference.SetToolTip(btnMockup, CodePlanner.Core.LocalizationService.T($"Připojený vizuální mockup: {_projekt.MockupName}\n\nKliknutím zobrazíte možnosti.", $"Attached visual mockup: {_projekt.MockupName}\n\nClick to view options."));
                }
            }
        }

        private void BtnMockup_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64))
            {
                LoadMockup();
            }
            else
            {
                menuMockup.Show(btnMockup, new Point(0, btnMockup.Height));
            }
        }

        private void LoadMockup()
        {
            using var dlg = new OpenFileDialog();
            dlg.Filter = CodePlanner.Core.LocalizationService.T(
                "Obrázky (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Všechny soubory (*.*)|*.*",
                "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*"
            );
            dlg.Title = CodePlanner.Core.LocalizationService.T("Vyberte obrázek mockupu nebo náčrtu rozhraní", "Select mockup or interface sketch image");

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(dlg.FileName);
                    if (bytes.Length == 0)
                    {
                        MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Vybraný soubor je prázdný.", "The selected file is empty."),
                            CodePlanner.Core.LocalizationService.T("Prázdný soubor", "Empty File"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (bytes.Length > 4 * 1024 * 1024)
                    {
                        MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Vybraný soubor je příliš velký (maximální velikost je 4 MB).", "The selected file is too large (maximum size is 4 MB)."),
                            CodePlanner.Core.LocalizationService.T("Příliš velký soubor", "Large File"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Validate image format
                    try
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var tempImg = Image.FromStream(ms))
                        {
                            // If FromStream doesn't throw, image is valid
                        }
                    }
                    catch
                    {
                        MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Vybraný soubor není platným obrázkem.", "The selected file is not a valid image."),
                            CodePlanner.Core.LocalizationService.T("Neplatný formát", "Invalid Format"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _projekt.MockupBase64 = Convert.ToBase64String(bytes);
                    _projekt.MockupName = Path.GetFileName(dlg.FileName);
                    SpecificationService.LogChange(_projekt, CodePlanner.Core.LocalizationService.T("Náčrt", "Sketch"), CodePlanner.Core.LocalizationService.T($"Připojen vizuální mockup {_projekt.MockupName}.", $"Attached visual mockup {_projekt.MockupName}."));

                    MarkChanged();
                    RefreshAll();
                    SetStatus(CodePlanner.Core.LocalizationService.T($"Připojen vizuální mockup: {_projekt.MockupName}", $"Attached visual mockup: {_projekt.MockupName}"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error reading file:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowMockup()
        {
            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64)) return;

            try
            {
                byte[] bytes = Convert.FromBase64String(_projekt.MockupBase64);
                using var ms = new MemoryStream(bytes);
                using (var img = Image.FromStream(ms))
                using (var dlg = new Form
                {
                    Text = CodePlanner.Core.LocalizationService.T($"Prohlížeč mockupů: {_projekt.MockupName}", $"Mockup viewer: {_projekt.MockupName}"),
                    Size = new Size(800, 600),
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = false,
                    MaximizeBox = true,
                    ShowIcon = false
                })
                {
                    dlg.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
                    dlg.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

                    var pb = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        Image = img,
                        SizeMode = PictureBoxSizeMode.Zoom
                    };

                    dlg.Controls.Add(pb);
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Nepodařilo se zobrazit obrázek:\n\n", "Failed to display image:\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba zobrazení", "Display Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveMockup()
        {
            if (string.IsNullOrWhiteSpace(_projekt.MockupBase64)) return;

            string? nazev = _projekt.MockupName;
            _projekt.MockupBase64 = null;
            _projekt.MockupName = null;
            SpecificationService.LogChange(_projekt, CodePlanner.Core.LocalizationService.T("Náčrt", "Sketch"), CodePlanner.Core.LocalizationService.T($"Odstraněn vizuální mockup {nazev}.", $"Removed visual mockup {nazev}."));

            MarkChanged();
            RefreshAll();
            SetStatus(CodePlanner.Core.LocalizationService.T("Vizuální mockup byl odstraněn.", "Visual mockup removed."));
        }

        private bool ConfirmUnsavedChanges()
        {
            if (!_dirty) return true;
            var res = MessageBox.Show(this,
                CodePlanner.Core.LocalizationService.T(
                    "Máte neuložené změny. Chcete je před pokračováním uložit?",
                    "You have unsaved changes. Do you want to save them before proceeding?"
                ),
                CodePlanner.Core.LocalizationService.T("Neuložené změny", "Unsaved Changes"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Cancel) return false;
            if (res == DialogResult.Yes) return SaveProject();
            return true;
        }

        internal static string GetSafeFilename(string? nazev, string vychozi)
        {
            if (string.IsNullOrWhiteSpace(nazev)) return vychozi;
            var neplatne = Path.GetInvalidFileNameChars();
            var s = new string(nazev.Trim().Select(c => neplatne.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(s) ? vychozi : s;
        }
    }
}
