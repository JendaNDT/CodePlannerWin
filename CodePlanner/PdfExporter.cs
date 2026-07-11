using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using CodePlanner.Core;

namespace CodePlanner
{
    public class PdfExporter
    {
        private readonly ProjectSpecification _project;
        private readonly List<string> _radky;
        private int _aktualniRadek = 0;
        private int _vnitrniRadekIndex = 0;
        private int _strana = 0;

        public PdfExporter(ProjectSpecification project)
        {
            _project = project;
            string md = SpecificationService.RenderMarkdown(project);
            _radky = md.Replace("\r\n", "\n").Split('\n').ToList();
        }

        public void Export(IWin32Window parent, string pdfPath)
        {
            using (var pd = new PrintDocument())
            {
                // Vybereme PDF tiskárnu (preferujeme přesně Microsoft Print to PDF)
                var tiskarny = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
                string? tiskarna = tiskarny.FirstOrDefault(p => string.Equals(p, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase))
                               ?? tiskarny.FirstOrDefault(p => p.Contains("Print to PDF"))
                               ?? tiskarny.FirstOrDefault(p => p.Contains("PDF"));
                if (tiskarna != null)
                {
                    pd.PrinterSettings.PrinterName = tiskarna;
                    pd.PrinterSettings.PrintToFile = true;
                    pd.PrinterSettings.PrintFileName = pdfPath;
                }
                else
                {
                    throw new Exception(CodePlanner.Core.LocalizationService.T(
                        "Nebyla nalezena žádná tiskárna PDF (např. Microsoft Print to PDF). Nainstalujte prosím tiskárnu PDF a zkuste to znovu.",
                        "No PDF printer found (e.g. Microsoft Print to PDF). Please install a PDF printer and try again."
                    ));
                }

                pd.PrintPage += Pd_PrintPage;
                _aktualniRadek = 0;
                _vnitrniRadekIndex = 0;
                _strana = 0;
                pd.Print();
            }
        }

        private void Pd_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            if (g == null) return;
            _strana++;

            // Margins
            float marginL = e.MarginBounds.Left;
            float marginT = e.MarginBounds.Top;
            float width = e.MarginBounds.Width;
            float height = e.MarginBounds.Height;
            float marginR = e.MarginBounds.Right;
            float marginB = e.MarginBounds.Bottom;

            // Barvy z DesignSystem
            var navy = DesignSystem.Navy;
            var teal = DesignSystem.Teal;
            var oranzova = DesignSystem.Oranzova;

