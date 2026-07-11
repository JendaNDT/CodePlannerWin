using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodePlanner.Core;

namespace CodePlanner
{
    public partial class MainForm : Form
    {
        // barvy podle vizuálu návrhu (tmavě modrá + teal)
        private static readonly Color Navy = Color.FromArgb(16, 35, 63);
        private static readonly Color Teal = Color.FromArgb(23, 176, 160);
        private static readonly Color TealSvetla = Color.FromArgb(224, 244, 241);
        private static readonly Color SvetlePozadi = Color.FromArgb(246, 248, 250);
        private static readonly Color Zelena = Color.FromArgb(0, 150, 90);
        private static readonly Color Oranzova = Color.FromArgb(230, 140, 0);
        private static readonly Color SedaText = Color.FromArgb(105, 105, 105);

        private ProjectSpecification _projekt = new ProjectSpecification();
        private string? _cestaSouboru = null;
        private bool _dirty = false;
        private bool _nacitani = false;   // potlačí eventy při programových změnách
        private string _napadSnapshot = "";
        private string _nazevSnapshot = "";
        private System.Windows.Forms.Timer _debounceTimer = default!;     // Debounce pro název a nápad
        private ToolStrip toolBar = default!;        // Uchovává instanci toolbar pro snadné vypnutí
        private bool _chatBusy = false;   // Flag proti vícenásobnému odeslání chatu
        private CancellationTokenSource? _ctsAi = null;
        private CancellationTokenSource? _ctsChat = null;                 // storno běžící chatové zprávy
        private CancellationTokenSource? _ctsTranscribe = null;           // storno běžícího přepisu diktování
        private bool _transcribing = false;                              // flag zda zrovna probíhá přepis diktování
        private System.Windows.Forms.Timer _autosaveTimer = default!;               // automatická záloha rozdělané práce (2 min)
        private System.Windows.Forms.Timer _prubehTimer = default!;                 // ukazuje uplynulý čas běžící AI operace (1 s)
        private System.Windows.Forms.Timer _diktovaniLimitTimer = default!;         // auto-stop diktování po 3 minutách
        private DateTime _casStartuAiOperace;                            // start běžící AI operace (pro průběžný čas)
        private bool _snapshotAnalyzyExistuje = false;                   // v této session vznikla záloha před AI analýzou
        private Button? _tlacitkoDiktovaniAktivni = null;                 // které mikrofonní tlačítko právě nahrává
        private ToolStripButton btnVratitAnalyzu = default!;                        // „↩ Vrátit analýzu“ v toolbaru
        private LinkLabel lblApiBanner = default!;                                  // banner „chybí API klíč“ n，oře

        private static string PlaceholderChatu => CodePlanner.Core.LocalizationService.T("např. 'Co chybí v požadavcích?' nebo 'Co bude nejobtížnější část?'", "e.g. 'What is missing in the requirements?' or 'What will be the hardest part?'");
        private static string TipDiktovani => CodePlanner.Core.LocalizationService.T("Držte a mluvte, nebo klikněte pro přepnutí. Přepis je poháněn AI (Gemini). Tip: Win+H je vestavěné bezplatné diktování ve Windows.", "Hold and talk, or click to toggle. Transcription is powered by AI (Gemini). Tip: Win+H is free built-in Windows dictation.");
        private static string RadaMikrofon => CodePlanner.Core.LocalizationService.T("Zkontrolujte mikrofon v Nastavení Windows → Soukromí → Mikrofon.", "Check your microphone in Windows Settings → Privacy → Microphone.");

        private static string SlozkaDatAplikace
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodePlanner");
        private static string CestaAutosave => Path.Combine(SlozkaDatAplikace, "autosave.vcbrief");
        private static string CestaZalohyPredAnalyzou => Path.Combine(SlozkaDatAplikace, "pred_analyzou.vcbrief");

        // stav otázek pro vykreslení seznamu (plní ObnovSeznamOtazek)
        private readonly List<char> _stavyOtazek = new List<char>();     // '○' / '≈' / '✔'
        private readonly List<bool> _vysokyDopad = new List<bool>();
        private double _podilHotovo = 0;                                  // 0..1 pro progress bar

        // ovládací prvky
        private TextBox txtNazev = default!;
        private ComboBox cmbTyp = default!;
        private ToolStripSplitButton btnOtevritSplit = default!;
        private TextBox txtNapad = default!;
        private Button btnDiktovatNapad = default!;
        private Button btnReferencie = default!;
        private ContextMenuStrip menuReferencie = default!;
        private ListBox lstOtazky = default!;
        private Label lblOtazka = default!;
        private Label lblNapoveda = default!;
        private TextBox txtOdpoved = default!;
        private Button btnOdpovedet = default!;
        private Button btnPredpoklad = default!;
        private Button btnDiktovatOdpoved = default!;
        private Button btnAiAnalyza = default!;
        private Label lblPostup = default!;
        private Panel pnlPostup = default!;
        private DateTime _casSpusteniDiktovani;
        private bool _diktovaniClickToggle = false;
        private bool _ignorujDalsiMouseUp = false;
        private bool _isBusy = false;
        private Font? _chatFontItalic;
        private Font? _chatFontBold;
        private Font? _chatFontRegular;
        private RichTextBox rtbSpec = default!;
        private Label lblSpecHlavicka = default!;
        private LinkLabel lblNalezy = default!;
        private List<ConsistencyFinding> _nalezy = new List<ConsistencyFinding>();
        private ListView lvLog = default!;
        private ToolStripStatusLabel lblStav = default!;
        private ToolTip _tipReference = default!;
        private FlowLayoutPanel pnlQuickOptions = default!;
        private RichTextBox rtbChatLog = default!;
        private TextBox txtChatInput = default!;
        private Button btnSendChat = default!;
        private Button btnClearChat = default!;
        private Button btnMockup = default!;
        private ContextMenuStrip menuMockup = default!;

