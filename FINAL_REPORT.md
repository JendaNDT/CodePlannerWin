# CodePlanner – Závěrečná zpráva k projektu (v2.0.0)

Tento dokument shrnuje kompletní vývoj, architekturu a funkce aplikace **CodePlanner** (původně *VoiceCoder Brief*). Aplikace byla úspěšně vyvinuta, otestována, lokálně podepsána a publikována do repozitáře a na GitHub Releases.

---

## 🎯 Přehled projektu
CodePlanner je lehký, přenosný (single-file `.exe`) desktopový asistent pro Windows, který pomáhá vývojářům a analytikům transformovat neformální nápady na detailní a technologicky proveditelné projektové specifikace, agilní backlog (User Stories) a časové/finanční odhady.

### Vývojový cyklus (Verze v0.1 – v2.0):
- **v0.1 – Jádro**: 10 řízených otázek, rozdělení do 7 sekcí specifikace, log změn, ukládání `.vcbrief`, exports MD/JSON.
- **v0.2 – Facelift**: Formátovaný RTF náhled (barevné odlišení předpokladů), barevný seznam otázek, progress bar, klávesové zkratky (Ctrl+Enter, Ctrl+S, Ctrl+P).
- **v0.3 – Kontrola konzistence**: 9 offline pravidel hlídajících logické rozpory a rizika ve specifikaci s vizuálním panelem nálezů.
- **v1.4 – PDF Export**: Profesionální vektorový export specifikace přes GDI+ s titulní stranou a českou diakritikou (Segoe UI).
- **v1.5 – Agilní backlog**: Generování uživatelských příběhů (User Stories) přes Gemini API a export backlogu do Markdown a CSV (pro import do Jira / Trello).
- **v1.6 – AI Chat Asistent**: Integrovaný chat se znalostí celé specifikace a historie konverzace přímo v `.vcbrief`.
- **v1.7 – Analýza skic (Mockupů)**: Nahrávání a zobrazení obrázků (PNG/JPG), které Gemini bere jako vizuální kontext.
- **v1.8 – AI Odhady a metriky**: Výpočet člověkohodin, složitosti, týmu, rozpočtu, technického rozboru a rizik odhadu.
- **v1.9 – HTML micro-site**: Export celého projektu do jednoho interaktivního HTML souboru s přepínačem tmavého režimu, live search a odškrtávacím backlogem.
- **v2.0 – Rebranding**: Kompletní přejmenování projektu, složek, namespaces a sestavení na **CodePlanner** a publikace na GitHub.

---

## 🏗️ Architektura a kód
Projekt je postaven na moderním a udržitelném stacku **C# / .NET 8** s Windows Forms s ohledem na vysoké DPI (PerMonitorV2).

### Klíčové komponenty (`CodePlanner`):
- [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs): Definuje datové modely (`SpecProjekt`, `Odpoved`, `Otazka`, `ProjektMetriky`, `UserStory`), provádí JSON serializaci a spouští offline konzistenční kontroly. Obsahuje generátory HTML a Markdownu.
- [GeminiService.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/GeminiService.cs): Zabezpečuje asynchronní komunikaci s Gemini API (`gemini-2.5-flash`), definuje JSON schémata pro strukturované AI výstupy a promptuje asistentovy odpovědi.
- [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs): Hlavní prezentační vrstva. Řídí dynamické dotazování, vykresluje RichText náhledy a obsluhuje toolbarové nástroje a asynchronní chat.
- [HlasovyVstup.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/HlasovyVstup.cs): Zajišťuje nahrávání zvuku z mikrofonu pomocí standardního rozhraní `winmm.dll`.

---

## 🧪 Testovací pokrytí (Unit Tests)
V projektu [CoreTests](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CoreTests) je implementováno celkem **128 automatických testů** pokrývajících:
- Správnost a postupy řízených otázek.
- Serializaci a roundtrip specifikací, backlogu, historie chatu, skic i projektových metrik.
- Validitu generovaného JSONu, Markdownu a HTML.
- Všechny kontrolní mechanismy offline kontroly konzistence.

> [!NOTE]
> Všechny testy v testovací sadě prošly úspěšně (**128/128 OK**).

---

## 📦 Distribuce a instalace
Aplikace je distribuována jako přenosný (portable) ZIP balíček. Nic se neinstaluje, stačí rozbalit a spustit.

- **Oficiální GitHub Vydání**: [CodePlanner v2.0.0 na GitHubu](https://github.com/JendaNDT/CodePlannerWin/releases/tag/v2.0.0)
- **Lokální ZIP archiv**: [CodePlanner_v2.0_Windows.zip](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner_v2.0_Windows.zip)
- **Rozbalená složka**: [CodePlanner_v2.0_Windows](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner_v2.0_Windows)

### Lokální podpis aplikace (SmartScreen):
Při prvním spuštění `.exe` souboru na novém počítači se může zobrazit upozornění Windows SmartScreen. V balíčku je přiložen skript `PodepsatAplikaci.ps1`, který vytvoří lokální podpisový certifikát a soubor `CodePlanner.exe` trvale podepíše, čímž se varování navždy odstraní.

---

## 📘 Uživatelský návod (Quick Start)
1. Spusťte `CodePlanner.exe`.
2. V horním panelu zvolte **⚙ Nastavení AI…** a zadejte svůj Gemini API klíč.
3. Napište nebo nadiktujte (pomocí klávesové zkratky **Win+H** v textovém poli) svůj původní nápad.
4. Odpovídejte na otázky v levém sloupci. Můžete využít **AI Quick Options** k rychlému generování odpovědí jedním kliknutím.
5. Využijte **💬 AI Asistent (Chat)** k prodiskutování architektonických detailů.
6. Připojte vizuální nákres přes **🖼 Připojit skicu**.
7. Nechte si spočítat pracnost a rozpočet přes **📊 Metriky a Odhad…** a vygenerujte agilní backlog v **💡 User Stories…**.
8. Exportujte hotové dílo do **PDF**, **Markdownu**, **JSONu** pro agenty nebo jako **interaktivní HTML web** pro klienta.
