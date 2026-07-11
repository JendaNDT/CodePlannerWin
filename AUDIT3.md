# CodePlanner v2.1.0 – finální komplexní audit

*Audit proveden 11. 7. 2026 nad aktuálním obsahem větve `main`. Starší soubory `AUDIT.md`, `AUDIT1.md` a `NAVRHY_WORKFLOW.md` nebyly brány jako důkaz; nálezy byly znovu ověřeny v kódu.*

## Krátké shrnutí

CodePlanner má překvapivě solidní jádro: Release build je čistý, všech 174 deklarovaných kontrol skutečně prochází, ukládání projektu je atomické a síťová vrstva má timeout, opakování i storno. Současně ale v2.1.0 není bezpečné označit za finálně stabilní: největší regresí je rozpojení českých klíčů typů v GUI od anglických enumů v jádře, takže volby Hra/Evidence/Nástroj ve skutečnosti používají obecnou šablonu. Dvě další kritická rizika mohou ztratit nebo promíchat práci: otevření „Nedávného projektu“ obchází dotaz na neuložené změny a pozdní odpověď chatu či diktování se může zapsat do mezitím otevřeného jiného projektu. Doporučení je nejdřív opravit tyto integrační toky a doplnit end-to-end testy GUI↔jádro; teprve potom řešit přístupnost, menší displeje a rozdělení velkých souborů.

## Jak byl audit ověřen

- `dotnet build CodePlanner/CodePlanner.csproj -c Release`: **úspěch, 0 chyb, 0 varování**.
- `dotnet run --project CoreTests/CoreTests.csproj -c Release`: **174/174 kontrol OK**.
- Testy pokrývají hlavně `SpecCore.cs` a `GeminiService.cs`; nekompilují formuláře, PDF ani hlasovou vrstvu (`CoreTests/CoreTests.csproj:11-14`). Výsledek proto není důkazem správnosti celého desktopového workflow.
- Audit UI je statická kontrola WinForms konstrukce a exportovaného HTML/PDF. Reálné vykreslení na Windows při různém DPI, práce tiskárny, mikrofon a skutečné Gemini odpovědi vyžadují ještě ruční test.

---

## 1. Vnitřní logika

### Silné stránky

- Datový model je srozumitelný a drží specifikaci, otázky, odpovědi, log, chat, stories i metriky na jednom místě (`CodePlanner/Core/SpecCore.cs:261-298`).
- Změny běžných odpovědí mají historii „bylo → je“, aktualizují verzi i čas (`CodePlanner/Core/SpecCore.cs:670-710`).
- Uložení používá dočasný soubor a při přepisu vytváří `.bak`, což výrazně snižuje riziko poškození jediné kopie (`CodePlanner/Core/SpecCore.cs:1231-1250`).
- Reference mají limit 2 MB, mockupy 4 MB a obrázek se před uložením skutečně dekóduje (`CodePlanner/MainForm.cs:2287-2303`, `2411-2439`).
- HTML export escapuje uživatelský obsah a čistí identifikátory User Stories před vložením do DOM/JavaScriptu (`CodePlanner/Core/SpecCore.cs:891-1144`).

### Kritické nálezy

1. **GUI typy projektu nejsou propojené s přeloženým jádrem.** Combo posílá `Obecna/Hra/Evidence/Nastroj` (`CodePlanner/MainForm.cs:2555-2564`), ale enum a parser znají `General/Game/Registry/Tool` (`CodePlanner/Core/SpecCore.cs:79-85`, `141-192`). `ChangeProjectType` proto u tří českých klíčů nastaví interně `General` (`CodePlanner/Core/SpecCore.cs:634-647`) a otázky, nápovědy i předpoklady spadnou na obecnou variantu. Testy tuto vazbu míjejí, protože volají přímo `ProjectType.Game` (`CoreTests/Program.cs:143-154`). **Dopad:** jedna z hlavních funkcí v GUI dává jiný výsledek, než uživatel zvolil.

2. **„Nedávné projekty“ mohou bez varování zahodit aktivní práci.** Běžné otevření nejdřív volá `PotvrdNeulozene()` (`CodePlanner/MainForm.cs:1257-1268`), ale kliknutí v historii jde přímo do `OtevritProjektCestu` (`CodePlanner/MainForm.cs:2531-2536`), kde se aktivní `_projekt` rovnou nahradí (`CodePlanner/MainForm.cs:1283-1287`). **Dopad:** jediný klik může nevratně ztratit změny od posledního uložení.

### Důležité nálezy

