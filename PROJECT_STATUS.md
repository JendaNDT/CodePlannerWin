# CodePlanner – Project Status
*Naposled aktualizováno: 11. 7. 2026 (v2.2.0 – Finální lokalizace)*

## 🎯 Co to je
Windows .exe demonstrátor projektu CodePlanner AI (dle PDF návrhu, kap. 18): z volně popsaného nápadu vytvoří řízenými otázkami verzovanou specifikaci s exportem pro kódovacího agenta.
Stack: C# / .NET 8, WinForms, self-contained single-file exe (win-x64), kompilováno ze sandboxu přes EnableWindowsTargeting.

## ⏭️ Příští krok
**Jenda otestuje verzi v2.2.0 – plně lokalizovanou a stabilizovanou aplikaci CodePlanner v češtině i angličtině.** Spusťte zkompilovaný ZIP, otestujte přepínání jazyků, funkčnost s Gemini API a načtení starých .vcbrief souborů!

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
- **v0.4: šablony otázek** (Obecná aplikace, Hra, Evidence, Nástroj) – dynamicky mění otázky, nápovědy a předpoklady; auto-aktualizace nepotvrzených předpokladů při změně šablony; zapojeno do promptu pro AI analýzu přes Gemini API; verze 0.4.0 u serialization; 60 testů prošlo.
- **v0.5: hlasový vstup (STT) přes Gemini API** (Push-to-Talk) – tlačítka "Diktovat" pro Nápad a Odpověď s podporou Hold-to-Talk (držením) i Toggle-to-Talk (kliknutím); přepis řeči zajišťuje stávající Gemini API; automatické vkládání na pozici kurzoru; nahrávání 16-bit WAV PCM 16kHz mono přes winmm.dll (MCI); 60 testů prošlo.
- **v0.6: vlastní ikona a lokální podepisování** (odstranění SmartScreen) – vygenerována 3D ikona (mikrofon s kódem), která je zakompilována do exe a zobrazuje se u všech oken; přibalen PowerShell skript `PodepsatAplikaci.ps1` pro rychlé lokální podepsání a import certifikátu do důvěryhodných autorit na stroji uživatele; 60 testů prošlo.
- **v0.7: referenční podklady pro AI** – do rozhraní i datového modelu přidána možnost přikládat k AI analýze referenční dokumentaci (API specifikace, zadání, atd.). 67 testů OK.
- **v0.8: seznam nedávných projektů (Recent Projects)** – do tlačítka Otevřít integrován SplitButton se seznamem nedávno otevřených souborů, automatickým čištěním a možností resetu historie. 80 testů OK.
- **v0.9: definice vlastních šablon** – přidána podpora pro nahrávání vlastních definic šablon z externího `sablony.json` souboru ve složce aplikace. 88 testů OK.
- **v1.0: dynamické otázky na míru nápadu** – analýza Gemini API nyní dynamicky generuje celou sadu 7 až 10 doplňujících specifikačních otázek (s nápovědami a předpoklady) na základě vstupního zadání, čímž vzniká zcela personalizovaná specifikace. 102 testů OK.
- **v1.1: vylepšení UI/UX a vyhledávání** – implementovány grafické badges stavu otázek v seznamu, 1px spodní oddělovače a větší řádkování, moderní ploché okraje textových polí a interaktivní vyhledávací lišta nad specifikací se zvýrazňováním shody. 102 testů OK.
- **v1.2: hloubková AI kontrola konzistence** – integrována sémantická kontrola specifikace přes Gemini API s vlastním dialogovým oknem nálezů a přehlednou tabulkou, a přidána dvě nová offline pravidla (SQLite na webu, role bez auth). 104 testů OK.
- **v1.3: rychlé nápovědy odpovědí (Quick Options)** – přidán panel se 3 rychlými nápovědami pro každou otázku (generovanými Gemini na míru nebo z vestavěného fallbacku) s možností předvyplnění a automatického postupu jedním kliknutím. 109 testů OK.
- **v1.4: profesionální PDF export** – přidán export do designově vyladěného PDF dokumentu s titulní stranou, dělícími linkami, stylovanými bloky a plnou prohledávatelností a češtinou přes nativní vektorové vykreslování GDI+. 109 testů OK.
- **v1.5: agilní User Stories a backlog** – přidán dialog backlogu s barevně odlišenými prioritami, generování uživatelských příběhů na vyžádání přes Gemini a podpora exportu do Markdown (s checkboxy) a CSV (pro snadný import do Jira/Trello). 114 testů OK.
- **v1.6: interaktivní AI asistent (chat)** – pravý panel předělán na záložky (Specifikace vs Chat), asistent odpovídá se znalostí celé specifikace (předávané v systemInstruction), podporuje odesílání přes Enter a konverzace se trvale ukládá do souboru `.vcbrief`. 117 testů OK.
- **v1.7: import a analýza skic (mockupů)** – přidáno tlačítko pro nahrání skic rozhraní a screenshotů (PNG/JPG), které Gemini zkoumá jako vizuální kontext při AI analýze nápadu i v chatu, a vestavěn obrázkový prohlížeč. 119 testů OK.
- **v1.8: AI odhad náročnosti a metriky** – integrován panel metrik, který zkoumá specifikaci i backlog a spočítá odhad pracnosti (hodiny), složitost projektu, optimální tým, rozpočet, technický rozbor a rizika odhadu. 124 testů OK.
- **v1.9: interaktivní HTML export (micro-site)** – přidán export do jednoho přenosného HTML souboru s plnohodnotným interaktivním webovým portálem projektu (přepínač tmavého režimu, odškrtávací backlog, live search a responzivní vzhled). 128 testů OK.
- **v2.0.1: refaktor, stabilizace a opravy kritických chyb** – dokončena modularizace (rozdělení MainForm a SpecCore na samostatné soubory), vyřešeny 4 kritické bezpečnostní a stabilizační nálezy (L1, L2, S1 a S2), zavedeny příslušné jednotkové testy a celkový počet testů vzrostl na 174 (všechny OK).
- **v2.1.0: překlad do angličtiny, design systém a nová pravidla konzistence** – kompletní překlad datových struktur jádra, API klienta, hlasového nahrávání a formulářů z češtiny do angličtiny. Zajištěna 100% zpětná kompatibilita pro načítání českých specifikací `.vcbrief`. Sjednoceny barvy a písma do `DesignSystem.cs`, čímž se zamezilo GDI leakům. Přidána nová pravidla offline kontroly konzistence: **Rule 12 (strategie zálohování)** a **Rule 13 (dokumentace k externímu API)**. Testovací sada byla přepsána a úspěšně otestována (všechny testy OK). Zkompilován finální distribuční ZIP `CodePlanner-v2.1.0.zip`.
- **v2.2.0: robustní validace, kompletní lokalizace a opravy chyb z auditu** – implementována hloubková doménová validace a čištění deserializovaných projektů v loaderu, zaveden Safe Load Workflow (vytvoření a otestování lokální kopie před prohozením a překreslením UI), omezení délky ukládané chybové odpovědi na 100k znaků, zobrazování chyb načítání šablon z `sablony.json` v UI, odstranění nepoužívaného a opuštěného kódu. Kompletně do-lokalizovány zbývající dialogy, menu, statusy, nápovědy a PDF exporty, čímž vznikl plně bilingvní (česko-anglický) systém. Repozitář vyčištěn od dočasných a starých sestavení. Všech 174 testů jádra prošlo úspěšně.


