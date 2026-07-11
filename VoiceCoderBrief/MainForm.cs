using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using VoiceCoderBrief.Core;

namespace VoiceCoderBrief
{
    public class MainForm : Form
    {
        // barvy podle vizuálu návrhu (tmavě modrá + teal)
        private static readonly Color Navy = Color.FromArgb(16, 35, 63);
        private static readonly Color Teal = Color.FromArgb(23, 176, 160);
        private static readonly Color TealSvetla = Color.FromArgb(224, 244, 241);
        private static readonly Color SvetlePozadi = Color.FromArgb(246, 248, 250);
        private static readonly Color Zelena = Color.FromArgb(0, 150, 90);
        private static readonly Color Oranzova = Color.FromArgb(230, 140, 0);
        private static readonly Color SedaText = Color.FromArgb(105, 105, 105);

        private SpecProjekt _projekt = new SpecProjekt();
        private string _cestaSouboru = null;
        private bool _dirty = false;
        private bool _nacitani = false;   // potlačí eventy při programových změnách
        private string _napadSnapshot = "";

        // stav otázek pro vykreslení seznamu (plní ObnovSeznamOtazek)
        private readonly List<char> _stavyOtazek = new List<char>();     // '○' / '≈' / '✔'
        private readonly List<bool> _vysokyDopad = new List<bool>();
        private double _podilHotovo = 0;                                  // 0..1 pro progress bar

        // ovládací prvky
        private TextBox txtNazev;
        private TextBox txtNapad;
        private ListBox lstOtazky;
        private Label lblOtazka;
        private Label lblNapoveda;
        private TextBox txtOdpoved;
        private Button btnOdpovedet;
        private Button btnPredpoklad;
        private Label lblPostup;
        private Panel pnlPostup;
        private RichTextBox rtbSpec;
        private Label lblSpecHlavicka;
        private Label lblNalezy;
        private List<Nalez> _nalezy = new List<Nalez>();
        private ListView lvLog;
        private ToolStripStatusLabel lblStav;

        public MainForm()
        {
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.75f);
            BackColor = SvetlePozadi;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            var split = PostavHlavniPlochu();
            var logBox = PostavLog();
            var oddelovac = new Splitter
            {
                Dock = DockStyle.Bottom,
                Height = 6,
                BackColor = SvetlePozadi,
                MinExtra = 300,   // minimum pro hlavní plochu
                MinSize = 90      // minimum pro log
            };
            var tool = PostavToolbar();
            var status = PostavStatusBar();

            // pořadí přidání řídí docking: později přidané se dokují dřív
            Controls.Add(split);       // Fill
            Controls.Add(oddelovac);   // Bottom (nad logem, umožní měnit jeho výšku)
            Controls.Add(logBox);      // Bottom
            Controls.Add(tool);        // Top
            Controls.Add(status);      // Bottom (pod logem)

            FormClosing += (s, e) =>
            {
                if (!PotvrdNeulozene()) e.Cancel = true;
            };

            NovyProjekt(prvniSpusteni: true);
        }

        // ---------------- klávesové zkratky ----------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.N:
                    if (PotvrdNeulozene()) NovyProjekt(false);
                    return true;
                case Keys.Control | Keys.O:
                    OtevritProjekt();
                    return true;
                case Keys.Control | Keys.S:
                    UlozitProjekt();
                    return true;
                case Keys.Control | Keys.M:
                    Export(true);
                    return true;
                case Keys.Control | Keys.J:
                    Export(false);
                    return true;
                case Keys.Control | Keys.Enter:
                    if (txtOdpoved.Focused) { UlozOdpoved(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- stavba UI ----------------

        private ToolStrip PostavToolbar()
        {
            var tool = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(8, 5, 8, 5),
                BackColor = Color.White,
                RenderMode = ToolStripRenderMode.System
            };

            ToolStripButton Tlacitko(string text, string tip, EventHandler klik)
            {
                var b = new ToolStripButton(text)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    ToolTipText = tip,
                    Padding = new Padding(4, 2, 4, 2)
                };
                b.Click += klik;
                return b;
            }

            tool.Items.Add(Tlacitko("🗒 Nový", "Založit nový projekt (Ctrl+N)", (s, e) => { if (PotvrdNeulozene()) NovyProjekt(false); }));
            tool.Items.Add(Tlacitko("📂 Otevřít…", "Otevřít uložený projekt (Ctrl+O)", (s, e) => OtevritProjekt()));
            tool.Items.Add(Tlacitko("💾 Uložit", "Uložit projekt (Ctrl+S)", (s, e) => UlozitProjekt()));
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(Tlacitko("⬇ Markdown…", "Export specifikace pro člověka (Ctrl+M)", (s, e) => Export(true)));
            tool.Items.Add(Tlacitko("⬇ JSON…", "Export specifikace pro kódovacího agenta (Ctrl+J)", (s, e) => Export(false)));
            tool.Items.Add(new ToolStripSeparator());

            var tip2 = new ToolStripLabel("🎤 Diktování Windows: stiskni Win+H v libovolném poli")
            {
                ForeColor = SedaText
            };
            tool.Items.Add(tip2);

            var verze = new ToolStripLabel("v0.3")
            {
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = Color.Silver
            };
            tool.Items.Add(verze);

            return tool;
        }