1. **Deklarovaná 100% kompatibilita starých `.vcbrief` není implementačně doložená.** Migrace přejmenovává klíče JSON, ale skalární hodnoty nechává beze změny (`CodePlanner/Core/SpecCore.cs:1276-1298`). Staré hodnoty enumu typu `Hra/Evidence/Nastroj` tak anglický `JsonStringEnumConverter` při deserializaci nemusí převést (`CodePlanner/Core/SpecCore.cs:602-607`, `1253-1261`). „Starý formát“ v testu obsahuje jen název a nové AI pole, nikoli český typ (`CoreTests/Program.cs:588-600`). **Dopad:** přesně ruční scénář uvedený jako příští krok v `PROJECT_STATUS.md:9` může skončit chybou načtení.

2. **Uložení přes Ctrl+S může obejít verzování názvu/nápadu.** `TextChanged` mění model a dirty flag hned, ale `Version`, `UpdatedAt` a log se mění až v `Leave` (`CodePlanner/MainForm.cs:408-427`, `539-559`). Ctrl+S volá uložení přímo, aniž by vynutil commit aktivního editoru (`CodePlanner/MainForm.cs:223-225`). Po následném `Leave` navíc vznikne nová změna logu bez opětovného nastavení `_dirty`. **Dopad:** obsah souboru, verze, log a detekce zastaralých odhadů se mohou rozcházet.

3. **Výstup AI má jen syntaktickou, ne doménovou validaci.** U dynamických otázek se po deserializaci kontroluje jen neprázdné ID a case-sensitive deduplikace (`CodePlanner/MainForm.cs:1897-1906`); neověřuje se null položka, text, sekce, rozsah 7–10 otázek, ID bez ohledu na velikost písmen ani rozumný počet možností (`CodePlanner/MainForm.cs:1918-1954`). User Stories obdobně přijmou prázdný nebo duplicitní backlog (`CodePlanner/Core/GeminiService.cs:723-742`). **Dopad:** validní, ale nekvalitní AI JSON může vytvořit neovladatelný nebo prázdný projekt a přepsat předchozí data.

4. **Varování o zastaralých User Stories je v produkčním toku nefunkční.** Kontrola vyžaduje `StoriesGenerationTimestamp` (`CodePlanner/Core/SpecCore.cs:732-734`), ale generování tento údaj nenastavuje (`CodePlanner/UserStoriesForm.cs:283-299`). Testy si timestamp vkládají ručně (`CoreTests/Program.cs:607-637`). **Dopad:** změněná specifikace může dál exportovat starý backlog bez upozornění.

5. **Načtený projekt nemá doménovou validaci.** Loader doinicializuje top-level seznamy, ale nekontroluje null prvky, duplicitní/prázdná ID ani velikost (`CodePlanner/Core/SpecCore.cs:1253-1270`). `Questions:[null]` následně spadne při práci se sekcí (`CodePlanner/Core/SpecCore.cs:588-599`), `Answers:[null]` v lookupu (`CodePlanner/Core/SpecCore.cs:714-715`). Aktivní model je navíc nahrazen dřív, než celý UI refresh bezpečně doběhne (`CodePlanner/MainForm.cs:1283-1308`).

6. **„Bezpečné uložení API klíče“ znamená jen bezpečnější přenos, ne disk.** Klíč se sice posílá v hlavičce (`CodePlanner/Core/GeminiService.cs:356-360`), ale celý `GeminiApiKey` se zapisuje prostým JSONem do `%AppData%\CodePlanner\settings.json` (`CodePlanner/Core/GeminiService.cs:79-93`). README to prezentuje jako bezpečné uložení (`README.md:20`). Pro lokální desktopový nástroj je vhodné DPAPI/Windows Credential Manager nebo preferovat proměnnou prostředí.

7. **V kořeni workspace je plaintext GitHub token.** Soubor `.github_token:1` je správně ignorovaný (`.gitignore:15`), ale má podle `PROJECT_STATUS.md:68` zápisová práva k repozitáři. Ignorování chrání před běžným commitem, ne před omylem při balení, synchronizací složky nebo lokálním malwarem. Distribuční ZIP byl zkontrolován a token neobsahuje.

### Kosmetické nálezy

- Titulek okna a toolbar stále zobrazují `v2.0.1`, přestože sestavení i dokumentace jsou 2.1.0 (`CodePlanner/MainForm.cs:309-314`, `1794-1798`; `CodePlanner/CodePlanner.csproj:12`).
- `UseAssumption` neodmítá prázdný výchozí předpoklad (`CodePlanner/Core/SpecCore.cs:693-703`); po nekvalitní vlastní/AI šabloně může být prázdná odpověď vykázána jako vyřešená.

---

## 2. Provázanost funkcí

### Silné stránky

