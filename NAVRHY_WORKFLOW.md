# CodePlanner – Návrhy vylepšení workflow (audit 3 agentů)
*Vytvořeno: 11. 7. 2026 · Stav: VŠECHNY BODY ÚSPĚŠNĚ IMPLEMENTOVÁNY*

Cíl: aby appka byla intuitivní pro neprogramátora – vedla ho za ruku, nikdy mu neztratila práci a mluvila jeho jazykem. Všechny navržené body (P1 až P4) byly zapracovány, lokalizovány a otestovány.

---

## 🔴 P1 – Ochrana práce (největší riziko ztráty důvěry)

### 1. Auto-save + atomické ukládání + obnova po pádu
- **Problém:** Jediná ochrana je hvězdička + dotaz při zavření. `UlozProjekt` (SpecCore.cs ř. 1133) zapisuje přímo `File.WriteAllText` – pád uprostřed zápisu může poškodit jedinou kopii projektu. Žádný auto-save, žádná obnova po pádu.
- **Návrh:** (a) zápis do `.tmp` + `File.Replace` se zálohou `.bak` (quick win), (b) auto-save každé 2 min do `%AppData%\CodePlanner\autosave.vcbrief` + nabídka obnovy při startu (střední).

### 2. AI analýza tiše maže User Stories a metriky
- **Problém:** `BtnAiAnalyza_Click` (MainForm.cs ř. 1751–1753) maže odpovědi, stories i metriky, ale potvrzovací dialog (ř. 1720) zmiňuje jen odpovědi. Žádná cesta zpět.
- **Návrh:** (a) doplnit do dialogu plný výčet co se smaže (quick win), (b) před analýzou snapshot projektu do paměti + tlačítko „Vrátit poslední analýzu" (střední).

### 3. Verzování bez cesty zpět
- **Problém:** „Verze 37" je jen čítač. Přepsaná odpověď zmizí – do logu jde jen nový text. Uživatel čeká, že se k verzím dá vrátit.
- **Návrh:** (a) do logu ukládat „bylo → je" (quick win), (b) undo stack posledních N stavů + Ctrl+Z (střední).

### 4. Prázdná odpověď z AI = zelená fajfka
- **Problém:** AI analýza přidá i prázdné odpovědi (MainForm.cs ř. 1769–1775) → progress hlásí hotovo, do exportu jdou prázdná pole.
- **Návrh:** prázdné odpovědi nepřidávat, nebo spadnout na výchozí předpoklad. Quick win.

---

## 🟠 P2 – Vedení uživatele (intuitivnost od prvního spuštění)

### 5. First-run: chybí kroky 1-2-3
- **Problém:** Po spuštění uživatel vidí naráz prázdný nápad, AI tlačítko i 10 otázek – neví, kde začít. Když nejdřív odpoví a pak dá AI analýzu, o odpovědi přijde.
- **Návrh:** očíslovat workflow v UI (1. Nápad → 2. AI otázky na míru → 3. Odpovědi → 4. Export); dokud je nápad prázdný, otázky zašedit / překrýt nápovědou. Střední.

### 6. Onboarding API klíče – tři různé reakce na tutéž situaci
- **Problém:** AI analýza a diktování správně nabídnou Nastavení; dialogy (Stories/Metriky/Nálezy) jen zašedí tlačítko „(chybí API klíč)" bez cesty dál; chat klíč nekontroluje vůbec a vyhodí syrovou chybu (OdeslatChat, ř. 2421+). Prázdné stavy dialogů navíc odkazují na tlačítko, které je zrovna zakázané.
- **Návrh:** všude jednotný vzor „vysvětlení + Otevřít nastavení"; při startu bez klíče nenásilný banner „AI funkce vyžadují klíč zdarma – nastavit teď". Quick win.

### 7. Uložení klíče bez ověření
- **Problém:** „Uložit" v nastavení přijme i překlep; chyba se projeví až později jinde. Test připojení je nepovinný krok stranou.
- **Návrh:** po Uložit klíč automaticky otestovat; při neúspěchu „Uložit přesto / Opravit". Střední. (Bonus: popisky modelů v combu – „flash = rychlý, doporučeno".)

