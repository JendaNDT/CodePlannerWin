using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace CodePlanner.Core
{
    /// <summary>
    /// Model nastavení pro Gemini API, ukládaný v uživatelském profilu.
    /// </summary>
    /// <summary>
    /// Model nastavení pro Gemini API, ukládaný v uživatelském profilu.
    /// </summary>
    public class GeminiSettings
    {
        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-2.5-flash";
        public List<string> RecentProjects { get; set; } = new List<string>();

        public void AddRecentProject(string cesta)
        {
            if (string.IsNullOrWhiteSpace(cesta)) return;

            if (RecentProjects == null)
            {
                RecentProjects = new List<string>();
            }

            RecentProjects.RemoveAll(x => string.Equals(x, cesta, StringComparison.OrdinalIgnoreCase));
            RecentProjects.Insert(0, cesta);

            if (RecentProjects.Count > 5)
            {
                RecentProjects.RemoveRange(5, RecentProjects.Count - 5);
            }

            Save();
        }

        public void RemoveRecentProject(string cesta)
        {
            if (string.IsNullOrWhiteSpace(cesta) || RecentProjects == null) return;
            RecentProjects.RemoveAll(x => string.Equals(x, cesta, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        public static string? SettingsPathOverride { get; set; } = null;

        private static string GetSettingsPath()
        {
            if (!string.IsNullOrEmpty(SettingsPathOverride)) return SettingsPathOverride;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "CodePlanner", "settings.json");
        }

        public static GeminiSettings Load()
        {
            try
            {
                string cesta = GetSettingsPath();
                if (File.Exists(cesta))
                {
                    string json = File.ReadAllText(cesta);
                    var nastaveni = JsonSerializer.Deserialize<GeminiSettings>(json);
                    if (nastaveni != null)
                    {
                        if (!string.IsNullOrWhiteSpace(nastaveni.GeminiApiKey))
                        {
                            try
                            {
                                byte[] cipher = Convert.FromBase64String(nastaveni.GeminiApiKey);
                                byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                                nastaveni.GeminiApiKey = Encoding.UTF8.GetString(plain);
                            }
                            catch
                            {
                                // Pokud dešifrování selže (např. starý plaintext klíč),
                                // ponecháme jej beze změny (při příštím uložení se zašifruje).
                            }
                        }
                        return nastaveni;
                    }
                }
            }
            catch
            {
                // V případě chyby vrátíme výchozí nastavení
            }

            return new GeminiSettings();
        }

        public void Save()
        {
            try
            {
                string cesta = GetSettingsPath();
                string? slozka = Path.GetDirectoryName(cesta);
                if (!string.IsNullOrEmpty(slozka) && !Directory.Exists(slozka))
                {
                    Directory.CreateDirectory(slozka);
                }

                // Vytvoříme klon s šifrovaným klíčem pro uložení
                var clone = new GeminiSettings
                {
                    GeminiModel = this.GeminiModel,
                    RecentProjects = new List<string>(this.RecentProjects ?? new List<string>())
                };

                if (!string.IsNullOrWhiteSpace(this.GeminiApiKey))
                {
                    byte[] plain = Encoding.UTF8.GetBytes(this.GeminiApiKey.Trim());
                    byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                    clone.GeminiApiKey = Convert.ToBase64String(cipher);
                }

                var opt = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(clone, opt);
                File.WriteAllText(cesta, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("Nepodařilo se uložit nastavení: " + ex.Message, ex);
            }
        }

        [JsonIgnore]
        public string EffectiveApiKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(GeminiApiKey)) return GeminiApiKey.Trim();
                string? envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                return envKey != null ? envKey.Trim() : "";
            }
        }
    }



    public class GeminiDynamicResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("questions")]
        public List<GeminiDynamicQuestion> Questions { get; set; } = new List<GeminiDynamicQuestion>();
    }

    public class GeminiDynamicQuestion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        [JsonPropertyName("impact")]
        public string Impact { get; set; } = "Medium"; // High / Medium

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("helpText")]
        public string HelpText { get; set; } = "";

        [JsonPropertyName("defaultAssumption")]
        public string DefaultAssumption { get; set; } = "";

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = "";

        [JsonPropertyName("isAssumption")]
        public bool IsAssumption { get; set; }

        [JsonPropertyName("options")]
        public List<string> Options { get; set; } = new List<string>();
    }

    public class GeminiConsistencyResult
    {
        [JsonPropertyName("findings")]
        public List<GeminiConsistencyFinding> Findings { get; set; } = new List<GeminiConsistencyFinding>();
    }

    public class GeminiConsistencyFinding
    {
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "Warning";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "";
    }

    public class GeminiUserStoriesResult
    {
        [JsonPropertyName("stories")]
        public List<GeminiUserStory> Stories { get; set; } = new List<GeminiUserStory>();
    }

    public class GeminiUserStory
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("criteria")]
        public List<string> Criteria { get; set; } = new List<string>();

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "Medium";
    }

    /// <summary>
    /// Služba zajišťující volání Gemini API pro strukturovanou analýzu.
    /// </summary>
    public static class GeminiService
    {
        private static readonly HttpClient Client;

        static GeminiService()
        {
            Client = new HttpClient();
            Client.Timeout = TimeSpan.FromSeconds(90);
        }

        private static string CleanJson(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return "{}";
            string clean = rawJson.Trim();
            if (clean.StartsWith("```json")) clean = clean.Substring(7);
            else if (clean.StartsWith("```")) clean = clean.Substring(3);
            if (clean.EndsWith("```")) clean = clean.Substring(0, clean.Length - 3);
            return clean.Trim();
        }

        /// <summary>Ořízne příliš dlouhý text pro prompt na daný počet znaků a doplní poznámku o zkrácení.</summary>
        public static string OrezText(string text, int maxZnaku = 100_000)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxZnaku) return text;
            return text.Substring(0, maxZnaku) + Environment.NewLine + "[…zkráceno]";
        }

        /// <summary>Cesta k souboru s poslední surovou AI odpovědí – ukládá se sem při chybě parsování pro diagnostiku.</summary>
        public static string CestaPosledniAiOdpovedi
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodePlanner", "posledni_ai_odpoved.txt");

        private static void UlozSurovouAiOdpoved(string surovaOdpoved)
        {
            try
            {
                string cesta = CestaPosledniAiOdpovedi;
                string? slozka = Path.GetDirectoryName(cesta);
                if (!string.IsNullOrEmpty(slozka)) Directory.CreateDirectory(slozka);
                
                string zkracena = surovaOdpoved ?? "";
                if (zkracena.Length > 100000)
                {
                    zkracena = zkracena.Substring(0, 100000) + "\r\n[Zkráceno z důvodu velikosti...]";
                }
                File.WriteAllText(cesta, zkracena, Encoding.UTF8);
            }
            catch
            {
                // Diagnostický zápis nesmí shodit hlavní operaci.
            }
        }

        private static readonly JsonSerializerOptions AiJsonOpt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>Deserializace JSON odpovědi od AI. Při nevalidním JSONu uloží surovou odpověď
        /// do %AppData%\CodePlanner\posledni_ai_odpoved.txt a vyhodí srozumitelnou výjimku (inner = JsonException).</summary>
        private static T DeserializujAiOdpoved<T>(string surovaOdpoved) where T : class
        {
            string cleanText = CleanJson(surovaOdpoved);
            T? vysledek;
            try
            {
                vysledek = JsonSerializer.Deserialize<T>(cleanText, AiJsonOpt);
            }
            catch (JsonException ex)
            {
                UlozSurovouAiOdpoved(surovaOdpoved);
                throw new Exception("AI returned an answer in an unexpected format. The diagnostic log has been saved to the settings folder (posledni_ai_odpoved.txt). Please try again.", ex);
            }

            if (vysledek == null)
            {
                UlozSurovouAiOdpoved(surovaOdpoved);
                throw new Exception("AI returned an answer in an unexpected format. The diagnostic log has been saved to the settings folder (posledni_ai_odpoved.txt). Please try again.");
            }

            // Smažeme diagnostickou odpověď z disku, pokud se deserializace podařila
            try
            {
                string cesta = CestaPosledniAiOdpovedi;
                if (File.Exists(cesta)) File.Delete(cesta);
            }
            catch { }

            return vysledek;
        }

        public static async Task TestConnectionAsync(string apiKey, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč nesmí být prázdný.");

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = "Say 'OK' and nothing else." }
                        }
                    }
                }
            };

            await PosliGeminiRequestAsync(apiKey, model, requestBody, default);
        }

        /// <summary>Interní výjimka nesoucí HTTP status kód – slouží k rozhodnutí, zda má smysl pokus opakovat.</summary>
        private sealed class GeminiApiException : Exception
        {
            public int StatusCode { get; }

            public GeminiApiException(string message, int statusCode) : base(message)
            {
                StatusCode = statusCode;
            }
        }

        /// <summary>Pauzy mezi automatickými opakováními při dočasné chybě (max. 2 opakování: 2 s a 5 s).</summary>
        private static readonly TimeSpan[] PauzyMeziPokusy = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };

        /// <summary>Dočasné chyby, u kterých má smysl automaticky opakovat: HTTP 429/500/502/503, timeout a síťová chyba.</summary>
        private static bool JeDocasnaChyba(Exception ex)
        {
            if (ex is GeminiApiException api)
                return api.StatusCode == 429 || api.StatusCode == 500 || api.StatusCode == 502 || api.StatusCode == 503;
            return ex is TimeoutException || ex is HttpRequestException;
        }

        private static async Task<string> PosliGeminiRequestAsync(string apiKey, string model, object requestBody, CancellationToken cancellationToken)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            string requestJson = JsonSerializer.Serialize(requestBody);

            for (int pokus = 0; ; pokus++)
            {
                try
                {
                    return await PosliGeminiRequestJednouAsync(url, apiKey, requestJson, cancellationToken);
                }
                catch (Exception ex) when (pokus < PauzyMeziPokusy.Length && JeDocasnaChyba(ex) && !cancellationToken.IsCancellationRequested)
                {
                    // Dočasná chyba – počkáme a zkusíme to znovu. Task.Delay s tokenem se při zrušení okamžitě ukončí.
                    await Task.Delay(PauzyMeziPokusy[pokus], cancellationToken);
                }
            }
        }

        private static async Task<string> PosliGeminiRequestJednouAsync(string url, string apiKey, string requestJson, CancellationToken cancellationToken)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("x-goog-api-key", apiKey);
            requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            try
            {
                using var response = await Client.SendAsync(requestMessage, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = "";
                    try { errContent = await response.Content.ReadAsStringAsync(); } catch { }
                    
                    string? friendlyMsg = null;
                    if (!string.IsNullOrEmpty(errContent))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(errContent);
                            if (doc.RootElement.TryGetProperty("error", out var errorProp))
                            {
                                if (errorProp.TryGetProperty("message", out var msgProp))
                                {
                                    string? origMsg = msgProp.GetString();
                                    if (origMsg != null)
                                    {
                                        if (origMsg.Contains("API key not valid") || origMsg.Contains("API_KEY_INVALID") || origMsg.Contains("key is invalid"))
                                        {
                                            friendlyMsg = "The provided API key is invalid. Please verify your key in AI Settings.";
                                        }
                                        else if (origMsg.Contains("Quota exceeded") || origMsg.Contains("RESOURCE_EXHAUSTED") || origMsg.Contains("429"))
                                        {
                                            friendlyMsg = "Request limit exceeded (Quota Exceeded / Rate Limit). Please try again in a minute.";
                                        }
                                        else
                                        {
                                            friendlyMsg = $"API Error: {origMsg}";
                                        }
                                    }
                                }
                             }
                         }
                        catch { }
                    }

                    if (friendlyMsg == null)
                    {
                        friendlyMsg = $"Gemini API returned error {(int)response.StatusCode} ({response.ReasonPhrase}).";
                        if (!string.IsNullOrEmpty(errContent)) friendlyMsg += $" Detail: {errContent}";
                    }
                    throw new GeminiApiException(friendlyMsg, (int)response.StatusCode);
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                return ZiskejTextZCandidate(responseJson);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The request to Gemini API timed out. Please check your internet connection and Gemini service status.", ex);
            }
        }

        private static string ZiskejTextZCandidate(string responseJson)
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                throw new Exception("V odpovědi Gemini API chybí kandidáti (candidates).");

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) || 
                !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                throw new Exception("V odpovědi Gemini API chybí obsah kandidáta (content.parts).");

            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textProp))
                throw new Exception("Odpověď z Gemini API neobsahuje textové pole.");

            return textProp.GetString() ?? "";
        }

        public static string SestavPrompt(string napad, string typProjektuKlic, string? referencniText = null, bool maMockup = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert in software analysis and creating specifications for coding agents.");
            sb.AppendLine($"Your task is to analyze the user's original idea for a \"{SpecificationService.GetProjectTypeName(typProjektuKlic)}\" application and propose:");
            sb.AppendLine("1. A short, concise project name (maximum 5 words).");
            sb.AppendLine("2. A list of 7 to 10 additional specification questions tailored specifically to this project and its technical domain.");
            sb.AppendLine();
            sb.AppendLine("Rules for questions:");
            sb.AppendLine("- Each question must belong to one of these sections: Target & Users, Scope, UX, Data, Technology, Acceptance, Risks.");
            sb.AppendLine("- Questions must be specific (e.g. for a fitness app ask about sensors/APIs, for an e-shop ask about payment gateways, not generic questions).");
            sb.AppendLine("- Each question must have a specified impact (High for architecture/pricing/security, Medium for UX/minor details).");
            sb.AppendLine("- If applicable, reuse the following standard identifiers (IDs) to map them to these domains:");
            sb.AppendLine("  * `cil-problem` (for project purpose/main problem)");
            sb.AppendLine("  * `cil-uzivatele` (for roles/users)");
            sb.AppendLine("  * `tech-platforma` (for target platform/languages)");
            sb.AppendLine("  * `tech-offline` (for offline/online mode)");
            sb.AppendLine("  * `data-obsah` (for stored data/database)");
            sb.AppendLine("  * `data-export` (for export/print)");
            sb.AppendLine("  * `rozsah-nongoals` (for what the application will definitely NOT do)");
            sb.AppendLine("  * `akceptace` (for completion criteria)");
            sb.AppendLine("  * `ux-styl` (for visual style/DPI/screens)");
            sb.AppendLine("  * `rizika-reseni` (for risks and mitigation)");
            sb.AppendLine("- If the project requires a specific question outside of these standard ones, create a new descriptive identifier for it.");
            sb.AppendLine("- For each question, propose:");
            sb.AppendLine("  a) A help text for the user (how to think about the question).");
            sb.AppendLine("  b) A default recommended assumption to use if the user does not know.");
            sb.AppendLine("  c) A tailored answer: If the user's original idea implies an answer, formulate it and set \"isAssumption\": false. If the information is missing from the idea, fill in the default assumption text and set \"isAssumption\": true.");
            sb.AppendLine("  d) Exactly 3 short, concrete options for a quick answer (array of strings named \"options\") that the user can select (e.g. [\"SQLite local DB\", \"PostgreSQL in cloud\", \"JSON files local storage\"]).");
            sb.AppendLine("- Write all generated questions, help texts, default assumptions, options, and answers in Czech, because the user speaks Czech.");
            sb.AppendLine();
            sb.AppendLine("Here are the standard template questions for your inspiration:");

            foreach (var ot in StandardQuestions.All)
            {
                sb.AppendLine($"- Default ID: {ot.Id}");
                sb.AppendLine($"  Default section: {ot.Section}");
                sb.AppendLine($"  Default question: {ot.GetText(typProjektuKlic)}");
                sb.AppendLine($"  Default assumption: {ot.GetDefaultAssumption(typProjektuKlic)}");
            }

            sb.AppendLine();
            sb.AppendLine("Respond exclusively in JSON format matching the following schema. Return only the raw JSON. Do not wrap it in markdown code blocks like ```json.");
            sb.AppendLine("{");
            sb.AppendLine("  \"name\": \"Suggested project name\",");
            sb.AppendLine("  \"questions\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"id\": \"question-identifier\",");
            sb.AppendLine("      \"section\": \"Section Name (Target & Users, Scope, UX, Data, Technology, Acceptance, or Risks)\",");
            sb.AppendLine("      \"impact\": \"High\" or \"Medium\",");
            sb.AppendLine("      \"text\": \"Tailored question in Czech\",");
            sb.AppendLine("      \"helpText\": \"Tailored help text in Czech\",");
            sb.AppendLine("      \"defaultAssumption\": \"Tailored default assumption in Czech\",");
            sb.AppendLine("      \"options\": [\"quick choice 1 in Czech\", \"quick choice 2 in Czech\", \"quick choice 3 in Czech\"],");
            sb.AppendLine("      \"answer\": \"Suggested answer or default assumption text in Czech\",");
            sb.AppendLine("      \"isAssumption\": true or false");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Here is the user's original idea:");
            sb.AppendLine(napad);

            if (!string.IsNullOrWhiteSpace(referencniText))
            {
                sb.AppendLine();
                sb.AppendLine("The user has also attached the following reference material for the project. Use it to provide more precise questions and answers:");
                sb.AppendLine(OrezText(referencniText));
            }

            if (maMockup)
            {
                sb.AppendLine();
                sb.AppendLine("The user has also attached a UI mockup/screenshot as an image. Inspect this image and use it to adjust the questions, help texts, and answers so they align with the visual mockup.");
            }

            return sb.ToString();
        }

        public static async Task<GeminiDynamicResult> AnalyzeIdeaAsync(string apiKey, string model, string napad, string typProjektuKlic, string? referencniText = null, string? mockupBase64 = null, string? mockupMime = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            if (string.IsNullOrWhiteSpace(napad))
                throw new ArgumentException("Nápad k analýze nesmí být prázdný.");

            var partsList = new List<object>
            {
                new { text = SestavPrompt(napad, typProjektuKlic, referencniText, !string.IsNullOrWhiteSpace(mockupBase64)) }
            };

            if (!string.IsNullOrWhiteSpace(mockupBase64))
            {
                partsList.Add(new
                {
                    inlineData = new
                    {
                        mimeType = mockupMime ?? "image/png",
                        data = mockupBase64
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = partsList.ToArray()
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            if (string.IsNullOrWhiteSpace(textResponse))
                throw new Exception("Odpověď z Gemini API je prázdná.");

            var result = DeserializujAiOdpoved<GeminiDynamicResult>(textResponse);
            if (result == null) throw new Exception("AI vrátila prázdný výsledek.");
            if (result.Questions == null) result.Questions = new List<GeminiDynamicQuestion>();
            
            // Odstraníme otázky bez ID nebo bez textu
            result.Questions.RemoveAll(q => string.IsNullOrWhiteSpace(q.Id) || string.IsNullOrWhiteSpace(q.Text));
            if (result.Questions.Count == 0) throw new Exception("AI analýza nevrátila žádné platné otázky.");
            return result;
        }

        public static async Task<string> TranscribeAudioAsync(string apiKey, string model, string cestaWav, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            if (!File.Exists(cestaWav))
                throw new FileNotFoundException("Zvukový soubor nebyl nalezen.", cestaWav);

            byte[] audioBytes = File.ReadAllBytes(cestaWav);
            string base64Audio = Convert.ToBase64String(audioBytes);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = "audio/wav",
                                    data = base64Audio
                                }
                            },
                            new
                            {
                                text = "Transcribe the audio exactly as spoken. Output only the transcript in Czech, without any introductory or concluding text."
                            }
                        }
                    }
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            return textResponse?.Trim() ?? "";
        }

        public static async Task<List<ConsistencyFinding>> AnalyzeConsistencyAsync(string apiKey, string model, ProjectSpecification projekt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key for Gemini cannot be empty.");

            var sb = new StringBuilder();
            sb.AppendLine("You are an expert in software architecture and requirements analysis.");
            sb.AppendLine("Your task is to analyze the following project specification and find any logical conflicts, technical inconsistencies, security gaps, or missing links between requirements and proposed answers.");
            sb.AppendLine();
            sb.AppendLine("Focus on:");
            sb.AppendLine("1. True logical conflicts (e.g. one answer says data is purely local, another talks about server sync).");
            sb.AppendLine("2. Technical inconsistencies (e.g. SQLite database for a serverless web app, or credit card payments without SSL/security).");
            sb.AppendLine("3. Critical gaps (e.g. users define 'Administrator' role but authentication is missing).");
            sb.AppendLine("4. Unrealistic or vague plans.");
            sb.AppendLine("- Write the title and detail of each finding in Czech because the user speaks Czech.");
            sb.AppendLine();
            sb.AppendLine("Respond exclusively in JSON format matching the following schema. Return only the raw JSON. Do not wrap it in markdown code blocks.");
            sb.AppendLine("{");
            sb.AppendLine("  \"findings\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"severity\": \"Conflict\" or \"Warning\",");
            sb.AppendLine("      \"title\": \"Title of the problem in Czech\",");
            sb.AppendLine("      \"detail\": \"Detailed description of the conflict or gap and suggested resolution in Czech.\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Here is the complete project specification:");
            sb.AppendLine(OrezText(SpecificationService.RenderMarkdown(projekt)));

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = sb.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            var vysledek = DeserializujAiOdpoved<GeminiConsistencyResult>(textResponse);

            var findings = new List<ConsistencyFinding>();
            if (vysledek != null && vysledek.Findings != null)
            {
                foreach (var n in vysledek.Findings)
                {
                    if (string.IsNullOrWhiteSpace(n.Title)) continue;
                    findings.Add(new ConsistencyFinding
                    {
                        Severity = string.Equals(n.Severity, "Conflict", StringComparison.OrdinalIgnoreCase) || string.Equals(n.Severity, "Rozpor", StringComparison.OrdinalIgnoreCase) ? Severity.Conflict : Severity.Warning,
                        Title = n.Title.Trim(),
                        Detail = (n.Detail ?? "").Trim()
                    });
                }
            }

            return findings;
        }

        public static async Task<List<UserStory>> GenerateUserStoriesAsync(string apiKey, string model, ProjectSpecification projekt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key for Gemini cannot be empty.");

            var sb = new StringBuilder();
            sb.AppendLine("You are an agile coach and software analyst.");
            sb.AppendLine("Your task is to propose a set of concrete, actionable, and clear User Stories for developers based on the project specification below.");
            sb.AppendLine();
            sb.AppendLine("For each User Story, define:");
            sb.AppendLine("1. A unique identifier (e.g. US-01, US-02). Generate between 8 and 15 stories depending on the scope.");
            sb.AppendLine("2. A short, concise title.");
            sb.AppendLine("3. Description in the standard format: 'Jako [role] chci [funkce], abych [přínos].' (write this in Czech).");
            sb.AppendLine("4. A list of 3 to 6 concrete, testable Acceptance Criteria (write these in Czech).");
            sb.AppendLine("5. Priority: 'High', 'Medium', or 'Low'.");
            sb.AppendLine("- All titles, descriptions, and criteria must be written in Czech because the user speaks Czech.");
            sb.AppendLine();
            sb.AppendLine("Respond exclusively in JSON format matching the following schema. Return only the raw JSON. Do not wrap it in markdown code blocks.");
            sb.AppendLine("{");
            sb.AppendLine("  \"stories\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"id\": \"US-01\",");
            sb.AppendLine("      \"title\": \"Title of the user story in Czech\",");
            sb.AppendLine("      \"description\": \"Jako... chci... abych... in Czech\",");
            sb.AppendLine("      \"criteria\": [");
            sb.AppendLine("        \"criterion 1 in Czech\",");
            sb.AppendLine("        \"criterion 2 in Czech\"");
            sb.AppendLine("      ],");
            sb.AppendLine("      \"priority\": \"High\", \"Medium\", or \"Low\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Here is the complete project specification:");
            sb.AppendLine(OrezText(SpecificationService.RenderMarkdown(projekt)));

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = sb.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            var vysledek = DeserializujAiOdpoved<GeminiUserStoriesResult>(textResponse);

            var stories = new List<UserStory>();
            if (vysledek != null && vysledek.Stories != null)
            {
                var pouzitaId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in vysledek.Stories)
                {
                    if (string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Title)) continue;
                    
                    string idClean = s.Id.Trim();
                    if (pouzitaId.Contains(idClean)) continue;
                    pouzitaId.Add(idClean);

                    string prioMapped = "Medium";
                    if (string.Equals(s.Priority, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(s.Priority, "Vysoká", StringComparison.OrdinalIgnoreCase))
                    {
                        prioMapped = "High";
                    }
                    else if (string.Equals(s.Priority, "Low", StringComparison.OrdinalIgnoreCase) || string.Equals(s.Priority, "Nízká", StringComparison.OrdinalIgnoreCase))
                    {
                        prioMapped = "Low";
                    }

                    stories.Add(new UserStory
                    {
                        Id = idClean,
                        Title = s.Title.Trim(),
                        Description = (s.Description ?? "").Trim(),
                        Criteria = s.Criteria ?? new List<string>(),
                        Priority = prioMapped
                    });
                }
            }

            return stories;
        }

        public static async Task<string> SendChatMessageAsync(string apiKey, string model, ProjectSpecification projekt, List<ChatMessage> novyChatLog, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            string specMarkdown = OrezText(SpecificationService.RenderMarkdown(projekt));
            string systemPrompt = "Jsi zkušený softwarový architekt, agilní kouč a seniorní vývojář. Odpovídáš na dotazy ohledně navrhovaného projektu.\n\n" +
                                 "Zde je kompletní specifikace projektu, která je tvým jediným zdrojem pravdy o cílech a parametrech systému. Všechny své odpovědi přizpůsob tomuto kontextu:\n\n" +
                                 specMarkdown + "\n\n" +
                                 "Odpovídej přímo, konstruktivně a srozumitelně v češtině. Pomáhej s architekturou, databázemi, návrhem rozhraní, kódem nebo testováním.";

            var vsechnyZpravy = novyChatLog ?? new List<ChatMessage>();

            var zpravy = vsechnyZpravy.Count > 20
                ? vsechnyZpravy.Skip(vsechnyZpravy.Count - 20).ToList()
                : vsechnyZpravy;

            bool prilozitMockup = vsechnyZpravy.Count == 1 && !string.IsNullOrWhiteSpace(projekt.MockupBase64);

            var turnsList = new List<object>();
            for (int i = 0; i < zpravy.Count; i++)
            {
                var msg = zpravy[i];
                var parts = new List<object>
                {
                    new { text = msg.Text }
                };

                if (prilozitMockup && i == 0 && string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    string mime = (projekt.MockupName != null && (projekt.MockupName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || projekt.MockupName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))) ? "image/jpeg" : "image/png";
                    parts.Add(new
                    {
                        inlineData = new
                        {
                            mimeType = mime,
                            data = projekt.MockupBase64
                        }
                    });
                }

                turnsList.Add(new
                {
                    role = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase) ? "user" : "model",
                    parts = parts.ToArray()
                });
            }

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                },
                contents = turnsList.ToArray()
            };

            return await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
        }

        public static async Task<ProjectMetrics> GenerateMetricsAsync(string apiKey, string model, ProjectSpecification projekt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key for Gemini cannot be empty.");

            var sb = new StringBuilder();
            sb.AppendLine("You are a senior project manager, IT architect, and software estimator.");
            sb.AppendLine("Your task is to analyze the complete project specification and parameters to estimate effort, suggest team composition, recommend a budget, and write a technical breakdown.");
            sb.AppendLine();
            sb.AppendLine("Respond exclusively in JSON format matching the following schema. Return only the raw JSON. Do not wrap it in markdown code blocks.");
            sb.AppendLine("{");
            sb.AppendLine("  \"timeEstimateMin\": \"E.g. 80 hours\",");
            sb.AppendLine("  \"timeEstimateMax\": \"E.g. 120 hours\",");
            sb.AppendLine("  \"complexity\": \"Low\", \"Medium\", or \"High\",");
            sb.AppendLine("  \"teamComposition\": \"Recommended developer and tester roles in Czech\",");
            sb.AppendLine("  \"recommendedBudget\": \"E.g. 100 000 - 150 000 CZK in Czech\",");
            sb.AppendLine("  \"technicalAnalysis\": \"Short bullet points with recommendations for architecture, technologies, and databases in Czech\",");
            sb.AppendLine("  \"metricRisks\": [\"risk 1 in Czech\", \"risk 2 in Czech\", \"risk 3 in Czech\"]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Here is the current project specification:");
            sb.AppendLine(OrezText(SpecificationService.RenderMarkdown(projekt)));

            if (projekt.UserStories != null && projekt.UserStories.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Here is the list of agile User Stories for a more accurate estimate:");
                foreach (var us in projekt.UserStories)
                {
                    sb.AppendLine($"- {us.Id}: {us.Title} (Priority: {us.Priority})");
                }
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = sb.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            var vysledek = DeserializujAiOdpoved<GeminiMetricsResult>(textResponse);
            if (vysledek == null) throw new Exception("AI returned empty result for metrics.");

            string compMapped = "Medium";
            if (string.Equals(vysledek.Complexity, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(vysledek.Complexity, "Vysoká", StringComparison.OrdinalIgnoreCase))
            {
                compMapped = "High";
            }
            else if (string.Equals(vysledek.Complexity, "Low", StringComparison.OrdinalIgnoreCase) || string.Equals(vysledek.Complexity, "Nízká", StringComparison.OrdinalIgnoreCase))
            {
                compMapped = "Low";
            }

            return new ProjectMetrics
            {
                TimeEstimateMin = (vysledek.TimeEstimateMin ?? "").Trim(),
                TimeEstimateMax = (vysledek.TimeEstimateMax ?? "").Trim(),
                Complexity = compMapped,
                TeamComposition = (vysledek.TeamComposition ?? "").Trim(),
                RecommendedBudget = (vysledek.RecommendedBudget ?? "").Trim(),
                TechnicalAnalysis = (vysledek.TechnicalAnalysis ?? "").Trim(),
                MetricRisks = (vysledek.MetricRisks ?? new List<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList(),
                CalculationTimestamp = DateTime.Now
            };
        }
    }


    public class GeminiMetricsResult
    {
        [JsonPropertyName("timeEstimateMin")]
        public string TimeEstimateMin { get; set; } = "";

        [JsonPropertyName("timeEstimateMax")]
        public string TimeEstimateMax { get; set; } = "";

        [JsonPropertyName("complexity")]
        public string Complexity { get; set; } = "";

        [JsonPropertyName("teamComposition")]
        public string TeamComposition { get; set; } = "";

        [JsonPropertyName("recommendedBudget")]
        public string RecommendedBudget { get; set; } = "";

        [JsonPropertyName("technicalAnalysis")]
        public string TechnicalAnalysis { get; set; } = "";

        [JsonPropertyName("metricRisks")]
        public List<string> MetricRisks { get; set; } = new List<string>();
    }
}
// end of file