- Závislosti mají převážně jednosměrný tvar: formuláře koordinují UI a volají `Core`; jádro na WinForms nezávisí. Klasickou kruhovou závislost mezi soubory jsem nenašel.
- `GeminiService` soustřeďuje síťovou komunikaci, `VoiceRecorder` MCI a `PdfExporter` tisk. Oproti jednomu monolitu je to dobrý základ pro další rozdělení.
- Všechny hlavní exporty čerpají ze stejného modelu a HTML/Markdown sdílejí helpery pro stav a zastaralost.

### Kritické nálezy

1. **Chat a diktování nejsou svázané s projektem, ve kterém začaly.** Chat předá službě původní objekt, ale po `await` zapisuje odpověď přes aktuální field `_projekt` (`CodePlanner/MainForm.cs:2651-2683`). Toolbar zůstává myší aktivní (`CodePlanner/MainForm.cs:267-300`), takže uživatel může mezitím otevřít/nově založit projekt. Diktování má obdobný problém: není v globálním busy stavu a pozdní přepis vloží do aktuálního textboxu (`CodePlanner/MainForm.cs:2165-2193`). **Dopad:** odpověď nebo přepis se může objevit v jiném projektu, než ke kterému patří.

### Důležité nálezy

1. **AI nálezy jsou datově připravené, ale funkčně „visí ve vzduchu“.** Model obsahuje `AiFindings` a `AiCheckTimestamp` (`CodePlanner/Core/SpecCore.cs:295-297`) a migrace jejich staré názvy (`CodePlanner/Core/SpecCore.cs:1340-1345`), avšak `NalezyForm` výsledky pouze vykreslí do lokálního `ListView` (`CodePlanner/NalezyForm.cs:181-196`). `MainForm` je po zavření nepřevezme (`CodePlanner/MainForm.cs:1737-1745`) a exporty počítají jen offline nálezy (`CodePlanner/Core/SpecCore.cs:814-820`, `1086-1098`). **Dopad:** hloubková kontrola zmizí zavřením dialogu.

2. **Stories timestamp je druhý nedokončený spoj.** Pole a renderování varování existují (`CodePlanner/Core/SpecCore.cs:295`, `732-737`), ale produkční generátor zapisuje jen list a log (`CodePlanner/UserStoriesForm.cs:283-299`). Test ověřuje pouze ručně sestavený stav. To je typický případ funkce, která je hotová v izolovaných vrstvách, ale ne end-to-end.

3. **JSON export nepřenáší část hodnotných výsledků aplikace.** `RenderJson` exportuje specifikaci, otevřené otázky, offline kontrolu a log, ale ne User Stories, metriky, AI nálezy, chat ani mockup (`CodePlanner/Core/SpecCore.cs:843-878`). HTML naopak stories a metriky zahrnuje (`CodePlanner/Core/SpecCore.cs:1031-1064`, `1106-1129`). Nemusí to být bug, ale pro deklaraci „JSON pro AI agenta“ je nutné rozhodnout, zda má agent dostat celý projekt, nebo jen specifikaci.

4. **Design systém není skutečně centrální.** Formuláře vedle `DesignSystem` používají jeho tokeny, ale `MainForm` duplikuje celou paletu a vytváří desítky vlastních fontů (`CodePlanner/MainForm.cs:18-25`, `119-132`, `452-518`; `CodePlanner/DesignSystem.cs:8-29`). Změna brandu nebo kontrastu se proto nepropíše všude a tvrzení o plném napojení GUI v `PROJECT_STATUS.md:80` neodpovídá kódu.

5. **Chybí integrační testy přes veřejné workflow.** `CoreTests` zdrojově přilinkují pouze dva core soubory (`CoreTests/CoreTests.csproj:11-14`). Tím se neověřuje combo→core, formulář→timestamp, nedávné projekty→confirm, async projektový kontext ani export dialogy — právě místa, kde audit našel kritické chyby.

### Kosmetické nálezy

- Nevyužité/opuštěné prvky: `GeminiAnalysisResult` a `GeminiAnswer` nemají produkční použití (`CodePlanner/Core/GeminiService.cs:112-134`), `DesignSystemSmallItalic` se jen deklaruje (`CodePlanner/NalezyForm.cs:138-139`) a `ZmerVyskuOdstavce` nemá volající (`CodePlanner/PdfExporter.cs:345-354`).
- `AiFinding.FromFinding` používá pouze test, ne skutečný tok ukládání AI nálezů (`CodePlanner/Core/SpecCore.cs:252-257`; `CoreTests/Program.cs:603-605`).

---

## 3. Stabilita

### Silné stránky

