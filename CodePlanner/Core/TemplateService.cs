using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CodePlanner.Core
{
    public static class TemplateService
    {
        public static List<ProjectTemplate> CustomTemplates { get; private set; } = new List<ProjectTemplate>();
        public static string? LoadError { get; private set; } = null;

        public static void LoadCustomTemplates()
        {
            CustomTemplates = new List<ProjectTemplate>();
            LoadError = null;
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sablony.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<ProjectTemplate>>>(json, opt);
                    if (data != null && data.TryGetValue("sablony", out var list))
                    {
                        var validTemplates = new List<ProjectTemplate>();
                        foreach (var temp in list)
                        {
                            if (temp == null || string.IsNullOrWhiteSpace(temp.Key) || string.IsNullOrWhiteSpace(temp.Name)) continue;
                            
                            // Zabráníme kolizi klíčů custom šablon s vestavěnými typy projektů
                            if (string.Equals(temp.Key, "General", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Game", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Registry", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Tool", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Obecna", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Hra", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Evidence", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(temp.Key, "Nastroj", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (temp.Questions != null)
                            {
                                temp.Questions.RemoveAll(q => q == null || string.IsNullOrWhiteSpace(q.Id) || string.IsNullOrWhiteSpace(q.Text));
                            }
                            else
                            {
                                temp.Questions = new List<TemplateQuestion>();
                            }

                            validTemplates.Add(temp);
                        }
                        CustomTemplates = validTemplates;
                    }
                }
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Error loading custom templates: {ex.Message}");
            }
        }
    }
}
