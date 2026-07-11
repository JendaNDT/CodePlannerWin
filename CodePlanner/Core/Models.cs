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
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string HelpText { get; set; } = "";
        public Impact Impact { get; set; }
        public string Section { get; set; } = "";
        public string DefaultAssumption { get; set; } = "";
        public List<string> Options { get; set; } = new List<string>();

        [JsonIgnore]
        public Dictionary<ProjectType, string> Texts { get; set; } = new Dictionary<ProjectType, string>();
        [JsonIgnore]
        public Dictionary<ProjectType, string> Helps { get; set; } = new Dictionary<ProjectType, string>();
        [JsonIgnore]
        public Dictionary<ProjectType, string> Assumptions { get; set; } = new Dictionary<ProjectType, string>();

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
                    return new List<string> { "Web (React + Node.js)", "Mobilní (React Native)", "Desktop (Windows Forms)" };
                case "tech-offline":
                    return new List<string> { "Plně offline (lokální ukládání)", "Primárně online (s REST API)", "Hybridní (offline-first s cloud synchronizací)" };
                case "data-obsah":
                    return new List<string> { "SQLite databáze v souboru", "PostgreSQL na cloudovém serveru", "Lokální JSON konfigurační soubor" };
                case "data-export":
                    return new List<string> { "Export do CSV a Excelu", "Kompletní JSON záloha", "Žádný export (pouze v aplikaci)" };
                case "rizika-reseni":
                    return new List<string> { "Automatické denní zálohy na pozadí", "Jednoduché chybové hlášení uživateli", "Omezení velikosti nahrávaných dat" };
                case "akceptace":
                    return new List<string> { "Prochází všechny automatické testy", "Uživatel dokáže úspěšně dokončit celý scénář", "Aplikace splňuje výkonnostní limity (odezva pod 100ms)" };
                default:
                    return new List<string>();
            }
        }

        public string GetText(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return Text;
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