- Síťová vrstva má sdílený `HttpClient`, timeout 90 s, dvě opakování pro 429/5xx a `CancellationToken` (`CodePlanner/Core/GeminiService.cs:220-228`, `326-415`).
- Hlavní AI analýza mění projekt až po obdržení výsledku, před přepsáním vytváří vratný snapshot a během práce vypíná většinu UI (`CodePlanner/MainForm.cs:1871-1895`, `1908-1976`, `1997-2033`).
- Autosave po dvou minutách a obnova po pádu jsou rozumně oddělené od ručního souboru (`CodePlanner/MainForm.cs:2750-2807`).
- Chat při chybě odstraní dočasnou uživatelskou zprávu z historie a vrátí text do vstupního pole (`CodePlanner/MainForm.cs:2686-2708`).

### Kritické nálezy

1. **Race condition při chatu/diktování může kontaminovat jiný projekt.** Detail je v sekci Provázanost; rozhodující řádky jsou `CodePlanner/MainForm.cs:2651-2683` a `2165-2193`. Oprava musí buď zamknout změnu projektu po dobu operace, nebo před zápisem ověřit identitu/generaci původního projektu a výsledek zahodit či nabídnout uživateli.

### Důležité nálezy

1. **Krátká hlasová nahrávka zůstane v `%TEMP%`.** Větev pod 0,4 s se vrátí před `finally`, ve kterém se WAV maže (`CodePlanner/MainForm.cs:2146-2163`, `2203-2213`). Opakované krátké pokusy hromadí soubory.

2. **Selhání MCI po otevření zařízení ho nemusí zavřít.** Při chybě `record recsound` se vyhodí výjimka bez `close recsound` (`CodePlanner/Core/VoiceRecorder.cs:23-40`). Další pokus starý alias zavře, do té doby ale může mikrofon zůstat rezervovaný.

3. **Diktování nemá storno síťového přepisu.** `TranscribeAudioAsync` token umí, ale `ZastavADiktuj` ho nepředává (`CodePlanner/MainForm.cs:2118-2213`; `CodePlanner/Core/GeminiService.cs:563-601`). Esc ruší jen `_ctsAi/_ctsChat` (`CodePlanner/MainForm.cs:196-213`). Uživatel může čekat až na timeout a mezitím měnit projekt.

4. **Zavření formulářů neruší běžící požadavky.** Hlavní `FormClosing` řeší uložení a fonty, ne `_ctsAi/_ctsChat` (`CodePlanner/MainForm.cs:173-185`). Dialogy Stories/Metriky/Nálezy také dovolí zavření bez `Cancel`; guardy zabrání pozdějšímu zápisu do disposed UI, ale síťová práce pokračuje do dokončení nebo timeoutu (`CodePlanner/UserStoriesForm.cs:269-323`, `CodePlanner/MetrikyForm.cs:299-359`, `CodePlanner/NalezyForm.cs:164-220`).

5. **Nevalidní projekt může zanechat UI v částečně nahrazeném stavu.** `_projekt` se přepíše před kompletním ověřením a refresh může vyhodit výjimku (`CodePlanner/MainForm.cs:1283-1308`). Bezpečnější je načíst, normalizovat a validovat do lokální proměnné, teprve potom atomicky přepnout aktivní projekt.

6. **Diagnostická AI odpověď může bez retence zůstat na disku.** Při chybě parsování se celý text uloží do `%AppData%\CodePlanner\posledni_ai_odpoved.txt` (`CodePlanner/Core/GeminiService.cs:247-281`). Může obsahovat citlivé části zadání; chybí upozornění, automatické smazání po úspěchu nebo omezení délky.

7. **Test runner sahá do produkčního profilu.** Test nastavení dočasně přepisuje skutečný `%AppData%\CodePlanner\settings.json` a obnoví ho až ve `finally` (`CoreTests/Program.cs:156-226`). Při násilném ukončení procesu může uživateli zůstat testovací klíč nebo historie. Testy by měly používat injektovanou/temp cestu.

### Kosmetické nálezy

- `HttpResponseMessage` není explicitně disponován (`CodePlanner/Core/GeminiService.cs:356-411`). Obsah se čte celé, takže nejde o okamžitý leak, ale `using` je správnější lifecycle při opakovaných požadavcích.
- Chyba validního, ale strukturálně vadného `sablony.json` je jen v `Debug.WriteLine`; uživatel neví, proč vlastní šablony zmizely (`CodePlanner/Core/SpecCore.cs:54-74`). Null šablona/`Questions` navíc může později spadnout v dereferenci (`CodePlanner/Core/SpecCore.cs:108-112`).
- PDF zalamuje pouze na mezerách, takže dlouhé URL/identifikátory přetečou mimo stránku (`CodePlanner/PdfExporter.cs:314-340`).

