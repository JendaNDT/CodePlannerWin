using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodePlanner.Core
{
    public static class ConsistencyChecker
    {
        public static List<ConsistencyFinding> Check(ProjectSpecification p)
        {
            var findings = new List<ConsistencyFinding>();
            CheckOfflineOnline(p, findings);
            CheckWebOffline(p, findings);
            CheckPersonalData(p, findings);
            CheckNonGoals(p, findings);
            CheckAcceptanceCriteria(p, findings);
            CheckDataExport(p, findings);
            CheckPlatform(p, findings);
            CheckAssumptions(p, findings);
            CheckMissingIdea(p, findings);
            CheckSqliteOnWeb(p, findings);
            CheckRolesWithoutAuth(p, findings);
            CheckBackupStrategy(p, findings);
            CheckApiDocumentation(p, findings);
            return findings;
        }

        private static string GetAnswerText(ProjectSpecification p, string questionId)
        {
            var o = SpecificationService.GetAnswerFor(p, questionId);
            return o != null ? o.Text : "";
        }

        private static bool SaysOffline(ProjectSpecification p)
        {
            string s = Norm(GetAnswerText(p, "tech-offline"));
            return s.Contains("plne offline") || s.Contains("lokalni uklada") || s.Contains("bez internetu") ||
                   s.Contains("fully offline") || s.Contains("local storage") || s.Contains("without internet");
        }

        private static List<string> GetSources(ProjectSpecification p, params string[] excludeIds)
        {
            var list = new List<string>();
            foreach (var ot in SpecificationService.GetProjectQuestions(p))
            {
                if (excludeIds.Contains(ot.Id)) continue;
                var o = SpecificationService.GetAnswerFor(p, ot.Id);
                if (o != null && !o.IsAssumption && !string.IsNullOrWhiteSpace(o.Text))
                {
                    list.Add(LocalizationService.T("otázka", "question") + " \"" + ot.GetText(p.ProjectTypeKey) + "\": '" + o.Text + "'");
                }
            }
            if (!string.IsNullOrWhiteSpace(p.Idea))
            {
                list.Add(LocalizationService.T("původní nápad", "original idea") + ": '" + p.Idea + "'");
            }
            return list;
        }

        private static string? FindWord(List<string> zdroje, string[] hledana, out string? zdroj)
        {
            foreach (var z in zdroje)
            {
                string nz = Norm(z);
                foreach (var h in hledana)
                {
                    if (nz.Contains(Norm(h)))
                    {
                        zdroj = z.Split(':')[0];
                        return h;
                    }
                }
            }
            zdroj = null;
            return null;
        }

        private static string Norm(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            foreach (char c in text.ToLower())
            {
                switch (c)
                {
                    case 'á': sb.Append('a'); break;
                    case 'č': sb.Append('c'); break;
                    case 'ď': sb.Append('d'); break;
                    case 'é': sb.Append('e'); break;
                    case 'ě': sb.Append('e'); break;
                    case 'í': sb.Append('i'); break;
                    case 'ň': sb.Append('n'); break;
                    case 'ó': sb.Append('o'); break;
                    case 'ř': sb.Append('r'); break;
                    case 'š': sb.Append('s'); break;
                    case 'ť': sb.Append('t'); break;
                    case 'ú': sb.Append('u'); break;
                    case 'ů': sb.Append('u'); break;
                    case 'ý': sb.Append('y'); break;
                    case 'ž': sb.Append('z'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ---------- rules ----------

        private static void CheckOfflineOnline(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            if (!SaysOffline(p)) return;
            var zdroje = GetSources(p, "tech-offline");
            string? zdroj;
            string? slovo = FindWord(zdroje, new[] { "cloud", "synchronizac", "online", "synchroniz", "sync" }, out zdroj);
            if (slovo != null)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Conflict,
                    Title = LocalizationService.T("Offline vs. online", "Offline vs. online"),
                    Detail = LocalizationService.T("Sekce Technika říká \"běží offline\", ale " + zdroj + " zmiňuje \"" + slovo + "\". Rozhodněte, co platí.", "Technology section says \"runs offline\", but " + zdroj + " mentions \"" + slovo + "\". Decide which one applies.")
                });
        }

        private static void CheckWebOffline(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string plat = Norm(GetAnswerText(p, "tech-platforma"));
            if (!SaysOffline(p) || (!plat.Contains("web") && !plat.Contains("prohlizec") && !plat.Contains("browser"))) return;
            findings.Add(new ConsistencyFinding
            {
                Severity = Severity.Warning,
                Title = LocalizationService.T("Web + plně offline", "Web + fully offline"),
                Detail = LocalizationService.T("Webová aplikace bez internetu funguje pouze jako PWA s offline podporou – ověřte, zda je to tak myšleno.", "A web application without internet only works as a PWA with offline support - verify if this is what you mean.")
            });
        }

        private static void CheckPersonalData(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string data = Norm(GetAnswerText(p, "data-obsah"));
            bool bezOsobnich = data.Contains("bez osobnich") || data.Contains("neosobni") || data.Contains("zadne osobni") ||
                               data.Contains("no personal") || data.Contains("without personal");
            if (!bezOsobnich) return;

            var zdroje = GetSources(p, "data-obsah");
            string? zdroj;
            string? slovo = FindWord(zdroje, new[] { "jmeno", "jmena", "email", "e-mail", "telefon", "heslo", "registrac", "prihlas", "name", "email", "phone", "password", "register", "login" }, out zdroj);
            if (slovo != null)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Conflict,
                    Title = LocalizationService.T("Osobní údaje", "Personal Data"),
                    Detail = LocalizationService.T("Sekce Data říká \"žádné osobní údaje\", ale " + zdroj + " zmiňuje \"" + slovo + "\". Osobní údaje znamenají GDPR a vyšší nároky na zabezpečení.", "Data section says \"no personal data\", but " + zdroj + " mentions \"" + slovo + "\". Personal data = GDPR and higher compliance requirements.")
                });
        }

        private static void CheckNonGoals(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            var odp = SpecificationService.GetAnswerFor(p, "rozsah-nongoals");
            if (odp == null || odp.IsAssumption) return;

            var stop = new HashSet<string> { "zadne", "nebude", "nebudou", "nechci", "nesmi", "prvni", "verze", "verzi",
                "aplikace", "appka", "zatim", "pozdeji", "budou", "chceme", "nechceme", "resit", "nema", "mit",
                "no", "not", "will", "without", "first", "version", "application", "app", "for", "now", "later", "solve", "have", "has" };

            var zdroje = GetSources(p, "rozsah-nongoals", "akceptace", "rizika");
            int hitu = 0;

            foreach (var fragment in odp.Text.Split(new[] { ',', ';', '\n', '•' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var slova = Norm(fragment)
                    .Split(new[] { ' ', '.', '!', '?', '(', ')', '"', '-', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 5 && !stop.Contains(w));

                foreach (var s in slova)
                {
                    string? zdroj;
                    string? slovo = FindWord(zdroje, new[] { s }, out zdroj);
                    if (slovo != null)
                    {
                        hitu++;
                        findings.Add(new ConsistencyFinding
                        {
                            Severity = Severity.Warning,
                            Title = LocalizationService.T("Non-goal popsaný jako cíl", "Non-goal described as a goal"),
                            Detail = LocalizationService.T("V non-goals vylučujete \"" + fragment.Trim() + "\", ale " + zdroj + " obsahuje zmínku o \"" + slovo + "\".", "In non-goals you exclude \"" + fragment.Trim() + "\", but " + zdroj + " contains a mention of \"" + slovo + "\".")
                        });
                        if (hitu >= 3) return; // Max 3 warnings
                    }
                }
            }
        }

        private static void CheckAcceptanceCriteria(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string akc = Norm(GetAnswerText(p, "akceptace"));
            if (string.IsNullOrEmpty(akc)) return;

            bool vagni = akc.Contains("podle specifikace") || akc.Contains("budou splneny") ||
                          akc.Contains("vsechny body") || akc.Contains("az bude hotovo") ||
                          akc.Contains("according to spec") || akc.Contains("when finished") ||
                          akc.Contains("all points") || akc.Contains("is done");
            if (vagni)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Vágní akceptační kritéria", "Vague acceptance criteria"),
                    Detail = LocalizationService.T("Akceptační kritéria říkají \"podle specifikace / až to bude fungovat\". Zkuste uvést měřitelné cíle (např. ruční průchod scénářem).", "Acceptance criteria say \"according to specification / when it works\". Try to list measurable goals (e.g. manual scenario walkthrough).")
                });
        }

        private static void CheckDataExport(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string exp = Norm(GetAnswerText(p, "data-export"));
            bool rikaBezExportu = exp.Contains("zadny export") || exp.Contains("netreba export") || exp.Contains("pouze v aplikaci") || exp.Contains("neni vyzadovan") ||
                                  exp.Contains("no export") || exp.Contains("not needed") || exp.Contains("only in app") ||
                                  (exp.Contains("zadny") && exp.Contains("export")) || (exp.Contains("no") && exp.Contains("export"));
            if (!rikaBezExportu) return;

            var zdroje = GetSources(p, "data-export");
            string? zdroj;
            string? slovo = FindWord(zdroje, new[] { "export", "stahnout", "stahovani", "zaloha", "zalohovani", "download", "backup" }, out zdroj);
            if (slovo != null)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Conflict,
                    Title = LocalizationService.T("Export dat vs. žádný export", "Data export vs. no export"),
                    Detail = LocalizationService.T("Sekce Export odmítá exporty, ale " + zdroj + " zmiňuje \"" + slovo + "\". Rozhodněte, zda lze data stahovat.", "Export section rejects exports, but " + zdroj + " mentions \"" + slovo + "\". Decide if data can be downloaded.")
                });
        }

        private static void CheckPlatform(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string plat = Norm(GetAnswerText(p, "tech-platforma"));
            if (string.IsNullOrEmpty(plat)) return;

            bool web = plat.Contains("web") || plat.Contains("prohlizec") || plat.Contains("browser");
            bool mob = plat.Contains("mobil") || plat.Contains("android") || plat.Contains("ios") || plat.Contains("telefon");
            bool dsk = plat.Contains("desktop") || plat.Contains("windows") || plat.Contains("macos") || plat.Contains("linux") || plat.Contains("pc");

            if (web && mob && dsk)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Příliš široký rozsah platforem", "Too broad platform scope"),
                    Detail = LocalizationService.T("Plánujete web, mobil i desktop současně. Pro MVP se doporučuje začít s jedinou platformou (např. webem).", "You plan web, mobile, and desktop at the same time. For MVP, it is recommended to start with a single platform (e.g., web).")
                });
        }

        private static void CheckAssumptions(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            int pCount = SpecificationService.GetAssumptionsCount(p);
            if (pCount >= 3)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Vysoký počet předpokladů (" + pCount + ")", "High number of assumptions (" + pCount + ")"),
                    Detail = LocalizationService.T("Nahradili jste " + pCount + " otázek výchozími předpoklady. Zkuste na ně odpovědět pro přesnější specifikaci.", "You have replaced " + pCount + " questions with default assumptions. Try answering them for a more precise specification.")
                });
        }

        private static void CheckMissingIdea(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(p.Idea) && SpecificationService.GetAnsweredCount(p) > 0)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Chybí původní nápad", "Empty original idea"),
                    Detail = LocalizationService.T("Specifikace obsahuje odpovědi na otázky, ale chybí původní nápad. Doplňte původní nápad v kroku 1.", "The specification contains answers to questions, but the original idea is missing. Complete the original idea in step 1.")
                });
        }

        private static void CheckSqliteOnWeb(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string plat = Norm(GetAnswerText(p, "tech-platforma"));
            string data = Norm(GetAnswerText(p, "data-obsah"));

            bool isWeb = plat.Contains("web") || plat.Contains("prohlizec") || plat.Contains("browser");
            bool isSqlite = data.Contains("sqlite");

            if (isWeb && isSqlite && !data.Contains("wasm") && !data.Contains("localstorage"))
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("SQLite databáze na webu", "SQLite database on web"),
                    Detail = LocalizationService.T("Sekce Technika uvádí webovou aplikaci a SQLite databázi. SQLite v běžném prohlížeči neběží. Zvažte LocalStorage/IndexedDB, případně specifikujte použití WASM nebo backendu.", "Technology section lists a web application and SQLite database. SQLite does not run in a standard browser. Consider LocalStorage/IndexedDB, or specify it as WASM/backend.")
                });
            }
        }

        private static void CheckRolesWithoutAuth(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string uziv = Norm(GetAnswerText(p, "cil-uzivatele"));
            string tech = Norm(GetAnswerText(p, "tech-platforma"));
            string data = Norm(GetAnswerText(p, "data-obsah"));

            bool maRole = uziv.Contains("admin") || uziv.Contains("moderator") || uziv.Contains("opravneni") || uziv.Contains("role");
            bool maPrihlaseni = tech.Contains("prihlas") || tech.Contains("login") || tech.Contains("auth") || tech.Contains("heslo") || tech.Contains("ucet") || tech.Contains("registrac") ||
                                data.Contains("prihlas") || data.Contains("login") || data.Contains("auth") || data.Contains("heslo") || data.Contains("ucet") || data.Contains("registrac");

            if (maRole && !maPrihlaseni)
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Uživatelské role bez přihlašování", "User roles without authentication"),
                    Detail = LocalizationService.T("Zmiňujete uživatelské role (např. administrátor, oprávnění), ale technika ani data neřeší přihlašování. Jak se budou role identifikovat?", "You mention user roles (e.g. administrator, permissions), but technology or data does not address authentication. How will roles be identified?")
                });
            }
        }

        private static void CheckBackupStrategy(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string data = Norm(GetAnswerText(p, "data-obsah"));
            string rizika = Norm(GetAnswerText(p, "rizika-reseni"));

            bool utilizesDatabase = data.Contains("databaze") || data.Contains("db") || data.Contains("postgresql") ||
                                    data.Contains("mysql") || data.Contains("mssql") || data.Contains("mongodb") ||
                                    data.Contains("oracle") || data.Contains("sql") || data.Contains("sqlite");

            bool mentionsBackup = rizika.Contains("zaloha") || rizika.Contains("zaloh") || rizika.Contains("backup") ||
                                  rizika.Contains("dump") || rizika.Contains("sync") ||
                                  data.Contains("zaloha") || data.Contains("zaloh") || data.Contains("backup");

            if (utilizesDatabase && !mentionsBackup)
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Chybí strategie zálohování", "Missing backup strategy"),
                    Detail = LocalizationService.T("Aplikace využívá databázi, ale plán rizik nezmiňuje zálohování (záloha, backup). Zvažte doplnění strategie zálohování.", "The application utilizes a database, but the risk plan does not mention backup (backup, dump). Consider adding a backup strategy.")
                });
            }
        }

        private static void CheckApiDocumentation(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string tech = Norm(GetAnswerText(p, "tech-platforma"));
            string data = Norm(GetAnswerText(p, "data-obsah"));

            bool mentionsApi = tech.Contains("api") || tech.Contains("rest") || tech.Contains("graphql") ||
                              tech.Contains("soap") || tech.Contains("webhook") || tech.Contains("integrace") ||
                              tech.Contains("tretich stran") || tech.Contains("tretichstran") || tech.Contains("napojeni") ||
                              data.Contains("api") || data.Contains("rest") || data.Contains("integrace");

            bool hasReference = !string.IsNullOrWhiteSpace(p.ReferenceText);
            bool mentionsDocs = false;

            if (hasReference)
            {
                string refs = Norm(p.ReferenceText!);
                mentionsDocs = refs.Contains("dokumentace") || refs.Contains("doc") || refs.Contains("swagger") ||
                               refs.Contains("openapi") || refs.Contains("schema") || refs.Contains("specification");
            }

            if (mentionsApi && !mentionsDocs)
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = LocalizationService.T("Chybí dokumentace k externímu API", "Missing external API documentation"),
                    Detail = LocalizationService.T("Projekt zmiňuje integraci externího API nebo služeb třetích stran, ale chybí odkazy na jejich dokumentaci (dokumentace, swagger, OpenAPI).", "The project mentions integrating an external API or third-party services, but references to their documentation (documentation, swagger, OpenAPI) are missing.")
                });
            }
        }
    }
}
