# VoiceCoder Brief – Project Status
*Naposled aktualizováno: 11. 7. 2026 (v0.3)*

## 🎯 Co to je
Windows .exe demonstrátor projektu VoiceCoder AI (dle PDF návrhu, kap. 18): z volně popsaného nápadu vytvoří řízenými otázkami verzovanou specifikaci s exportem pro kódovacího agenta. Bez AI, plně offline.
Stack: C# / .NET 8, WinForms, self-contained single-file exe (win-x64), kompilováno ze sandboxu přes EnableWindowsTargeting.

## ⏭️ Příští krok
**Jenda otestuje v0.3 – kontrolu konzistence.** Tip: zkus zadat rozporný projekt (např. „plně offline" + nápad se synchronizací do cloudu) a sleduj barevný pruh nad specifikací.
ZIP `VoiceCoderBrief_v0.3_Windows.zip` je ve složce i na GitHubu (release v0.3.0).

## ✅ Hotovo
- v0.1 kompletní a **ověřená Jendou na reálných Windows** (spuštění, okno, diktování, exporty – vše OK)
- Jádro: model specifikace se 7 bloky, 10 řízených otázek, verzování, log, export MD/JSON, .vcbrief
- WinForms GUI dle kap. 11 + 36 automatických testů jádra
- **v0.2 zkompilováno (63 MB exe, ZIP ve složce) + všech 36 testů jádra prošlo:**
  - Formátovaný náhled specifikace (RTF: nadpisy, odrážky, tučné, oranžové [PŘEDPOKLAD])
  - Barevný seznam otázek (✔ zelená / ≈ oranžová / ○ šedá + štítek dopadu V/S)
  - Progress bar postupu (teal, zaoblený)
  - Klávesové zkratky: Ctrl+N/O/S, Ctrl+M (export MD), Ctrl+J (export JSON), Ctrl+Enter (uložit odpověď)
  - Hvězdička v titulku při neuložených změnách + název projektu v titulku
  - Roztahovatelný log (Splitter)
  - Ikonky a tooltipy v toolbaru, hover efekty tlačítek, PerMonitorV2 DPI
- v0.2 pushnutá na GitHub (main) + release v0.2.0 se ZIPem
- v0.2 **otestovaná Jendou na Windows – vzhled schválen**
- **v0.3: kontrola konzistence** (9 pravidel: offline×online, web+offline, osobní údaje, non-goals jinde, vágní akceptace, export, desktop×mobil, moc předpokladů, chybějící nápad) – pruh nálezů v GUI + sekce v MD a pole v JSON exportu; 47 testů prošlo; release v0.3.0 na GitHubu

## 📝 TODO
### MVP (nutné pro v1)
- Jenda otestuje kontrolu konzistence ve v0.3

### Backlog (později)
- Napojení na Claude API (AI otázky a generování specifikace) – vlastní API klíč v nastavení
- Skutečný hlasový vstup přes STT API místo Win+H (push-to-talk dle kap. 6)
- Vlastní ikona aplikace a podepsání exe (odstraní SmartScreen varování)
- Šablony otázek podle typu aplikace (hra / evidence / nástroj)
- Další pravidla konzistence podle zkušeností z používání

## 🐛 Známé bugy
- Zatím žádné hlášené. Riziko k ověření ve v0.2: RTF náhled (nová věc) – při chybě má fallback na syrový markdown.

## 🏗️ Klíčová rozhodnutí
- **Rozsah v1:** demonstrátor bez AI dle kap. 18 PDF – ověřit workflow otázky→specifikace dřív, než se přidá AI a agenti.
- **Stack:** C# WinForms místo webové appky, protože Jenda chtěl vyloženě .exe; self-contained single file, aby nebyla potřeba instalace .NET.
- **Hlas v v1:** přes diktování Windows (Win+H) do textových polí – nula kódu, žádné API klíče; skutečné STT až později.
- **Verzování:** každé rozhodnutí zvyšuje číslo verze specifikace a zapisuje se do logu (dle kap. 7 „živý kontrakt").
- **JSON export:** stabilní struktura (sekce → položky → predpoklad flag), aby ji později mohl číst orchestrátor beze změn.
- **Náhled specifikace (v0.2):** vlastní mini-převod markdown→RTF přímo v MainForm (žádná externí knihovna – jednodušší build, žádné závislosti); při chybě fallback na plain text.
- **Git na ploše:** připojená složka nepodporuje mazání/zámky souborů → commit+push se dělá ze sandboxu, ne přímo ze složky.
- **GitHub přístup:** Jendův fine-grained token (jen repo VoiceCoderWin, Contents RW) je uložený v `.github_token` ve složce projektu, je v .gitignore a do repa nesmí. Používat pro push i releases. Až vyprší, vygenerovat nový stejným postupem.
- **Kontrola konzistence (v0.3):** pravidlová, bez AI – porovnává klíčová slova bez diakritiky. Falešný poplach je OK, mlčení ne. Vlastní odstranění diakritiky (mapovací tabulka), protože `string.Normalize()` nefunguje s InvariantGlobalization na Linuxu.
- **Sandbox (pro Clauda):** mount občas servíruje starou velikost souboru (čtení je uříznuté) → před buildem ověřovat grep počty; při zaseknutí rekonstruovat soubory v /tmp/build a pushovat z nich, ne z mountu.

## 📁 Stav souborů
- `VoiceCoderBrief/Core/SpecCore.cs` – jádro: model, otázky, verzování, render MD/JSON, ukládání + KonzistencniKontrola (v0.3)
- `VoiceCoderBrief/MainForm.cs` – celé GUI (v0.2 facelift, v0.3 pruh nálezů)
- `VoiceCoderBrief/Program.cs` – vstupní bod (PerMonitorV2 DPI)
- `VoiceCoderBrief/VoiceCoderBrief.csproj` – konfigurace buildu (v0.3.0)
- `CoreTests/` – automatické testy jádra, 47 kontrol (spustitelné i na Linuxu)
- `VoiceCoderBrief_v0.3_Windows.zip` – hotová aplikace v0.3 + návod CTI_ME.txt
