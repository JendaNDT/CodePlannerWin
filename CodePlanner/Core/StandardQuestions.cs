using System;
using System.Collections.Generic;
using System.Linq;

namespace CodePlanner.Core
{
    public static class StandardQuestions
    {
        public static readonly IReadOnlyList<Question> All = new List<Question>
        {
            new Question { Id = "cil-problem", Section = "Target & Users", Impact = Impact.High,
                Text = "What problem is the application supposed to solve and what value do you expect from it?",
                HelpText = "Defines the purpose of the specification – everything else follows from this.",
                DefaultAssumption = "The value is only described in the original idea; it will be refined after the first demo.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "What experience should the game bring and what is the player's goal?" },
                    { ProjectType.Registry, "What objects will be tracked and what benefit should their overview bring?" },
                    { ProjectType.Tool, "What task or operation should the tool automate and what is the goal?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "For games, the main benefit is fun, gameplay, relaxation, or high score." },
                    { ProjectType.Registry, "For registries/databases, it is about clarity, quick search, and reliable storage." },
                    { ProjectType.Tool, "For tools, it is about saving time and eliminating human errors in routine work." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "A game for fun, player's goal is to achieve the highest score." },
                    { ProjectType.Registry, "Tracking and clear storage of specific records with filtering capabilities." },
                    { ProjectType.Tool, "A single-purpose tool to automate a specific task and save time." }
                }
            },

            new Question { Id = "cil-uzivatele", Section = "Target & Users", Impact = Impact.High,
                Text = "Who will use the application? (roles, computer experience, how many people)",
                HelpText = "Different UX for receptionists than for developers. Affects interface complexity.",
                DefaultAssumption = "Single user – the author of the idea.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Who are the target players? (age, casual, hardcore, local multiplayer?)" },
                    { ProjectType.Registry, "Who will manage the data and who will only view it? (roles, permissions)" },
                    { ProjectType.Tool, "Who is the typical user of the tool and what are their technical skills?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Affects difficulty, controls, and presence of multiplayer." },
                    { ProjectType.Registry, "Determines whether we need user accounts and access levels." },
                    { ProjectType.Tool, "Tools are often run by developers via CLI, or by users in a simple GUI." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "For regular casual players of all age categories, simple controls." },
                    { ProjectType.Registry, "Administrator inserts and modifies records, regular user only reads." },
                    { ProjectType.Tool, "Technically skilled user who understands the task being executed." }
                }
            },

            new Question { Id = "rozsah-funkce", Section = "Scope", Impact = Impact.High,
                Text = "What are the 3 most important features without which the application makes no sense?",
                HelpText = "So-called MVP (Minimum Viable Product). The rest of features are postponed for later.",
                DefaultAssumption = "The MVP contains basic display and editing of the main object.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "What are the main game mechanics? (e.g. movement, shooting, collecting points)" },
                    { ProjectType.Registry, "What 3 main operations must we be able to do with records? (e.g. import, filters, print)" },
                    { ProjectType.Tool, "What are the key processing steps? (e.g. load input, transform, write output)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "The gameplay loop, which the player does 90% of the time." },
                    { ProjectType.Registry, "For example, fast fulltext search, export to Excel, and field editing." },
                    { ProjectType.Tool, "Describe the sequence of steps for how the tool processes input into the desired output." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Character movement, obstacle interaction, and score counter." },
                    { ProjectType.Registry, "Record creation, search by name, and export to CSV." },
                    { ProjectType.Tool, "Loading configuration file, performing analysis, and printing errors." }
                }
            },

            new Question { Id = "rozsah-nongoals", Section = "Scope", Impact = Impact.Medium,
                Text = "What will the application definitely NOT do in the first version? (Non-Goals)",
                HelpText = "Important for defining boundaries – e.g. no mobile app, no cloud, no roles.",
                DefaultAssumption = "The first version does not address user login, synchronization, or custom design.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "What will definitely not be in the game in the first version? (multiplayer, 3D graphics, online leaderboards...)" },
                    { ProjectType.Registry, "What features will we postpone to the second version? (change history, batch editing, API...)" },
                    { ProjectType.Tool, "What will the tool not address? (automatic execution, GUI interface, complex formats...)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Keep the scope grounded. Golden rule: make local singleplayer first." },
                    { ProjectType.Registry, "For registries/databases, advanced reporting and automatic notifications are often postponed." },
                    { ProjectType.Tool, "CLI tools typically do not address GUI wrappers or OS integrations in the first version." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "No online multiplayer, no microtransactions, only local score storage." },
                    { ProjectType.Registry, "No connection to external billing systems, no batch data modifications." },
                    { ProjectType.Tool, "No task scheduler (cron) and no web API for integration." }
                }
            },