### 8. Fokus nejde do pole odpovědi
- **Problém:** Po uložení odpovědi a přechodu na další otázku zůstává fokus na tlačítku – u každé otázky klik navíc a Ctrl+Enter přestane fungovat. (Focus se volá jen u „Tip" chipů, ř. 1489.)
- **Návrh:** `txtOdpoved.Focus()` na konci `UkazVybranouOtazku`. Quick win – 1 řádek, velký efekt v hlavní smyčce.

### 9. Kontrola konzistence je schovaná a výsledky se zahazují
- **Problém:** Okno nálezů se otevře jen klikem na banner – když offline kontrola nic nenajde, AI hloubková kontrola je nedosažitelná. AI nálezy se navíc nikam neukládají (NalezyForm nemá callback ani export) – zavřením okna zmizí.
- **Návrh:** tlačítko „✅ Kontrola…" do toolbaru (funkční i bez nálezů); AI nálezy ukládat do projektu + „📋 Kopírovat". Quick win až střední. Dlouhodobě: nálezy jako nemodální panel, ať jde zároveň opravovat specifikace.

---

## 🟡 P3 – Plynulost a async UX

### 10. Chat a diktování nejdou stornovat, Esc nic nedělá
- **Problém:** Hlavní analýza storno má, chat a přepis hlasu ne – při pomalé síti je UI až 90 s zamčené; Esc je odchycený, ale nenapojený; Ctrl+S se během čekání tiše zahodí.
- **Návrh:** CancellationToken do chatu i přepisu, Esc = zrušit aktivní operaci, do status baru „⏳ Komunikuji… (12 s)". Quick win/střední.

### 11. Zastaralé stories/metriky se tváří jako aktuální
- **Problém:** Po změně specifikace zůstávají stories, odhady i v exportech beze změny – klient dostane PDF s odhadem pro starou verzi.
- **Návrh:** porovnat čas výpočtu vs. `Upraveno` → badge „Vygenerováno pro v X (nyní v Y) – přegenerovat?" v dialozích i exportech. Quick win až střední.

### 12. Export bez kontroly kompletnosti a vodítka
- **Problém:** Export pustí ven 3/10 odpovědí bez varování; k čemu je který formát, říkají jen tooltipy.
- **Návrh:** před exportem souhrn („Zodpovězeno 3/10, 2 rozpory – přesto exportovat?"); jeden dialog Export s popisem: MD = pro čtení, JSON = pro AI agenta, PDF = pro klienta, HTML = interaktivní sdílení. Střední.

### 13. Diktování bez limitu délky
- **Problém:** Toggle režim nahrává donekonečna; dlouhý WAV → limit Gemini + timeout s nic neříkající chybou. Odepřený mikrofon = jen „nerozpoznáno žádné slovo".
- **Návrh:** auto-stop po ~3 min s odpočtem; kontrola velikosti před uploadem; do chyb tip na Nastavení Windows → Soukromí → Mikrofon. Quick win.

### 14. Šetřit kontext posílaný do Gemini
- **Problém:** Chat posílá pokaždé celou specifikaci vč. až 2MB referenčního textu + celou historii + mockup → pomalé odezvy, rychlejší 429.
- **Návrh:** ořez reference (~100 kB), okno posledních N zpráv, mockup jen jednou. Střední. (Bonus: retry s backoffem pro 429/503 a srozumitelná hláška při vadném JSON z AI.)

---

## 🟢 P4 – Balíček textových oprav (vše quick win, ~hodina práce)

1. **„(demonstrátor bez AI)"** v patičce každého exportu (SpecCore.cs ř. 705) – nepravda, nahradit „Vytvořeno nástrojem CodePlanner v2.0".
2. **Překlep** „Metriky **and** odhad projektu" (MetrikyForm.cs ř. 38) + sjednotit názvy funkcí (toolbar/titulek/hlavička se liší u Metrik i Stories).
3. **Tykání × vykání** – sjednotit (doporučení: vykání).
4. **Žargon** pro neprogramátory: „asynchronní analýza", „sémantické posouzení", chat placeholder „Napiš SQL schémata / refactoring" → přepsat lidsky.
5. **Legenda ✔/P/V/S** u seznamu otázek (mini-legenda nebo tooltipy).
6. **Diktování:** sjednotit popisky obou tlačítek + tooltip vysvětlující držet/kliknout a rozdíl vs. Win+H.
7. **MessageBoxy po úspěchu** (analýza, stories, metriky) → nahradit status labelem; MessageBox jen pro chyby.
8. **MD/JSON export** → doplnit „Otevřít soubor / Zobrazit ve složce" jako u PDF/HTML.
9. **Combo Typ/Šablona po AI analýze** – zamknout s vysvětlením (teď tiše přepisuje předpoklady bez viditelné změny).
10. **„?" v toolbaru** – přehled zkratek a mini-nápověda workflow; hint „Enter = odeslat, Shift+Enter = nový řádek" u chatu.
11. **PDF tiskárna:** preferovat přesně „Microsoft Print to PDF" (teď se bere první s „PDF" v názvu – PDF24 apod. rozbijí export).

---

## ✅ Co nerozbít (funguje dobře)
- Odpovídací smyčka: auto-výběr další otázky, „Nevím → předpoklad", Tip chipy, barevné stavy, progress bar.
- Storno hlavní AI analýzy + vzor „❌ Zrušit" v dialozích; transakční chat (neodeslaná zpráva se vrátí do vstupu).
- SettingsForm: odkaz na získání klíče, test připojení, maskování klíče.
- Offline konzistenční kontrola po každé změně + propis do exportů; srozumitelné české překlady API chyb; ošetřené načítání starších souborů.

---

## Doporučené pořadí implementace
1. **Balíček A – Ochrana práce** (P1: atomický zápis, auto-save, férový confirm, snapshot před analýzou, prázdné odpovědi) – největší dopad na důvěru.
2. **Balíček B – Vedení uživatele** (P2: fokus, API klíč všude jednotně, kroky 1-2-3, kontrola konzistence viditelná a trvalá).
3. **Balíček C – Textové opravy** (P4 – levné, hodně zvednou dojem).
4. **Balíček D – Async a exporty** (P3).