---

## 4. Čistota a přehlednost kódu

### Silné stránky

- Repozitář je malý, bez externích NuGet závislostí a základní rozdělení `Core`, formuláře a exportéry se rychle orientuje.
- Názvy datových modelů a veřejných core metod jsou po v2.1 převážně anglické a mají XML summary na hlavních typech.
- Nejsou přítomny `TODO`, `FIXME`, `HACK`, `NotImplementedException` ani zjevné kruhové souborové závislosti.
- Release build nemá compiler warnings.

### Kritické nálezy

- **Žádný samostatný kritický nález čistoty; kritické chyby vznikají hlavně na hranicích modulů.** Právě to ale ukazuje, že chybí testy těchto hranic.

### Důležité nálezy

1. **Dva soubory zůstávají „god objects“.** `MainForm.cs` má 2 955 řádků a současně staví UI, spravuje projekt, persistence workflow, chat, diktování, přílohy, recent menu, autosave i rendering. `SpecCore.cs` má 1 778 řádků a míchá modely, šablony, business pravidla, tři exporty, migraci a kontrolu konzistence. Výsledkem je vysoká kognitivní zátěž a snadné přehlédnutí integrační chyby, jak ukázaly klíče typů.

2. **`Nullable` je vypnuté v aplikaci i testech** (`CodePlanner/CodePlanner.csproj:8`, `CoreTests/CoreTests.csproj:8`). Kód přitom intenzivně pracuje s null u deserializace, AI a ovládacích prvků. Zapnutí po modulech by kompilátor nechalo najít část pádů z vadného JSONu dřív než runtime.

3. **Jazyk a styl nejsou po „kompletním překladu“ konzistentní.** Core typy jsou anglické, ale většina metod a fieldů formulářů zůstává česky (`CodePlanner/MainForm.cs:27-99`, `1222-1495`), prompty a JSON schémata Gemini používají české property names (`CodePlanner/Core/GeminiService.cs:670-740`, `808-872`) a dokumentace tvrdí kompletní angličtinu (`PROJECT_STATUS.md:43`, `72`, `80`). Nejde o funkční problém sám o sobě, ale tvrzení mate dalšího vývojáře a přispělo k regresi klíčů.

4. **Testy jsou monolitický vlastní runner, ne izolovaná test suite.** 174 „kontrol“ jsou jednotlivé asserty v jednom `Program.cs`; chybí pojmenované test cases, fixture izolace a přímé testy UI kontraktů. Počet 174 proto působí silněji než skutečné pokrytí (`CoreTests/Program.cs:8-640`).

5. **Pevné rozměry, barvy a fonty jsou rozptýlené.** `MainForm` obsahuje množství `new Size`, `Width`, `Height`, `Padding`, lokálních barev a fontů (`CodePlanner/MainForm.cs:129-170`, `355-620`, `804-984`). To ztěžuje DPI opravy a konzistentní změny designu.

6. **AI služba je obtížně izolovatelná v testech.** Statický `GeminiService` v jednom typu spojuje transport, retry, parsování, DTO i prompty a vytváří `HttpClient` natvrdo (`CodePlanner/Core/GeminiService.cs:220-228`, `337-873`). Bez injektovaného transportu nelze deterministicky testovat timeouty, 429/5xx a vadné odpovědi.

7. **Invariant verzování není centrálně vynucený.** Běžné rozhodnutí jde přes `LogChange`, který aktualizuje verzi, čas i log (`CodePlanner/Core/SpecCore.cs:705-710`), ale Stories a Metriky zapisují `ChangeLog` přímo a callback nastaví jen dirty (`CodePlanner/UserStoriesForm.cs:290-299`, `CodePlanner/MetrikyForm.cs:327-335`, `CodePlanner/MainForm.cs:1788-1792`). Dokumentované „každé rozhodnutí zvyšuje verzi“ proto neplatí univerzálně.

### Kosmetické nálezy

- Duplicitní XML summary nad `GeminiSettings` (`CodePlanner/Core/GeminiService.cs:14-19`).
- Zastaralý text verze na dvou místech místo jedné hodnoty z assembly (`CodePlanner/MainForm.cs:309-314`, `1794-1798`).
- Směs `this.Property`, holých properties, českých/anglických komentářů a různých stylů `using` zhoršuje konzistenci, i když build neovlivňuje.
- Statické fonty `DesignSystem` jsou procesní singletony bez explicitního shutdown dispose (`CodePlanner/DesignSystem.cs:19-29`); růstový leak to není, ale tvrzení „zamezuje GDI leakům“ je příliš absolutní, protože `MainForm` dál vytváří lokální fonty.
- Repozitář nemá solution ani CI workflow; build a testy jsou odkázané na ruční spuštění podle README.