            new Question { Id = "ux-styl", Section = "UX", Impact = Impact.Medium,
                Text = "What look and interface style do you prefer? (minimalist, dark mode, retro...)",
                HelpText = "Determines the design system and aesthetic demands on the application.",
                DefaultAssumption = "Clean, modern look with dark mode support.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "What graphic style and camera view does the game use? (2D side view, retro pixel art, 3D...)" },
                    { ProjectType.Registry, "Is information density (tables) or spacious design (cards) more important?" },
                    { ProjectType.Tool, "Do you prefer a purely text-based CLI (command line) or simple GUI forms?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Game aesthetics fundamentally affect the complexity of graphics assets." },
                    { ProjectType.Registry, "Admin systems require clear and dense tables for fast work." },
                    { ProjectType.Tool, "CLI interface is faster to develop and popular among programmers." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Simple 2D graphics, top-down view, clean colors." },
                    { ProjectType.Registry, "Table view with fixed header, focus on readability and contrast." },
                    { ProjectType.Tool, "Simple window with input fields and one action button." }
                }
            },

            new Question { Id = "tech-platforma", Section = "Technology", Impact = Impact.High,
                Text = "On what devices and platforms should the application run? (web, Windows, mobile...)",
                HelpText = "Crucial for choosing technology stack (C#, React, Swift...).",
                DefaultAssumption = "Desktop application for Windows 10/11.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Where will the game be played? (in browser, on mobile, on PC/Steam, consoles?)" },
                    { ProjectType.Registry, "How will users access the registry? (from local network, internet, mobile)" },
                    { ProjectType.Tool, "Where will the tool be run? (locally on developer's PC, on a Linux server...)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Browser (HTML5/WASM) is the easiest for quick sharing with players." },
                    { ProjectType.Registry, "Web app in browser is the standard for most registries today." },
                    { ProjectType.Tool, "Tools in .NET or Python can be easily compiled for Windows and Linux." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Web game running in a modern browser without installation." },
                    { ProjectType.Registry, "Web application accessible via standard browser from local network." },
                    { ProjectType.Tool, "Local command-line application (CLI) for Windows and Linux." }
                }
            },

            new Question { Id = "tech-offline", Section = "Technology", Impact = Impact.High,
                Text = "Must the application work fully offline, or is constant internet expected?",
                HelpText = "Offline requires a local database (SQLite), online uses cloud API.",
                DefaultAssumption = "Fully online application requiring internet connection.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Is the game state and score saved locally on the device, or on a cloud server?" },
                    { ProjectType.Registry, "Can data be edited without connection (offline sync), or only online?" },
                    { ProjectType.Tool, "Will the tool call an external API, or does it work purely with local files?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Local storage (LocalStorage) is easiest, cloud enables leaderboards." },
                    { ProjectType.Registry, "Offline-first apps with later sync are technically very demanding." },
                    { ProjectType.Tool, "If the tool processes sensitive data, offline operation increases security." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Local progress saving on the device (IndexedDB / LocalStorage)." },
                    { ProjectType.Registry, "Requires a constant internet connection and a central database." },
                    { ProjectType.Tool, "Purely local file processing without any network traffic." }
                }
            },

            new Question { Id = "data-obsah", Section = "Data", Impact = Impact.High,
                Text = "What data will we store and how long do we need to keep it?",
                HelpText = "Determines requirements for data storage, database, and data security.",
                DefaultAssumption = "Data is stored permanently in a local database (e.g. SQLite).",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Is the game progress (save game), settings, and high score saved?" },
                    { ProjectType.Registry, "What specific information about objects do we store and how big will the data be?" },
                    { ProjectType.Tool, "Does the tool store any processing history, or does it work statelessly?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Bezstavové hry (arkády) ukládají jen skóre, RPG vyžadují robustní serializaci." },
                    { ProjectType.Registry, "Evidenční karty obsahují texty, čísla, data založení a případně přílohy." },
                    { ProjectType.Tool, "Bezstavový nástroj (input->output) je řádově jednodušší a bezpečnější." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Only a table with the top 10 results (name and score) is stored." },
                    { ProjectType.Registry, "Structured text and numeric data with change history is stored." },
                    { ProjectType.Tool, "Stateless tool – settings are loaded from command-line arguments." }
                }
            },

            new Question { Id = "data-export", Section = "Data", Impact = Impact.Medium,
                Text = "Do you require data export for other systems? (e.g. to Excel, CSV, PDF)",
                HelpText = "Key for reports and data portability. Often forgotten.",
                DefaultAssumption = "No automatic data export to external formats is required.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Will it be possible to share scores or replays on social networks or export an image?" },
                    { ProjectType.Registry, "Do managers need table exports to Excel (XLSX) or PDF for printing?" },
                    { ProjectType.Tool, "In what format does the tool return results? (JSON to console, HTML report, log...)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Generating a screenshot with game results is a great marketing tool." },
                    { ProjectType.Registry, "Exporting to CSV/XLSX is almost always a must for further data analysis." },
                    { ProjectType.Tool, "Machine-readable output (JSON/XML) allows integration of the tool into CI/CD pipelines." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Sharing results in the form of a generated image with statistics." },
                    { ProjectType.Registry, "Option to export the currently filtered table to CSV (for Excel)." },
                    { ProjectType.Tool, "The tool writes results to a standard text log and JSON file." }
                }
            },

            new Question { Id = "rizika-reseni", Section = "Risks", Impact = Impact.Medium,
                Text = "What is the biggest risk of the project and how will we resolve it in the first version?",
                HelpText = "E.g. internet outage, data loss, slow response, complex controls.",
                DefaultAssumption = "The biggest risk is data loss; we will resolve it with automatic local saving.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "What could annoy the player the most? (poor controls, save loss, repetition...)" },
                    { ProjectType.Registry, "What happens if the system fails? (availability, backup, data recovery)" },
                    { ProjectType.Tool, "What if the tool receives invalid or too large input? (error states)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Poor controls on mobile will reliably kill even a great game." },
                    { ProjectType.Registry, "For company registries, backup recovery speed is key in case of crash." },
                    { ProjectType.Tool, "The tool should always perform input validation and print a clear error." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Loss of game progress; resolved by auto-saving on each scene change." },
                    { ProjectType.Registry, "Data loss; resolved by daily automatic database backups to cloud." },
                    { ProjectType.Tool, "Application crash on input error; resolved by thorough exception handling." }
                }
            },

            new Question { Id = "akceptace", Section = "Acceptance", Impact = Impact.Medium,
                Text = "How will we know the application is done and works correctly? (acceptance criteria)",
                HelpText = "Defines successful completion – e.g. 'passes checkout scenario', 'loads 1000 items under 2s'.",
                DefaultAssumption = "The project is finished if it meets all specification points and passes manual test.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "When is the game ready for release? (completing whole level without bugs, stable FPS)" },
                    { ProjectType.Registry, "What specific tests must the system pass before deployment to production?" },
                    { ProjectType.Tool, "How will we verify output correctness? (reference inputs and comparison with expected result)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Usually tested by playing through from start to end and measuring smoothness (e.g. 60 FPS)." },
                    { ProjectType.Registry, "For example, successful insert, search, edit, and delete of a test record." },
                    { ProjectType.Tool, "Automated tests comparing the generated file with a reference template." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Player can complete the tutorial and first 3 levels without application crash." },
                    { ProjectType.Registry, "Complete scenario run: user registration, record creation, export." },
                    { ProjectType.Tool, "The tool correctly processes all 5 attached test cases." }
                }
            }
        };

        public static Question? Under(ProjectSpecification p, string id)
            => All.FirstOrDefault(o => o.Id == id);
    }
}
