using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CodePlanner.Core;

internal static class Testy
{
    private static int _ok = 0;

    private static void Over(bool podminka, string popis)
    {
        if (!podminka) throw new Exception("TEST SELHAL: " + popis);
        _ok++;
        Console.WriteLine("  ok: " + popis);
    }

    private static void Main()
    {
        Console.WriteLine("== Testy jádra CodePlanner ==");

        string testSettingsPath = Path.Combine(Path.GetTempPath(), "codeplanner_test_settings.json");
        GeminiSettings.SettingsPathOverride = testSettingsPath;

        try
        {
            // --- základní stav ---
            var p = new ProjectSpecification { Name = "Hotelová evidence" };
        Over(StandardQuestions.All.Count == 11, "sada má 11 řízených otázek");
        Over(StandardQuestions.All.Count(o => o.Impact == Impact.High) == 6, "sada má 6 otázek s vysokým dopadem");
        Over(SpecificationService.GetNextUnansweredQuestion(p).Id == "cil-problem", "první otázka je cíl/problém");

        // --- nápad ---
        SpecificationService.SetIdea(p, "Udělej mi aplikaci na počítání hostů v hotelu.\nPotřebuju ji i bez internetu.");
        Over(p.Version == 2, "úprava nápadu zvýšila verzi specifikace");
        Over(p.ChangeLog.Any(l => l.Action == "Nápad"), "úprava nápadu je v logu");

        // --- odpovědi a předpoklady ---
        SpecificationService.AnswerQuestion(p, "cil-problem", "Recepce ztrácí přehled o počtu hostů; appka dá rychlý denní přehled.");
        SpecificationService.AnswerQuestion(p, "tech-platforma", "Windows počítač na recepci.");
        SpecificationService.UseAssumption(p, "rozsah-nongoals");
        Over(SpecificationService.GetAnsweredCount(p) == 2, "2 skutečné odpovědi");
        Over(SpecificationService.GetAssumptionsCount(p) == 1, "1 označený předpoklad");
        Over(SpecificationService.GetOpenQuestions(p).Count == 8, "8 otázek zůstává otevřených");
        Over(p.Version == 5, "každé rozhodnutí zvyšuje verzi (2+3=5)");

        // --- přepsání odpovědi ---
        SpecificationService.AnswerQuestion(p, "tech-platforma", "Windows počítač na recepci + později tablet.");
        Over(SpecificationService.GetAnsweredCount(p) == 2, "přepsání odpovědi neduplikuje záznam");
        Over(p.ChangeLog.Any(l => l.Action == "Změna odpovědi"), "přepsání odpovědi je v logu jako změna");

        // --- Markdown ---
        var md = SpecificationService.RenderMarkdown(p);
        Over(md.Contains("# Specification: Hotelová evidence"), "MD: nadpis s názvem projektu");
        foreach (var s in SpecificationService.SectionOrder)
            Over(md.Contains("## " + s), "MD: obsahuje sekci " + s);
        Over(md.Contains("[ASSUMPTION]"), "MD: předpoklad je viditelně označen");
        Over(md.Contains("> Udělej mi aplikaci"), "MD: původní nápad je citován");
        Over(md.Contains("## Open Questions"), "MD: sekce otevřených otázek");
        Over(md.Contains("## Decision Log"), "MD: log rozhodnutí");

        // --- JSON ---
        var json = SpecificationService.RenderJson(p);
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            Over(root.GetProperty("project").GetString() == "Hotelová evidence", "JSON: název projektu");
            Over(root.GetProperty("sections").GetArrayLength() == 7, "JSON: 7 sekcí");
            Over(root.GetProperty("openQuestions").GetArrayLength() == 8, "JSON: 8 otevřených otázek");
            Over(root.GetProperty("decisionLog").GetArrayLength() == p.ChangeLog.Count, "JSON: kompletní log");
            var technika = root.GetProperty("sections").EnumerateArray()
                .First(s => s.GetProperty("name").GetString() == "Technology");
            Over(technika.GetProperty("items").GetArrayLength() == 1, "JSON: Technology má 1 položku");
            Over(json.Contains("Hotelová"), "JSON: česká diakritika není escapovaná");
        }

        // --- uložení a načtení projektu ---
        string tmp = Path.Combine(Path.GetTempPath(), "test_projekt.vcbrief");
        SpecificationService.SaveProject(p, tmp);
        var p2 = SpecificationService.LoadProject(tmp);
        Over(p2.Name == p.Name, "roundtrip: název sedí");
        Over(p2.Answers.Count == p.Answers.Count, "roundtrip: počet odpovědí sedí");
        Over(p2.ChangeLog.Count == p.ChangeLog.Count, "roundtrip: log sedí");
        Over(p2.Version == p.Version, "roundtrip: verze sedí");
        Over(SpecificationService.RenderMarkdown(p2).Contains("[ASSUMPTION]"), "roundtrip: render funguje i po načtení");
        File.Delete(tmp);

        // --- prázdný projekt se vyrenderuje bez chyby ---
        var prazdny = new ProjectSpecification();
        var mdPrazdny = SpecificationService.RenderMarkdown(prazdny);
        Over(mdPrazdny.Contains("(unnamed project)"), "prázdný projekt: bezpečný nadpis");
        Over(mdPrazdny.Contains("(not entered yet"), "prázdný projekt: výzva k zadání nápadu");
        SpecificationService.RenderJson(prazdny);

        // --- kontrola konzistence ---
        var cisty = new ProjectSpecification { Idea = "Jednoduchá kalkulačka." };
        SpecificationService.AnswerQuestion(cisty, "tech-offline", "Plně offline.");
        Over(ConsistencyChecker.Check(cisty).Count == 0, "kontrola: čistý projekt bez nálezů");

        var k1 = new ProjectSpecification { Idea = "Appka se synchronizací do cloudu mezi zařízeními." };
        SpecificationService.AnswerQuestion(k1, "tech-offline", "Musí fungovat plně offline.");
        var n1 = ConsistencyChecker.Check(k1);
        Over(n1.Any(n => n.Title == "Offline vs. online" && n.Severity == Severity.Conflict), "kontrola: offline×cloud je rozpor");

        var k2 = new ProjectSpecification { Idea = "Evidence hostů: jméno a email každého hosta." };
        SpecificationService.AnswerQuestion(k2, "data-obsah", "Jen neosobní provozní data, bez osobních údajů.");
        Over(ConsistencyChecker.Check(k2).Any(n => n.Title == "Personal Data"), "kontrola: osobní údaje×bez osobních je rozpor");

        var k3 = new ProjectSpecification { Idea = "Program na skladovou evidenci." };
        SpecificationService.AnswerQuestion(k3, "rozsah-nongoals", "tisk sestav, statistiky prodeje");
        SpecificationService.AnswerQuestion(k3, "ux-styl", "Hlavní obrazovka se seznamem, detail položky, tisk sestav na konci měsíce.");
        Over(ConsistencyChecker.Check(k3).Any(n => n.Title == "Non-goal described as a goal"), "kontrola: non-goal zmíněný v UX je varování");

        var k4 = new ProjectSpecification();
        SpecificationService.AnswerQuestion(k4, "akceptace", "Až budou splněny všechny body specifikace.");
        Over(ConsistencyChecker.Check(k4).Any(n => n.Title == "Vague acceptance criteria"), "kontrola: vágní akceptace je varování");

        var k5 = new ProjectSpecification { Idea = "Chci export do CSV pro účetní." };
        SpecificationService.UseAssumption(k5, "data-export");
        Over(ConsistencyChecker.Check(k5).Any(n => n.Title == "Data export vs. no export"), "kontrola: export v nápadu × bez exportu je rozpor");

        var k6 = new ProjectSpecification();
        SpecificationService.UseAssumption(k6, "cil-problem");
        SpecificationService.UseAssumption(k6, "cil-uzivatele");
        SpecificationService.UseAssumption(k6, "tech-platforma");
        SpecificationService.AnswerQuestion(k6, "rozsah-funkce", "Chci nějaké základní skladové funkce.");
        var n6 = ConsistencyChecker.Check(k6);
        Over(n6.Any(n => n.Title.StartsWith("High number of assumptions")), "kontrola: 3 předpoklady s vysokým dopadem jsou varování");
        Over(n6.Any(n => n.Title == "Empty original idea"), "kontrola: prázdný nápad + odpovědi je varování");

        Over(SpecificationService.RenderMarkdown(k1).Contains("## Consistency Checks"), "MD: sekce kontroly konzistence při nálezech");
        Over(!SpecificationService.RenderMarkdown(cisty).Contains("## Consistency Checks"), "MD: sekce kontroly chybí bez nálezů");
        using (var docK = JsonDocument.Parse(SpecificationService.RenderJson(k1)))
            Over(docK.RootElement.GetProperty("consistencyCheck").GetArrayLength() >= 1, "JSON: pole kontrolaKonzistence");

        var kSqliteWeb = new ProjectSpecification();
        SpecificationService.AnswerQuestion(kSqliteWeb, "tech-platforma", "Funguje to ve webovém prohlížeči.");
        SpecificationService.AnswerQuestion(kSqliteWeb, "data-obsah", "Uložíme to do SQLite databáze.");
        Over(ConsistencyChecker.Check(kSqliteWeb).Any(n => n.Title == "SQLite database on web"), "kontrola: SQLite na webu je varování");

        var kAuth = new ProjectSpecification();
        SpecificationService.AnswerQuestion(kAuth, "cil-uzivatele", "Bude tam administrátor a běžný host.");
        SpecificationService.AnswerQuestion(kAuth, "tech-platforma", "Pouze desktop.");
        SpecificationService.AnswerQuestion(kAuth, "data-obsah", "Data budeme ukládat lokálně do souborů.");
        Over(ConsistencyChecker.Check(kAuth).Any(n => n.Title == "User roles without authentication"), "kontrola: role bez auth je varování");

        // --- šablony otázek ---
        var pSablony = new ProjectSpecification { Name = "Test šablon" };
        Over(pSablony.ProjectType == ProjectType.General, "výchozí typ projektu je Obecna");
        SpecificationService.UseAssumption(pSablony, "cil-problem");
        var odpObecna = SpecificationService.GetAnswerFor(pSablony, "cil-problem");
        Over(odpObecna.Text == StandardQuestions.Under(pSablony, "cil-problem").GetDefaultAssumption(ProjectType.General), "předpoklad odpovídá obecnému typu");

        SpecificationService.ChangeProjectType(pSablony, ProjectType.Game);
        Over(pSablony.ProjectType == ProjectType.Game, "změna typu projektu na Hra");
        var odpHra = SpecificationService.GetAnswerFor(pSablony, "cil-problem");
        Over(odpHra.Text == StandardQuestions.Under(pSablony, "cil-problem").GetDefaultAssumption(ProjectType.Game), "předpoklad se zaktualizoval na Hra");
        Over(pSablony.ChangeLog.Any(l => l.Action == "Typ projektu" && l.Detail.Contains("Změna typu projektu")), "záznam o změně typu v logu");

        // --- Gemini nastavení a prompt testy ---
        try
        {
            var settings = new GeminiSettings
            {
                GeminiApiKey = "test-key-12345",
                GeminiModel = "gemini-2.5-flash"
            };
            settings.Save();

            var loaded = GeminiSettings.Load();
            Over(loaded.GeminiApiKey == "test-key-12345", "nastavení: načtení API klíče funguje");
            Over(loaded.GeminiModel == "gemini-2.5-flash", "nastavení: načtení modelu funguje");
            Over(loaded.EffectiveApiKey == "test-key-12345", "nastavení: efektivní klíč vrací hodnotu z nastavení");

            Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-key-999");
            loaded.GeminiApiKey = "";
            Over(loaded.EffectiveApiKey == "env-key-999", "nastavení: fallback na proměnnou prostředí funguje");
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);

            // --- Testy historie nedávných projektů (v0.8) ---
            var settingsRef = new GeminiSettings();
            Over(settingsRef.RecentProjects != null, "nastavení: nedávné projekty nejsou null po inicializaci");

            settingsRef.AddRecentProject("c:\\projekty\\a.vcbrief");
            Over(settingsRef.RecentProjects.Count == 1, "nedávné: počet položek je 1");
            Over(settingsRef.RecentProjects[0] == "c:\\projekty\\a.vcbrief", "nedávné: nová položka je na začátku");

            settingsRef.AddRecentProject("c:\\projekty\\b.vcbrief");
            Over(settingsRef.RecentProjects.Count == 2, "nedávné: počet položek je 2");
            Over(settingsRef.RecentProjects[0] == "c:\\projekty\\b.vcbrief", "nedávné: druhá přidaná položka je na začátku");
            Over(settingsRef.RecentProjects[1] == "c:\\projekty\\a.vcbrief", "nedávné: první položka se posunula");

            // duplicitní přidání by mělo položku přesunout na začátek
            settingsRef.AddRecentProject("c:\\projekty\\a.vcbrief");
            Over(settingsRef.RecentProjects.Count == 2, "nedávné: duplicitní přidání nezvýšilo počet");
            Over(settingsRef.RecentProjects[0] == "c:\\projekty\\a.vcbrief", "nedávné: přesunutí na začátek");

            // naplníme více než 5 souborů
            settingsRef.AddRecentProject("c:\\projekty\\1.vcbrief");
            settingsRef.AddRecentProject("c:\\projekty\\2.vcbrief");
            settingsRef.AddRecentProject("c:\\projekty\\3.vcbrief");
            settingsRef.AddRecentProject("c:\\projekty\\4.vcbrief");
            settingsRef.AddRecentProject("c:\\projekty\\5.vcbrief");
            Over(settingsRef.RecentProjects.Count == 5, "nedávné: maximální počet položek je 5");
            Over(settingsRef.RecentProjects[0] == "c:\\projekty\\5.vcbrief", "nedávné: poslední přidaný je na indexu 0");
            Over(!settingsRef.RecentProjects.Contains("c:\\projekty\\a.vcbrief"), "nedávné: nejstarší byl vytlačen");

            // odebrání položky
            settingsRef.RemoveRecentProject("c:\\projekty\\3.vcbrief");
            Over(settingsRef.RecentProjects.Count == 4, "nedávné: odebrání snížilo počet na 4");
            Over(!settingsRef.RecentProjects.Contains("c:\\projekty\\3.vcbrief"), "nedávné: odebraný prvek chybí");
        }
        finally
        {
            if (File.Exists(testSettingsPath)) File.Delete(testSettingsPath);
        }