        public MainForm()
        {
            TemplateService.LoadCustomTemplates();
            if (!string.IsNullOrEmpty(TemplateService.LoadError))
            {
                MessageBox.Show("Nepodařilo se načíst uživatelské šablony (sablony.json):\n\n" + TemplateService.LoadError + "\n\nAplikace použije výchozí šablony.", "Chyba načítání šablon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _tipReference = new ToolTip();
            _debounceTimer = new System.Windows.Forms.Timer();
            _debounceTimer.Interval = 500;
            _debounceTimer.Tick += DebounceTimer_Tick;

            _autosaveTimer = new System.Windows.Forms.Timer { Interval = 120_000 };
            _autosaveTimer.Tick += AutosaveTimer_Tick;
            _autosaveTimer.Start();

            _prubehTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _prubehTimer.Tick += PrubehTimer_Tick;

            _diktovaniLimitTimer = new System.Windows.Forms.Timer { Interval = 180_000 };
            _diktovaniLimitTimer.Tick += DiktovaniLimitTimer_Tick;

            _chatFontItalic = new Font("Segoe UI", 10f, FontStyle.Italic);
            _chatFontBold = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _chatFontRegular = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

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

            toolBar = PostavToolbar();
            var status = PostavStatusBar();

            lblApiBanner = new LinkLabel
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "🔑 AI features require a Gemini API key – click to configure.",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.FromArgb(255, 248, 214),
                LinkColor = Color.FromArgb(133, 100, 4),
                ActiveLinkColor = Color.FromArgb(133, 100, 4),
                VisitedLinkColor = Color.FromArgb(133, 100, 4),
                Cursor = Cursors.Hand,
                Visible = false,
                AccessibleRole = AccessibleRole.Link,
                AccessibleName = "AI features require a Gemini API key – click to configure.",
                TabStop = true
            };
            lblApiBanner.Links.Add(0, lblApiBanner.Text.Length);
            lblApiBanner.Click += (s, e) => OpenSettings();

            // pořadí přidání řídí docking: později přidané se dokují dřív
            Controls.Add(split);          // Fill
            Controls.Add(oddelovac);      // Bottom (nad logem, umožní měnit jeho výšku)
            Controls.Add(logBox);         // Bottom
            Controls.Add(lblApiBanner);   // Top (pod toolbarem)
            Controls.Add(toolBar);        // Top
            Controls.Add(status);         // Bottom (pod logem)

            FormClosing += (s, e) =>
            {
                if (!ConfirmUnsavedChanges())
                {
                    e.Cancel = true;
                }
                else if (!e.Cancel)
                {
                    if (!_dirty) DeleteAutosave();   // čisté zavření – automatická záloha už není potřeba
                    _chatFontItalic?.Dispose();
                    _chatFontBold?.Dispose();
                    _chatFontRegular?.Dispose();

                    try { _ctsAi?.Cancel(); _ctsAi?.Dispose(); } catch { }
                    try { _ctsChat?.Cancel(); _ctsChat?.Dispose(); } catch { }
                    try { _ctsTranscribe?.Cancel(); _ctsTranscribe?.Dispose(); } catch { }
                }
            };

            NewProject(prvniSpusteni: true);
            RefreshRecentMenu();
            RefreshApiBanner();
            Shown += (s, e) => OfferAutosaveRecovery();
        }

        // ---------------- klávesové zkratky ----------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isBusy || _chatBusy || _transcribing)
            {
                if (keyData == Keys.Escape)
                {
                    // Esc zruší běžící AI operaci (analýzu, chat i přepis diktování)
                    _ctsAi?.Cancel();
                    _ctsChat?.Cancel();
                    _ctsTranscribe?.Cancel();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    // uložit lze kdykoli, i během práce AI
                    SaveProject();
                    return true;
                }
                return true;   // ostatní zkratky během AI operace blokujeme
            }
            switch (keyData)
            {
                case Keys.Control | Keys.N:
                    if (ConfirmUnsavedChanges()) NewProject(false);
                    return true;
                case Keys.Control | Keys.O:
                    OpenProject();
                    return true;
                case Keys.Control | Keys.S:
                    SaveProject();
                    return true;
                case Keys.Control | Keys.M:
                    Export(true);
                    return true;
                case Keys.Control | Keys.J:
                    Export(false);
                    return true;
                case Keys.Control | Keys.P:
                    ExportPdf();
                    return true;
                case Keys.Control | Keys.Enter:
                    // funguje i mimo pole odpovědi, pokud je vybraná otázka (chat má vlastní Enter)
                    if (!txtChatInput.Focused && SelectedQuestion() != null) { SaveAnswer(); return true; }
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

            tool.Items.Add(Tlacitko(
                CodePlanner.Core.LocalizationService.T("🗒 Nový", "🗒 New"),
                CodePlanner.Core.LocalizationService.T("Vytvořit nový projekt (Ctrl+N)", "Create a new project (Ctrl+N)"),
                (s, e) => { if (ConfirmUnsavedChanges()) NewProject(false); }));
            
            btnOtevritSplit = new ToolStripSplitButton(CodePlanner.Core.LocalizationService.T("📂 Otevřít...", "📂 Open..."))
            {
                ToolTipText = CodePlanner.Core.LocalizationService.T("Otevřít existující projekt (Ctrl+O)", "Open an existing project (Ctrl+O)"),
                Padding = new Padding(4, 2, 4, 2),
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            btnOtevritSplit.ButtonClick += (s, e) => OpenProject();
            tool.Items.Add(btnOtevritSplit);

            tool.Items.Add(Tlacitko(
                CodePlanner.Core.LocalizationService.T("💾 Uložit", "💾 Save"),
                CodePlanner.Core.LocalizationService.T("Uložit projekt (Ctrl+S)", "Save the project (Ctrl+S)"),
                (s, e) => SaveProject()));
            tool.Items.Add(new ToolStripSeparator());

            var btnExporty = new ToolStripDropDownButton(CodePlanner.Core.LocalizationService.T("⬇ Exportovat", "⬇ Export"))
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Padding = new Padding(4, 2, 4, 2),
                ToolTipText = CodePlanner.Core.LocalizationService.T("Možnosti exportu specifikace", "Specification export options")
            };

            ToolStripMenuItem PolozkaMenu(string text, string tip, EventHandler klik)
            {
                var item = new ToolStripMenuItem(text)
                {
                    ToolTipText = tip
                };
                item.Click += klik;
                return item;
            }

            btnExporty.DropDownItems.Add(PolozkaMenu(
                CodePlanner.Core.LocalizationService.T("Markdown... (Ctrl+M)", "Markdown... (Ctrl+M)"),
                CodePlanner.Core.LocalizationService.T("Pro lidi – čitelný dokument", "For humans – readable document"),
                (s, e) => Export(true)));
            btnExporty.DropDownItems.Add(PolozkaMenu(
                CodePlanner.Core.LocalizationService.T("JSON... (Ctrl+J)", "JSON... (Ctrl+J)"),
                CodePlanner.Core.LocalizationService.T("Pro AI agenty – strojově čitelná data", "For AI agent – machine readable data"),
                (s, e) => Export(false)));
            btnExporty.DropDownItems.Add(PolozkaMenu(
                CodePlanner.Core.LocalizationService.T("PDF... (Ctrl+P)", "PDF... (Ctrl+P)"),
                CodePlanner.Core.LocalizationService.T("Exportovat specifikaci do PDF pro klienty", "Export specification to PDF for clients"),
                (s, e) => ExportPdf()));
            btnExporty.DropDownItems.Add(PolozkaMenu(
                CodePlanner.Core.LocalizationService.T("HTML Web...", "HTML Web..."),
                CodePlanner.Core.LocalizationService.T("Exportovat specifikaci do interaktivního HTML webu", "Export specification to interactive HTML website"),
                (s, e) => ExportHtml()));

            tool.Items.Add(btnExporty);
            tool.Items.Add(new ToolStripSeparator());

            tool.Items.Add(Tlacitko(
                CodePlanner.Core.LocalizationService.T("💡 User Stories...", "💡 User Stories..."),
                CodePlanner.Core.LocalizationService.T("Správa a generování uživatelských příběhů", "Manage and generate developer user stories"),
                (s, e) => ShowUserStories()));
            tool.Items.Add(Tlacitko(
                CodePlanner.Core.LocalizationService.T("📊 Odhady a metriky...", "📊 Estimate & Metrics..."),
                CodePlanner.Core.LocalizationService.T("Metriky projektu a AI časový odhad", "Project metrics and AI time estimate"),
                (s, e) => ShowMetrics()));
            tool.Items.Add(Tlacitko(
                CodePlanner.Core.LocalizationService.T("✔ Kontrola konzistence...", "✔ Consistency Check..."),
                CodePlanner.Core.LocalizationService.T("Zkontrolovat konzistenci specifikace – rozpory a varování", "Check specification consistency – conflicts and warnings"),
                (s, e) => ShowFindings(true)));

            btnVratitAnalyzu = new ToolStripButton(CodePlanner.Core.LocalizationService.T("↩ Vrátit analýzu", "↩ Revert Analysis"))
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = CodePlanner.Core.LocalizationService.T("Obnovit projekt ze zálohy vytvořené před poslední AI analýzou.", "Restore the project from the backup created before the last AI analysis."),
                Padding = new Padding(4, 2, 4, 2),
                Visible = false
            };
            btnVratitAnalyzu.Click += (s, e) => RevertAnalysis();
            tool.Items.Add(btnVratitAnalyzu);

            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(Tlacitko(
                CodePlanner.Core.LocalizationService.T("⚙ AI Nastavení...", "⚙ AI Settings..."),
                CodePlanner.Core.LocalizationService.T("Konfigurace API klíče Gemini a výběr modelu", "Configure Gemini API key and model selection"),
                (s, e) => OpenSettings()));
            tool.Items.Add(Tlacitko("❓",
                CodePlanner.Core.LocalizationService.T("Nápověda – jak postupovat a klávesové zkratky", "Help – how to proceed and keyboard shortcuts"),
                (s, e) => ShowHelp()));
            tool.Items.Add(new ToolStripSeparator());

            var tip2 = new ToolStripLabel(CodePlanner.Core.LocalizationService.T(
                "🎤 Diktování: držte tlačítko a mluvte · Win+H = integrované bezplatné diktování ve Windows",
                "🎤 Dictation: hold button and talk · Win+H = built-in free Windows dictation"
            ))
            {
                ForeColor = SedaText
            };
            tool.Items.Add(tip2);

            var verze = new ToolStripLabel("v2.2.0")
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
            lblStav = new ToolStripStatusLabel("Ready.");
            status.Items.Add(lblStav);
            return status;
        }

