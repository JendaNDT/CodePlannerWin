using System;
using System.Drawing;

namespace CodePlanner
{
    public static class DesignSystem
    {
        // Paleta barev (Sjednocená brandová paleta)
        public static readonly Color Navy = Color.FromArgb(16, 35, 63);           // #10233F - Tmavé záhlaví, hlavní nadpisy
        public static readonly Color Teal = Color.FromArgb(15, 115, 104);         // #0F7368 - Akcentní barva, tlačítka (WCAG 2.1 AA kontrast 5.89:1)
        public static readonly Color TealSvetla = Color.FromArgb(224, 244, 241);   // #E0F4F1 - Výběr, zvýraznění
        public static readonly Color SvetlePozadi = Color.FromArgb(246, 248, 250); // #F6F8FA - Hlavní pozadí formulářů
        public static readonly Color InactivePozadi = Color.FromArgb(233, 236, 239); // #E9ECEF - Pozadí neaktivních prvků
        public static readonly Color Zelena = Color.FromArgb(0, 150, 90);          // #00965A - Úspěch, vyřešeno
        public static readonly Color Oranzova = Color.FromArgb(230, 140, 0);        // #E68C00 - Varování, předpoklady
        public static readonly Color Cervena = Color.FromArgb(155, 28, 28);         // #9B1C1C - Chyba, konflikt
        public static readonly Color SedaText = Color.FromArgb(105, 105, 105);      // #696969 - Vedlejší text, nápověda

        // Typografická škála (Statické fonty k eliminaci GDI handle leaků)
        public static readonly Font HeaderLarge = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font HeaderMedium = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font CardHeader = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font BodyBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font BodyItalic = new Font("Segoe UI", 9.5f, FontStyle.Italic);
        public static readonly Font Small = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font SmallBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public static readonly Font SmallUnderline = new Font("Segoe UI", 8.5f, FontStyle.Underline);
        public static readonly Font Mono = new Font("Consolas", 9f, FontStyle.Regular);

        // Pomocné metody pro HTML hex kódy (pro exportér do HTML)
        public static string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