        var prompt = GeminiService.SestavPrompt("Chci vytvořit jednoduchou kalkulačku", ProjectType.General.ToString());
        Over(!string.IsNullOrWhiteSpace(prompt), "prompt: sestavení promptu vrací neprázdný text");
        Over(prompt.Contains("Chci vytvořit jednoduchou kalkulačku"), "prompt: obsahuje původní nápad");
        Over(prompt.Contains("cil-problem"), "prompt: obsahuje ID otázky");
        Over(prompt.Contains("Default assumption: Fully online"), "prompt: obsahuje výchozí předpoklady");

        // --- Testy referenčních podkladů (v0.7) ---
        var promptSReferenci = GeminiService.SestavPrompt("Nápad", ProjectType.General.ToString(), "Toto jsou referenční specifikace.");
        Over(promptSReferenci.Contains("Toto jsou referenční specifikace."), "prompt: obsahuje referenční podklady");

        var pRef = new ProjectSpecification
        {
            Name = "Test reference",
            Idea = "Nápad",
            ReferenceText = "Dokumentace k API v1",
            ReferenceName = "api.txt"
        };

        string mdRef = SpecificationService.RenderMarkdown(pRef);
        Over(mdRef.Contains("Reference Materials (api.txt)"), "MD: obsahuje sekci referenčních podkladů");
        Over(mdRef.Contains("Dokumentace k API v1"), "MD: obsahuje text referenčních podkladů");