        private StatusStrip PostavStatusBar()
        {
            var status = new StatusStrip();
            lblStav = new ToolStripStatusLabel("Připraveno.");
            status.Items.Add(lblStav);
            return status;
        }

        private GroupBox PostavLog()
        {
            var box = new GroupBox
            {
                Text = "Log rozhodnutí (každá změna má čas a důvod) – výšku upravíš tažením horního okraje",
                Dock = DockStyle.Bottom,
                Height = 150,
                Padding = new Padding(8),
                ForeColor = Navy
            };

            lvLog = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            lvLog.Columns.Add("Čas", 140);
            lvLog.Columns.Add("Akce", 140);
            lvLog.Columns.Add("Detail", 820);

            box.Controls.Add(lvLog);
            return box;
        }

        private SplitContainer PostavHlavniPlochu()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 450,
                SplitterWidth = 6,
                BackColor = SvetlePozadi
            };

            // ----- LEVÝ PANEL: nápad + řízené otázky -----
            var levy = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10, 8, 4, 8),
                BackColor = SvetlePozadi
            };
            levy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            levy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            levy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            levy.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            levy.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblNazev = Nadpis("Název projektu");
            txtNazev = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 8) };
            txtNazev.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Nazev = txtNazev.Text;
                OznacZmenu();
                RenderSpecifikaci();
            };

            var lblNapad = Nadpis("Nápad (piš, nebo diktuj přes Win+H)");
            txtNapad = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 8)
            };
            txtNapad.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Napad = txtNapad.Text;
                OznacZmenu();
                RenderSpecifikaci();
            };
            txtNapad.Enter += (s, e) => _napadSnapshot = _projekt.Napad ?? "";
            txtNapad.Leave += (s, e) =>
            {
                if (_nacitani) return;
                if ((_projekt.Napad ?? "") != _napadSnapshot)
                {
                    _projekt.Verze++;
                    _projekt.Upraveno = DateTime.Now;
                    _projekt.Log.Add(new Rozhodnuti { Cas = DateTime.Now, Akce = "Nápad", Detail = "Upraven text původního nápadu." });
                    ObnovLog();
                    ObnovStav();
                    RenderSpecifikaci();
                }
            };

            var otazkyBox = PostavOtazky();

            levy.Controls.Add(lblNazev, 0, 0);
            levy.Controls.Add(txtNazev, 0, 1);
            levy.Controls.Add(lblNapad, 0, 2);
            levy.Controls.Add(txtNapad, 0, 3);
            levy.Controls.Add(otazkyBox, 0, 4);

            split.Panel1.Controls.Add(levy);

            // ----- PRAVÝ PANEL: živá specifikace -----
            lblSpecHlavicka = new Label
            {
                Text = "Živá specifikace",
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                ForeColor = Color.White,
                BackColor = Navy,
                Font = new Font("Segoe UI Semibold", 10f)
            };

            rtbSpec = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                WordWrap = true
            };

            lblNalezy = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Visible = false,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            lblNalezy.Click += (s, e) => ZobrazNalezy();

            split.Panel2.Controls.Add(rtbSpec);
            split.Panel2.Controls.Add(lblNalezy);
            split.Panel2.Controls.Add(lblSpecHlavicka);

            return split;
        }

        private GroupBox PostavOtazky()
        {
            var box = new GroupBox
            {
                Text = "Řízené otázky (nejdřív ty s největším dopadem)",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ForeColor = Navy
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = Color.Transparent
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // seznam otázek
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // otázka
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // nápověda
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));   // odpověď
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // tlačítka
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));   // progress bar
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // postup text

            lstOtazky = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.Black,
                Margin = new Padding(0, 2, 0, 6),
                DrawMode = DrawMode.OwnerDrawFixed,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstOtazky.ItemHeight = (int)(lstOtazky.Font.Height * 1.8);
            lstOtazky.DrawItem += KresliOtazku;
            lstOtazky.SelectedIndexChanged += (s, e) => UkazVybranouOtazku();

            lblOtazka = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Navy,
                Margin = new Padding(0, 4, 0, 2)
            };

            lblNapoveda = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                ForeColor = SedaText,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Margin = new Padding(0, 0, 0, 4)
            };

            txtOdpoved = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 4)
            };

            var tlacitka = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            btnOdpovedet = new Button
            {
                Text = "Uložit odpověď  (Ctrl+Enter)",
                AutoSize = true,
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnOdpovedet.FlatAppearance.BorderSize = 0;
            btnOdpovedet.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnOdpovedet.Click += (s, e) => UlozOdpoved();

            btnPredpoklad = new Button
            {
                Text = "Nevím → použij předpoklad",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnPredpoklad.FlatAppearance.BorderColor = Teal;
            btnPredpoklad.FlatAppearance.MouseOverBackColor = TealSvetla;
            btnPredpoklad.Click += (s, e) => PouzitPredpoklad();

            tlacitka.Controls.Add(btnOdpovedet);
            tlacitka.Controls.Add(btnPredpoklad);

            pnlPostup = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent
            };
            pnlPostup.Paint += KresliPostup;
            pnlPostup.Resize += (s, e) => pnlPostup.Invalidate();

            lblPostup = new Label
            {
                AutoSize = true,
                ForeColor = SedaText,
                Margin = new Padding(0, 2, 0, 0)
            };

            tlp.Controls.Add(lstOtazky, 0, 0);
            tlp.Controls.Add(lblOtazka, 0, 1);
            tlp.Controls.Add(lblNapoveda, 0, 2);
            tlp.Controls.Add(txtOdpoved, 0, 3);
            tlp.Controls.Add(tlacitka, 0, 4);
            tlp.Controls.Add(pnlPostup, 0, 5);
            tlp.Controls.Add(lblPostup, 0, 6);

            box.Controls.Add(tlp);
            return box;
        }

        private static Label Nadpis(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = Navy,
            Margin = new Padding(0, 4, 0, 0)
        };

        // ---------------- vlastní vykreslování ----------------

        /// <summary>Řádek seznamu otázek: barevný stav, štítek dopadu, text s výpustkou.</summary>
        private void KresliOtazku(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstOtazky.Items.Count) return;

            bool vybrano = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var pozadi = vybrano ? TealSvetla : Color.White;
            using (var b = new SolidBrush(pozadi)) e.Graphics.FillRectangle(b, e.Bounds);
            if (vybrano)
                using (var b = new SolidBrush(Teal))
                    e.Graphics.FillRectangle(b, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);

            char stav = e.Index < _stavyOtazek.Count ? _stavyOtazek[e.Index] : '○';
            bool vysoky = e.Index < _vysokyDopad.Count && _vysokyDopad[e.Index];

            Color barvaStavu = stav == '✔' ? Zelena : (stav == '≈' ? Oranzova : Color.Silver);
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, stav.ToString(), f,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y, 22, e.Bounds.Height),
                    barvaStavu, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            // štítek dopadu
            var chip = new Rectangle(e.Bounds.X + 28, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 20, 16);
            using (var b = new SolidBrush(vysoky ? Navy : Color.Gainsboro))
                e.Graphics.FillRectangle(b, chip);
            using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, vysoky ? "V" : "S", f, chip,
                    vysoky ? Color.White : Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var textRect = new Rectangle(e.Bounds.X + 54, e.Bounds.Y, e.Bounds.Width - 58, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, lstOtazky.Items[e.Index].ToString(), lstOtazky.Font,
                textRect, Color.Black,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        /// <summary>Progress bar postupu: teal výplň na světlé dráze, zaoblené rohy.</summary>
        private void KresliPostup(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = pnlPostup.ClientRectangle;
            r.Inflate(-1, -2);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (var draha = Zaobli(r, r.Height / 2))
            using (var b = new SolidBrush(Color.FromArgb(225, 230, 234)))
                g.FillPath(b, draha);

            int w = (int)(r.Width * Math.Max(0, Math.Min(1, _podilHotovo)));
            if (w > r.Height)   // aby zaoblení nedegenerovalo
            {
                var fill = new Rectangle(r.X, r.Y, w, r.Height);
                using (var cesta = Zaobli(fill, r.Height / 2))
                using (var b = new SolidBrush(Teal))
                    g.FillPath(b, cesta);
            }
        }

        private static GraphicsPath Zaobli(Rectangle r, int polomer)
        {
            var p = new GraphicsPath();
            int d = polomer * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---------------- formátovaný náhled specifikace ----------------

        /// <summary>Převede markdown z RenderMarkdown na jednoduché RTF (nadpisy, odrážky, citace, tučné, zvýrazněné předpoklady).</summary>
        private static string MarkdownNaRtf(string md)
        {
            var sb = new StringBuilder();
            // fonty a barvy: 1=Navy 2=Teal 3=šedá 4=oranžová(předpoklad) 5=text
            sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}");
            sb.Append(@"{\colortbl ;\red16\green35\blue63;\red23\green176\blue160;\red105\green105\blue105;\red217\green119\blue6;\red33\green37\blue41;}");
            sb.Append(@"\f0\fs20 ");

            foreach (var syrovy in md.Replace("\r\n", "\n").Split('\n'))
            {
                string radek = syrovy.TrimEnd();

                if (radek.StartsWith("# "))
                {
                    sb.Append(@"{\pard\sb40\sa80\b\cf1\fs32 ");
                    PridejInline(sb, radek.Substring(2));
                    sb.Append(@"\par}");
                }
                else if (radek.StartsWith("## "))
                {
                    sb.Append(@"{\pard\sb160\sa50\b\cf1\fs25 ");
                    PridejInline(sb, radek.Substring(3));
                    sb.Append(@"\par}");
                }
                else if (radek.StartsWith("> "))
                {
                    sb.Append(@"{\pard\li280\cf3\i ");
                    PridejRtfText(sb, radek.Substring(2));
                    sb.Append(@"\i0\par}");
                }
                else if (radek.StartsWith("- "))
                {
                    sb.Append(@"{\pard\li300\fi-160\sa20\cf2\b ");
                    PridejRtfText(sb, "•  ");
                    sb.Append(@"\b0\cf5 ");
                    PridejInline(sb, radek.Substring(2));
                    sb.Append(@"\par}");
                }
                else if (radek.StartsWith("  ") && radek.Trim().Length > 0)
                {
                    sb.Append(@"{\pard\li300\sa20\cf5 ");
                    PridejInline(sb, radek.Trim());
                    sb.Append(@"\par}");
                }
                else if (radek.Length == 0)
                {
                    sb.Append(@"{\pard\fs10\par}");
                }
                else
                {
                    sb.Append(@"{\pard\cf5 ");
                    PridejInline(sb, radek);
                    sb.Append(@"\par}");
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Zpracuje **tučné**, *kurzívu* a zvýrazní [PŘEDPOKLAD] oranžově.</summary>
        private static void PridejInline(StringBuilder sb, string text)
        {
            foreach (var kus in Regex.Split(text, @"(\*\*.*?\*\*|\*.*?\*)"))
            {
                if (kus.Length == 0) continue;

                if (kus.Length >= 4 && kus.StartsWith("**") && kus.EndsWith("**"))
                {
                    string vnitrek = kus.Substring(2, kus.Length - 4);
                    if (vnitrek.Contains("[PŘEDPOKLAD]"))
                    {
                        sb.Append(@"{\b\cf4 ");
                        PridejRtfText(sb, vnitrek);
                        sb.Append('}');
                    }
                    else
                    {
                        sb.Append(@"{\b ");
                        PridejRtfText(sb, vnitrek);
                        sb.Append('}');
                    }
                }
                else if (kus.Length >= 2 && kus.StartsWith("*") && kus.EndsWith("*"))
                {
                    sb.Append(@"{\i\cf3 ");
                    PridejRtfText(sb, kus.Substring(1, kus.Length - 2));
                    sb.Append('}');
                }
                else
                {
                    PridejRtfText(sb, kus);
                }
            }
        }

        /// <summary>RTF escapování včetně českých znaků (\uNNNN?).</summary>
        private static void PridejRtfText(StringBuilder sb, string text)
        {
            foreach (char c in text)
            {
                if (c == '\\' || c == '{' || c == '}') { sb.Append('\\').Append(c); }
                else if (c > 127)
                {
                    int v = c;
                    if (v > 32767) v -= 65536;   // RTF \u je signed 16-bit (emoji, surrogáty)
                    sb.Append(@"\u").Append(v).Append('?');
                }
                else sb.Append(c);
            }
        }

        // ---------------- logika UI ----------------

        private void NovyProjekt(bool prvniSpusteni)
        {
            _projekt = new SpecProjekt();
            _projekt.Log.Add(new Rozhodnuti { Cas = DateTime.Now, Akce = "Projekt", Detail = "Založen nový projekt." });
            _cestaSouboru = null;
            _dirty = false;

            _nacitani = true;
            txtNazev.Text = "";
            txtNapad.Text = "";
            txtOdpoved.Text = "";
            _nacitani = false;

            ObnovVse();
            VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));
            if (!prvniSpusteni) Stav("Nový projekt založen.");
        }

        private void OtevritProjekt()
        {
            if (!PotvrdNeulozene()) return;

            using var dlg = new OpenFileDialog
            {
                Title = "Otevřít projekt",
                Filter = "Projekt VoiceCoder Brief (*.vcbrief)|*.vcbrief|Všechny soubory (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _projekt = SpecSluzba.NactiProjekt(dlg.FileName);
                _cestaSouboru = dlg.FileName;
                _dirty = false;

                _nacitani = true;
                txtNazev.Text = _projekt.Nazev ?? "";
                txtNapad.Text = _projekt.Napad ?? "";
                txtOdpoved.Text = "";
                _nacitani = false;

                ObnovVse();
                VyberOtazku(SpecSluzba.DalsiNezodpovezena(_projekt));
                Stav("Otevřeno: " + Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Soubor se nepodařilo načíst.\n\n" + ex.Message,
                    "Chyba při otevírání", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool UlozitProjekt()
        {
            if (_cestaSouboru == null)
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Uložit projekt",
                    Filter = "Projekt VoiceCoder Brief (*.vcbrief)|*.vcbrief",
                    FileName = BezpecnyNazevSouboru(_projekt.Nazev, "projekt") + ".vcbrief"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                _cestaSouboru = dlg.FileName;
            }

            try
            {
                SpecSluzba.UlozProjekt(_projekt, _cestaSouboru);
                _dirty = false;
                ObnovTitulek();
                Stav("Uloženo: " + Path.GetFileName(_cestaSouboru));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Projekt se nepodařilo uložit.\n\n" + ex.Message,
                    "Chyba při ukládání", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void Export(bool markdown)
        {
            using var dlg = new SaveFileDialog
            {
                Title = markdown ? "Export specifikace (Markdown)" : "Export specifikace (JSON)",
                Filter = markdown ? "Markdown (*.md)|*.md" : "JSON (*.json)|*.json",
                FileName = BezpecnyNazevSouboru(_projekt.Nazev, "specifikace") + (markdown ? ".md" : ".json")
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var obsah = markdown ? SpecSluzba.RenderMarkdown(_projekt) : SpecSluzba.RenderJson(_projekt);
                File.WriteAllText(dlg.FileName, obsah, new System.Text.UTF8Encoding(true));
                Stav("Export hotový: " + Path.GetFileName(dlg.FileName));
                MessageBox.Show(this, "Specifikace byla exportována:\n\n" + dlg.FileName,
                    "Export hotový", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export se nepodařil.\n\n" + ex.Message,
                    "Chyba exportu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UlozOdpoved()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            if (string.IsNullOrWhiteSpace(txtOdpoved.Text))
            {
                MessageBox.Show(this, "Napiš odpověď, nebo zvol „Nevím → použij předpoklad“.",
                    "Chybí odpověď", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SpecSluzba.Odpovez(_projekt, ot.Id, txtOdpoved.Text);
            OznacZmenu();
            ObnovVse();
            PrejdiNaDalsi(ot);
        }

        private void PouzitPredpoklad()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            SpecSluzba.PouzijPredpoklad(_projekt, ot.Id);
            OznacZmenu();
            ObnovVse();
            PrejdiNaDalsi(ot);
        }

        private void PrejdiNaDalsi(Otazka posledni)
        {
            var dalsi = SpecSluzba.DalsiNezodpovezena(_projekt);
            if (dalsi != null)
            {
                VyberOtazku(dalsi);
            }
            else
            {
                VyberOtazku(posledni);
                Stav("Všechny otázky vyřešené – specifikaci můžeš exportovat.");
            }
        }

        // ---------------- pomocné ----------------

        private Otazka VybranaOtazka()
        {
            int i = lstOtazky.SelectedIndex;
            if (i < 0 || i >= Otazky.Vse.Count) return null;
            return Otazky.Vse[i];
        }

        private void VyberOtazku(Otazka ot)
        {
            if (ot == null) return;
            for (int i = 0; i < Otazky.Vse.Count; i++)
            {
                if (Otazky.Vse[i].Id == ot.Id)
                {
                    lstOtazky.SelectedIndex = i;
                    return;
                }
            }
        }

        private void UkazVybranouOtazku()
        {
            var ot = VybranaOtazka();
            if (ot == null) return;

            lblOtazka.Text = ot.Text;
            lblNapoveda.Text = ot.Napoveda + "  (Když nevíš, předpoklad bude: „" + ot.VychoziPredpoklad + "“)";

            var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
            _nacitani = true;
            txtOdpoved.Text = odp != null && !odp.JePredpoklad ? odp.Text : "";
            _nacitani = false;
        }

        private void ObnovVse()
        {
            ObnovSeznamOtazek();
            RenderSpecifikaci();
            ObnovLog();
            ObnovStav();
            ObnovTitulek();
        }

        private void ObnovSeznamOtazek()
        {
            int vybrano = lstOtazky.SelectedIndex;
            _nacitani = true;
            lstOtazky.BeginUpdate();
            lstOtazky.Items.Clear();
            _stavyOtazek.Clear();
            _vysokyDopad.Clear();

            foreach (var ot in Otazky.Vse)
            {
                var odp = SpecSluzba.OdpovedNa(_projekt, ot.Id);
                _stavyOtazek.Add(odp == null ? '○' : (odp.JePredpoklad ? '≈' : '✔'));
                _vysokyDopad.Add(ot.Dopad == Dopad.Vysoky);
                lstOtazky.Items.Add(ot.Text);
            }

            if (vybrano >= 0 && vybrano < lstOtazky.Items.Count)
                lstOtazky.SelectedIndex = vybrano;
            lstOtazky.EndUpdate();
            _nacitani = false;
        }

        private void RenderSpecifikaci()
        {
            string md = SpecSluzba.RenderMarkdown(_projekt);
            try
            {
                rtbSpec.Rtf = MarkdownNaRtf(md);
            }
            catch
            {
                rtbSpec.Text = md;   // nouzový režim: syrový markdown
            }
            lblSpecHlavicka.Text = "Živá specifikace · verze " + _projekt.Verze +
                " · zodpovězeno " + (SpecSluzba.PocetZodpovezenych(_projekt) + SpecSluzba.PocetPredpokladu(_projekt)) +
                "/" + Otazky.Vse.Count;
            ObnovNalezy();
        }

        private void ObnovNalezy()
        {
            _nalezy = KonzistencniKontrola.Zkontroluj(_projekt);
            if (_nalezy.Count == 0)
            {
                lblNalezy.Visible = false;
                return;
            }

            int rozpory = _nalezy.Count(n => n.Zavaznost == Zavaznost.Rozpor);
            int varovani = _nalezy.Count - rozpory;
            var casti = new List<string>();
            if (rozpory > 0) casti.Add(Mnozne(rozpory, "rozpor", "rozpory", "rozporů"));
            if (varovani > 0) casti.Add(Mnozne(varovani, "varování", "varování", "varování"));

            lblNalezy.Text = (rozpory > 0 ? "❗ " : "⚠️ ") + "Kontrola konzistence: " +
                string.Join(" a ", casti) + " – klikni pro detail";
            lblNalezy.BackColor = rozpory > 0 ? Color.FromArgb(253, 232, 232) : Color.FromArgb(255, 244, 219);
            lblNalezy.ForeColor = rozpory > 0 ? Color.FromArgb(155, 28, 28) : Color.FromArgb(146, 90, 4);
            lblNalezy.Visible = true;
        }

        private void ZobrazNalezy()
        {
            if (_nalezy.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var n in _nalezy)
            {
                sb.Append(n.Zavaznost == Zavaznost.Rozpor ? "❗ ROZPOR: " : "⚠ Varování: ");
                sb.AppendLine(n.Titulek);
                sb.AppendLine("     " + n.Detail);
                sb.AppendLine();
            }
            sb.AppendLine("(Kontrola je orientační – porovnává klíčová slova, nechápe význam.)");
            MessageBox.Show(this, sb.ToString(), "Kontrola konzistence",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Mnozne(int n, string jedna, string dvaAzCtyri, string vice)
            => n == 1 ? "1 " + jedna : (n >= 2 && n <= 4 ? n + " " + dvaAzCtyri : n + " " + vice);

        private void ObnovLog()
        {
            lvLog.BeginUpdate();
            lvLog.Items.Clear();
            // nejnovější nahoře
            foreach (var r in Enumerable.Reverse(_projekt.Log))
            {
                var it = new ListViewItem(r.Cas.ToString("d. M. yyyy H:mm:ss"));
                it.SubItems.Add(r.Akce);
                it.SubItems.Add(r.Detail);
                lvLog.Items.Add(it);
            }
            lvLog.EndUpdate();
        }

        private void ObnovStav()
        {
            int z = SpecSluzba.PocetZodpovezenych(_projekt);
            int p = SpecSluzba.PocetPredpokladu(_projekt);
            int otevrene = SpecSluzba.OtevreneOtazky(_projekt).Count;
            _podilHotovo = (z + p) / (double)Otazky.Vse.Count;
            pnlPostup.Invalidate();
            lblPostup.Text = "Zodpovězeno " + z + " · předpoklady " + p + " · otevřené " + otevrene + " (z " + Otazky.Vse.Count + ")";
            Stav("Verze specifikace " + _projekt.Verze + " · zodpovězeno " + z + "/" + Otazky.Vse.Count +
                 " · předpoklady " + p + " · otevřené otázky " + otevrene);
        }

        private void OznacZmenu()
        {
            _dirty = true;
            ObnovTitulek();
        }

        private void ObnovTitulek()
        {
            string nazev = string.IsNullOrWhiteSpace(_projekt.Nazev) ? "nový projekt" : _projekt.Nazev.Trim();
            Text = "VoiceCoder Brief – " + nazev + (_dirty ? " *" : "") + " – v0.3";
        }

        private void Stav(string text) => lblStav.Text = text;

        private bool PotvrdNeulozene()
        {
            if (!_dirty) return true;
            var res = MessageBox.Show(this,
                "Máš neuložené změny. Chceš je před pokračováním uložit?",
                "Neuložené změny", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Cancel) return false;
            if (res == DialogResult.Yes) return UlozitProjekt();
            return true;
        }

        private static string BezpecnyNazevSouboru(string nazev, string vychozi)
        {
            if (string.IsNullOrWhiteSpace(nazev)) return vychozi;
            var neplatne = Path.GetInvalidFileNameChars();
            var s = new string(nazev.Trim().Select(c => neplatne.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(s) ? vychozi : s;
        }
    }
}