            if (_strana == 1)
            {
                // Vykreslíme titulní stranu
                g.Clear(Color.White);

                // Levý dekorační panel (navy)
                using (var b = new SolidBrush(navy))
                {
                    g.FillRectangle(b, 0, 0, 60, e.PageBounds.Height);
                }
                // Levý tenký proužek (teal)
                using (var b = new SolidBrush(teal))
                {
                    g.FillRectangle(b, 60, 0, 6, e.PageBounds.Height);
                }

                float startX = 100;
                float startY = 200;

                using (var fTag = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (var bTeal = new SolidBrush(teal))
                {
                    g.DrawString(CodePlanner.Core.LocalizationService.T("SPECIFIKACE PROJEKTU", "PROJECT SPECIFICATION"), fTag, bTeal, startX, startY);
                }

                startY += 30;
                string nazev = string.IsNullOrWhiteSpace(_project.Name) ? CodePlanner.Core.LocalizationService.T("Nový projekt", "New project") : _project.Name.Trim();
                using (var fTitle = new Font("Segoe UI", 26f, FontStyle.Bold))
                using (var bNavy = new SolidBrush(navy))
                {
                    // Zabalíme název, kdyby byl moc dlouhý
                    VykresliOdstavec(g, nazev, fTitle, bNavy, startX, startY, e.PageBounds.Width - startX - 50, 6);
                }

                startY += 100;
                using (var fSub = new Font("Segoe UI", 12f, FontStyle.Italic))
                using (var bGray = new SolidBrush(Color.Gray))
                {
                    g.DrawString(CodePlanner.Core.LocalizationService.T("Strukturovaný technický brief a analýza požadavků", "Structured technical brief and requirements analysis"), fSub, bGray, startX, startY);
                }

                startY = e.PageBounds.Height - 220;
                using (var fLabel = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (var fVal = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (var bNavy = new SolidBrush(navy))
                using (var bGray = new SolidBrush(Color.DimGray))
                {
                    g.DrawString(CodePlanner.Core.LocalizationService.T("Verze specifikace:", "Specification version:"), fLabel, bNavy, startX, startY);
                    g.DrawString(_project.Version.ToString(), fVal, bGray, startX + 130, startY);
 
                    g.DrawString(CodePlanner.Core.LocalizationService.T("Datum vygenerování:", "Generation date:"), fLabel, bNavy, startX, startY + 22);
                    g.DrawString(DateTime.Now.ToString(CodePlanner.Core.LocalizationService.T("d. M. yyyy", "yyyy-MM-dd")), fVal, bGray, startX + 130, startY + 22);
 
                    g.DrawString(CodePlanner.Core.LocalizationService.T("Typ / Šablona:", "Type / Template:"), fLabel, bNavy, startX, startY + 44);
                    g.DrawString(SpecificationService.GetProjectTypeName(_project.ProjectTypeKey), fVal, bGray, startX + 130, startY + 44);
 
                    g.DrawString(CodePlanner.Core.LocalizationService.T("Nástroj:", "Tool:"), fLabel, bNavy, startX, startY + 66);
                    g.DrawString(CodePlanner.Core.LocalizationService.T("CodePlanner (s podporou AI)", "CodePlanner (AI-Powered)"), fVal, bGray, startX + 130, startY + 66);
                }

                e.HasMorePages = true;
                return;
            }

            // Následující stránky s obsahem
            g.Clear(Color.White);

            // Záhlaví
            using (var fHead = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var bGray = new SolidBrush(Color.Gray))
            using (var pen = new Pen(Color.FromArgb(220, 224, 230), 0.75f))
            {
                string headText = CodePlanner.Core.LocalizationService.T(
                    $"Specifikace projektu: {(_project.Name ?? "nový projekt")}",
                    $"Project specification: {(_project.Name ?? "new project")}"
                );
                g.DrawString(headText, fHead, bGray, marginL, marginT - 25);
                g.DrawLine(pen, marginL, marginT - 12, marginR, marginT - 12);
            }

            // Zápatí
            using (var fFoot = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var bGray = new SolidBrush(Color.Gray))
            {
                string footText = CodePlanner.Core.LocalizationService.T($"Strana {_strana}", $"Page {_strana}");
                var size = g.MeasureString(footText, fFoot);
                g.DrawString(footText, fFoot, bGray, marginL + (width - size.Width) / 2, marginB + 15);
            }

            float currentY = marginT;
            using (var fH1 = new Font("Segoe UI Semibold", 16f, FontStyle.Bold))
            using (var fH2 = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
            using (var fH3 = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            using (var fText = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (var bNavy = new SolidBrush(navy))
            using (var bBlack = new SolidBrush(Color.FromArgb(33, 37, 41)))
            {
                while (_aktualniRadek < _radky.Count)
                {
                    string radekRaw = _radky[_aktualniRadek];
                    string radek = radekRaw.TrimEnd();

                    // Určíme font a parametry podle typu řádku
                    Font font = fText;
                    Brush brush = bBlack;
                    float indent = 0;
                    float maxSirka = width;
                    float radekSpacing = 3;
                    bool isHeader = false;
                    bool isBullet = false;
                    bool isQuote = false;

                    if (radek.StartsWith("# ") && !radek.StartsWith("##"))
                    {
                        font = fH1;
                        brush = bNavy;
                        isHeader = true;
                        radekRaw = radekRaw.Substring(2);
                    }
                    else if (radek.StartsWith("## "))
                    {
                        font = fH2;
                        brush = bNavy;
                        isHeader = true;
                        radekRaw = radekRaw.Substring(3);
                    }
                    else if (radek.StartsWith("### "))
                    {
                        font = fH3;
                        brush = bNavy;
                        isHeader = true;
                        radekRaw = radekRaw.Substring(4);
                    }
                    else if (radek.StartsWith("- "))
                    {
                        font = fText;
                        brush = bBlack;
                        isBullet = true;
                        indent = 18;
                        maxSirka = width - 18;
                        radekRaw = radekRaw.Substring(2);
                    }
                    else if (radek.StartsWith("> "))
                    {
                        font = fText;
                        brush = bBlack;
                        isQuote = true;
                        indent = 12;
                        maxSirka = width - 20;
                        radekRaw = radekRaw.Substring(2);
                    }

                    // Strip markdown double asterisks and single asterisks for clean PDF output
                    radekRaw = radekRaw.Replace("**", "").Replace("*", "");

                    if (radek.Length == 0)
                    {
                        // Prázdný řádek
                        float h = 10;
                        if (currentY + h > marginB && currentY > marginT)
                        {
                            e.HasMorePages = true;
                            return;
                        }
                        currentY += h;
                        _aktualniRadek++;
                        _vnitrniRadekIndex = 0;
                        continue;
                    }

                    // Pokud je to nadpis, přidáme trochu horního odsazení na začátku odstavce
                    if (isHeader && _vnitrniRadekIndex == 0)
                    {
                        float extraSpace = radek.StartsWith("# ") ? 14 : (radek.StartsWith("## ") ? 10 : 4);
                        if (currentY + extraSpace > marginB)
                        {
                            e.HasMorePages = true;
                            return;
                        }
                        currentY += extraSpace;
                    }

                    // Zabalíme text na řádky
                    var zabalene = ZabalText(g, radekRaw, font, maxSirka);

                    while (_vnitrniRadekIndex < zabalene.Count)
                    {
                        string z = zabalene[_vnitrniRadekIndex];
                        float lineH = font.Height + radekSpacing;

                        // U citace nebo odrážky na začátku odstavce (první řádek) kreslíme odrážku / levý okraj
                        float extraHeight = 0;
                        if (isQuote) extraHeight = 8; // padding nahoru/dolů
                        if (isBullet && _vnitrniRadekIndex == 0) extraHeight = 4;

                        if (currentY + lineH + extraHeight > marginB && currentY > marginT)
                        {
                            e.HasMorePages = true;
                            return;
                        }

                        // Kreslení speciálních prvků pro citaci nebo odrážku
                        if (isQuote)
                        {
                            using (var pozadi = new SolidBrush(Color.FromArgb(245, 247, 250)))
                            using (var linka = new SolidBrush(oranzova))
                            using (var fItalic = new Font(font.FontFamily, font.Size, FontStyle.Italic))
                            using (var bGray = new SolidBrush(Color.FromArgb(60, 60, 60)))
                            {
                                // Pozadí pro tento řádek citace
                                g.FillRectangle(pozadi, marginL, currentY, width, lineH);
                                g.FillRectangle(linka, marginL, currentY, 3, lineH);
                                g.DrawString(z, fItalic, bGray, marginL + indent, currentY + 2);
                            }
                        }
                        else if (isBullet && _vnitrniRadekIndex == 0)
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            using (var bTeal = new SolidBrush(teal))
                            {
                                g.FillEllipse(bTeal, marginL + 4, currentY + (font.Height - 5) / 2, 5, 5);
                            }
                            g.DrawString(z, font, brush, marginL + indent, currentY);
                        }
                        else
                        {
                            g.DrawString(z, font, brush, marginL + indent, currentY);
                        }

                        currentY += lineH;
                        _vnitrniRadekIndex++;
                    }

                    // Dokončili jsme odstavec
                    _aktualniRadek++;
                    _vnitrniRadekIndex = 0;
                }
            }

            e.HasMorePages = false;
        }

        private static List<string> ZabalText(Graphics g, string text, Font font, float maxSirka)
        {
            var lines = new List<string>();
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return new List<string> { "" };

            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                var size = g.MeasureString(testLine, font);
                if (size.Width > maxSirka && currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear().Append(word);
                }
                else
                {
                    currentLine.Append(currentLine.Length == 0 ? word : " " + word);
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }



        private static float VykresliOdstavec(Graphics g, string text, Font font, Brush brush, float x, float y, float maxSirka, float radekSpacing = 3)
        {
            float startY = y;
            foreach (var radek in text.Split('\n'))
            {
                var zabalene = ZabalText(g, radek, font, maxSirka);
                foreach (var z in zabalene)
                {
                    g.DrawString(z, font, brush, x, y);
                    y += font.Height + radekSpacing;
                }
            }
            return y - startY;
        }
    }
}