        string jsonRef = SpecificationService.RenderJson(pRef);
        Over(jsonRef.Contains("\"referenceText\": \"Dokumentace k API v1\""), "JSON: obsahuje referencniText");
        Over(jsonRef.Contains("\"referenceName\": \"api.txt\""), "JSON: obsahuje referencniNazev");

        // Otestujeme uložení a načtení (roundtrip)
        string tmpCestaRef = Path.Combine(Path.GetTempPath(), "test_ref.vcbrief");
        try
        {
            SpecificationService.SaveProject(pRef, tmpCestaRef);
            var nactenyRef = SpecificationService.LoadProject(tmpCestaRef);
            Over(nactenyRef.ReferenceText == "Dokumentace k API v1", "roundtrip: referencniText sedí");
            Over(nactenyRef.ReferenceName == "api.txt", "roundtrip: referencniNazev sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaRef)) File.Delete(tmpCestaRef);
        }

        // --- Testy vlastních šablon (v0.9) ---
        var customSablona = new ProjectTemplate
        {
            Key = "discord-bot",
            Name = "Discord Bot",
            Questions = new List<TemplateQuestion>
            {
                new TemplateQuestion
                {
                    Id = "cil-problem",
                    Text = "Jaké příkazy má Discord bot umět?",
                    HelpText = "Nápověda k botovi.",
                    DefaultAssumption = "Základní příkazy."
                }
            }
        };

        TemplateService.CustomTemplates.Add(customSablona);

        var pCustom = new ProjectSpecification
        {
            Name = "Test bot",
            Idea = "Nápad na bota",
            ProjectTypeKey = "discord-bot"
        };

        var otCil = StandardQuestions.Under(pCustom, "cil-problem");
        Over(otCil.GetText(pCustom.ProjectTypeKey) == "Jaké příkazy má Discord bot umět?", "custom: přepsaný text otázky");
        Over(otCil.GetHelpText(pCustom.ProjectTypeKey) == "Nápověda k botovi.", "custom: přepsaná nápověda");
        Over(otCil.GetDefaultAssumption(pCustom.ProjectTypeKey) == "Základní příkazy.", "custom: přepsaný předpoklad");

        var otTech = StandardQuestions.Under(pCustom, "tech-platforma");
        Over(otTech.GetText(pCustom.ProjectTypeKey) == otTech.GetText("Obecna"), "custom fallback: text otázky z Obecna");
        Over(otTech.GetDefaultAssumption(pCustom.ProjectTypeKey) == otTech.GetDefaultAssumption("Obecna"), "custom fallback: předpoklad z Obecna");

        var pCustomZmena = new ProjectSpecification();
        SpecificationService.ChangeProjectType(pCustomZmena, "discord-bot");
        Over(pCustomZmena.ProjectTypeKey == "discord-bot", "custom: změna typu projektu klíče");
        
        SpecificationService.UseAssumption(pCustomZmena, "cil-problem");
        var odpCil = SpecificationService.GetAnswerFor(pCustomZmena, "cil-problem");
        Over(odpCil.Text == "Základní příkazy.", "custom: použitý předpoklad z vlastní šablony");

        Over(pCustomZmena.ChangeLog.Any(l => l.Action == "Typ projektu" && l.Detail.Contains("Změna typu projektu z Obecná aplikace na Discord Bot")), "custom: správná zpráva v logu změn");

        TemplateService.CustomTemplates.Clear();

        // --- Testy dynamických otázek (v1.0) ---
        var pDyn = new ProjectSpecification
        {
            Name = "Dynamicky projekt",
            Idea = "Dynamicky napad"
        };

        pDyn.Questions.Add(new Question
        {
            Id = "dyn-q1",
            Section = "Technika",
            Impact = Impact.High,
            Text = "Question 1?",
            HelpText = "HelpText 1",
            DefaultAssumption = "Predpoklad 1"
        });
        pDyn.Questions.Add(new Question
        {
            Id = "dyn-q2",
            Section = "Data",
            Impact = Impact.Medium,
            Text = "Question 2?",
            HelpText = "HelpText 2",
            DefaultAssumption = "Predpoklad 2"
        });

        var dynQuestions = SpecificationService.GetProjectQuestions(pDyn).ToList();
        Over(dynQuestions.Count == 2, "dynamic: projekt má přesně 2 otázky");
        Over(dynQuestions[0].Id == "dyn-q1", "dynamic: první otázka sedí");

        Over(SpecificationService.GetNextUnansweredQuestion(pDyn).Id == "dyn-q1", "dynamic: další nezodpovězená je první");
        Over(SpecificationService.GetAnsweredCount(pDyn) == 0, "dynamic: 0 zodpovězených");

        SpecificationService.AnswerQuestion(pDyn, "dyn-q1", "Moje odpoved 1");
        Over(SpecificationService.GetAnsweredCount(pDyn) == 1, "dynamic: 1 zodpovězená");
        Over(SpecificationService.GetNextUnansweredQuestion(pDyn).Id == "dyn-q2", "dynamic: další nezodpovězená je druhá");

        SpecificationService.UseAssumption(pDyn, "dyn-q2");
        Over(SpecificationService.GetAssumptionsCount(pDyn) == 1, "dynamic: 1 předpoklad");
        Over(SpecificationService.GetNextUnansweredQuestion(pDyn) == null, "dynamic: žádná další nezodpovězená");

        string mdDyn = SpecificationService.RenderMarkdown(pDyn);
        Over(mdDyn.Contains("Question 1?"), "dynamic MD: obsahuje text první otázky");
        Over(mdDyn.Contains("Moje odpoved 1"), "dynamic MD: obsahuje odpověď na první");
        Over(mdDyn.Contains("Predpoklad 2"), "dynamic MD: obsahuje předpoklad na druhou");

        string tmpCestaDyn = Path.Combine(Path.GetTempPath(), "test_dyn.vcbrief");
        try
        {
            SpecificationService.SaveProject(pDyn, tmpCestaDyn);
            var nactenyDyn = SpecificationService.LoadProject(tmpCestaDyn);
            Over(nactenyDyn.Questions.Count == 2, "dynamic roundtrip: počet uložených otázek sedí");
            Over(nactenyDyn.Questions[0].Id == "dyn-q1", "dynamic roundtrip: ID první otázky sedí");
            Over(nactenyDyn.Questions[1].Text == "Question 2?", "dynamic roundtrip: text druhé otázky sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaDyn)) File.Delete(tmpCestaDyn);
        }

        // --- rychlé nápovědy odpovědí (quick options) ---
        var pQ = new ProjectSpecification();
        var otQ1 = SpecificationService.GetProjectQuestions(pQ).First(o => o.Id == "tech-platforma");
        var opts1 = otQ1.GetOptions(pQ.ProjectTypeKey);
        Over(opts1.Count == 3, "quick options: výchozí otázka má 3 možnosti");
        Over(opts1.Contains("Web (React + Node.js)"), "quick options: obsahuje správnou výchozí platformu");

        var pDynQ = new ProjectSpecification();
        pDynQ.Questions.Add(new Question { Id = "q-dyn", Text = "Spec q?", Options = new List<string> { "Ano", "Ne", "Možná" } });
        var otDynQ = pDynQ.Questions[0];
        Over(otDynQ.GetOptions(pDynQ.ProjectTypeKey).Count == 3, "quick options: dynamická otázka vrací své možnosti");

        string tmpCestaQ = Path.Combine(Path.GetTempPath(), "test_quick.vcbrief");
        try
        {
            SpecificationService.SaveProject(pDynQ, tmpCestaQ);
            var nactenyQ = SpecificationService.LoadProject(tmpCestaQ);
            Over(nactenyQ.Questions[0].Options.Count == 3, "quick options roundtrip: počet možností sedí");
            Over(nactenyQ.Questions[0].Options[2] == "Možná", "quick options roundtrip: hodnota možnosti sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaQ)) File.Delete(tmpCestaQ);
        }

        // --- uživatelské příběhy (user stories) ---
        var pUS = new ProjectSpecification();
        pUS.UserStories.Add(new UserStory
        {
            Id = "US-100",
            Title = "Test story",
            Description = "Jako tester chci spustit test, abych overil kod.",
            Priority = "Vysoká",
            Criteria = new List<string> { "Kriterium 1", "Kriterium 2" }
        });

        string tmpCestaUS = Path.Combine(Path.GetTempPath(), "test_us.vcbrief");
        try
        {
            SpecificationService.SaveProject(pUS, tmpCestaUS);
            var nactenyUS = SpecificationService.LoadProject(tmpCestaUS);
            Over(nactenyUS.UserStories.Count == 1, "user stories roundtrip: počet stories sedí");
            Over(nactenyUS.UserStories[0].Id == "US-100", "user stories roundtrip: ID story sedí");
            Over(nactenyUS.UserStories[0].Title == "Test story", "user stories roundtrip: titulek story sedí");
            Over(nactenyUS.UserStories[0].Criteria.Count == 2, "user stories roundtrip: počet kritérií sedí");
            Over(nactenyUS.UserStories[0].Criteria[1] == "Kriterium 2", "user stories roundtrip: hodnota kritéria sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaUS)) File.Delete(tmpCestaUS);
        }

        // --- chat history ---
        var pChat = new ProjectSpecification();
        pChat.ChatHistory.Add(new ChatMessage { Role = "user", Text = "Dotaz 1", Timestamp = DateTime.Now });
        pChat.ChatHistory.Add(new ChatMessage { Role = "model", Text = "Answer 1", Timestamp = DateTime.Now });

        string tmpCestaChat = Path.Combine(Path.GetTempPath(), "test_chat.vcbrief");
        try
        {
            SpecificationService.SaveProject(pChat, tmpCestaChat);
            var nactenyChat = SpecificationService.LoadProject(tmpCestaChat);
            Over(nactenyChat.ChatHistory.Count == 2, "chat history roundtrip: počet zpráv sedí");
            Over(nactenyChat.ChatHistory[0].Role == "user", "chat history roundtrip: role první zprávy sedí");
            Over(nactenyChat.ChatHistory[1].Text == "Answer 1", "chat history roundtrip: text druhé zprávy sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaChat)) File.Delete(tmpCestaChat);
        }

        // --- mockup image ---
        var pMockup = new ProjectSpecification();
        pMockup.MockupName = "mockup.png";
        pMockup.MockupBase64 = "SGVsbG8gd29ybGQ="; // Base64 for "Hello world"

        string tmpCestaMockup = Path.Combine(Path.GetTempPath(), "test_mockup.vcbrief");
        try
        {
            SpecificationService.SaveProject(pMockup, tmpCestaMockup);
            var nactenyMockup = SpecificationService.LoadProject(tmpCestaMockup);
            Over(nactenyMockup.MockupName == "mockup.png", "mockup roundtrip: název mockupů sedí");
            Over(nactenyMockup.MockupBase64 == "SGVsbG8gd29ybGQ=", "mockup roundtrip: Base64 kódování sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaMockup)) File.Delete(tmpCestaMockup);
        }

        // --- metrics ---
        var pMetrics = new ProjectSpecification();
        pMetrics.Metrics = new ProjectMetrics
        {
            TimeEstimateMin = "80",
            TimeEstimateMax = "120",
            Complexity = "Vysoká",
            TeamComposition = "1x dev",
            RecommendedBudget = "100k",
            TechnicalAnalysis = "arch",
            MetricRisks = new List<string> { "Risk 1" },
            CalculationTimestamp = DateTime.Now
        };

        string tmpCestaMetrics = Path.Combine(Path.GetTempPath(), "test_metriky.vcbrief");
        try
        {
            SpecificationService.SaveProject(pMetrics, tmpCestaMetrics);
            var nactenyMetrics = SpecificationService.LoadProject(tmpCestaMetrics);
            Over(nactenyMetrics.Metrics.TimeEstimateMin == "80", "metrics roundtrip: odhad min sedí");
            Over(nactenyMetrics.Metrics.TimeEstimateMax == "120", "metrics roundtrip: odhad max sedí");
            Over(nactenyMetrics.Metrics.Complexity == "Vysoká", "metrics roundtrip: komplexita sedí");
            Over(nactenyMetrics.Metrics.MetricRisks.Count == 1, "metrics roundtrip: počet rizik sedí");
            Over(nactenyMetrics.Metrics.MetricRisks[0] == "Risk 1", "metrics roundtrip: detail rizika sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaMetrics)) File.Delete(tmpCestaMetrics);
        }

        // --- HTML rendering ---
        var pHtml = new ProjectSpecification { Name = "HTML Test Projekt" };
        string html = SpecificationService.RenderHtml(pHtml);
        Over(!string.IsNullOrWhiteSpace(html), "HTML rendering: výstup není prázdný");
        Over(html.Contains("<!DOCTYPE html>"), "HTML rendering: obsahuje doctype");
        Over(html.Contains("HTML Test Projekt"), "HTML rendering: obsahuje název projektu");
        Over(html.Contains("toggleTheme"), "HTML rendering: obsahuje JS přepínač témat");

        // --- atomické ukládání (.tmp / .bak) ---
        string tmpAtom = Path.Combine(Path.GetTempPath(), "test_atomic.vcbrief");
        string tmpAtomTmp = tmpAtom + ".tmp";
        string tmpAtomBak = tmpAtom + ".bak";
        try
        {
            if (File.Exists(tmpAtom)) File.Delete(tmpAtom);
            if (File.Exists(tmpAtomTmp)) File.Delete(tmpAtomTmp);
            if (File.Exists(tmpAtomBak)) File.Delete(tmpAtomBak);

            var pAtom = new ProjectSpecification { Name = "Atomický test" };
            SpecificationService.SaveProject(pAtom, tmpAtom);
            Over(File.Exists(tmpAtom), "atomický zápis: první uložení vytvoří soubor");
            Over(!File.Exists(tmpAtomTmp), "atomický zápis: po uložení nezůstává .tmp");
            Over(!File.Exists(tmpAtomBak), "atomický zápis: první uložení nevytváří .bak");

            pAtom.Name = "Atomický test v2";
            SpecificationService.SaveProject(pAtom, tmpAtom);
            Over(File.Exists(tmpAtomBak), "atomický zápis: druhé uložení vytvoří .bak");
            Over(!File.Exists(tmpAtomTmp), "atomický zápis: po druhém uložení nezůstává .tmp");

            var pAtomNacteny = SpecificationService.LoadProject(tmpAtom);
            Over(pAtomNacteny.Name == "Atomický test v2", "atomický zápis: obsah jde načíst zpět");

            var pAtomZaloha = SpecificationService.LoadProject(tmpAtomBak);
            Over(pAtomZaloha.Name == "Atomický test", "atomický zápis: .bak obsahuje předchozí verzi");

            string slozkaAtomKoren = Path.Combine(Path.GetTempPath(), "cp_atomic_" + Guid.NewGuid().ToString("N"));
            string cestaVPodslozce = Path.Combine(slozkaAtomKoren, "podslozka", "novy.vcbrief");
            try
            {
                SpecificationService.SaveProject(pAtom, cestaVPodslozce);
                Over(File.Exists(cestaVPodslozce), "atomický zápis: založí chybějící adresář");
            }
            finally
            {
                if (Directory.Exists(slozkaAtomKoren)) Directory.Delete(slozkaAtomKoren, true);
            }
        }
        finally
        {
            if (File.Exists(tmpAtom)) File.Delete(tmpAtom);
            if (File.Exists(tmpAtomTmp)) File.Delete(tmpAtomTmp);
            if (File.Exists(tmpAtomBak)) File.Delete(tmpAtomBak);
        }

        // --- log přepisu odpovědi „bylo → je“ ---
        var pByloJe = new ProjectSpecification();
        SpecificationService.AnswerQuestion(pByloJe, "cil-problem", "První verze odpovědi");
        var prvniZaznam = pByloJe.ChangeLog.Last(l => l.Action == "Odpověď");
        Over(!prvniZaznam.Detail.Contains("bylo:"), "log: první odpověď nemá formát bylo→je");

        SpecificationService.AnswerQuestion(pByloJe, "cil-problem", "Druhá verze odpovědi");
        var zaznamZmeny = pByloJe.ChangeLog.Last(l => l.Action == "Změna odpovědi");
        Over(zaznamZmeny.Detail.Contains("bylo: 'První verze odpovědi'"), "log změny: obsahuje původní text (bylo)");
        Over(zaznamZmeny.Detail.Contains("je: 'Druhá verze odpovědi'"), "log změny: obsahuje nový text (je)");

        SpecificationService.AnswerQuestion(pByloJe, "cil-problem", "Druhá verze odpovědi");
        var stejnyText = pByloJe.ChangeLog.Last(l => l.Action == "Změna odpovědi");
        Over(!stejnyText.Detail.Contains("bylo:"), "log změny: přepis stejným textem nemá formát bylo→je");

        // --- roundtrip nových polí (AiFindings, CasGenerovaniStories, CasAiKontroly) ---
        var pNovaPole = new ProjectSpecification { Name = "Nová pole" };
        pNovaPole.StoriesGenerationTimestamp = new DateTime(2026, 1, 2, 3, 4, 5);
        pNovaPole.AiCheckTimestamp = new DateTime(2026, 2, 3, 4, 5, 6);
        pNovaPole.AiFindings.Add(new AiFinding { Severity = "Rozpor", Title = "AI titulek", Detail = "AI detail" });
        pNovaPole.AiFindings.Add(new AiFinding { Severity = "Varovani", Title = "AI titulek 2", Detail = "AI detail 2" });

        string tmpCestaNova = Path.Combine(Path.GetTempPath(), "test_nova_pole.vcbrief");
        try
        {
            SpecificationService.SaveProject(pNovaPole, tmpCestaNova);
            var nactenyNova = SpecificationService.LoadProject(tmpCestaNova);
            Over(nactenyNova.StoriesGenerationTimestamp == pNovaPole.StoriesGenerationTimestamp, "nová pole roundtrip: CasGenerovaniStories sedí");
            Over(nactenyNova.AiCheckTimestamp == pNovaPole.AiCheckTimestamp, "nová pole roundtrip: CasAiKontroly sedí");
            Over(nactenyNova.AiFindings.Count == 2, "nová pole roundtrip: počet AI nálezů sedí");
            Over(nactenyNova.AiFindings[0].Severity == "Rozpor", "nová pole roundtrip: závažnost nálezu sedí");
            Over(nactenyNova.AiFindings[1].Title == "AI titulek 2", "nová pole roundtrip: titulek nálezu sedí");
            Over(nactenyNova.AiFindings[1].Detail == "AI detail 2", "nová pole roundtrip: detail nálezu sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaNova)) File.Delete(tmpCestaNova);
            if (File.Exists(tmpCestaNova + ".bak")) File.Delete(tmpCestaNova + ".bak");
        }

        // --- zpětná kompatibilita: starý soubor bez nových polí ---
        string tmpStary = Path.Combine(Path.GetTempPath(), "test_stary_format.vcbrief");
        try
        {
            // Otestujeme novou camelCase strukturu bez nových polí (chybějící nová pole se doinicializují)
            File.WriteAllText(tmpStary, "{\"name\":\"Starý projekt\",\"aiFindings\":null}");
            var pStaryFormat = SpecificationService.LoadProject(tmpStary);
            Over(pStaryFormat.AiFindings != null && pStaryFormat.AiFindings.Count == 0, "zpětná kompatibilita: AiFindings se doinicializují");
            Over(pStaryFormat.StoriesGenerationTimestamp == null, "zpětná kompatibilita: CasGenerovaniStories je null");
            Over(pStaryFormat.AiCheckTimestamp == null, "zpětná kompatibilita: CasAiKontroly je null");

            // Otestujeme, že načtení starého formátu (s českými klíči jako Nazev) vyhodí výjimku
            bool vyhodilVyjimku = false;
            try
            {
                File.WriteAllText(tmpStary, "{\"Nazev\":\"Starý projekt\"}");
                SpecificationService.LoadProject(tmpStary);
            }
            catch (Exception)
            {
                vyhodilVyjimku = true;
            }
            Over(vyhodilVyjimku, "zpětná kompatibilita: starý formát s českými klíči vyhodí výjimku");
        }
        finally
        {
            if (File.Exists(tmpStary)) File.Delete(tmpStary);
        }

        // --- převod nálezu kontroly na AiFinding ---
        var prevod = AiFinding.FromFinding(new ConsistencyFinding { Severity = Severity.Conflict, Title = "T", Detail = "D" });
        Over(prevod.Severity == "Conflict" && prevod.Title == "T" && prevod.Detail == "D", "AiFinding.Z: převod z ConsistencyFinding sedí 1:1");

        // --- poznámky o zastaralosti v exportech ---
        var pStale = new ProjectSpecification { Name = "Zastaralé odhady" };
        pStale.Metrics = new ProjectMetrics
        {
            TimeEstimateMin = "10 h",
            TimeEstimateMax = "20 h",
            Complexity = "Nízká",
            TechnicalAnalysis = "rozbor",
            CalculationTimestamp = DateTime.Now.AddHours(-2)
        };
        pStale.UserStories.Add(new UserStory { Id = "US-01", Title = "Story", Description = "Description", Priority = "Vysoká" });
        pStale.StoriesGenerationTimestamp = DateTime.Now.AddHours(-2);
        pStale.UpdatedAt = DateTime.Now;

        string mdStale = SpecificationService.RenderMarkdown(pStale);
        Over(mdStale.Contains("The estimate was generated for an older version of the specification"), "MD: poznámka o zastaralém odhadu");
        Over(mdStale.Contains("User stories were generated for an older version of the specification"), "MD: poznámka o zastaralých stories");

        string htmlStale = SpecificationService.RenderHtml(pStale);
        Over(htmlStale.Contains("The estimate was generated for an older version of the specification"), "HTML: poznámka o zastaralém odhadu");
        Over(htmlStale.Contains("User stories were generated for an older version of the specification"), "HTML: poznámka o zastaralých stories");

        var pCerstve = new ProjectSpecification { Name = "Čerstvé odhady" };
        pCerstve.Metrics = new ProjectMetrics { TimeEstimateMin = "10 h", CalculationTimestamp = DateTime.Now.AddHours(1) };
        pCerstve.UserStories.Add(new UserStory { Id = "US-01", Title = "Story" });
        pCerstve.StoriesGenerationTimestamp = DateTime.Now.AddHours(1);
        Over(SpecificationService.AreMetricsOutdated(pStale), "helper: AreMetricsOutdated platí pro starý výpočet");
        Over(!SpecificationService.AreMetricsOutdated(pCerstve), "helper: AreMetricsOutdated neplatí pro čerstvý výpočet");
        Over(SpecificationService.AreStoriesOutdated(pStale), "helper: AreStoriesOutdated platí pro staré stories");
        Over(!SpecificationService.AreStoriesOutdated(new ProjectSpecification()), "helper: AreStoriesOutdated neplatí bez stories");

        // --- patička exportů ---
        Over(mdStale.Contains("*Created by CodePlanner*"), "MD: patička obsahuje nový text");
        Over(!mdStale.Contains("demonstrátor bez AI"), "MD: patička už neobsahuje „demonstrátor bez AI“");
        Over(!htmlStale.Contains("demonstrátor bez AI"), "HTML: neobsahuje „demonstrátor bez AI“");

        // --- OrezText (ořez kontextu pro AI prompty) ---
        string dlouhyText = new string('a', 500);
        string orezanyText = GeminiService.OrezText(dlouhyText, 100);
        Over(orezanyText.Contains("[…zkráceno]"), "OrezText: přidává poznámku o zkrácení");
        Over(orezanyText.StartsWith(new string('a', 100)), "OrezText: zachová prvních maxZnaku znaků");
        Over(orezanyText.Length < dlouhyText.Length, "OrezText: výsledek je kratší než původní text");

        string kratkyText = "krátký text";
        Over(GeminiService.OrezText(kratkyText, 100) == kratkyText, "OrezText: krátký text zůstává beze změny");
        Over(GeminiService.OrezText(null) == null, "OrezText: null projde bez chyby");
        Over(GeminiService.OrezText(dlouhyText) == dlouhyText, "OrezText: výchozí limit 100 000 znaků text pod limitem nemění");

        // --- L1: JSON export po AI analýze obsahuje všechny odpovědi ---
        var pJsonDyn = new ProjectSpecification { Name = "JSON Dyn" };
        pJsonDyn.Questions.Add(new Question { Id = "dyn-q1", Section = "Technika", Text = "Q1?", DefaultAssumption = "P1" });
        SpecificationService.AnswerQuestion(pJsonDyn, "dyn-q1", "Answer na dynamic");
        string jsonDyn = SpecificationService.RenderJson(pJsonDyn);
        Over(jsonDyn.Contains("\"id\": \"dyn-q1\""), "L1 test: JSON obsahuje ID dynamické otázky");
        Over(jsonDyn.Contains("\"answer\": \"Answer na dynamic\""), "L1 test: JSON obsahuje odpověď na dynamickou otázku");

        // --- L2: XSS / JS injection v HTML exportu přes ID user story ---
        var pXss = new ProjectSpecification { Name = "HTML XSS Test" };
        pXss.UserStories.Add(new UserStory
        {
            Id = "US-1<script>alert('XSS')</script>",
            Title = "Test story",
            Description = "Description story",
            Priority = "Vysoká"
        });
        string htmlXss = SpecificationService.RenderHtml(pXss);
        Over(!htmlXss.Contains("<div class=\"backlog-item\" id=\"story-US-1<script>"), "L2 test: HTML neobsahuje surový skript v id");
        Over(htmlXss.Contains("id=\"story-US-1scriptalertXSSscript\""), "L2 test: HTML obsahuje bezpečně vyčištěné id");
        Over(htmlXss.Contains("toggleStory(this, 'story-US-1scriptalertXSSscript')"), "L2 test: HTML obsahuje bezpečně vyčištěné id v onchange");
        Over(htmlXss.Contains("US-1&lt;script&gt;alert("), "L2 test: us.Id je v textu správně escapováno");

        }
        finally
        {
            GeminiSettings.SettingsPathOverride = null;
            if (File.Exists(testSettingsPath))
            {
                try { File.Delete(testSettingsPath); } catch { }
            }
        }

        Console.WriteLine();
        Console.WriteLine("VSECHNY TESTY OK (" + _ok + " kontrol)");
    }
}
// konec souboru