---

## 5. UX

### Silné stránky

- Hlavní tok je vizuálně označen jako nápad → otázky → specifikace; po odpovědi se automaticky vybere další otevřená otázka (`CodePlanner/MainForm.cs:451-568`, `804-975`, `1524-1535`).
- Uživatel má předpoklady, rychlé volby, barevný postup, hledání a srozumitelný souhrn před neúplným exportem (`CodePlanner/MainForm.cs:1562-1625`, `1640-1709`, `2882-2904`).
- Destruktivní AI analýza férově popisuje mazání odpovědí/stories/metrik a vytváří vratnou zálohu (`CodePlanner/MainForm.cs:1861-1885`).
- Chat vrací neodeslaný text po chybě a průběh AI ukazuje čas i možnost Esc (`CodePlanner/MainForm.cs:2686-2708`, `2930-2940`).
- Stories a metriky mají použitelné prázdné stavy a zakazují export/kopírování bez dat (`CodePlanner/UserStoriesForm.cs:199-223`, `CodePlanner/MetrikyForm.cs:251-263`).

### Kritické nálezy

1. **Ztráta práce přes Nedávné projekty** je přímé UX selhání důvěry (`CodePlanner/MainForm.cs:2531-2536`). Viz Vnitřní logika.

2. **Pozdní chat/diktování může skončit v jiném projektu** a uživatel nemá informaci, že kontext mezitím přestal platit (`CodePlanner/MainForm.cs:2651-2683`, `2165-2193`).

### Důležité nálezy

1. **Diktování nejde ovládat klávesnicí.** Funkce je napojená na `MouseDown/MouseUp`, zatímco standardní klávesnicové `Click` je prázdné (`CodePlanner/MainForm.cs:480-483`, `929-932`, `2036-2116`).

2. **Klikací bannery nejsou skutečné ovládací prvky.** API banner a banner nálezů jsou `Label` s mouse `Click`, bez klávesové aktivace a přístupné role (`CodePlanner/MainForm.cs:151-163`, `653-663`). Uživatel bez myši se k jejich akcím nedostane.

3. **Vedlejší AI dialogy bez klíče jsou slepá ulička.** Tlačítko se pouze zakáže a text řekne „chybí API klíč“ (`CodePlanner/UserStoriesForm.cs:102-106`, `CodePlanner/MetrikyForm.cs:242-246`, `CodePlanner/NalezyForm.cs:99-103`), ale modalita blokuje hlavní banner/nastavení. Prázdný stav Stories přitom radí kliknout právě na zakázané tlačítko (`CodePlanner/UserStoriesForm.cs:217-223`). Přidejte přímo akci „Otevřít nastavení“.

4. **Kontrola bez nálezů nemá jasný success/empty stav.** `ListView` zůstane prázdný a obecný status popisuje princip, nikoli výsledek „Bez nálezů“ (`CodePlanner/NalezyForm.cs:76-83`, `135-161`).

5. **Toolbar je přeplněný a má slabou hierarchii.** Současně ukazuje nový/otevřít/uložit, čtyři exporty, stories, metriky, kontrolu, undo analýzy, nastavení, nápovědu, dlouhý tip diktování a verzi (`CodePlanner/MainForm.cs:267-314`). Pro začátečníka by pomohla primární akce a seskupené menu Export/Další nástroje.

6. **Zpětná vazba je nekonzistentní.** Hlavní analýza záměrně používá jen status (`CodePlanner/MainForm.cs:1975-1976`), ale Stories a Metriky po úspěchu přidají modální MessageBox (`CodePlanner/UserStoriesForm.cs:298-300`, `CodePlanner/MetrikyForm.cs:334-336`). Stejný typ operace by měl mít stejný vzor.

7. **Stav „zastaralé Stories“ se nikdy neukáže.** UI/export tak působí jistě, i když backlog vznikl pro starší specifikaci (`CodePlanner/Core/SpecCore.cs:732-737`; `CodePlanner/UserStoriesForm.cs:283-299`).

### Kosmetické nálezy

- Text „Checkte mikrofon“ je neuhlazený (`CodePlanner/MainForm.cs:49`; další použití v okolí `1447`).
- Verze 2.0.1 v okně snižuje důvěru v balíček označený jako 2.1.0 (`CodePlanner/MainForm.cs:309`, `1797`).
- Úspěchy exportů a kopírování často používají další modální okna; u častého workflow by stačil status s akcí „Otevřít soubor/složku“.

---

## 6. UI

