using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace CodePlanner.Core
{
    public enum Impact
    {
        High,
        Medium
    }

    public enum ProjectType
    {
        General,
        Game,
        Registry,
        Tool
    }

    public enum Severity
    {
        Conflict,
        Warning
    }

    public class ProjectTemplate
    {
        [JsonPropertyName("klic")]
        public string Key { get; set; } = "";

        [JsonPropertyName("nazev")]
        public string Name { get; set; } = "";

        [JsonPropertyName("otazky")]
        public List<TemplateQuestion> Questions { get; set; } = new List<TemplateQuestion>();
    }

    public class TemplateQuestion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("napoveda")]
        public string HelpText { get; set; } = "";

        [JsonPropertyName("vychoziPredpoklad")]
        public string DefaultAssumption { get; set; } = "";

        [JsonPropertyName("moznosti")]
        public List<string> Options { get; set; } = new List<string>();
    }

    public class Question
    {
        private string _text = "";
        private string _helpText = "";
        private string _section = "";
        private string _defaultAssumption = "";

        public string Id { get; set; } = "";

        public string Text
        {
            get => LocalizationService.T(StandardQuestionsTranslations.Get(Id + "_Text", _text), _text);
            set => _text = value;
        }

        public string HelpText
        {
            get => LocalizationService.T(StandardQuestionsTranslations.Get(Id + "_HelpText", _helpText), _helpText);
            set => _helpText = value;
        }

        public Impact Impact { get; set; }

        public string Section
        {
            get => LocalizationService.T(TranslateSection(_section), _section);
            set => _section = value;
        }

        public string DefaultAssumption
        {
            get => LocalizationService.T(StandardQuestionsTranslations.Get(Id + "_DefaultAssumption", _defaultAssumption), _defaultAssumption);
            set => _defaultAssumption = value;
        }

        public List<string> Options { get; set; } = new List<string>();

        [JsonIgnore]
        public Dictionary<ProjectType, string> Texts { get; set; } = new Dictionary<ProjectType, string>();
        [JsonIgnore]
        public Dictionary<ProjectType, string> Helps { get; set; } = new Dictionary<ProjectType, string>();
        [JsonIgnore]
        public Dictionary<ProjectType, string> Assumptions { get; set; } = new Dictionary<ProjectType, string>();

        private static string TranslateSection(string s)
        {
            return s switch
            {
                "Target & Users" => "Cíl a uživatelé",
                "Scope" => "Rozsah",
                "UX" => "UX",
                "Data" => "Data",
                "Technology" => "Technika",
                "Acceptance" => "Akceptace",
                "Risks" => "Rizika",
                _ => s
            };
        }

        public List<string> GetOptions(string typeKey)
        {
            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && ot.Options != null && ot.Options.Count > 0) return ot.Options;
            }

            if (Options != null && Options.Count > 0) return Options;

            return GetDefaultOptionsForId(Id);
        }

        private static List<string> GetDefaultOptionsForId(string id)
        {
            switch (id)
            {
                case "tech-platforma":
                    return new List<string> {
                        LocalizationService.T("Web (React + Node.js)", "Web (React + Node.js)"),
                        LocalizationService.T("Mobilní (React Native)", "Mobile (React Native)"),
                        LocalizationService.T("Desktop (Windows Forms)", "Desktop (Windows Forms)")
                    };
                case "tech-offline":
                    return new List<string> {
                        LocalizationService.T("Plně offline (lokální ukládání)", "Fully offline (local storage)"),
                        LocalizationService.T("Primárně online (s REST API)", "Primarily online (with REST API)"),
                        LocalizationService.T("Hybridní (offline-first s cloud synchronizací)", "Hybrid (offline-first with cloud sync)")
                    };
                case "data-obsah":
                    return new List<string> {
                        LocalizationService.T("SQLite databáze v souboru", "SQLite file database"),
                        LocalizationService.T("PostgreSQL na cloudovém serveru", "PostgreSQL on a cloud server"),
                        LocalizationService.T("Lokální JSON konfigurační soubor", "Local JSON config file")
                    };
                case "data-export":
                    return new List<string> {
                        LocalizationService.T("Export do CSV a Excelu", "Export to CSV and Excel"),
                        LocalizationService.T("Kompletní JSON záloha", "Full JSON backup"),
                        LocalizationService.T("Žádný export (pouze v aplikaci)", "No export (app only)")
                    };
                case "rizika-reseni":
                    return new List<string> {
                        LocalizationService.T("Automatické denní zálohy na pozadí", "Automatic daily background backups"),
                        LocalizationService.T("Jednoduché chybové hlášení uživateli", "Simple user error reporting"),
                        LocalizationService.T("Omezení velikosti nahrávaných dat", "Limit uploaded data size")
                    };
                case "akceptace":
                    return new List<string> {
                        LocalizationService.T("Prochází všechny automatické testy", "All automated tests pass"),
                        LocalizationService.T("Uživatel dokáže úspěšně dokončit celý scénář", "User successfully completes the scenario"),
                        LocalizationService.T("Aplikace splňuje výkonnostní limity (odezva pod 100ms)", "App meets performance limits (response under 100ms)")
                    };
                default:
                    return new List<string>();
            }
        }

        public string GetText(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return Text;

                if (string.Equals(LocalizationService.CurrentLanguage, "cs", StringComparison.OrdinalIgnoreCase))
                {
                    string dictKey = Id + "_Text_" + enumType;
                    string translated = StandardQuestionsTranslations.Get(dictKey, "");
                    if (!string.IsNullOrWhiteSpace(translated)) return translated;
                }

                return Texts.TryGetValue(enumType, out var val) && !string.IsNullOrWhiteSpace(val) ? val : Text;
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.Text)) return ot.Text;
            }

            return Text;
        }

        public string GetHelpText(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return HelpText;

                if (string.Equals(LocalizationService.CurrentLanguage, "cs", StringComparison.OrdinalIgnoreCase))
                {
                    string dictKey = Id + "_HelpText_" + enumType;
                    string translated = StandardQuestionsTranslations.Get(dictKey, "");
                    if (!string.IsNullOrWhiteSpace(translated)) return translated;
                }

                return Helps.TryGetValue(enumType, out var val) && !string.IsNullOrWhiteSpace(val) ? val : HelpText;
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.HelpText)) return ot.HelpText;
            }

            return HelpText;
        }

        public string GetDefaultAssumption(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return DefaultAssumption;

                if (string.Equals(LocalizationService.CurrentLanguage, "cs", StringComparison.OrdinalIgnoreCase))
                {
                    string dictKey = Id + "_DefaultAssumption_" + enumType;
                    string translated = StandardQuestionsTranslations.Get(dictKey, "");
                    if (!string.IsNullOrWhiteSpace(translated)) return translated;
                }

                return Assumptions.TryGetValue(enumType, out var val) && !string.IsNullOrWhiteSpace(val) ? val : DefaultAssumption;
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.DefaultAssumption)) return ot.DefaultAssumption;
            }

            return DefaultAssumption;
        }

        public string GetText(ProjectType typ) => GetText(typ.ToString());
        public string GetHelpText(ProjectType typ) => GetHelpText(typ.ToString());
        public string GetDefaultAssumption(ProjectType typ) => GetDefaultAssumption(typ.ToString());
    }

    public class Answer
    {
        public string QuestionId { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsAssumption { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DecisionLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class UserStory
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Criteria { get; set; } = new List<string>();
        public string Priority { get; set; } = "Střední";
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ProjectMetrics
    {
        public string TimeEstimateMin { get; set; } = "";
        public string TimeEstimateMax { get; set; } = "";
        public string Complexity { get; set; } = "";
        public string TeamComposition { get; set; } = "";
        public string RecommendedBudget { get; set; } = "";
        public string TechnicalAnalysis { get; set; } = "";
        public List<string> MetricRisks { get; set; } = new List<string>();
        public DateTime CalculationTimestamp { get; set; }
    }

    public class AiFinding
    {
        public string Severity { get; set; } = "Varovani";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";

        public static AiFinding FromFinding(ConsistencyFinding? nalez) => new AiFinding
        {
            Severity = nalez == null ? "Varovani" : nalez.Severity.ToString(),
            Title = nalez != null ? (nalez.Title ?? "") : "",
            Detail = nalez != null ? (nalez.Detail ?? "") : ""
        };
    }

    public class ProjectSpecification
    {
        public string Name { get; set; } = "";
        public string Idea { get; set; } = "";
        public ProjectType ProjectType { get; set; } = ProjectType.General;

        private string? _projectTypeKey = null;
        public string ProjectTypeKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_projectTypeKey))
                {
                    return ProjectType.ToString();
                }
                return _projectTypeKey;
            }
            set => _projectTypeKey = value;
        }

        public string? ReferenceText { get; set; } = null;
        public string? ReferenceName { get; set; } = null;
        public string? MockupBase64 { get; set; } = null;
        public string? MockupName { get; set; } = null;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public int Version { get; set; } = 1;
        public List<Answer> Answers { get; set; } = new List<Answer>();
        public List<DecisionLogEntry> ChangeLog { get; set; } = new List<DecisionLogEntry>();
        public List<Question> Questions { get; set; } = new List<Question>();
        public List<UserStory> UserStories { get; set; } = new List<UserStory>();
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
        public ProjectMetrics Metrics { get; set; } = new ProjectMetrics();

        public DateTime? StoriesGenerationTimestamp { get; set; }
        public List<AiFinding> AiFindings { get; set; } = new List<AiFinding>();
        public DateTime? AiCheckTimestamp { get; set; }
    }

    public class ConsistencyFinding
    {
        public Severity Severity { get; set; }
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
    }
}
