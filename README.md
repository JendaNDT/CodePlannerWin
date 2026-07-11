# CodePlanner v2.2

Windows aplikace (.exe), která z volně popsaného nápadu udělá strukturovanou, verzovanou specifikaci a agilní backlog připravený pro kódovací agenty.

Aplikace využívá **Gemini API** k hluboké sémantické analýze, automatickému generování uživatelských příběhů (User Stories), odhadu pracnosti a rizik a hlasovému diktování.

## Hlavní funkce (v2.2)

- **Interaktivní tvorba specifikace**: Zápis nápadu textem, nebo diktováním přímo v aplikaci (s vizuálním indikátorem nahrávání a automatickým přepisem).
- **AI Asistent (Chat)**: Integrovaný chat s kontextem aktuálního projektu pro ladění detailů.
- **Doplňující otázky na míru**: Gemini navrhne otázky specifické pro váš nápad, včetně rychlých voleb odpovědí.
- **Hloubková AI kontrola konzistence**: Odhalování logických rozporů a bezpečnostních děr pomocí LLM.
- **Generování User Stories**: Automatické rozpadnutí specifikace na agilní backlog (exportovatelný do Markdown a CSV pro Jira/Trello).
- **Metriky a odhady**: AI odhad pracnosti (v hodinách), složitosti, složení týmu, doporučeného rozpočtu a technického rozboru.
- **Robustní exporty**:
  - **Markdown** (strukturovaný dokument pro čtení)
  - **JSON** (v2.2 formát, stabilní struktura v angličtině pro agenty)
  - **HTML** (interaktivní dashboard s vyhledáváním a tmavým režimem, včetně backlogu a logu změn)
  - **PDF** (profesionální tiskový export s titulní stranou a záhlavím/zápatím)
- **Bezpečné uložení API klíče**: Možnost zadat klíč v rozhraní (přenáší se v hlavičkách `x-goog-api-key`, ne v URL) a otestovat připojení přímo v nastavení.

## Stažení a spuštění

Hotové `CodePlanner.exe` najdeš v [Releases](../../releases). Aplikace je plně přenositelná (portable) – stačí ji spustit na Windows 10/11.

## Stavba ze zdrojáků

Vyžaduje .NET 8 SDK:

```bash
cd CodePlanner
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Testy jádra (174 automatických kontrol):

```bash
cd CoreTests
dotnet run -c Release
```

## Struktura projektu

| Cesta | Obsah |
|---|---|
| `CodePlanner/Core/SpecCore.cs` | Jádro: model specifikace, šablony typů projektů, exporty MD/JSON/HTML |
| `CodePlanner/Core/GeminiService.cs` | Komunikace s Gemini API a zpracování strukturovaných JSON odpovědí |
| `CodePlanner/Core/VoiceRecorder.cs` | Nahrávání zvuku přes MCI rozhraní (winmm.dll) |
| `CodePlanner/DesignSystem.cs` | Centrální barvy a písma zabraňující únikům GDI v GUI |
| `CodePlanner/ProjectTypeComboItem.cs` | Pomocný model pro ComboBox šablon |
| `CodePlanner/MainForm.cs` | Hlavní formulář a koordinátor GUI |
| `CodePlanner/NalezyForm.cs` | Dialog kontroly konzistence a AI analýzy |
| `CodePlanner/UserStoriesForm.cs` | Dialog backlogu, generování a exportu User Stories |
| `CodePlanner/MetrikyForm.cs` | Dialog AI odhadů pracnosti a architektury |
| `CodePlanner/PdfExporter.cs` | Modul pro tiskový export do PDF |
| `CodePlanner/SettingsForm.cs` | Nastavení Gemini API s odkazem do AI Studio a testem připojení |
| `CoreTests/` | 174 automatických jednotkových testů |

---
*Vzniklo v kooperaci člověka s AI (Antigravity).*