### Silné stránky

- Paleta a typografická škála mají jasný základ v `DesignSystem` (`CodePlanner/DesignSystem.cs:6-35`).
- Hlavní rozhraní používá docking, split containery, resizable log a PerMonitorV2 DPI (`CodePlanner/Program.cs:9-14`, `CodePlanner/MainForm.cs:129-170`, `355-379`).
- HTML export má viewport, světlý/tmavý motiv, systémovou detekci a základní přechod dvou sloupců na jeden pod 900 px (`CodePlanner/Core/SpecCore.cs:887-927`, `960-983`).
- Vstupní hodnoty jsou v HTML escapované; vizuální web tak není snadno rozbitelný běžným textem projektu.

### Kritické nálezy

- **Žádný samostatný kritický vizuální bug nebyl statickou kontrolou doložen.** Kritické UI dopady jsou funkční: špatná šablona, ztráta práce a async kontaminace.

### Důležité nálezy

1. **Kontrast hlavních CTA nevyhovuje běžnému textu.** Bílá na teal `#17B0A0` má přibližně 2,71:1 místo 4,5:1. Týká se např. Analyzovat, Uložit odpověď a Uložit nastavení (`CodePlanner/DesignSystem.cs:10`; `CodePlanner/MainForm.cs:452-464`, `891-902`; `CodePlanner/SettingsForm.cs:170-182`). Použijte tmavší teal nebo tmavý text.

2. **Owner-drawn stav otázek není dostupný čtečkám.** Fajfka/předpoklad a dopad V/S se jen kreslí, nejsou součástí přístupného názvu položky (`CodePlanner/MainForm.cs:842-854`, `988-1067`). Formuláře obecně nenastavují `AccessibleName`, `AccessibleDescription` ani explicitní tab order na složených prvcích (`CodePlanner/MainForm.cs:381-441`, `842-880`).

3. **Hlavní okno není pro menší obrazovky.** Minimum `1100×720` (`CodePlanner/MainForm.cs:129-145`) je na malém notebooku či vysokém DPI těsné. WinForms verze nemá mobilní layout; „mobilní zkušenost“ lze reálně hodnotit jen u HTML exportu.

4. **Dialogy mají kolizní pevné šířky.** Metriky dovolí minimum 600 px, ale status má 350 px a pravá tlačítka dohromady 340 px (`CodePlanner/MetrikyForm.cs:39-43`, `187-234`). Stories nemají minimum, zatímco pravý footer má 580 px (`CodePlanner/UserStoriesForm.cs:36-46`, `149-163`). Při zmenšení nebo větším systémovém fontu hrozí ořez/překryv.

5. **Nastavení je pevný dialog `500×275`.** S `AutoScaleMode.Font` může při zvětšeném systémovém textu oříznout popisky či tlačítka (`CodePlanner/SettingsForm.cs:29-38`).

6. **HTML má jen částečnou responsivitu a sémantiku.** Media query mění pouze grid; absolutní přepínač motivu může na úzkém displeji překrýt nadpis (`CodePlanner/Core/SpecCore.cs:918-927`, `973-983`). Karty používají `<div>` místo nadpisů, search má jen placeholder a checkbox nemá `<label>` (`CodePlanner/Core/SpecCore.cs:929`, `985-986`, `1010-1018`, `1116-1127`).

7. **PDF není přístupné.** Obsah se kreslí přímo přes GDI/`PrintDocument`, bez tagované struktury nadpisů a popisů (`CodePlanner/PdfExporter.cs:29-54`, `57-311`). Pro běžný tisk je to přijatelné, pro screen reader ne.

### Kosmetické nálezy

- HTML generátor před `<body>` nevypíše `</head>`, takže prohlížeč musí nevalidní strukturu opravit (`CodePlanner/Core/SpecCore.cs:886-959`).
- Vyhledávání v HTML ruší standardní outline a nahrazuje ho jen slabým teal borderem (`CodePlanner/Core/SpecCore.cs:924-925`).
- Backlog checkbox má jen 16×16 px a desktopová AI/přílohová tlačítka výšku 22 px (`CodePlanner/Core/SpecCore.cs:947-950`; `CodePlanner/MainForm.cs:452-518`); pro dotyk a motorická omezení jsou cíle malé.
- Titulní strana PDF posune podtitul pevně o 100 px bez ohledu na počet řádků názvu a dlouhá slova se nerozdělí (`CodePlanner/PdfExporter.cs:100-113`, `314-340`).
- Lokální barvy/fonty v `MainForm` mohou časem vizuálně ujet od `DesignSystem` (`CodePlanner/MainForm.cs:18-25`, `119-132`).

---