        private GroupBox PostavLog()
        {
            var box = new GroupBox
            {
                Text = "Decision log (each change records time and reason) – drag top border to resize",
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
            lvLog.Columns.Add("Time", 140);
            lvLog.Columns.Add("Action", 140);
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

            var pnlNazevATypHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Height = 20
            };
            pnlNazevATypHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            pnlNazevATypHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            var lblNazev = Nadpis("Project Title");
            var lblTyp = Nadpis("Type / Template");
            pnlNazevATypHeader.Controls.Add(lblNazev, 0, 0);
            pnlNazevATypHeader.Controls.Add(lblTyp, 1, 0);

            var pnlNazevATyp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8),
                Height = 32
            };
            pnlNazevATyp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            pnlNazevATyp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            txtNazev = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 0), BorderStyle = BorderStyle.FixedSingle };
            txtNazev.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Name = txtNazev.Text;
                MarkChanged();
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            txtNazev.Enter += (s, e) => _nazevSnapshot = _projekt.Name ?? "";
            txtNazev.Leave += (s, e) =>
            {
                if (_nacitani) return;
                _debounceTimer.Stop();
                if ((_projekt.Name ?? "") != _nazevSnapshot)
                {
                    SpecificationService.LogChange(_projekt, "Name", $"Project name changed to '{_projekt.Name}'.");
                    RefreshLog();
                    RefreshStatus();
                }
                RenderSpecification();
            };

            cmbTyp = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(4, 2, 0, 0)
            };
            RefreshProjectTypeCombo();
            SetTypeCombo("General");
            cmbTyp.SelectedIndexChanged += CmbType_SelectedIndexChanged;

            pnlNazevATyp.Controls.Add(txtNazev, 0, 0);
            pnlNazevATyp.Controls.Add(cmbTyp, 1, 0);

            var pnlNapadHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Height = 28,
                Margin = new Padding(0)
            };
            var lblNapad = Nadpis("1 · Idea (type or dictate)");
            btnAiAnalyza = new Button
            {
                Text = "🤖 Analyze with Gemini",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnAiAnalyza.FlatAppearance.BorderSize = 0;
            btnAiAnalyza.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnAiAnalyza.Click += BtnAiAnalysis_Click;

            btnDiktovatNapad = new Button
            {
                Text = "🎤 Dictate",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Color.Gainsboro,
                ForeColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnDiktovatNapad.FlatAppearance.BorderSize = 0;
            btnDiktovatNapad.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnDiktovatNapad.MouseDown += BtnDictate_MouseDown;
            btnDiktovatNapad.MouseUp += BtnDictate_MouseUp;
            btnDiktovatNapad.Click += BtnDictate_Click;
            _tipReference.SetToolTip(btnDiktovatNapad, TipDiktovani);

            btnReferencie = new Button
            {
                Text = "📎 References",
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Color.Gainsboro,
                ForeColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnReferencie.FlatAppearance.BorderSize = 0;
            btnReferencie.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnReferencie.Click += BtnReferences_Click;

            menuReferencie = new ContextMenuStrip();
            menuReferencie.Items.Add(CodePlanner.Core.LocalizationService.T("Zobrazit podklady...", "Show references..."), null, (s, e) => ShowReferenceContent());
            menuReferencie.Items.Add(CodePlanner.Core.LocalizationService.T("Změnit podklady...", "Change references..."), null, (s, e) => LoadReference());
            menuReferencie.Items.Add(CodePlanner.Core.LocalizationService.T("Odstranit podklady", "Remove references"), null, (s, e) => RemoveReference());

            btnMockup = new Button
            {
                Text = CodePlanner.Core.LocalizationService.T("🖼 Vizuální mockup (náčrt)", "🖼 Visual mockup (sketch)"),
                Height = 22,
                Margin = new Padding(12, 2, 0, 0),
                BackColor = Color.Gainsboro,
                ForeColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnMockup.FlatAppearance.BorderSize = 0;
            btnMockup.FlatAppearance.MouseOverBackColor = Color.LightGray;
            btnMockup.Click += BtnMockup_Click;

            menuMockup = new ContextMenuStrip();
            menuMockup.Items.Add(CodePlanner.Core.LocalizationService.T("Zobrazit mockup...", "Show mockup..."), null, (s, e) => ShowMockup());
            menuMockup.Items.Add(CodePlanner.Core.LocalizationService.T("Změnit mockup...", "Change mockup..."), null, (s, e) => LoadMockup());
            menuMockup.Items.Add(CodePlanner.Core.LocalizationService.T("Odstranit mockup", "Remove mockup"), null, (s, e) => RemoveMockup());

            pnlNapadHeader.Controls.Add(lblNapad);
            pnlNapadHeader.Controls.Add(btnAiAnalyza);
            pnlNapadHeader.Controls.Add(btnDiktovatNapad);
            pnlNapadHeader.Controls.Add(btnReferencie);
            pnlNapadHeader.Controls.Add(btnMockup);

            txtNapad = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 2, 0, 8),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtNapad.TextChanged += (s, e) =>
            {
                if (_nacitani) return;
                _projekt.Idea = txtNapad.Text;
                MarkChanged();
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
            txtNapad.Enter += (s, e) => _napadSnapshot = _projekt.Idea ?? "";
            txtNapad.Leave += (s, e) =>
            {
                if (_nacitani) return;
                _debounceTimer.Stop();
                if ((_projekt.Idea ?? "") != _napadSnapshot)
                {
                    SpecificationService.LogChange(_projekt, "Idea", "Project idea text modified.");
                    RefreshLog();
                    RefreshStatus();
                    RenderSpecification();
                }
            };


            var otazkyBox = PostavOtazky();

            levy.Controls.Add(pnlNazevATypHeader, 0, 0);
            levy.Controls.Add(pnlNazevATyp, 0, 1);
            levy.Controls.Add(pnlNapadHeader, 0, 2);
            levy.Controls.Add(txtNapad, 0, 3);
            levy.Controls.Add(otazkyBox, 0, 4);

            split.Panel1.Controls.Add(levy);

            // ----- PRAVÝ PANEL: TabControl (Specifikace vs AI Asistent) -----
            var tabRight = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            // TAB 1: Specifikace
            var tabSpecPage = new TabPage(CodePlanner.Core.LocalizationService.T("3 · 📄 Specifikace a exporty", "3 · 📄 Specification & Exports"));
            tabSpecPage.BackColor = Color.White;
            
            var pnlSpecHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Navy
            };

            lblSpecHlavicka = new Label
            {
                Text = "Live Specification",
                Dock = DockStyle.Left,
                Width = 250,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                ForeColor = Color.White,
                BackColor = Navy,
                Font = new Font("Segoe UI Semibold", 10f)
            };

            var txtHledat = new TextBox
            {
                Width = 160,
                Height = 20,
                Location = new Point(tabRight.Width - 180, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Gray,
                Text = CodePlanner.Core.LocalizationService.T("Hledat...", "Search...")
            };

            txtHledat.Enter += (s, e) =>
            {
                if (txtHledat.Text == CodePlanner.Core.LocalizationService.T("Hledat...", "Search..."))
                {
                    txtHledat.Text = "";
                    txtHledat.ForeColor = Color.Black;
                }
            };

            txtHledat.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtHledat.Text))
                {
                    txtHledat.Text = CodePlanner.Core.LocalizationService.T("Hledat...", "Search...");
                    txtHledat.ForeColor = Color.Gray;
                }
            };

            txtHledat.TextChanged += (s, e) =>
            {
                if (txtHledat.Text != CodePlanner.Core.LocalizationService.T("Hledat...", "Search..."))
                {
                    SearchText(txtHledat.Text);
                }
            };

            pnlSpecHeader.Controls.Add(lblSpecHlavicka);
            pnlSpecHeader.Controls.Add(txtHledat);

            rtbSpec = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                WordWrap = true
            };

            lblNalezy = new LinkLabel
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Visible = false,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.5f),
                LinkColor = DesignSystem.Cervena,
                ActiveLinkColor = DesignSystem.Cervena,
                VisitedLinkColor = DesignSystem.Cervena,
                AccessibleRole = AccessibleRole.Link,
                AccessibleName = "Consistency check warning link",
                TabStop = true
            };
            lblNalezy.Click += (s, e) => ShowFindings();

            tabSpecPage.Controls.Add(rtbSpec);
            tabSpecPage.Controls.Add(lblNalezy);
            tabSpecPage.Controls.Add(pnlSpecHeader);
            tabRight.TabPages.Add(tabSpecPage);

            // TAB 2: AI Asistent (Chat)
            var tabChatPage = new TabPage(CodePlanner.Core.LocalizationService.T("💬 AI Asistent (Chat)", "💬 AI Assistant (Chat)"));
            tabChatPage.BackColor = Color.White;
            
            var tlpChat = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White
            };
            tlpChat.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // chat history log
            tlpChat.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // input panel
            tlpChat.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // hint ke klávesám

            rtbChatLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                WordWrap = true
            };

            var pnlChatInputArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(6)
            };

            txtChatInput = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f),
                Text = PlaceholderChatu
            };
            txtChatInput.ForeColor = Color.Gray;

            txtChatInput.Enter += (s, e) =>
            {
                if (txtChatInput.ForeColor == Color.Gray)
                {
                    txtChatInput.Text = "";
                    txtChatInput.ForeColor = Color.Black;
                }
            };
            txtChatInput.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtChatInput.Text))
                {
                    txtChatInput.Text = PlaceholderChatu;
                    txtChatInput.ForeColor = Color.Gray;
                }
            };

            txtChatInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    SendChat();
                }
            };

            btnSendChat = new Button
            {
                Text = CodePlanner.Core.LocalizationService.T("Odeslat", "Send"),
                Width = 75,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Navy,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9f)
            };
            btnSendChat.FlatAppearance.BorderSize = 0;
            btnSendChat.Click += (s, e) => SendChat();

            btnClearChat = new Button
            {
                Text = CodePlanner.Core.LocalizationService.T("Vymazat", "Clear"),
                Width = 65,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f)
            };
            btnClearChat.FlatAppearance.BorderColor = Color.Silver;
            btnClearChat.Click += (s, e) => ClearChat();

            var pnlChatButtons = new Panel
            {
                Width = 145,
                Dock = DockStyle.Right,
                Padding = new Padding(4, 0, 0, 0)
            };
            pnlChatButtons.Controls.Add(btnSendChat);
            pnlChatButtons.Controls.Add(btnClearChat);

            pnlChatInputArea.Controls.Add(txtChatInput);
            pnlChatInputArea.Controls.Add(pnlChatButtons);

            var lblChatHint = new Label
            {
                Text = CodePlanner.Core.LocalizationService.T("Enter = odeslat · Shift+Enter = nový řádek", "Enter = send · Shift+Enter = new line"),
                AutoSize = true,
                ForeColor = SedaText,
                Font = new Font("Segoe UI", 8f),
                Margin = new Padding(8, 0, 0, 4)
            };

            tlpChat.Controls.Add(rtbChatLog, 0, 0);
            tlpChat.Controls.Add(pnlChatInputArea, 0, 1);
            tlpChat.Controls.Add(lblChatHint, 0, 2);

            tabChatPage.Controls.Add(tlpChat);
            tabRight.TabPages.Add(tabChatPage);

            split.Panel2.Controls.Add(tabRight);

            tabRight.Resize += (s, e) =>
            {
                txtHledat.Location = new Point(tabRight.Width - 180, 6);
            };

            return split;
        }

        private GroupBox PostavOtazky()
        {
            var box = new GroupBox
            {
                Text = "2 · Questions & Answers (highest impact first)",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ForeColor = Navy
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                BackColor = Color.Transparent
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // seznam otázek
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // mini-legenda značek
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // otázka
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // nápověda
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));   // odpověď
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // AI rychlé nápovědy odpovědí (pnlQuickOptions)
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // tlačítka
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));   // progress bar
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // postup text

            pnlQuickOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 4),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };

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
            lstOtazky.ItemHeight = (int)(lstOtazky.Font.Height * 2.2);
            lstOtazky.DrawItem += DrawQuestionItem;
            lstOtazky.SelectedIndexChanged += (s, e) => ShowSelectedQuestion();

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
                Margin = new Padding(0, 2, 0, 4),
                BorderStyle = BorderStyle.FixedSingle
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
                Text = "Save Answer (Ctrl+Enter)",
                AutoSize = true,
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnOdpovedet.FlatAppearance.BorderSize = 0;
            btnOdpovedet.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 150, 137);
            btnOdpovedet.Click += (s, e) => SaveAnswer();

            btnPredpoklad = new Button
            {
                Text = "I don't know → use assumption",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnPredpoklad.FlatAppearance.BorderColor = Teal;
            btnPredpoklad.FlatAppearance.MouseOverBackColor = TealSvetla;
            btnPredpoklad.Click += (s, e) => UseAssumption();

            btnDiktovatOdpoved = new Button
            {
                Text = "🎤 Dictate",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Navy,
                Padding = new Padding(8, 2, 8, 2),
                Cursor = Cursors.Hand
            };
            btnDiktovatOdpoved.FlatAppearance.BorderColor = Teal;
            btnDiktovatOdpoved.FlatAppearance.MouseOverBackColor = TealSvetla;
            btnDiktovatOdpoved.MouseDown += BtnDictate_MouseDown;
            btnDiktovatOdpoved.MouseUp += BtnDictate_MouseUp;
            btnDiktovatOdpoved.Click += BtnDictate_Click;
            _tipReference.SetToolTip(btnDiktovatOdpoved, TipDiktovani);

            tlacitka.Controls.Add(btnOdpovedet);
            tlacitka.Controls.Add(btnPredpoklad);
            tlacitka.Controls.Add(btnDiktovatOdpoved);

            pnlPostup = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 0),
                BackColor = Color.Transparent
            };
            pnlPostup.Paint += DrawProgress;
            pnlPostup.Resize += (s, e) => pnlPostup.Invalidate();

            lblPostup = new Label
            {
                AutoSize = true,
                ForeColor = SedaText,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblLegenda = new Label
            {
                AutoSize = true,
                Text = "✔ answered · ≈ assumption · ○ unanswered",
                ForeColor = SedaText,
                Font = new Font("Segoe UI", 8f),
                Margin = new Padding(0, 0, 0, 4)
            };

            tlp.Controls.Add(lstOtazky, 0, 0);
            tlp.Controls.Add(lblLegenda, 0, 1);
            tlp.Controls.Add(lblOtazka, 0, 2);
            tlp.Controls.Add(lblNapoveda, 0, 3);
            tlp.Controls.Add(txtOdpoved, 0, 4);
            tlp.Controls.Add(pnlQuickOptions, 0, 5);
            tlp.Controls.Add(tlacitka, 0, 6);
            tlp.Controls.Add(pnlPostup, 0, 7);
            tlp.Controls.Add(lblPostup, 0, 8);

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

        // ---------------- custom drawing ----------------

        /// <summary>Question list item: colored state, impact badge, and truncated text.</summary>
        private void DrawQuestionItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstOtazky.Items.Count) return;

            bool vybrano = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var pozadi = vybrano ? TealSvetla : Color.White;
            using (var b = new SolidBrush(pozadi)) e.Graphics.FillRectangle(b, e.Bounds);
            if (vybrano)
                using (var b = new SolidBrush(Teal))
                    e.Graphics.FillRectangle(b, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);

            // Spodní jemný oddělovač řádku
            using (var p = new Pen(Color.FromArgb(240, 240, 240)))
                e.Graphics.DrawLine(p, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            char stav = e.Index < _stavyOtazek.Count ? _stavyOtazek[e.Index] : '○';
            bool vysoky = e.Index < _vysokyDopad.Count && _vysokyDopad[e.Index];

            // Dynamický výpočet velikostí podle DPI
            float scale = (float)this.DeviceDpi / 96f;
            int badgeSize = (int)(14 * scale);
            int chipWidth = (int)(20 * scale);
            int chipHeight = (int)(16 * scale);
            int padding = (int)(8 * scale);
            int chipOffset = padding + badgeSize + (int)(6 * scale);
            int textOffset = chipOffset + chipWidth + (int)(6 * scale);

            // 1. Badge stavu (grafický kruh)
            var badgeRect = new Rectangle(e.Bounds.X + padding, e.Bounds.Y + (e.Bounds.Height - badgeSize) / 2, badgeSize, badgeSize);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (stav == '✔')
            {
                using (var b = new SolidBrush(Zelena))
                {
                    e.Graphics.FillEllipse(b, badgeRect);
                }
                using (var f = new Font("Segoe UI", Math.Max(6.5f, 8f * scale), FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, "✓", f, badgeRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            else if (stav == '≈')
            {
                using (var b = new SolidBrush(Oranzova))
                {
                    e.Graphics.FillEllipse(b, badgeRect);
                }
                using (var f = new Font("Segoe UI", Math.Max(6f, 7.5f * scale), FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, "A", f, badgeRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            else // '○'
            {
                using (var p = new Pen(Color.Silver, 1.5f))
                {
                    e.Graphics.DrawEllipse(p, badgeRect);
                }
            }

            // 2. Štítek dopadu (pill tag se zaoblenými rohy)
            var chip = new Rectangle(e.Bounds.X + chipOffset, e.Bounds.Y + (e.Bounds.Height - chipHeight) / 2, chipWidth, chipHeight);
            using (var b = new SolidBrush(vysoky ? Navy : Color.Gainsboro))
            using (var path = Zaobli(chip, (int)(3 * scale)))
                e.Graphics.FillPath(b, path);

            using (var f = new Font("Segoe UI", Math.Max(6f, 7.5f * scale), FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, vysoky ? "H" : "M", f, chip,
                    vysoky ? Color.White : Color.DimGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 3. Text otázky
            var textRect = new Rectangle(e.Bounds.X + textOffset, e.Bounds.Y, e.Bounds.Width - (textOffset + 4), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, lstOtazky.Items[e.Index].ToString(), lstOtazky.Font,
                textRect, Color.Black,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        /// <summary>Progress bar: teal fill on light track with rounded corners.</summary>
        private void DrawProgress(object? sender, PaintEventArgs e)
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

        private void CmbType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_nacitani) return;
            if (!(cmbTyp.SelectedItem is ProjectTypeComboItem vybrany)) return;
            if (string.Equals(_projekt.ProjectTypeKey, vybrany.Key, StringComparison.OrdinalIgnoreCase)) return;

            SpecificationService.ChangeProjectType(_projekt, vybrany.Key);
            MarkChanged();
            RefreshAll();
            ShowSelectedQuestion();
        }

        private void ShowUserStories()
        {
            var nastaveni = GeminiSettings.Load();
            using (var dlg = new UserStoriesForm(_projekt.UserStories, nastaveni.EffectiveApiKey, nastaveni.GeminiModel, _projekt, () => MarkChanged()))
            {
                dlg.ShowDialog(this);
            }
            RefreshAll();
        }

        private void ShowMetrics()
        {
            var nastaveni = GeminiSettings.Load();
            using (var dlg = new MetrikyForm(_projekt.Metrics, nastaveni.EffectiveApiKey, nastaveni.GeminiModel, _projekt, () => MarkChanged()))
            {
                dlg.ShowDialog(this);
            }
            RefreshAll();
        }

        private void SaveAnswer()
        {
            var ot = SelectedQuestion();
            if (ot == null) return;

            if (string.IsNullOrWhiteSpace(txtOdpoved.Text))
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Napište prosím odpověď, nebo zvolte 'Nevím -> použít předpoklad'.", "Please write an answer, or select 'I don't know -> use assumption'."),
                    CodePlanner.Core.LocalizationService.T("Chybějící odpověď", "Missing Answer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SpecificationService.AnswerQuestion(_projekt, ot.Id, txtOdpoved.Text);
            MarkChanged();
            RefreshAll();
            MoveToNext(ot);
        }

        private void UseAssumption()
        {
            var ot = SelectedQuestion();
            if (ot == null) return;

            SpecificationService.UseAssumption(_projekt, ot.Id);
            MarkChanged();
            RefreshAll();
            MoveToNext(ot);
        }

        private void MoveToNext(Question posledni)
        {
            var dalsi = SpecificationService.GetNextUnansweredQuestion(_projekt);
            if (dalsi != null)
            {
                SelectQuestion(dalsi);
            }
            else
            {
                SelectQuestion(posledni);
                SetStatus("All questions answered – you can now export the specification (step 3).");
            }
        }

        // ---------------- pomocné ----------------

        private Question? SelectedQuestion()
        {
            int i = lstOtazky.SelectedIndex;
            var otazky = SpecificationService.GetProjectQuestions(_projekt).ToList();
            if (i < 0 || i >= otazky.Count) return null;
            return otazky[i];
        }

        private void SelectQuestion(Question? ot)
        {
            if (ot == null) return;
            var otazky = SpecificationService.GetProjectQuestions(_projekt).ToList();
            for (int i = 0; i < otazky.Count; i++)
            {
                if (otazky[i].Id == ot.Id)
                {
                    lstOtazky.SelectedIndex = i;
                    return;
                }
            }
        }

        private void ShowSelectedQuestion()
        {
            var ot = SelectedQuestion();
            if (ot == null) return;

            bool programoveVolani = _nacitani;   // true = jde o programovou obnovu seznamu, ne o volbu uživatele

            lblOtazka.Text = ot.GetText(_projekt.ProjectTypeKey);
            lblNapoveda.Text = ot.GetHelpText(_projekt.ProjectTypeKey) + "  (If you don't know, the default will be: '" + ot.GetDefaultAssumption(_projekt.ProjectTypeKey) + "')";

            var odp = SpecificationService.GetAnswerFor(_projekt, ot.Id);
            _nacitani = true;
            txtOdpoved.Text = odp != null && !odp.IsAssumption ? odp.Text : "";
            _nacitani = programoveVolani;

            foreach (Control ctrl in pnlQuickOptions.Controls)
            {
                ctrl.Dispose();
            }
            pnlQuickOptions.Controls.Clear();
            var moznosti = ot.GetOptions(_projekt.ProjectTypeKey);
            if (moznosti != null && moznosti.Count > 0)
            {
                var lblTip = new Label
                {
                    Text = "Tip:",
                    AutoSize = true,
                    ForeColor = SedaText,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Margin = new Padding(0, 4, 4, 0)
                };
                pnlQuickOptions.Controls.Add(lblTip);

                foreach (var m in moznosti)
                {
                    var btnVolba = new Button
                    {
                        Text = m,
                        AutoSize = true,
                        BackColor = TealSvetla,
                        ForeColor = Navy,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("Segoe UI", 8f),
                        Margin = new Padding(2, 0, 2, 0),
                        Padding = new Padding(4, 1, 4, 1)
                    };
                    btnVolba.FlatAppearance.BorderSize = 0;
                    btnVolba.Click += (s, e) =>
                    {
                        txtOdpoved.Text = m;
                        txtOdpoved.Focus();
                        SaveAnswer();
                    };
                    pnlQuickOptions.Controls.Add(btnVolba);
                }
            }

            // fokus rovnou do pole odpovědi, aby šlo hned psát (ne při programové obnově seznamu ani během AI)
            if (!programoveVolani && !_isBusy && !_chatBusy && txtOdpoved.Enabled)
            {
                txtOdpoved.Focus();
            }
        }

        private void RefreshAll()
        {
            RefreshQuestionList();
            RenderSpecification();
            RefreshLog();
            RefreshStatus();
            RefreshTitle();
            RefreshReferenceButton();
            RefreshMockupButton();
            RefreshTypeCombo();
            RenderChatHistory();
        }

        private void RefreshQuestionList()
        {
            int vybrano = lstOtazky.SelectedIndex;
            _nacitani = true;
            lstOtazky.BeginUpdate();
            lstOtazky.Items.Clear();
            _stavyOtazek.Clear();
            _vysokyDopad.Clear();

            var otazky = SpecificationService.GetProjectQuestions(_projekt).ToList();
            foreach (var ot in otazky)
            {
                var odp = SpecificationService.GetAnswerFor(_projekt, ot.Id);
                _stavyOtazek.Add(odp == null ? '○' : (odp.IsAssumption ? '≈' : '✔'));
                _vysokyDopad.Add(ot.Impact == Impact.High);
                lstOtazky.Items.Add(ot.GetText(_projekt.ProjectTypeKey));
            }

            if (vybrano >= 0 && vybrano < lstOtazky.Items.Count)
                lstOtazky.SelectedIndex = vybrano;
            lstOtazky.EndUpdate();
            _nacitani = false;
        }

        private void RenderSpecification()
        {
            string md = SpecificationService.RenderMarkdown(_projekt);
            try
            {
                rtbSpec.Rtf = MarkdownNaRtf(md);
            }
            catch
            {
                rtbSpec.Text = md;   // nouzový režim: syrový markdown
            }
            int celkem = SpecificationService.GetProjectQuestions(_projekt).Count();
            lblSpecHlavicka.Text = "Live Specification · version " + _projekt.Version +
                " · answered " + (SpecificationService.GetAnsweredCount(_projekt) + SpecificationService.GetAssumptionsCount(_projekt)) +
                "/" + celkem;
            RefreshFindings();
        }

        private void SearchText(string dotaz)
        {
            // Resetujeme formátování na původní stav
            RenderSpecification();

            if (string.IsNullOrWhiteSpace(dotaz) || dotaz.Length < 2) return;

            int prvyIndex = -1;
            int start = 0;
            while (start < rtbSpec.TextLength)
            {
                int index = rtbSpec.Find(dotaz, start, RichTextBoxFinds.None);
                if (index == -1) break;

                if (prvyIndex == -1) prvyIndex = index;

                rtbSpec.Select(index, dotaz.Length);
                rtbSpec.SelectionBackColor = Color.Yellow;
                rtbSpec.SelectionColor = Color.Black;

                start = index + dotaz.Length;
            }
            rtbSpec.SelectionLength = 0; // zrušíme výběr

            if (prvyIndex != -1)
            {
                rtbSpec.SelectionStart = prvyIndex;
                rtbSpec.ScrollToCaret();
            }
        }

        private void RefreshFindings()
        {
            _nalezy = ConsistencyChecker.Check(_projekt);
            if (_nalezy.Count == 0)
            {
                lblNalezy.Visible = false;
                return;
            }

            int rozpory = _nalezy.Count(n => n.Severity == Severity.Conflict);
            int varovani = _nalezy.Count - rozpory;
            var casti = new List<string>();
            if (rozpory > 0) casti.Add(Mnozne(rozpory, "conflict", "conflicts", "conflicts"));
            if (varovani > 0) casti.Add(Mnozne(varovani, "warning", "warnings", "warnings"));

            lblNalezy.Text = (rozpory > 0 ? "❗ " : "⚠️ ") + "Consistency Check: " +
                string.Join(" and ", casti) + " – click for details";
            lblNalezy.Links.Clear();
            lblNalezy.Links.Add(0, lblNalezy.Text.Length);
            
            lblNalezy.LinkColor = rozpory > 0 ? Color.FromArgb(155, 28, 28) : Color.FromArgb(146, 90, 4);
            lblNalezy.ActiveLinkColor = lblNalezy.LinkColor;
            lblNalezy.VisitedLinkColor = lblNalezy.LinkColor;
            
            lblNalezy.BackColor = rozpory > 0 ? Color.FromArgb(253, 232, 232) : Color.FromArgb(255, 244, 219);
            lblNalezy.Visible = true;
        }

        /// <summary>Opens consistency check window.</summary>
        private void ShowFindings(bool iKdyzPrazdne = false)
        {
            if (iKdyzPrazdne) RefreshFindings();   // ať pracujeme s aktuálním stavem
            if (_nalezy.Count == 0 && !iKdyzPrazdne) return;
            var nastaveni = GeminiSettings.Load();
            using (var dlg = new NalezyForm(_nalezy, nastaveni.EffectiveApiKey, nastaveni.GeminiModel, _projekt, () => MarkChanged()))
            {
                dlg.ShowDialog(this);
            }
        }

        private static string Mnozne(int n, string jedna, string dvaAzCtyri, string vice)
            => n == 1 ? "1 " + jedna : (n >= 2 && n <= 4 ? n + " " + dvaAzCtyri : n + " " + vice);

        private void RefreshLog()
        {
            lvLog.BeginUpdate();
            lvLog.Items.Clear();
            // nejnovější nahoře
            foreach (var r in Enumerable.Reverse(_projekt.ChangeLog))
            {
                var it = new ListViewItem(r.Timestamp.ToString("yyyy-MM-dd H:mm:ss"));
                it.SubItems.Add(r.Action);
                it.SubItems.Add(r.Detail);
                lvLog.Items.Add(it);
            }
            lvLog.EndUpdate();
        }

        private void RefreshStatus()
        {
            int z = SpecificationService.GetAnsweredCount(_projekt);
            int p = SpecificationService.GetAssumptionsCount(_projekt);
            int otevrene = SpecificationService.GetOpenQuestions(_projekt).Count;
            int celkem = SpecificationService.GetProjectQuestions(_projekt).Count();
            _podilHotovo = celkem > 0 ? (z + p) / (double)celkem : 0;
            pnlPostup.Invalidate();
            lblPostup.Text = "Answered " + z + " · assumptions " + p + " · open " + otevrene + " (out of " + celkem + ")";

            if (_isBusy || _chatBusy) return;   // průběžný stav AI operace nepřepisujeme

            if (string.IsNullOrWhiteSpace(_projekt.Idea) && _projekt.Answers.Count == 0)
            {
                SetStatus("Start by describing your idea (step 1) and let AI prepare customized questions.");
                return;
            }

            SetStatus("Specification version " + _projekt.Version + " · answered " + z + "/" + celkem +
                 " · assumptions " + p + " · open questions " + otevrene);
        }

        private void MarkChanged()
        {
            _dirty = true;
            RefreshTitle();
        }

        private void RefreshTitle()
        {
            string nazev = string.IsNullOrWhiteSpace(_projekt.Name) ? "new project" : _projekt.Name.Trim();
            Text = "CodePlanner – " + nazev + (_dirty ? " *" : "") + " – v2.2.0";
        }

        private void SetStatus(string text) => lblStav.Text = text;



        private void OpenSettings()
        {
            using var dlg = new SettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                SetStatus("Gemini API settings saved.");
            }
            RefreshApiBanner();   // po uložení klíče banner zmizí
        }

        private async void BtnAiAnalysis_Click(object? sender, EventArgs e)
        {
            if (_isBusy)
            {
                _ctsAi?.Cancel();
                return;
            }

            var nastaveni = GeminiSettings.Load();
            string apiKey = nastaveni.EffectiveApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T("API klíč pro Gemini není nakonfigurován.\nOtevřete prosím nastavení AI a zadejte svůj API klíč.", "Gemini API key is not configured.\nPlease open AI Settings and enter your API key."),
                    CodePlanner.Core.LocalizationService.T("Chybějící API klíč", "Missing API Key"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OpenSettings();
                return;
            }

            string napad = txtNapad.Text.Trim();
            if (string.IsNullOrWhiteSpace(napad))
            {
                MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T("Nejprve prosím zadejte nápad k analýze.", "Please enter an idea first to analyze."),
                    CodePlanner.Core.LocalizationService.T("Prázdný nápad", "Empty Idea"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtNapad.Focus();
                return;
            }

            if (_projekt.Answers.Count > 0 || _projekt.UserStories.Count > 0)
            {
                var confirm = MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T(
                        "Analýza přepíše stávající odpovědi (" + _projekt.Answers.Count + "), smaže User Stories (" +
                        _projekt.UserStories.Count + ") a vymaže metriky.\n" +
                        "Bude vytvořena záloha, kterou lze obnovit tlačítkem '↩ Vrátit analýzu'.\n\nChcete pokračovat?",
                        "Analysis will overwrite existing answers (" + _projekt.Answers.Count + "), delete User Stories (" +
                        _projekt.UserStories.Count + "), and clear metrics.\n" +
                        "A backup will be created, which can be restored using the '↩ Revert Analysis' button.\n\nDo you want to proceed?"
                    ),
                    CodePlanner.Core.LocalizationService.T("Přepsat specifikaci?", "Overwrite Specification?"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }

            // Záloha projektu před analýzou – lze ji vrátit tlačítkem „↩ Vrátit analýzu“ v liště.
            try
            {
                SpecificationService.SaveProject(_projekt, CestaZalohyPredAnalyzou);
                _snapshotAnalyzyExistuje = true;
                RefreshRevertAnalysisButton();
            }
            catch (Exception exZaloha)
            {
                var pokracovat = MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T(
                        "Nepodařilo se vytvořit zálohu před analýzou:\n\n" + exZaloha.Message +
                        "\n\nChcete přesto pokračovat (bez možnosti vrácení zpět)?",
                        "Failed to create backup before analysis:\n\n" + exZaloha.Message +
                        "\n\nDo you want to proceed anyway (without the ability to revert)?"
                    ),
                    CodePlanner.Core.LocalizationService.T("Zálohování selhalo", "Backup Failed"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (pokracovat != DialogResult.Yes) return;
            }

            string stavPoDokonceni = CodePlanner.Core.LocalizationService.T("Připraven.", "Ready.");
            SetBusyState(true, CodePlanner.Core.LocalizationService.T("Komunikuji s Gemini API...", "Communicating with Gemini API..."));
            _ctsAi = new CancellationTokenSource();
            var startujiciProjekt = _projekt;

            try
            {
                string model = nastaveni.GeminiModel;
                string mockupMime = (_projekt.MockupName != null && (_projekt.MockupName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || _projekt.MockupName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))) ? "image/jpeg" : "image/png";
                var vysledek = await GeminiService.AnalyzeIdeaAsync(apiKey, model, napad, _projekt.ProjectTypeKey, _projekt.ReferenceText, _projekt.MockupBase64, mockupMime, _ctsAi.Token);

                if (this.IsDisposed || !this.Created) return;
                if (startujiciProjekt != _projekt) return;

                if (vysledek == null || vysledek.Questions == null || vysledek.Questions.Count == 0)
                {
                    throw new Exception("AI analysis returned no questions.");
                }

                var uniqueQuestions = vysledek.Questions
                    .Where(ot => !string.IsNullOrWhiteSpace(ot.Id))
                    .GroupBy(ot => ot.Id)
                    .Select(g => g.First())
                    .ToList();

                try
                {
                    _nacitani = true;
                    _projekt.Name = vysledek.Name ?? "";
                    txtNazev.Text = _projekt.Name;
                    _projekt.Questions.Clear();
                    _projekt.Answers.Clear();
                    _projekt.UserStories.Clear();
                    _projekt.Metrics = new ProjectMetrics();

                    foreach (var ot in uniqueQuestions)
                    {
                        var dopadEnum = string.Equals(ot.Impact, "Vysoky", StringComparison.OrdinalIgnoreCase) ? Impact.High : Impact.Medium;
                        _projekt.Questions.Add(new Question
                        {
                            Id = ot.Id,
                            Section = ot.Section,
                            Impact = dopadEnum,
                            Text = ot.Text,
                            HelpText = ot.HelpText,
                            DefaultAssumption = ot.DefaultAssumption,
                            Options = ot.Options ?? new List<string>()
                        });

                        // Prázdnou odpověď od AI nepřidáváme – otázka zůstane otevřená.
                        // Má-li otázka výchozí předpoklad, použijeme ho a poctivě označíme jako předpoklad.
                        string textOdpovedi = (ot.Answer ?? "").Trim();
                        if (textOdpovedi.Length > 0)
                        {
                            _projekt.Answers.Add(new Answer
                            {
                                QuestionId = ot.Id,
                                Text = textOdpovedi,
                                IsAssumption = ot.IsAssumption,
                                Timestamp = DateTime.Now
                            });
                        }
                        else if (!string.IsNullOrWhiteSpace(ot.DefaultAssumption))
                        {
                            _projekt.Answers.Add(new Answer
                            {
                                QuestionId = ot.Id,
                                Text = ot.DefaultAssumption.Trim(),
                                IsAssumption = true,
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                }
                finally
                {
                    _nacitani = false;
                }

                _projekt.Version++;
                _projekt.UpdatedAt = DateTime.Now;
                _projekt.ChangeLog.Add(new DecisionLogEntry
                {
                    Timestamp = DateTime.Now,
                    Action = CodePlanner.Core.LocalizationService.T("AI Analýza", "AI Analysis"),
                    Detail = CodePlanner.Core.LocalizationService.T($"Specifikace vygenerována pomocí Gemini API (model: {model}).", $"Specification generated using Gemini API (model: {model}).")
                });

                MarkChanged();
                RefreshAll();
                SelectQuestion(SpecificationService.GetNextUnansweredQuestion(_projekt));

                // úspěch bez vyskakovacího okna – stačí stavový řádek (viz UX bod „méně modálů“)
                stavPoDokonceni = CodePlanner.Core.LocalizationService.T("✅ Analýza dokončena – zkontrolujte otázky a odpovědi (krok 2). Můžete ji vrátit zpět tlačítkem 'Vrátit analýzu'.", "✅ Analysis completed – review questions and answers (step 2). You can revert using 'Revert Analysis'.");
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    stavPoDokonceni = CodePlanner.Core.LocalizationService.T("Analýza zrušena – projekt zůstal nezměněn.", "Analysis cancelled – project remains unchanged.");
                    return;
                }
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T($"Během analýzy nápadu došlo k chybě:\n\n{ex.Message}", $"An error occurred during idea analysis:\n\n{ex.Message}"),
                    CodePlanner.Core.LocalizationService.T("Chyba AI analýzy", "AI Analysis Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                stavPoDokonceni = CodePlanner.Core.LocalizationService.T("Analýza selhala – projekt zůstal nezměněn.", "Analysis failed – project remains unchanged.");
            }
            finally
            {
                _ctsAi?.Dispose();
                _ctsAi = null;
                SetBusyState(false, stavPoDokonceni);
            }
        }

        private void SetBusyState(bool busy, string textStavu)
        {
            _isBusy = busy;
            SetStatus(textStavu);
            btnAiAnalyza.Enabled = true;
            btnAiAnalyza.Text = busy ? "❌ Cancel Analysis" : "🤖 Analyze with Gemini";

            if (busy)
            {
                _casStartuAiOperace = DateTime.Now;
                _prubehTimer.Start();
            }
            else
            {
                _prubehTimer.Stop();
            }

            txtNazev.Enabled = !busy;
            txtNapad.Enabled = !busy;
            lstOtazky.Enabled = !busy;
            txtOdpoved.Enabled = !busy;
            btnOdpovedet.Enabled = !busy;
            btnPredpoklad.Enabled = !busy;

            // typ zůstává zamčený, dokud má projekt dynamické otázky z AI analýzy (viz ObnovComboTypu)
            if (cmbTyp != null) cmbTyp.Enabled = !busy && (_projekt?.Questions == null || _projekt.Questions.Count == 0);
            if (btnDiktovatNapad != null) btnDiktovatNapad.Enabled = !busy;
            if (btnDiktovatOdpoved != null) btnDiktovatOdpoved.Enabled = !busy;
            if (btnReferencie != null) btnReferencie.Enabled = !busy;
            if (btnMockup != null) btnMockup.Enabled = !busy;

            if (toolBar != null) toolBar.Enabled = !busy;
            if (txtChatInput != null) txtChatInput.Enabled = !busy;
            if (btnSendChat != null) btnSendChat.Enabled = !busy;
            if (btnClearChat != null) btnClearChat.Enabled = !busy;

            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void BtnDictate_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var b = (Button)sender!;

            if (_diktovaniClickToggle)
            {
                _diktovaniClickToggle = false;
                _ignorujDalsiMouseUp = true;
                StopAndDictate(b);
                return;
            }
            
            // zkontrolujeme API klíč dřív, než začneme nahrávat
            var nastaveni = GeminiSettings.Load();
            if (string.IsNullOrWhiteSpace(nastaveni.EffectiveApiKey))
            {
                MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T("API klíč pro Gemini není nakonfigurován.\nNakonfigurujte prosím svůj API klíč v nastavení AI pro diktování.", "Gemini API key is not configured.\nPlease configure your API key in AI Settings for dictation."),
                    CodePlanner.Core.LocalizationService.T("Chybějící API klíč", "Missing API Key"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OpenSettings();
                return;
            }

            // spustíme nahrávání
            _casSpusteniDiktovani = DateTime.Now;
            try
            {
                VoiceRecorder.StartRecording();
                _tlacitkoDiktovaniAktivni = b;
                _diktovaniLimitTimer.Stop();
                _diktovaniLimitTimer.Start();   // pojistka: auto-stop po 3 minutách
                b.BackColor = Color.Crimson;
                b.ForeColor = Color.White;
                b.Text = CodePlanner.Core.LocalizationService.T("🎤 Nahrávání...", "🎤 Recording...");
                SetStatus(CodePlanner.Core.LocalizationService.T("Diktování spuštěno. Držte tlačítko nebo na něj znovu klikněte pro zastavení (limit 3 min).", "Dictation started. Hold the button, or click again to stop (3 min limit)."));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T("Nepodařilo se spustit nahrávání z mikrofonu:\n\n", "Failed to start recording from microphone:\n\n") + ex.Message + "\n\n" + RadaMikrofon,
                    CodePlanner.Core.LocalizationService.T("Chyba nahrávání", "Recording Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDictate_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            
            if (_ignorujDalsiMouseUp)
            {
                _ignorujDalsiMouseUp = false;
                return;
            }

            if (_diktovaniClickToggle) return; // v toggle režimu nereagujeme na uvolnění

            if (_tlacitkoDiktovaniAktivni == null) return; // nahrávání už neběží (např. auto-stop po 3 minutách, nebo selhal start)

            var b = (Button)sender!;
            double ms = (DateTime.Now - _casSpusteniDiktovani).TotalMilliseconds;

            if (ms < 400)
            {
                // stisk byl příliš rychlý -> přepneme do režimu klikni-a-mluv
                _diktovaniClickToggle = true;
                b.Text = CodePlanner.Core.LocalizationService.T("🎤 Nahrávání (kliknutím zastavíte)", "🎤 Recording (click to stop)");
                SetStatus(CodePlanner.Core.LocalizationService.T("Aktivován režim klikni-a-mluv. Nahrávám... Pro ukončení klikněte na tlačítko znovu (limit 3 min).", "Click-to-talk mode activated. Recording… Click the button again to finish (3 min limit)."));
            }
            else
            {
                // hold-to-talk: uvolněno, stopujeme a přepisujeme
                StopAndDictate(b);
            }
        }

        private void BtnDictate_Click(object? sender, EventArgs e)
        {
            // Click se spouští po MouseUp. Vše obsloužíme v MouseDown a MouseUp.
        }

        private async void StopAndDictate(Button b, bool autoStop = false)
        {
            _diktovaniLimitTimer.Stop();
            _tlacitkoDiktovaniAktivni = null;

            SetStatus(CodePlanner.Core.LocalizationService.T("Zastavuji nahrávání...", "Stopping recording..."));
            string? cestaWav = VoiceRecorder.StopRecording();

            // obnovíme výchozí vzhled tlačítka
            if (b == btnDiktovatNapad)
            {
                b.BackColor = Color.Gainsboro;
                b.ForeColor = Navy;
                b.Text = CodePlanner.Core.LocalizationService.T("🎤 Diktovat", "🎤 Dictate");
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Navy;
                b.Text = CodePlanner.Core.LocalizationService.T("🎤 Diktovat", "🎤 Dictate");
            }

            if (string.IsNullOrEmpty(cestaWav))
            {
                SetStatus(CodePlanner.Core.LocalizationService.T("Diktování zrušeno, nebo se nepodařilo uložit nahrávku. ", "Dictation cancelled, or could not save recording. ") + RadaMikrofon);
                return;
            }

            // zkontrolujeme, zda nahrávka trvala aspoň chvíli
            double delkaSekund = 0;
            try
            {
                if (File.Exists(cestaWav))
                {
                    var info = new FileInfo(cestaWav);
                    // 16kHz 16-bit mono wav = 32000 bajtů za sekundu (+ ~44 bajtů hlavička)
                    delkaSekund = (info.Length - 44) / 32000.0;
                }
            }
            catch { }

            if (delkaSekund < 0.4)
            {
                SetStatus(CodePlanner.Core.LocalizationService.T("Nahrávka byla příliš krátká.", "Recording was too short."));
                try { if (File.Exists(cestaWav)) File.Delete(cestaWav); } catch { }
                return;
            }

            TextBox cil = b == btnDiktovatNapad ? txtNapad : txtOdpoved;
            b.Enabled = false;
            b.Text = CodePlanner.Core.LocalizationService.T("⏳ Přepisuji...", "⏳ Transcribing...");
            SetStatus(CodePlanner.Core.LocalizationService.T("Komunikuji s Gemini API (přepisuji)...", "Communicating with Gemini API (transcribing)..."));

            _transcribing = true;
            _ctsTranscribe = new CancellationTokenSource();
            var startujiciProjekt = _projekt;

            try
            {
                var nastaveni = GeminiSettings.Load();
                string model = nastaveni.GeminiModel;
                string apiKey = nastaveni.EffectiveApiKey;

                string prepis = await GeminiService.TranscribeAudioAsync(apiKey, model, cestaWav, _ctsTranscribe.Token);
                
                if (this.IsDisposed || !this.Created) return;
                if (startujiciProjekt != _projekt) return; // projekt byl mezitím přepnut, výsledek zahodíme
                if (cil == null || cil.IsDisposed) return;

                if (string.IsNullOrWhiteSpace(prepis))
                {
                    MessageBox.Show(this,
                        CodePlanner.Core.LocalizationService.T("V nahrávce nebylo rozpoznáno žádné slovo.\n\n", "No speech was recognized from the recording.\n\n") + RadaMikrofon,
                        CodePlanner.Core.LocalizationService.T("Prázdný přepis", "Empty Transcription"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetStatus(CodePlanner.Core.LocalizationService.T("Přepis nevrátil žádný text.", "Transcription returned no text."));
                }
                else
                {
                    InsertTextAtCursor(cil, prepis);
                    SetStatus(autoStop
                        ? CodePlanner.Core.LocalizationService.T("⏱ Nahrávání bylo po 3 minutách automaticky zastaveno a řeč byla přepsána.", "⏱ Recording was automatically stopped after 3 minutes and speech transcribed.")
                        : CodePlanner.Core.LocalizationService.T("Řeč byla úspěšně přepsána.", "Speech successfully transcribed."));
                }
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (startujiciProjekt != _projekt) return; // projekt přepnut, ignorujeme chybu
                if (ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
                {
                    SetStatus(CodePlanner.Core.LocalizationService.T("Přepis zrušen.", "Transcription cancelled."));
                    return;
                }
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Během přepisu audia došlo k chybě:\n\n", "An error occurred during audio transcription:\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba přepisu", "Transcription Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus(CodePlanner.Core.LocalizationService.T("Chyba během přepisu.", "Error during transcription."));
            }
            finally
            {
                _transcribing = false;
                _ctsTranscribe?.Dispose();
                _ctsTranscribe = null;

                if (!this.IsDisposed && this.Created)
                {
                    b.Enabled = true;
                    b.Text = "🎤 Dictate";
                }
                
                // smažeme dočasný soubor
                try { if (File.Exists(cestaWav)) File.Delete(cestaWav); } catch { }
            }
        }

        private static void InsertTextAtCursor(TextBox tb, string novyText)
        {
            if (string.IsNullOrWhiteSpace(novyText)) return;
            
            tb.Focus();
            int index = tb.SelectionStart;
            string staryText = tb.Text ?? "";
            
            // přidáme mezery podle kontextu
            string vkladany = novyText.Trim();
            if (index > 0 && !char.IsWhiteSpace(staryText[index - 1]))
            {
                vkladany = " " + vkladany;
            }
            if (index < staryText.Length && !char.IsWhiteSpace(staryText[index]))
            {
                vkladany = vkladany + " ";
            }

            tb.SelectedText = vkladany;
            tb.SelectionStart = index + vkladany.Length;
            tb.SelectionLength = 0;
        }



        private void RefreshProjectTypeCombo()
        {
            _nacitani = true;
            cmbTyp.Items.Clear();

            // Přidáme built-in typy
            cmbTyp.Items.Add(new ProjectTypeComboItem { Key = "General", Name = "General Application" });
            cmbTyp.Items.Add(new ProjectTypeComboItem { Key = "Game", Name = "Game" });
            cmbTyp.Items.Add(new ProjectTypeComboItem { Key = "Registry", Name = "Registry / Database application" });
            cmbTyp.Items.Add(new ProjectTypeComboItem { Key = "Tool", Name = "Tool / Utility" });

            // Přidáme custom šablony
            foreach (var sab in TemplateService.CustomTemplates)
            {
                cmbTyp.Items.Add(new ProjectTypeComboItem { Key = sab.Key, Name = sab.Name });
            }

            _nacitani = false;
        }

        private void SetTypeCombo(string typKlic)
        {
            _nacitani = true;
            for (int i = 0; i < cmbTyp.Items.Count; i++)
            {
                if (cmbTyp.Items[i] is ProjectTypeComboItem item && string.Equals(item.Key, typKlic, StringComparison.OrdinalIgnoreCase))
                {
                    cmbTyp.SelectedIndex = i;
                    _nacitani = false;
                    return;
                }
            }
            cmbTyp.SelectedIndex = 0;
            _nacitani = false;
        }

        private void RenderChatHistory()
        {
            if (rtbChatLog == null) return;

            rtbChatLog.Clear();
            if (_projekt.ChatHistory == null)
            {
                _projekt.ChatHistory = new List<ChatMessage>();
            }

            if (_projekt.ChatHistory.Count == 0)
            {
                rtbChatLog.SelectionFont = _chatFontItalic ?? rtbChatLog.Font;
                rtbChatLog.SelectionColor = Color.Gray;
                rtbChatLog.AppendText("There are no messages yet. Ask anything about your specification – e.g. what might be missing in the requirements.\n\n");
                return;
            }

            foreach (var msg in _projekt.ChatHistory)
            {
                bool isUser = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase);
                rtbChatLog.SelectionFont = _chatFontBold ?? rtbChatLog.Font;
                rtbChatLog.SelectionColor = isUser ? Navy : Teal;
                string casText = msg.Timestamp == default ? "" : $" [{msg.Timestamp:H:mm}]";
                rtbChatLog.AppendText(isUser ? $"Me{casText}: " : $"Assistant{casText}: ");

                rtbChatLog.SelectionFont = _chatFontRegular ?? rtbChatLog.Font;
                rtbChatLog.SelectionColor = Color.Black;
                rtbChatLog.AppendText(msg.Text + "\n\n");
            }

            rtbChatLog.SelectionStart = rtbChatLog.Text.Length;
            rtbChatLog.ScrollToCaret();
        }

        private async void SendChat()
        {
            if (_chatBusy)
            {
                // tlačítko je během čekání přepnuté na „Zrušit“ – druhé kliknutí operaci stornuje
                _ctsChat?.Cancel();
                return;
            }

            if (txtChatInput.ForeColor == Color.Gray || string.IsNullOrWhiteSpace(txtChatInput.Text))
            {
                return;
            }

            // kontrola API klíče předem – zpráva se nesmí dostat do historie
            var nastaveni = GeminiSettings.Load();
            if (string.IsNullOrWhiteSpace(nastaveni.EffectiveApiKey))
            {
                MessageBox.Show(this,
                    "Gemini API key is not configured.\nPlease open AI Settings and enter your API key.",
                    "Missing API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OpenSettings();
                return;
            }

            string text = txtChatInput.Text.Trim();
            _chatBusy = true;
            txtChatInput.Enabled = false;
            btnSendChat.Text = "Cancel";
            btnClearChat.Enabled = false;
            txtChatInput.Clear();

            if (_projekt.ChatHistory == null)
            {
                _projekt.ChatHistory = new List<ChatMessage>();
            }

            var uzivatelZprava = new ChatMessage { Role = "user", Text = text, Timestamp = DateTime.Now };
            _projekt.ChatHistory.Add(uzivatelZprava);
            MarkChanged();
            RenderChatHistory();

            Cursor = Cursors.WaitCursor;
            SetStatus(CodePlanner.Core.LocalizationService.T("AI asistent odpovídá...", "AI Assistant is replying..."));
            _casStartuAiOperace = DateTime.Now;
            _prubehTimer.Start();
            _ctsChat = new CancellationTokenSource();
            var startujiciProjekt = _projekt;

            try
            {
                string odpoved = await GeminiService.SendChatMessageAsync(nastaveni.EffectiveApiKey, nastaveni.GeminiModel, _projekt, _projekt.ChatHistory, _ctsChat.Token);

                if (this.IsDisposed || !this.Created) return;
                if (startujiciProjekt != _projekt) return;

                var modelZprava = new ChatMessage { Role = "model", Text = odpoved, Timestamp = DateTime.Now };
                _projekt.ChatHistory.Add(modelZprava);
                MarkChanged();
                RenderChatHistory();
                SetStatus(CodePlanner.Core.LocalizationService.T("AI asistent odpověděl.", "AI Assistant replied."));
            }
            catch (Exception ex)
            {
                if (this.IsDisposed || !this.Created) return;
                if (startujiciProjekt != _projekt) return;

                // zprávu vrátíme zpět do vstupu, ať o ni uživatel nepřijde
                if (_projekt.ChatHistory != null && _projekt.ChatHistory.Contains(uzivatelZprava))
                {
                    _projekt.ChatHistory.Remove(uzivatelZprava);
                }
                txtChatInput.Text = text;
                txtChatInput.ForeColor = Color.Black;
                RenderChatHistory();

                bool zruseno = ex is OperationCanceledException || ex.InnerException is OperationCanceledException;
                if (zruseno)
                {
                    SetStatus(CodePlanner.Core.LocalizationService.T("Odesílání zprávy zrušeno – text zůstal ve vstupním poli.", "Message sending cancelled – text remains in input field."));
                }
                else
                {
                    MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Chyba při komunikaci s AI asistentem:\n\n", "Error communicating with AI Assistant:\n\n") + ex.Message, CodePlanner.Core.LocalizationService.T("Chyba AI", "AI Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetStatus(CodePlanner.Core.LocalizationService.T("Komunikace selhala.", "Communication failed."));
                }
            }
            finally
            {
                _ctsChat?.Dispose();
                _ctsChat = null;
                _chatBusy = false;
                _prubehTimer.Stop();

                if (!this.IsDisposed && this.Created)
                {
                    txtChatInput.Enabled = true;
                    btnSendChat.Text = CodePlanner.Core.LocalizationService.T("Odeslat", "Send");
                    btnSendChat.Enabled = true;
                    btnClearChat.Enabled = true;
                    Cursor = Cursors.Default;
                    txtChatInput.Focus();
                }
            }
        }

        private void ClearChat()
        {
            if (_projekt.ChatHistory == null || _projekt.ChatHistory.Count == 0) return;

            var confirm = MessageBox.Show(this,
                CodePlanner.Core.LocalizationService.T("Opravdu chcete vymazat celou historii chatu s asistentem?", "Are you sure you want to clear the entire assistant chat history?"),
                CodePlanner.Core.LocalizationService.T("Vymazat chat", "Clear Chat"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _projekt.ChatHistory.Clear();
                MarkChanged();
                RenderChatHistory();
                SetStatus(CodePlanner.Core.LocalizationService.T("Historie chatu vymazána.", "Chat history cleared."));
            }
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (_nacitani) return;
            RenderSpecification();
        }

        // ---------------- autosave, zálohy a další pomocné metody (UX fáze 2) ----------------

        /// <summary>Každé 2 minuty tiše uloží rozdělanou práci do %AppData%\CodePlanner\autosave.vcbrief.</summary>
        private void AutosaveTimer_Tick(object? sender, EventArgs e)
        {
            if (!_dirty || _isBusy || _chatBusy) return;
            if (ProjektJePrazdny()) return;

            try
            {
                SpecificationService.SaveProject(_projekt, CestaAutosave);
            }
            catch
            {
                // automatická záloha nikdy nesmí rušit práci uživatele
            }
        }

        private bool ProjektJePrazdny()
            => string.IsNullOrWhiteSpace(_projekt.Name)
               && string.IsNullOrWhiteSpace(_projekt.Idea)
               && _projekt.Answers.Count == 0
               && (_projekt.ChatHistory == null || _projekt.ChatHistory.Count == 0);

        private static void DeleteAutosave()
        {
            try { if (File.Exists(CestaAutosave)) File.Delete(CestaAutosave); } catch { }
        }

        /// <summary>Po startu nabídne obnovu automatické zálohy (např. po pádu aplikace nebo výpadku proudu).</summary>
        private void OfferAutosaveRecovery()
        {
            try
            {
                if (!File.Exists(CestaAutosave)) return;

                DateTime cas = File.GetLastWriteTime(CestaAutosave);
                var res = MessageBox.Show(this,
                    CodePlanner.Core.LocalizationService.T(
                        $"Byla nalezena automatická záloha neuložené práce (z {cas:HH:mm}). Chcete ji obnovit?",
                        $"An autosave of unsaved work was found (from {cas:HH:mm}). Do you want to restore it?"
                    ),
                    CodePlanner.Core.LocalizationService.T("Obnovit automatickou zálohu", "Restore Autosave"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (res == DialogResult.Yes)
                {
                    var projekt = SpecificationService.LoadProject(CestaAutosave);
                    _cestaSouboru = null;   // záloha nemá „domovský“ soubor – uložení si vyžádá cestu
                    LoadProjectToUi(projekt);
                    MarkChanged();
                    SetStatus("Autosave restored – do not forget to save the project (Ctrl+S).");
                }

                DeleteAutosave();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Nepodařilo se načíst soubor automatické zálohy.\n\n", "Failed to load the autosave file.\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba obnovy", "Recovery Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DeleteAutosave();
            }
        }

        /// <summary>Nahradí aktuální projekt daty z jiné instance a obnoví celé UI (ponechá _cestaSouboru beze změny).</summary>
        private void LoadProjectToUi(ProjectSpecification novy)
        {
            _projekt = novy ?? new ProjectSpecification();

            _nacitani = true;
            txtNazev.Text = _projekt.Name ?? "";
            txtNapad.Text = _projekt.Idea ?? "";
            SetTypeCombo(_projekt.ProjectTypeKey);
            txtOdpoved.Text = "";
            _nacitani = false;

            RefreshAll();
            SelectQuestion(SpecificationService.GetNextUnansweredQuestion(_projekt));
        }

        /// <summary>„↩ Vrátit analýzu“ – obnoví projekt ze zálohy vytvořené těsně před poslední AI analýzou.</summary>
        private void RevertAnalysis()
        {
            if (!_snapshotAnalyzyExistuje || !File.Exists(CestaZalohyPredAnalyzou))
            {
                _snapshotAnalyzyExistuje = false;
                RefreshRevertAnalysisButton();
                SetStatus("Backup before analysis is not available.");
                return;
            }

            var res = MessageBox.Show(this,
                CodePlanner.Core.LocalizationService.T(
                    "Projekt bude vrácen do stavu před poslední AI analýzou. Aktuální otázky a odpovědi budou nahrazeny.\n\nChcete pokračovat?",
                    "The project will revert to the state before the last AI analysis. The current questions and answers will be replaced.\n\nDo you want to proceed?"
                ),
                CodePlanner.Core.LocalizationService.T("Vrátit analýzu", "Revert Analysis"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;

            try
            {
                var projekt = SpecificationService.LoadProject(CestaZalohyPredAnalyzou);
                LoadProjectToUi(projekt);
                MarkChanged();
                SetStatus("Project reverted to the state before AI analysis.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CodePlanner.Core.LocalizationService.T("Nepodařilo se načíst zálohu.\n\n", "Failed to load backup.\n\n") + ex.Message,
                    CodePlanner.Core.LocalizationService.T("Chyba obnovy", "Recovery Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshRevertAnalysisButton()
        {
            if (btnVratitAnalyzu == null) return;
            btnVratitAnalyzu.Visible = _snapshotAnalyzyExistuje;
            btnVratitAnalyzu.Enabled = _snapshotAnalyzyExistuje;
        }

        /// <summary>Banner „chybí API klíč“ – zobrazí se jen, dokud uživatel klíč nenastaví.</summary>
        private void RefreshApiBanner()
        {
            if (lblApiBanner == null) return;
            var nastaveni = GeminiSettings.Load();
            lblApiBanner.Visible = string.IsNullOrWhiteSpace(nastaveni.EffectiveApiKey);
        }

        /// <summary>Po AI analýze je typ projektu daný vygenerovanými otázkami – combo se zamkne.</summary>
        private void RefreshTypeCombo()
        {
            if (cmbTyp == null) return;
            bool zafixovan = _projekt?.Questions != null && _projekt.Questions.Count > 0;
            cmbTyp.Enabled = !zafixovan && !_isBusy;
            _tipReference?.SetToolTip(cmbTyp, zafixovan
                ? CodePlanner.Core.LocalizationService.T("Typ projektu je pevně dán AI analýzou – nová analýza jej může změnit.", "The project type is fixed by AI analysis – a new analysis can change it.")
                : CodePlanner.Core.LocalizationService.T("Šablona pro vedené otázky na základě typu projektu.", "Template for guided questions based on project type."));
        }

        /// <summary>Před exportem shrne, co ve specifikaci ještě chybí nebo je zastaralé. Vrací true = exportovat.</summary>
        private bool ConfirmExportWithSummary()
        {
            int celkem = SpecificationService.GetProjectQuestions(_projekt).Count();
            int zodpovezeno = SpecificationService.GetAnsweredCount(_projekt) + SpecificationService.GetAssumptionsCount(_projekt);
            int otevrene = SpecificationService.GetOpenQuestions(_projekt).Count;
            int nalezy = ConsistencyChecker.Check(_projekt).Count;
            bool metrikyStare = SpecificationService.AreMetricsOutdated(_projekt);
            bool storiesStare = SpecificationService.AreStoriesOutdated(_projekt);

            if (otevrene == 0 && nalezy == 0 && !metrikyStare && !storiesStare) return true;

            var casti = new List<string>();
            if (otevrene > 0) casti.Add("answered " + zodpovezeno + " out of " + celkem + " questions");
            if (nalezy > 0) casti.Add(Mnozne(nalezy, "consistency check finding", "consistency check findings", "consistency check findings"));
            if (metrikyStare && storiesStare) casti.Add("both estimate and user stories are outdated");
            else if (metrikyStare) casti.Add("estimate is outdated");
            else if (storiesStare) casti.Add("user stories are outdated");

            var res = MessageBox.Show(this,
                CodePlanner.Core.LocalizationService.T(
                    "Specifikace ještě není kompletní:\n\n• " + string.Join("\n• ", casti) + "\n\nChcete ji přesto exportovat?",
                    "The specification is not complete yet:\n\n• " + string.Join("\n• ", casti) + "\n\nDo you want to export anyway?"
                ),
                CodePlanner.Core.LocalizationService.T("Kontrola stavu před exportem", "Export Summary Check"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return res == DialogResult.Yes;
        }

        /// <summary>„❓“ v toolbaru – přehled kroků práce a klávesových zkratek.</summary>
        private void ShowHelp()
        {
            MessageBox.Show(this,
                CodePlanner.Core.LocalizationService.T(
                    "JAK POSTUPOVAT (4 kroky):\n" +
                    "1. Popište svůj nápad vlastními slovy (nebo použijte diktování hlasem).\n" +
                    "2. Nechte AI připravit otázky na míru (🤖 Analyzovat), nebo začněte odpovídat na předdefinované šablony.\n" +
                    "3. Odpovězte na otázky – pokud nevíte, použijte předpoklady.\n" +
                    "4. Exportujte hotovou specifikaci: Markdown pro lidi, JSON pro AI agenty, PDF či HTML pro klienty.\n\n" +
                    "KLÁVESOVÉ ZKRATKY:\n" +
                    "Ctrl+N – nový projekt\n" +
                    "Ctrl+O – otevřít projekt\n" +
                    "Ctrl+S – uložit projekt (funguje i během běhu AI analýzy)\n" +
                    "Ctrl+M – exportovat do Markdownu\n" +
                    "Ctrl+J – exportovat do JSONu\n" +
                    "Ctrl+P – exportovat do PDF\n" +
                    "Ctrl+Enter – uložit odpověď na zvolenou otázku\n" +
                    "Esc – zrušit běžící AI operaci\n\n" +
                    "IKONY V SEZNAMU OTÁZEK:\n" +
                    "✔ zodpovězeno · ≈ předpoklad · ○ nezodpovězeno\n" +
                    "Červený pruh označuje otázku s vysokým dopadem.",

                    "HOW TO PROCEED (4 steps):\n" +
                    "1. Describe your idea in your own words (or use voice dictation).\n" +
                    "2. Let AI prepare customized questions (🤖 Analyze), or start answering predefined template questions.\n" +
                    "3. Answer the questions – if you don't know, use assumptions.\n" +
                    "4. Export the completed specification: Markdown for humans, JSON for AI agents, PDF or HTML for clients.\n\n" +
                    "KEYBOARD SHORTCUTS:\n" +
                    "Ctrl+N – new project\n" +
                    "Ctrl+O – open project\n" +
                    "Ctrl+S – save project (works even during AI processing)\n" +
                    "Ctrl+M – export Markdown\n" +
                    "Ctrl+J – export JSON\n" +
                    "Ctrl+P – export PDF\n" +
                    "Ctrl+Enter – save answer for selected question\n" +
                    "Esc – cancel running AI operation\n\n" +
                    "QUESTION LIST ICONS:\n" +
                    "✔ answered · ≈ assumption · ○ unanswered\n" +
                    "Red accent bar indicates high impact question."
                ),
                CodePlanner.Core.LocalizationService.T("Nápověda", "Help"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Každou sekundu ukazuje, jak dlouho už AI operace běží, a připomíná možnost zrušení.</summary>
        private void PrubehTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isBusy && !_chatBusy)
            {
                _prubehTimer.Stop();
                return;
            }
            int sekundy = (int)(DateTime.Now - _casStartuAiOperace).TotalSeconds;
            SetStatus(CodePlanner.Core.LocalizationService.T("⏳ Komunikuji s AI... (" + sekundy + "s) – stiskněte Esc pro zrušení", "⏳ Communicating with AI… (" + sekundy + "s) – Esc to cancel"));
        }

        /// <summary>Pojistka diktování: po 3 minutách nahrávání automaticky zastaví a odešle k přepisu.</summary>
        private void DiktovaniLimitTimer_Tick(object? sender, EventArgs e)
        {
            _diktovaniLimitTimer.Stop();
            var b = _tlacitkoDiktovaniAktivni;
            if (b == null) return;

            _diktovaniClickToggle = false;
            SetStatus(CodePlanner.Core.LocalizationService.T("⏱ Nahrávání dosáhlo limitu 3 minut – automaticky se zastavuje a přepisuje.", "⏱ Recording reached the 3 minute limit – automatically stopping and transcribing."));
            StopAndDictate(b, autoStop: true);
        }
    }

}
