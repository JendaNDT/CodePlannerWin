using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CodePlanner.Core
{
    public static class SpecificationService
    {
        // Fixed order of sections for documentation and forms
        public static readonly IReadOnlyList<string> SectionOrder = new List<string>
        {
            "Target & Users",
            "Scope",
            "UX",
            "Data",
            "Technology",
            "Acceptance",
            "Risks"
        };

        public static List<string> GetAllSections(ProjectSpecification p)
        {
            var projectSections = GetProjectQuestions(p).Select(o => o.Section).Distinct();
            var list = new List<string>(SectionOrder);
            foreach (var s in projectSections)
            {
                if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s))
                {
                    list.Add(s);
                }
            }
            return list;
        }

        private static readonly JsonSerializerOptions JsonOpt = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        };

        // ---------- state changes ----------

        public static string GetProjectTypeName(ProjectType typ) => GetProjectTypeName(typ.ToString());

        public static string GetProjectTypeName(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumTyp))
            {
                return enumTyp switch
                {
                    ProjectType.Game => "Hra (Game)",
                    ProjectType.Registry => "Evidence / Registr",
                    ProjectType.Tool => "Nástroj / Utilita",
                    _ => "Obecná aplikace"
                };
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null) return template.Name;

            return typeKey;
        }

        public static void ChangeProjectType(ProjectSpecification p, ProjectType newType) => ChangeProjectType(p, newType.ToString());

        public static void ChangeProjectType(ProjectSpecification p, string newTypeKey)
        {
            if (p.ProjectTypeKey == newTypeKey) return;
            var oldTypeKey = p.ProjectTypeKey;
            p.ProjectTypeKey = newTypeKey;

            if (Enum.TryParse<ProjectType>(newTypeKey, true, out var enumTyp))
            {
                p.ProjectType = enumTyp;
            }
            else
            {
                p.ProjectType = ProjectType.General;
            }

            // Update all automatic assumptions
            foreach (var ot in GetProjectQuestions(p))
            {
                var odp = GetAnswerFor(p, ot.Id);
                if (odp != null && odp.IsAssumption)
                {
                    odp.Text = ot.GetDefaultAssumption(newTypeKey);
                    odp.Timestamp = DateTime.Now;
                }
            }

            LogChange(p, "Typ projektu", $"Změna typu projektu z {GetProjectTypeName(oldTypeKey)} na {GetProjectTypeName(newTypeKey)}.");
        }

        public static void SetIdea(ProjectSpecification p, string idea)
        {
            if ((p.Idea ?? "") == (idea ?? "")) return;
            p.Idea = idea ?? "";
            LogChange(p, "Nápad", "Upraven text původního nápadu.");
        }

        public static void AnswerQuestion(ProjectSpecification p, string questionId, string text)
        {
            var ot = GetQuestionById(p, questionId);
            if (ot == null || string.IsNullOrWhiteSpace(text)) return;

            var stara = p.Answers.FirstOrDefault(o => o.QuestionId == questionId);
            if (stara != null) p.Answers.Remove(stara);

            string novyText = text.Trim();
            p.Answers.Add(new Answer { QuestionId = questionId, Text = novyText, IsAssumption = false, Timestamp = DateTime.Now });

            string detail;
            if (stara != null && stara.Text != novyText)
            {
                detail = ot.GetText(p.ProjectTypeKey) + " → bylo: '" + ShortenText(stara.Text, 120) + "' → je: '" + ShortenText(novyText, 120) + "'";
            }
            else
            {
                detail = ot.GetText(p.ProjectTypeKey) + " → " + ShortenText(novyText, 120);
            }
            LogChange(p, stara == null ? "Odpověď" : "Změna odpovědi", detail);
        }

        public static void UseAssumption(ProjectSpecification p, string questionId)
        {
            var ot = GetQuestionById(p, questionId);
            if (ot == null) return;

            var stara = p.Answers.FirstOrDefault(o => o.QuestionId == questionId);
            if (stara != null) p.Answers.Remove(stara);

            p.Answers.Add(new Answer { QuestionId = questionId, Text = ot.GetDefaultAssumption(p.ProjectTypeKey), IsAssumption = true, Timestamp = DateTime.Now });
            LogChange(p, "Předpoklad", ot.GetText(p.ProjectTypeKey) + " → [PŘEDPOKLAD] " + ot.GetDefaultAssumption(p.ProjectTypeKey));
        }

        public static void LogChange(ProjectSpecification p, string action, string detail)
        {
            p.Version++;
            p.UpdatedAt = DateTime.Now;
            p.ChangeLog.Add(new DecisionLogEntry { Timestamp = DateTime.Now, Action = action, Detail = detail });
        }

        // ---------- status queries ----------

        public static Answer? GetAnswerFor(ProjectSpecification p, string questionId)
            => p.Answers.FirstOrDefault(o => o.QuestionId == questionId);

        public static Question? GetNextUnansweredQuestion(ProjectSpecification p)
            => GetProjectQuestions(p).FirstOrDefault(ot => GetAnswerFor(p, ot.Id) == null);

        public static int GetAnsweredCount(ProjectSpecification p)
            => GetProjectQuestions(p).Count(ot => { var o = GetAnswerFor(p, ot.Id); return o != null && !o.IsAssumption; });

        public static int GetAssumptionsCount(ProjectSpecification p)
            => GetProjectQuestions(p).Count(ot => { var o = GetAnswerFor(p, ot.Id); return o != null && o.IsAssumption; });

        public static List<Question> GetOpenQuestions(ProjectSpecification p)
            => GetProjectQuestions(p).Where(ot => GetAnswerFor(p, ot.Id) == null).ToList();

        public static bool AreMetricsOutdated(ProjectSpecification p)
            => p != null && p.Metrics != null && p.Metrics.CalculationTimestamp != default && p.Metrics.CalculationTimestamp < p.UpdatedAt;

        public static bool AreStoriesOutdated(ProjectSpecification p)
            => p != null && p.UserStories != null && p.UserStories.Count > 0
               && p.StoriesGenerationTimestamp.HasValue && p.StoriesGenerationTimestamp.Value < p.UpdatedAt;

        public const string OutdatedMetricsNote = "⚠ The estimate was generated for an older version of the specification - we recommend recalculating.";
        public const string OutdatedStoriesNote = "⚠ User stories were generated for an older version of the specification - we recommend regenerating them.";

        // ---------- rendering ----------

        private static string FormatDate(DateTime d) => d.ToString("yyyy-MM-dd HH:mm");

        private static string ShortenText(string s, int max)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        public static string RenderMarkdown(ProjectSpecification p)
        {
            var sb = new StringBuilder();
            string name = string.IsNullOrWhiteSpace(p.Name) ? "(unnamed project)" : p.Name.Trim();

            sb.AppendLine("# Specification: " + name);
            sb.AppendLine("*Project type: " + GetProjectTypeName(p.ProjectTypeKey) + "*");
            sb.AppendLine("*Specification version " + p.Version + " · updated " + FormatDate(p.UpdatedAt) + "*");
            sb.AppendLine("*Created by CodePlanner*");
            sb.AppendLine();

            sb.AppendLine("## Original Idea");
            if (string.IsNullOrWhiteSpace(p.Idea))
                sb.AppendLine("> (not entered yet - type or dictate your idea)");
            else
                foreach (var radek in p.Idea.Trim().Split('\n'))
                    sb.AppendLine("> " + radek.TrimEnd());
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(p.ReferenceText))
            {
                sb.AppendLine("## Reference Materials (" + (p.ReferenceName ?? "attachment") + ")");
                sb.AppendLine("```text");
                sb.AppendLine(p.ReferenceText.Trim());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(p.MockupName))
            {
                sb.AppendLine("## User Interface Mockup");
                sb.AppendLine("- Visual mockup attached: **" + p.MockupName + "** (image sent as visual context to Gemini)");
                sb.AppendLine();
            }

            foreach (var sekce in GetAllSections(p))
            {
                sb.AppendLine("## " + sekce);
                var otazkySekce = GetProjectQuestions(p).Where(o => o.Section == sekce).ToList();
                bool neco = false;

                foreach (var ot in otazkySekce)
                {
                    var odp = GetAnswerFor(p, ot.Id);
                    if (odp == null) continue;
                    neco = true;
                    string znacka = odp.IsAssumption ? " **[ASSUMPTION]**" : "";
                    sb.AppendLine("- **" + ot.GetText(p.ProjectTypeKey) + "**" + znacka);
                    foreach (var radek in odp.Text.Trim().Split('\n'))
                        sb.AppendLine("  " + radek.TrimEnd());
                }

                if (!neco) sb.AppendLine("- *(no decisions yet)*");
                sb.AppendLine();
            }

            var otevrene = GetOpenQuestions(p);
            sb.AppendLine("## Open Questions");
            if (otevrene.Count == 0)
                sb.AppendLine("- *(none – all questions have been resolved)*");
            else
                foreach (var ot in otevrene)
                    sb.AppendLine("- [" + (ot.Impact == Impact.High ? "high impact" : "medium impact") + "] " + ot.GetText(p.ProjectTypeKey));
            sb.AppendLine();

            var nalezy = ConsistencyChecker.Check(p);
            var maAiNalezy = p.AiFindings != null && p.AiFindings.Count > 0;
            if (nalezy.Count > 0 || maAiNalezy)
            {
                sb.AppendLine("## Consistency Checks");
                foreach (var n in nalezy)
                    sb.AppendLine("- " + (n.Severity == Severity.Conflict ? "❗ **CONFLICT: " : "⚠️ **Warning: ") + n.Title + "** – " + n.Detail);
                if (maAiNalezy)
                {
                    sb.AppendLine();
                    sb.AppendLine($"*Deep AI analysis from {FormatDate(p.AiCheckTimestamp ?? DateTime.Now)}:*");
                    foreach (var n in p.AiFindings!)
                    {
                        bool isConflict = string.Equals(n.Severity, "Conflict", StringComparison.OrdinalIgnoreCase) || string.Equals(n.Severity, "Rozpor", StringComparison.OrdinalIgnoreCase);
                        sb.AppendLine("- " + (isConflict ? "🧠❗ **CONFLICT (AI): " : "🧠⚠️ **Warning (AI): ") + n.Title + "** – " + n.Detail);
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Status Summary");
            sb.AppendLine("- Answered: " + GetAnsweredCount(p) + " / " + GetProjectQuestions(p).Count());
            sb.AppendLine("- Marked assumptions: " + GetAssumptionsCount(p));
            sb.AppendLine("- Open questions: " + otevrene.Count);
            if (AreMetricsOutdated(p))
                sb.AppendLine("- " + OutdatedMetricsNote);
            if (AreStoriesOutdated(p))
                sb.AppendLine("- " + OutdatedStoriesNote);
            sb.AppendLine();

            sb.AppendLine("## Decision Log");
            if (p.ChangeLog.Count == 0)
                sb.AppendLine("- *(no decisions yet)*");
            else
                foreach (var r in p.ChangeLog)
                    sb.AppendLine("- " + FormatDate(r.Timestamp) + " · **" + r.Action + "** · " + r.Detail);

            return sb.ToString();
        }

        public static string RenderJson(ProjectSpecification p)
        {
            var sections = new List<object>();
            foreach (var sectionName in GetAllSections(p))
            {
                var items = new List<object>();
                foreach (var ot in GetProjectQuestions(p).Where(o => o.Section == sectionName))
                {
                    var odp = GetAnswerFor(p, ot.Id);
                    if (odp == null) continue;
                    items.Add(new { id = ot.Id, question = ot.GetText(p.ProjectTypeKey), answer = odp.Text, assumption = odp.IsAssumption });
                }
                sections.Add(new { name = sectionName, items });
            }

            var data = new
            {
                tool = "CodePlanner",
                toolVersion = "2.2.0",
                project = p.Name,
                projectType = p.ProjectTypeKey,
                projectTypeName = GetProjectTypeName(p.ProjectTypeKey),
                specificationVersion = p.Version,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
                idea = p.Idea,
                referenceText = p.ReferenceText,
                referenceName = p.ReferenceName,
                mockupBase64 = p.MockupBase64,
                mockupName = p.MockupName,
                sections,
                openQuestions = GetOpenQuestions(p).Select(o => new { id = o.Id, question = o.GetText(p.ProjectTypeKey) }).ToList(),
                consistencyCheck = ConsistencyChecker.Check(p)
                    .Select(n => new { severity = n.Severity.ToString(), title = n.Title, detail = n.Detail }).ToList(),
                decisionLog = p.ChangeLog.Select(r => new { timestamp = r.Timestamp, action = r.Action, detail = r.Detail }).ToList(),
                userStories = p.UserStories.Select(u => new { id = u.Id, title = u.Title, description = u.Description, criteria = u.Criteria, priority = u.Priority }).ToList(),
                chatHistory = p.ChatHistory.Select(c => new { role = c.Role, text = c.Text, timestamp = c.Timestamp }).ToList(),
                metrics = p.Metrics != null ? new {
                    timeEstimateMin = p.Metrics.TimeEstimateMin,
                    timeEstimateMax = p.Metrics.TimeEstimateMax,
                    complexity = p.Metrics.Complexity,
                    teamComposition = p.Metrics.TeamComposition,
                    recommendedBudget = p.Metrics.RecommendedBudget,
                    technicalAnalysis = p.Metrics.TechnicalAnalysis,
                    metricRisks = p.Metrics.MetricRisks,
                    calculationTimestamp = p.Metrics.CalculationTimestamp
                } : null,
                storiesGenerationTimestamp = p.StoriesGenerationTimestamp,
                aiFindings = p.AiFindings.Select(f => new { severity = f.Severity, title = f.Title, detail = f.Detail }).ToList(),
                aiCheckTimestamp = p.AiCheckTimestamp
            };

            return JsonSerializer.Serialize(data, JsonOpt);
        }

        public static string RenderHtml(ProjectSpecification p)
        {
            var sb = new StringBuilder();
            string name = string.IsNullOrWhiteSpace(p.Name) ? "(nepojmenovaný projekt)" : p.Name.Trim();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"cs\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>Specifikace: {System.Net.WebUtility.HtmlEncode(name)}</title>");
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
            sb.AppendLine("    <style>");
            sb.AppendLine("        :root {");
            sb.AppendLine("            --primary: #10233F;");
            sb.AppendLine("            --accent: #17B0A0;");
            sb.AppendLine("            --bg-page: #f5f7fa;");
            sb.AppendLine("            --bg-card: #ffffff;");
            sb.AppendLine("            --text: #212529;");
            sb.AppendLine("            --text-light: #6c757d;");
            sb.AppendLine("            --border: #dee2e6;");
            sb.AppendLine("            --prio-high: #dc3545;");
            sb.AppendLine("            --prio-med: #ffc107;");
            sb.AppendLine("            --prio-low: #28a745;");
            sb.AppendLine("        }");
            sb.AppendLine("        [data-theme=\"dark\"] {");
            sb.AppendLine("            --primary: #1e293b;");
            sb.AppendLine("            --accent: #2dd4bf;");
            sb.AppendLine("            --bg-page: #0f172a;");
            sb.AppendLine("            --bg-card: #1e293b;");
            sb.AppendLine("            --text: #f8fafc;");
            sb.AppendLine("            --text-light: #94a3b8;");
            sb.AppendLine("            --border: #334155;");
            sb.AppendLine("        }");
            sb.AppendLine("        * { box-sizing: border-box; margin: 0; padding: 0; transition: background-color 0.3s, border-color 0.3s; }");
            sb.AppendLine("        body { font-family: 'Inter', sans-serif; background-color: var(--bg-page); color: var(--text); line-height: 1.6; padding: 20px; }");
            sb.AppendLine("        .container { max-width: 1100px; margin: 0 auto; }");
            sb.AppendLine("        header { background-color: var(--primary); color: white; padding: 30px; border-radius: 12px; margin-bottom: 20px; position: relative; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }");
            sb.AppendLine("        h1 { font-size: 2.2rem; margin-bottom: 8px; }");
            sb.AppendLine("        .meta-subtitle { font-size: 0.95rem; color: #cbd5e1; display: flex; gap: 15px; flex-wrap: wrap; }");
            sb.AppendLine("        .theme-switch { position: absolute; top: 30px; right: 30px; background-color: rgba(255,255,255,0.1); border: 1px solid rgba(255,255,255,0.2); color: white; padding: 8px 12px; border-radius: 20px; cursor: pointer; font-size: 0.85rem; font-weight: 500; display: flex; align-items: center; gap: 8px; }");
            sb.AppendLine("        .theme-switch:hover { background-color: rgba(255,255,255,0.2); }");
            sb.AppendLine("        .search-bar-container { margin-bottom: 20px; }");
            sb.AppendLine("        .search-input { width: 100%; padding: 12px 20px; font-size: 1rem; border-radius: 8px; border: 1px solid var(--border); background-color: var(--bg-card); color: var(--text); outline: none; box-shadow: 0 2px 4px rgba(0,0,0,0.02); }");
            sb.AppendLine("        .search-input:focus { border-color: var(--accent); }");
            sb.AppendLine("        .dashboard-grid { display: grid; grid-template-columns: 2fr 1fr; gap: 20px; }");
            sb.AppendLine("        @media (max-width: 900px) { .dashboard-grid { grid-template-columns: 1fr; } }");
            sb.AppendLine("        .card { background-color: var(--bg-card); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.02); }");
            sb.AppendLine("        .card-title { font-size: 1.25rem; font-weight: 600; color: var(--primary); border-bottom: 2px solid var(--accent); padding-bottom: 8px; margin-bottom: 16px; display: flex; align-items: center; justify-content: space-between; }");
            sb.AppendLine("        [data-theme=\"dark\"] .card-title { color: var(--accent); }");
            sb.AppendLine("        .napad-quote { border-left: 4px solid var(--accent); padding: 8px 16px; background-color: rgba(23, 176, 160, 0.05); font-style: italic; border-radius: 0 8px 8px 0; margin-bottom: 12px; }");
            sb.AppendLine("        .spec-item { margin-bottom: 16px; border-bottom: 1px solid var(--border); padding-bottom: 12px; }");
            sb.AppendLine("        .spec-item:last-child { border-bottom: none; padding-bottom: 0; margin-bottom: 0; }");
            sb.AppendLine("        .spec-question { font-weight: 600; font-size: 0.95rem; margin-bottom: 4px; }");
            sb.AppendLine("        .spec-answer { font-size: 0.95rem; }");
            sb.AppendLine("        .badge { display: inline-block; padding: 2px 8px; font-size: 0.75rem; font-weight: 600; border-radius: 12px; margin-left: 8px; }");
            sb.AppendLine("        .badge-prio { color: white; }");
            sb.AppendLine("        .badge-prio-high { background-color: var(--prio-high); }");
            sb.AppendLine("        .badge-prio-med { background-color: var(--prio-med); color: #000; }");
            sb.AppendLine("        .badge-prio-low { background-color: var(--prio-low); }");
            sb.AppendLine("        .badge-predpoklad { background-color: #e2e8f0; color: #475569; }");
            sb.AppendLine("        [data-theme=\"dark\"] .badge-predpoklad { background-color: #334155; color: #cbd5e1; }");
            sb.AppendLine("        .metric-cards-container { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; margin-bottom: 16px; }");
            sb.AppendLine("        .metric-mini-card { background-color: var(--bg-page); border: 1px solid var(--border); border-radius: 8px; padding: 12px; }");
            sb.AppendLine("        .metric-mini-label { font-size: 0.75rem; font-weight: 700; color: var(--text-light); text-transform: uppercase; margin-bottom: 4px; }");
            sb.AppendLine("        .metric-mini-value { font-size: 1rem; font-weight: 600; }");
            sb.AppendLine("        .backlog-item { display: flex; align-items: flex-start; gap: 12px; padding: 12px 0; border-bottom: 1px dashed var(--border); }");
            sb.AppendLine("        .backlog-item:last-child { border-bottom: none; }");
            sb.AppendLine("        .backlog-checkbox { margin-top: 5px; width: 16px; height: 16px; cursor: pointer; }");
            sb.AppendLine("        .backlog-text { flex: 1; }");
            sb.AppendLine("        .backlog-title { font-weight: 600; font-size: 0.95rem; margin-bottom: 2px; }");
            sb.AppendLine("        .backlog-desc { font-size: 0.85rem; color: var(--text-light); margin-bottom: 6px; }");
            sb.AppendLine("        .backlog-criteria-list { padding-left: 15px; font-size: 0.85rem; }");
            sb.AppendLine("        .backlog-criteria-item { list-style-type: square; margin-bottom: 2px; }");
            sb.AppendLine("        .completed .backlog-title { text-decoration: line-through; color: var(--text-light); }");
            sb.AppendLine("        .finding-warning { border-left: 4px solid var(--prio-med); background-color: rgba(255,193,7,0.05); padding: 8px 12px; margin-bottom: 8px; border-radius: 0 6px 6px 0; font-size: 0.85rem; }");
            sb.AppendLine("        .finding-conflict { border-left: 4px solid var(--prio-high); background-color: rgba(220,53,69,0.05); padding: 8px 12px; margin-bottom: 8px; border-radius: 0 6px 6px 0; font-size: 0.85rem; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <script>");
            sb.AppendLine("        (function() {");
            sb.AppendLine("            const savedTheme = localStorage.getItem('theme');");
            sb.AppendLine("            let theme = 'light';");
            sb.AppendLine("            if (savedTheme) {");
            sb.AppendLine("                theme = savedTheme;");
            sb.AppendLine("            } else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {");
            sb.AppendLine("                theme = 'dark';");
            sb.AppendLine("            }");
            sb.AppendLine("            document.body.setAttribute('data-theme', theme);");
            sb.AppendLine("        })();");
            sb.AppendLine("    </script>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <header>");
            sb.AppendLine($"            <h1>Specification: {System.Net.WebUtility.HtmlEncode(name)}</h1>");
            sb.AppendLine("            <div class=\"meta-subtitle\">");
            sb.AppendLine($"                <span>Project type: <strong>{System.Net.WebUtility.HtmlEncode(GetProjectTypeName(p.ProjectTypeKey))}</strong></span>");
            sb.AppendLine($"                <span>Version: <strong>{p.Version}</strong></span>");
            sb.AppendLine($"                <span>Updated: <strong>{FormatDate(p.UpdatedAt)}</strong></span>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <button class=\"theme-switch\" onclick=\"toggleTheme()\">");
            sb.AppendLine("                <span id=\"theme-icon\">🌙</span> <span id=\"theme-label\">Dark Mode</span>");
            sb.AppendLine("            </button>");
            sb.AppendLine("        </header>");
            sb.AppendLine();
            sb.AppendLine("        <div class=\"search-bar-container\">");
            sb.AppendLine("            <input type=\"text\" class=\"search-input\" id=\"searchInput\" onkeyup=\"filterContent()\" placeholder=\"Search in questions, answers or backlog...\">");
            sb.AppendLine("        </div>");
            sb.AppendLine();
            sb.AppendLine("        <div class=\"dashboard-grid\">");
            sb.AppendLine("            <!-- LEFT COLUMN: Specification and Idea -->");
            sb.AppendLine("            <div class=\"left-column\">");
            sb.AppendLine("                <div class=\"card filterable-section\">");
            sb.AppendLine("                    <div class=\"card-title\">Original Idea</div>");
            sb.AppendLine("                    <div class=\"napad-quote\">");
            if (string.IsNullOrWhiteSpace(p.Idea))
                sb.AppendLine("                        (not entered yet - type or dictate your idea)");
            else
                foreach (var radek in p.Idea.Trim().Split('\n'))
                    sb.AppendLine($"                        {System.Net.WebUtility.HtmlEncode(radek.TrimEnd())}<br>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine();

            foreach (var sekce in GetAllSections(p))
            {
                var otazkySekce = GetProjectQuestions(p).Where(o => o.Section == sekce).ToList();
                var odpovezene = otazkySekce.Where(o => GetAnswerFor(p, o.Id) != null).ToList();
                if (odpovezene.Count == 0) continue;

                sb.AppendLine($"                <div class=\"card filterable-section\">");
                sb.AppendLine($"                    <div class=\"card-title\">{System.Net.WebUtility.HtmlEncode(sekce)}</div>");
                foreach (var ot in odpovezene)
                {
                    var odp = GetAnswerFor(p, ot.Id)!;
                    string predpokladBadge = odp.IsAssumption ? "<span class=\"badge badge-predpoklad\">Assumption</span>" : "";
                    sb.AppendLine("                    <div class=\"spec-item\">");
                    sb.AppendLine($"                        <div class=\"spec-question\">{System.Net.WebUtility.HtmlEncode(ot.GetText(p.ProjectTypeKey))}{predpokladBadge}</div>");
                    sb.AppendLine($"                        <div class=\"spec-answer\">{System.Net.WebUtility.HtmlEncode(odp.Text)}</div>");
                    sb.AppendLine("                    </div>");
                }
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            sb.AppendLine("            </div>");
            sb.AppendLine();
            sb.AppendLine("            <!-- RIGHT COLUMN: Metrics and Backlog -->");
            sb.AppendLine("            <div class=\"right-column\">");

            // Metriky
            if (p.Metrics != null && p.Metrics.CalculationTimestamp != default)
            {
                string komplexitaClass = (p.Metrics.Complexity.Contains("High") || p.Metrics.Complexity.Contains("Vysoká")) ? "prio-high" : ((p.Metrics.Complexity.Contains("Medium") || p.Metrics.Complexity.Contains("Střední")) ? "prio-med" : "prio-low");
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Project Metrics</div>");
                if (AreMetricsOutdated(p))
                    sb.AppendLine($"                    <div class=\"stale-note\" style=\"background-color: rgba(255,193,7,0.15); border: 1px solid var(--prio-med); border-radius: 8px; padding: 8px 12px; margin-bottom: 12px; font-size: 0.85rem;\">{OutdatedMetricsNote}</div>");
                sb.AppendLine("                    <div class=\"metric-cards-container\">");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Development (Estimate)</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metrics.TimeEstimateMin)} - {System.Net.WebUtility.HtmlEncode(p.Metrics.TimeEstimateMax)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Complexity</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\"><span class=\"badge badge-prio badge-{komplexitaClass}\" style=\"margin-left:0;\">{System.Net.WebUtility.HtmlEncode(p.Metrics.Complexity)}</span></div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Budget</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metrics.RecommendedBudget)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Recommended Team</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metrics.TeamComposition)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                    </div>");
                sb.AppendLine("                    <div style=\"font-size:0.85rem; line-height:1.4; border-top:1px solid var(--border); padding-top:12px;\">");
                sb.AppendLine("                        <strong>Architecture and Technology:</strong><br>");
                foreach (var radek in p.Metrics.TechnicalAnalysis.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(radek)) continue;
                    sb.AppendLine($"                        {System.Net.WebUtility.HtmlEncode(radek)}<br>");
                }
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Open Questions
            var otevrene = GetOpenQuestions(p);
            if (otevrene.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine($"                    <div class=\"card-title\">Open Questions <span class=\"badge badge-predpoklad\" style=\"margin-left:8px;\">{otevrene.Count}</span></div>");
                sb.AppendLine("                    <ul style=\"padding-left:20px; font-size:0.9rem;\">");
                foreach (var ot in otevrene)
                {
                    string dopadText = ot.Impact == Impact.High ? "high impact" : "medium impact";
                    sb.AppendLine($"                        <li style=\"margin-bottom:6px;\"><strong>{System.Net.WebUtility.HtmlEncode(ot.GetText(p.ProjectTypeKey))}</strong> <span style=\"color:var(--text-light); font-size:0.8rem;\">({dopadText})</span></li>");
                }
                sb.AppendLine("                    </ul>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Consistency Checks
            var nalezy = ConsistencyChecker.Check(p);
            var maAiNalezy = p.AiFindings != null && p.AiFindings.Count > 0;
            if (nalezy.Count > 0 || maAiNalezy)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Consistency Checks</div>");
                sb.AppendLine("                    <div>");
                foreach (var n in nalezy)
                {
                    string fClass = n.Severity == Severity.Conflict ? "finding-conflict" : "finding-warning";
                    string pfx = n.Severity == Severity.Conflict ? "❗ <strong>CONFLICT:</strong> " : "⚠️ <strong>Warning:</strong> ";
                    sb.AppendLine($"                        <div class=\"{fClass}\">");
                    sb.AppendLine($"                            {pfx}<strong>{System.Net.WebUtility.HtmlEncode(n.Title)}</strong> – {System.Net.WebUtility.HtmlEncode(n.Detail)}");
                    sb.AppendLine("                        </div>");
                }
                if (maAiNalezy)
                {
                    sb.AppendLine($"                        <div style=\"margin: 12px 0 6px 0; font-size: 0.85rem; color: var(--text-light); font-style: italic;\">🧠 Deep AI analysis from {FormatDate(p.AiCheckTimestamp ?? DateTime.Now)}:</div>");
                    foreach (var n in p.AiFindings!)
                    {
                        bool isConflict = string.Equals(n.Severity, "Conflict", StringComparison.OrdinalIgnoreCase) || string.Equals(n.Severity, "Rozpor", StringComparison.OrdinalIgnoreCase);
                        string fClass = isConflict ? "finding-conflict" : "finding-warning";
                        string pfx = isConflict ? "🧠❗ <strong>CONFLICT (AI):</strong> " : "🧠⚠️ <strong>Warning (AI):</strong> ";
                        sb.AppendLine($"                        <div class=\"{fClass}\">");
                        sb.AppendLine($"                            {pfx}<strong>{System.Net.WebUtility.HtmlEncode(n.Title)}</strong> – {System.Net.WebUtility.HtmlEncode(n.Detail)}");
                        sb.AppendLine("                        </div>");
                    }
                }
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Agile Backlog (User Stories)
            if (p.UserStories != null && p.UserStories.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Agile Backlog</div>");
                if (AreStoriesOutdated(p))
                    sb.AppendLine($"                    <div class=\"stale-note\" style=\"background-color: rgba(255,193,7,0.15); border: 1px solid var(--prio-med); border-radius: 8px; padding: 8px 12px; margin-bottom: 12px; font-size: 0.85rem;\">{OutdatedStoriesNote}</div>");
                foreach (var us in p.UserStories)
                {
                    string safeId = new string((us.Id ?? "").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
                    string prioClass = (us.Priority == "High" || us.Priority == "Vysoká") ? "prio-high" : ((us.Priority == "Medium" || us.Priority == "Střední") ? "prio-med" : "prio-low");
                    sb.AppendLine($"                    <div class=\"backlog-item\" id=\"story-{safeId}\">");
                    sb.AppendLine($"                        <input type=\"checkbox\" class=\"backlog-checkbox\" onchange=\"toggleStory(this, 'story-{safeId}')\">");
                    sb.AppendLine("                        <div class=\"backlog-text\">");
                    sb.AppendLine($"                            <div class=\"backlog-title\">{System.Net.WebUtility.HtmlEncode(us.Id)}: {System.Net.WebUtility.HtmlEncode(us.Title)} <span class=\"badge badge-prio badge-{prioClass}\">{System.Net.WebUtility.HtmlEncode(us.Priority)}</span></div>");
                    sb.AppendLine($"                            <div class=\"backlog-desc\">{System.Net.WebUtility.HtmlEncode(us.Description)}</div>");
                    sb.AppendLine("                            <ul class=\"backlog-criteria-list\">");
                    foreach (var crit in us.Criteria)
                    {
                        sb.AppendLine($"                                <li class=\"backlog-criteria-item\">{System.Net.WebUtility.HtmlEncode(crit)}</li>");
                    }
                    sb.AppendLine("                            </ul>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");
                }
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Decision Log
            if (p.ChangeLog != null && p.ChangeLog.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Decision Log</div>");
                sb.AppendLine("                    <div style=\"font-size:0.85rem; max-height:250px; overflow-y:auto;\">");
                foreach (var log in p.ChangeLog)
                {
                    sb.AppendLine("                        <div style=\"margin-bottom:8px; border-bottom:1px solid var(--border); padding-bottom:6px;\">");
                    sb.AppendLine($"                            <span style=\"color:var(--text-light); font-weight:500;\">{log.Timestamp:yyyy-MM-dd HH:mm}</span> - <strong>{System.Net.WebUtility.HtmlEncode(log.Action)}</strong><br>");
                    sb.AppendLine($"                            <span style=\"color:var(--text);\">{System.Net.WebUtility.HtmlEncode(log.Detail)}</span>");
                    sb.AppendLine("                        </div>");
                }
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine();
            sb.AppendLine("    <script>");
            sb.AppendLine("        document.addEventListener('DOMContentLoaded', () => {");
            sb.AppendLine("            const theme = document.body.getAttribute('data-theme');");
            sb.AppendLine("            document.getElementById('theme-icon').innerText = theme === 'dark' ? '☀' : '🌙';");
            sb.AppendLine("            document.getElementById('theme-label').innerText = theme === 'dark' ? 'Light Mode' : 'Dark Mode';");
            sb.AppendLine("        });");
            sb.AppendLine();
            sb.AppendLine("        function toggleTheme() {");
            sb.AppendLine("            const body = document.body;");
            sb.AppendLine("            const theme = body.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';");
            sb.AppendLine("            body.setAttribute('data-theme', theme);");
            sb.AppendLine("            localStorage.setItem('theme', theme);");
            sb.AppendLine("            document.getElementById('theme-icon').innerText = theme === 'dark' ? '☀' : '🌙';");
            sb.AppendLine("            document.getElementById('theme-label').innerText = theme === 'dark' ? 'Light Mode' : 'Dark Mode';");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        function toggleStory(checkbox, id) {");
            sb.AppendLine("            const element = document.getElementById(id);");
            sb.AppendLine("            if (checkbox.checked) {");
            sb.AppendLine("                element.classList.add('completed');");
            sb.AppendLine("            } else {");
            sb.AppendLine("                element.classList.remove('completed');");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        function filterContent() {");
            sb.AppendLine("            const query = document.getElementById('searchInput').value.toLowerCase();");
            sb.AppendLine("            const cards = document.querySelectorAll('.filterable-section');");
            sb.AppendLine("            cards.forEach(card => {");
            sb.AppendLine("                let cardVisible = false;");
            sb.AppendLine("                const specItems = card.querySelectorAll('.spec-item');");
            sb.AppendLine("                if (specItems.length > 0) {");
            sb.AppendLine("                    specItems.forEach(item => {");
            sb.AppendLine("                        const itemText = item.innerText.toLowerCase();");
            sb.AppendLine("                        if (itemText.includes(query)) {");
            sb.AppendLine("                            item.style.display = 'block';");
            sb.AppendLine("                            cardVisible = true;");
            sb.AppendLine("                        } else {");
            sb.AppendLine("                            item.style.display = 'none';");
            sb.AppendLine("                        }");
            sb.AppendLine("                    });");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    const cardText = card.innerText.toLowerCase();");
            sb.AppendLine("                    if (cardText.includes(query)) {");
            sb.AppendLine("                        cardVisible = true;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                const backlogItems = card.querySelectorAll('.backlog-item');");
            sb.AppendLine("                if (backlogItems.length > 0) {");
            sb.AppendLine("                    backlogItems.forEach(item => {");
            sb.AppendLine("                        const itemText = item.innerText.toLowerCase();");
            sb.AppendLine("                        if (itemText.includes(query)) {");
            sb.AppendLine("                            item.style.display = 'flex';");
            sb.AppendLine("                            cardVisible = true;");
            sb.AppendLine("                        } else {");
            sb.AppendLine("                            item.style.display = 'none';");
            sb.AppendLine("                        }");
            sb.AppendLine("                    });");
            sb.AppendLine("                }");
            sb.AppendLine("                if (cardVisible || query === '') {");
            sb.AppendLine("                    card.style.display = 'block';");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    card.style.display = 'none';");
            sb.AppendLine("                }");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("    </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ---------- project storage ----------

        public static void SaveProject(ProjectSpecification p, string filepath)
        {
            string json = JsonSerializer.Serialize(p, JsonOpt);

            string? folder = Path.GetDirectoryName(Path.GetFullPath(filepath));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string tmp = filepath + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);

            File.WriteAllText(tmp, json, new UTF8Encoding(true));

            if (File.Exists(filepath))
            {
                File.Replace(tmp, filepath, filepath + ".bak");
            }
            else
            {
                File.Move(tmp, filepath);
            }
        }

        public static ProjectSpecification LoadProject(string filepath)
        {
            var text = File.ReadAllText(filepath);
            var node = JsonNode.Parse(text);
            if (node != null)
            {
                if (node is JsonObject obj)
                {
                    string[] oldKeys = new[] { "Nazev", "Napad", "TypProjektu", "TypProjektuKlic", "ReferencniText", "ReferencniNazev", "Odpovedi", "Log", "Otazky", "Metriky", "CasGenerovaniStories", "AiNalezy", "CasAiKontroly" };
                    foreach (var k in oldKeys)
                    {
                        if (obj.ContainsKey(k))
                        {
                            throw new Exception("Soubor je ve starém formátu (.vcbrief), který již není v této verzi (v2.2.0) přímo podporován.");
                        }
                    }
                }
                var p = node.Deserialize<ProjectSpecification>(JsonOpt);
                if (p != null)
                {
                    // 1. Answers validation & cleaning
                    if (p.Answers == null)
                    {
                        p.Answers = new List<Answer>();
                    }
                    else
                    {
                        p.Answers = p.Answers
                            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.QuestionId) && a.Text != null)
                            .GroupBy(a => a.QuestionId)
                            .Select(g => g.Last())
                            .ToList();
                    }

                    // 2. Questions validation & cleaning
                    if (p.Questions == null)
                    {
                        p.Questions = new List<Question>();
                    }
                    else
                    {
                        p.Questions = p.Questions
                            .Where(q => q != null && !string.IsNullOrWhiteSpace(q.Id) && q.Text != null && q.Section != null)
                            .GroupBy(q => q.Id)
                            .Select(g => {
                                var q = g.First();
                                q.Options ??= new List<string>();
                                q.DefaultAssumption ??= "";
                                return q;
                            })
                            .ToList();
                    }

                    // 3. UserStories validation & cleaning
                    if (p.UserStories == null)
                    {
                        p.UserStories = new List<UserStory>();
                    }
                    else
                    {
                        p.UserStories = p.UserStories
                            .Where(us => us != null && !string.IsNullOrWhiteSpace(us.Id) && us.Title != null)
                            .GroupBy(us => us.Id)
                            .Select(g => {
                                var us = g.First();
                                us.Criteria ??= new List<string>();
                                us.Criteria = us.Criteria.Where(ac => ac != null).ToList();
                                return us;
                            })
                            .ToList();
                    }

                    // 4. ChangeLog validation & cleaning
                    if (p.ChangeLog == null)
                    {
                        p.ChangeLog = new List<DecisionLogEntry>();
                    }
                    else
                    {
                        p.ChangeLog = p.ChangeLog
                            .Where(l => l != null && l.Action != null && l.Detail != null)
                            .ToList();
                    }

                    // 5. ChatHistory validation & cleaning
                    if (p.ChatHistory == null)
                    {
                        p.ChatHistory = new List<ChatMessage>();
                    }
                    else
                    {
                        p.ChatHistory = p.ChatHistory
                            .Where(c => c != null && c.Role != null && c.Text != null)
                            .ToList();
                    }

                    // 6. Metrics validation & cleaning
                    if (p.Metrics == null)
                    {
                        p.Metrics = new ProjectMetrics();
                    }
                    else
                    {
                        p.Metrics.Complexity ??= "";
                        p.Metrics.TeamComposition ??= "";
                        p.Metrics.TechnicalAnalysis ??= "";
                        p.Metrics.RecommendedBudget ??= "";
                        if (p.Metrics.MetricRisks == null)
                        {
                            p.Metrics.MetricRisks = new List<string>();
                        }
                        else
                        {
                            p.Metrics.MetricRisks = p.Metrics.MetricRisks.Where(r => r != null).ToList();
                        }
                    }

                    // 7. AiFindings validation & cleaning
                    if (p.AiFindings == null)
                    {
                        p.AiFindings = new List<AiFinding>();
                    }
                    else
                    {
                        p.AiFindings = p.AiFindings
                            .Where(f => f != null && f.Title != null && f.Detail != null && f.Severity != null)
                            .ToList();
                    }

                    return p;
                }
            }
            return new ProjectSpecification();
        }

        private static JsonNode MigrateJson(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                var migrated = new JsonObject();
                foreach (var prop in obj)
                {
                    string oldKey = prop.Key;
                    string newKey = MapJsonKey(oldKey);
                    migrated[newKey] = prop.Value != null ? MigrateJson(prop.Value.DeepClone()) : null;
                }
                return migrated;
            }
            else if (node is JsonArray arr)
            {
                var migrated = new JsonArray();
                foreach (var item in arr)
                {
                    migrated.Add(item != null ? MigrateJson(item.DeepClone()) : null);
                }
                return migrated;
            }
            return node;
        }

        private static string MapJsonKey(string oldKey)
        {
            switch (oldKey)
            {
                // SpecProjekt / ProjectSpecification
                case "Nazev":
                case "nazev": return "Name";
                case "Napad":
                case "napad": return "Idea";
                case "TypProjektu":
                case "typProjektu": return "ProjectType";
                case "TypProjektuKlic":
                case "typProjektuKlic": return "ProjectTypeKey";
                case "ReferencniText":
                case "referencniText": return "ReferenceText";
                case "ReferencniNazev":
                case "referencniNazev": return "ReferenceName";
                case "MockupBase64":
                case "mockupBase64": return "MockupBase64";
                case "MockupNazev":
                case "mockupNazev": return "MockupName";
                case "Vytvoreno":
                case "vytvoreno": return "CreatedAt";
                case "Upraveno":
                case "upraveno": return "UpdatedAt";
                case "Verze":
                case "verze": return "Version";
                case "Odpovedi":
                case "odpovedi": return "Answers";
                case "Log":
                case "log": return "ChangeLog";
                case "Otazky":
                case "otazky": return "Questions";
                case "UserStories":
                case "userStories": return "UserStories";
                case "ChatHistory":
                case "chatHistory": return "ChatHistory";
                case "Metriky":
                case "metriky": return "Metrics";
                case "CasGenerovaniStories":
                case "casGenerovaniStories": return "StoriesGenerationTimestamp";
                case "AiNalezy":
                case "aiNalezy": return "AiFindings";
                case "CasAiKontroly":
                case "casAiKontroly": return "AiCheckTimestamp";

                // Odpoved / Answer
                case "OtazkaId":
                case "otazkaId": return "QuestionId";
                case "JePredpoklad":
                case "jePredpoklad": return "IsAssumption";
                case "Cas":
                case "cas": return "Timestamp";

                // Rozhodnuti / DecisionLogEntry
                case "Akce":
                case "akce": return "Action";
                case "Detail":
                case "detail": return "Detail";

                // UserStory
                case "Id":
                case "id": return "Id";
                case "Titulek":
                case "titulek": return "Title";
                case "Popis":
                case "popis": return "Description";
                case "Kriteria":
                case "kriteria": return "Criteria";
                case "Priorita":
                case "priorita": return "Priority";

                // ProjektMetriky / ProjectMetrics
                case "CasovyOdhadMin":
                case "casovyOdhadMin": return "TimeEstimateMin";
                case "CasovyOdhadMax":
                case "casovyOdhadMax": return "TimeEstimateMax";
                case "Komplexita":
                case "komplexita": return "Complexity";
                case "SlozeniTymu":
                case "slozeniTymu": return "TeamComposition";
                case "DoporucenyRozpocet":
                case "doporucenyRozpocet": return "RecommendedBudget";
                case "TechnickyRozbor":
                case "technickyRozbor": return "TechnicalAnalysis";
                case "RizikaMetriky":
                case "rizikaMetriky": return "MetricRisks";
                case "CasVypoctu":
                case "casVypoctu": return "CalculationTimestamp";

                // AiNalez / AiFinding
                case "Zavaznost":
                case "zavaznost": return "Severity";

                default: return oldKey;
            }
        }

        public static List<Question> GetProjectQuestions(ProjectSpecification p)
        {
            if (p.Questions != null && p.Questions.Count > 0)
            {
                return p.Questions;
            }
            return StandardQuestions.All.ToList();
        }

        public static Question? GetQuestionById(ProjectSpecification p, string id)
            => GetProjectQuestions(p).FirstOrDefault(o => o.Id == id);
    }
}