## 📝 TODO
### MVP (nutné pro v1)
- Jenda otestuje verzi v2.2.0 s kompletní lokalizací (zkompilovaný ZIP a funkčnost s Gemini API)

### Backlog (později)
- Napojení na Claude API (AI otázky a generování specifikace) – vlastní API klíč v nastavení (přeskočeno, Gemini stačí)
- Další pravidla konzistence podle zkušeností z používání

## 🐛 Známé bugy
- Zatím žádné hlášené.

## 🏗️ Klíčová rozhodnutí
- **Rozsah v1:** demonstrátor bez AI dle kap. 18 PDF – ověřit workflow otázky→specifikace dřív, než se přidá AI a agenti.
- **Stack:** C# WinForms místo webové appky, protože Jenda chtěl vyloženě .exe; self-contained single file, aby nebyla potřeba instalace .NET.
- **Hlas v v1 (původní):** přes diktování Windows (Win+H) do textových polí.
- **Hlas v v0.5 (nový):** Push-to-Talk nahrávání. Ukládá zvuk do WAV a posílá ho přes Gemini API `inlineData` `audio/wav` s promptem pro přepis. Nevyžaduje žádné dodatečné API klíče (využije stávající Gemini klíč) a má skvělé výsledky.
- **Nahrávání zvuku:** realizováno přes P/Invoke multimediální knihovny `winmm.dll` (MCI příkaz `mciSendString`), což zajišťuje nahrávání bez nutnosti stahovat externí NuGet balíčky, čímž udržujeme build 100% čistý a lehký.
- **Ikona aplikace (v0.6):** nastavena jako `<ApplicationIcon>` v `.csproj` a zakompilována do exe. Okna ji načítají přes `Icon.ExtractAssociatedIcon(Application.ExecutablePath)`, což je dynamické a spolehlivé.
- **Lokální podepisování (v0.6):** protože nákup komerčního certifikátu je drahý, dodáváme skript `PodepsatAplikaci.ps1`. Ten lokálně vytvoří samopodepsaný certifikát a podepíše s ním soubor. Po jednorázovém naimportování tohoto certifikátu jako důvěryhodného kořenového certifikátu se SmartScreen varování na daném počítači přestane zobrazovat.
- **Verzování:** každé rozhodnutí zvyšuje číslo verze specifikace a zapisuje se do logu (dle kap. 7 „živý kontrakt").
- **JSON export:** stabilní struktura (sekce → položky → predpoklad flag), aby ji později mohl číst orchestrátor beze změn.
- **Náhled specifikace (v0.2):** vlastní mini-převod markdown→RTF přímo v MainForm (žádná externí knihovna – jednodušší build, žádné závislosti); při chybě fallback na plain text.
- **Git na ploše:** připojená složka nepodporuje mazání/zámky souborů → commit+push se dělá ze sandboxu, ne přímo ze složky.
- **GitHub přístup:** Jendův fine-grained token (jen repo CodePlannerWin, Contents RW) is uložený v `.github_token` ve složce projektu, je v .gitignore a do repa nesmí. Používat pro push i releases. Až vyprší, vygenerovat nový stejným postupem.
- **Kontrola konzistence (v0.3):** pravidlová, bez AI – porovnává klíčová slova bez diakritiky. Falešný poplach je OK, mlčení ne. Vlastní odstranění diakritiky (mapovací tabulka), protože `string.Normalize()` nefunguje s InvariantGlobalization na Linuxu.
- **Šablony otázek (v0.4):** přepínání šablon automaticky přizpůsobuje znění a nápovědy. Pro zjednodušení si systém při změně šablony zachovává uživatelské ruční odpovědi, ale mění výchozí nepotvrzené předpoklady (`JePredpoklad = true`) na odpovídající hodnoty z nové šablony.
- **Sandbox (pro Clauda):** mount občas servíruje starou velikost souboru (čtení je uříznuté) → před buildem ověřovat grep počty; při zaseknutí rekonstruovat soubory v /tmp/build a pushovat z nich, ne z mountu.
- **Refaktoring do angličtiny (v2.1.0):** kódové struktury a názvy typů byly plně poangličtěny z důvodu mezinárodní konzistence kódu a snadnější integrace s AI vývojovými agenty, při zachování zpětné kompatibility načítání starých českých souborů.

## 📁 Stav souborů
- `CodePlanner/Core/SpecCore.cs` – jádro: business pravidla, persistence a rendering (Markdown, HTML, JSON)
- `CodePlanner/Core/Models.cs` – datové modely a enums s plnou nullable anotací
- `CodePlanner/Core/StandardQuestions.cs` – vestavěné otázky pro šablony
- `CodePlanner/Core/ConsistencyChecker.cs` – offline pravidla kontroly konzistence (poangličtěno)
- `CodePlanner/Core/TemplateService.cs` – správa custom šablon a sablony.json
- `CodePlanner/Core/GeminiService.cs` – AI integrace (deserializace, strukturované výstupy v angličtině)
- `CodePlanner/Core/VoiceRecorder.cs` – P/Invoke nahrávání zvuku z mikrofonu (winmm.dll)
- `CodePlanner/DesignSystem.cs` – barvy a písma pro UI chránící před GDI leakem
- `CodePlanner/ProjectTypeComboItem.cs` – model položek ComboBoxu pro výběr šablony
- `CodePlanner/MainForm.cs` – hlavní formulář GUI (UI koordinace, log, vyhledávání, chat)
- `CodePlanner/MainForm.ProjectLifecycle.cs` – správa životního cyklu projektu (ukládání, otevírání, export, nedávné a přílohy)
- `CodePlanner/SettingsForm.cs` – nastavení Gemini API
- `CodePlanner/Program.cs` – vstupní bod
- `CodePlanner/CodePlanner.csproj` – konfigurace buildu
- `CoreTests/` – automatické testy jádra (175 kontrol, testována všechna nová a refaktorovaná pravidla)
- `PodepsatAplikaci.ps1` – utilita pro lokální podepisování exe k obcházení SmartScreen
- `CodePlanner-v2.2.0.zip` – finální hotová aplikace v2.2.0 + návod CTI_ME.txt