## Doporučené další kroky

### P0 – opravit před dalším rozhodováním / reálným testem v2.1

1. **Sjednotit klíče typů na `General/Game/Registry/Tool`** v GUI, migraci i exportech. Přidat integrační test pro každou volbu v combu a test se skutečným starým českým `.vcbrief` včetně enumu.
2. **Zavřít cesty ke ztrátě/promíchání práce:** přidat `PotvrdNeulozene()` i do recent menu; během chatu a přepisu buď zamknout nový/otevřít, nebo výsledek podmínit identitou původního projektu. Diktování dostane vlastní CTS a Esc.
3. **Dopojit rozpracované funkce:** při generování nastavit `StoriesGenerationTimestamp`; AI nálezy uložit do `ProjectSpecification` včetně timestampu a zahrnout je podle rozhodnutí do exportů.
4. **Sjednotit commit textových polí:** jedna metoda musí při TextChanged/Leave/Save atomicky aktualizovat obsah, `Version`, `UpdatedAt`, log a dirty flag. Přidat test scénáře „psát → Ctrl+S bez opuštění pole“.

### P1 – stabilizace a ochrana dat

5. Validovat načtený `.vcbrief`, `sablony.json` i AI DTO před nahrazením aktivního modelu: velikost, null prvky, povinné texty, unikátní ID, maximální počty.
6. Uložit Gemini klíč přes DPAPI/Credential Manager; repo token přesunout mimo projektovou složku. Diagnostickou AI odpověď omezit, po úspěchu mazat a popsat uživateli.
7. U hlasu vždy uklidit WAV/MCI ve `finally`, rušit požadavky při zavření oken a disponovat `HttpResponseMessage`.
8. Oddělit testovací settings path od produkčního `%AppData%`; doplnit testy formulářových koordinátorů a alespoň smoke test načtení/uložení přes skutečný `MainForm`.

### P2 – kvalita, UX a UI

9. Rozdělit `MainForm` na služby/controllery (project lifecycle, AI operations, attachments, recent projects) a `SpecCore` na model/persistence/migration/renderery/checker. Zapínat `Nullable` po modulech.
10. Opravit kontrast CTA, použít skutečná tlačítka místo klikacích labelů, doplnit přístupné názvy/stavy a klávesové diktování.
11. Seskupit toolbar, sjednotit feedback bez zbytečných modálů a přidat přímé „Nastavit API klíč“ do vedlejších dialogů.
12. Přestavět footery dialogů na pružný layout; pro HTML doplnit sémantické nadpisy/labely, mobilní header a validní `<head>`.

### Lze odložit

- Tagovaný přístupný PDF export, pokud PDF slouží jen k tisku a HTML/Markdown jsou dostupnou alternativou.
- Plnohodnotný „mobilní“ layout nativní WinForms aplikace; smysluplnější je doladit mobilní HTML export.
- Kosmetické sjednocení všech názvů/metod do angličtiny až po funkčních opravách; nejdřív je důležitá konzistence kontraktů, ne plošné přejmenování.

## Otevřené otázky pro Jendu

1. **Co přesně má znamenat JSON „pro AI agenta“?** Jen strukturovanou specifikaci, nebo celý projekt včetně Stories, metrik, AI nálezů a mockupu? Aktuálně tyto výsledky chybí.
2. **Mají být hloubkové AI nálezy trvalou součástí projektu?** Datový model naznačuje ano, aktuální UI je zahazuje.
3. **Jaké staré verze `.vcbrief` musí v2.1 opravdu načíst?** Pro spolehlivý test je potřeba uchovat jeden skutečný soubor z každého zásadního formátu, ne jen umělý minimální JSON.
4. **Je cílová obrazovka vždy desktop alespoň 1100×720?** Pokud ano, lze menší WinForms layout odložit a soustředit se na mobilní HTML. Pokud ne, dialogy i hlavní split potřebují responsivní přestavbu.
5. **Jak citlivá data se v projektech očekávají?** Podle odpovědi se rozhodne, zda stačí DPAPI pro klíč a lokální soubory, nebo je potřeba i šifrování `.vcbrief`, omezení diagnostiky a jasné upozornění před odesláním reference/mockupu do Gemini.

## Celkový verdikt

**Stav: „funkčně bohatý a dobře rozpracovaný kandidát, ale ještě ne finální stabilní v2.1“.** Jádro a exporty mají dobrý základ a automatické kontroly dávají hodnotu. Čtyři P0 opravy výše jsou relativně ohraničené a měly by přijít před přidáváním dalších funkcí; po nich dává smysl krátký ruční regresní průchod na Windows a teprve pak rozhodnutí o dalším směru.
