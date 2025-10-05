using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoiceLite.Services
{
    public class TranscriptionPostProcessor
    {
        // Pre-compiled regex patterns for performance
        private static readonly Dictionary<Regex, string> TechnicalCorrections;

        // Pre-compiled regex patterns used in FixPunctuation and FixSpacing
        private static readonly Regex MultipleSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex CapitalizeAfterPeriodRegex = new Regex(@"(\. )([a-z])", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforePunctuationRegex = new Regex(@"\s+([.,!?;:])", RegexOptions.Compiled);
        private static readonly Regex SpaceAfterPunctuationRegex = new Regex(@"([.,!?;:])([A-Za-z])", RegexOptions.Compiled);
        private static readonly Regex SpaceAroundParenOpenRegex = new Regex(@"\s*\(\s*", RegexOptions.Compiled);
        private static readonly Regex SpaceAroundParenCloseRegex = new Regex(@"\s*\)\s*", RegexOptions.Compiled);
        private static readonly Regex SpaceAroundBracketOpenRegex = new Regex(@"\s*\[\s*", RegexOptions.Compiled);
        private static readonly Regex SpaceAroundBracketCloseRegex = new Regex(@"\s*\]\s*", RegexOptions.Compiled);

        // CRITICAL PERFORMANCE FIX: Pre-compile ALL context-aware regex patterns to eliminate runtime compilation
        private static readonly Regex GetHubRegex = new Regex(@"\bget\s*hub\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GetLabRegex = new Regex(@"\bget\s*lab\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GitIgnoreRegex = new Regex(@"\bgit\s*ignore\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YouStateRegex = new Regex(@"\byou\s*state\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YouEffectRegex = new Regex(@"\byou\s*effect\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YouContextRegex = new Regex(@"\byou\s*context\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YouReducerRegex = new Regex(@"\byou\s*reducer\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YouMemoRegex = new Regex(@"\byou\s*memo\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YouCallbackRegex = new Regex(@"\byou\s*callback\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PackageJasonRegex = new Regex(@"\bpackage\s*jason\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PackageLockRegex = new Regex(@"\bpackage\s*lock\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YarnLockRegex = new Regex(@"\byarn\s*lock\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex JaySonRegex = new Regex(@"\bjay\s*son\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ConsoleDotLogRegex = new Regex(@"\bconsole\s*dot\s*log\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DotThenRegex = new Regex(@"\bdot\s*then\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DotCatchRegex = new Regex(@"\bdot\s*catch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DotMapRegex = new Regex(@"\bdot\s*map\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DotFilterRegex = new Regex(@"\bdot\s*filter\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DotReduceRegex = new Regex(@"\bdot\s*reduce\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pre-compiled common misrecognition patterns
        private static readonly Regex EndPointRegex = new Regex(@"\bend\s*point\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DataBaseRegex = new Regex(@"\bdata\s*base\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FrameWorkRegex = new Regex(@"\bframe\s*work\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FrontEndRegex = new Regex(@"\bfront\s*end\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BackEndRegex = new Regex(@"\bback\s*end\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FullStackRegex = new Regex(@"\bfull\s*stack\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DevOpsRegex = new Regex(@"\bdev\s*ops\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CiCdRegex = new Regex(@"\bci\s*cd\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HttpsRegex = new Regex(@"\bhttp\s*s\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SqlLightRegex = new Regex(@"\bsql\s*light\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NoSqlRegex = new Regex(@"\bno\s*sql\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RestFullRegex = new Regex(@"\brest\s*full\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WebSocketRegex = new Regex(@"\bweb\s*socket\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LocalHostRegex = new Regex(@"\blocal\s*host\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IdeRegex = new Regex(@"\bI\s*D\s*E\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UrlRegex = new Regex(@"\bU\s*R\s*L\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UiRegex = new Regex(@"\bU\s*I\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UxRegex = new Regex(@"\bU\s*X\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SqlRegex = new Regex(@"\bS\s*Q\s*L\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex XmlRegex = new Regex(@"\bX\s*M\s*L\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlRegex = new Regex(@"\bH\s*T\s*M\s*L\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CssRegex = new Regex(@"\bC\s*S\s*S\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static TranscriptionPostProcessor()
        {
            // Pre-compile all technical correction patterns once at startup
            TechnicalCorrections = new Dictionary<Regex, string>
            {
                // Programming terms
                { new Regex(@"\bgit\s*hub\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "GitHub" },
                { new Regex(@"\bgit\s*lab\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "GitLab" },
                { new Regex(@"\bget\s*hub\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "GitHub" },
                { new Regex(@"\bnpm\s*install\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "npm install" },
                { new Regex(@"\bnode\s*js\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Node.js" },
                { new Regex(@"\breact\s*js\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "React.js" },
                { new Regex(@"\bview\s*js\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Vue.js" },
                { new Regex(@"\bnext\s*js\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Next.js" },
                { new Regex(@"\btype\s*script\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "TypeScript" },
                { new Regex(@"\bjava\s*script\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "JavaScript" },
                { new Regex(@"\bc\s*sharp\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "C#" },
                { new Regex(@"\bc\s*plus\s*plus\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "C++" },
                { new Regex(@"\bmy\s*sql\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "MySQL" },
                { new Regex(@"\bpost\s*gres\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PostgreSQL" },
                { new Regex(@"\bmongo\s*db\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "MongoDB" },
                { new Regex(@"\bred\s*is\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Redis" },
                { new Regex(@"\bdocker\s*file\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Dockerfile" },
                { new Regex(@"\byaml\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "YAML" },
                { new Regex(@"\bjson\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "JSON" },
                { new Regex(@"\bapi\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "API" },
                { new Regex(@"\brest\s*api\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "REST API" },
                { new Regex(@"\bgraph\s*ql\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "GraphQL" },
                { new Regex(@"\baws\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "AWS" },
                { new Regex(@"\blazure\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Azure" },
                { new Regex(@"\bgcp\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "GCP" },

                // Additional commonly misheard terms
                { new Regex(@"\bkubernetes\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Kubernetes" },
                { new Regex(@"\bk8s\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "k8s" },
                { new Regex(@"\belastic\s*search\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Elasticsearch" },
                { new Regex(@"\bjenkins\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Jenkins" },
                { new Regex(@"\bterraform\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Terraform" },
                { new Regex(@"\bansible\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Ansible" },
                { new Regex(@"\bnginx\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "nginx" },
                { new Regex(@"\bapache\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Apache" },
                { new Regex(@"\blinux\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Linux" },
                { new Regex(@"\bubuntu\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Ubuntu" },
                { new Regex(@"\bcentos\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "CentOS" },
                { new Regex(@"\bdebian\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Debian" },
                { new Regex(@"\bwsl\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "WSL" },
                { new Regex(@"\bpython\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Python" },
                { new Regex(@"\bdjango\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Django" },
                { new Regex(@"\bflask\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Flask" },
                { new Regex(@"\bfast\s*api\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "FastAPI" },
                { new Regex(@"\bruntime\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "runtime" },
                { new Regex(@"\bcompile\s*time\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "compile time" },
                { new Regex(@"\bdebugger\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "debugger" },
                { new Regex(@"\bbreak\s*point\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "breakpoint" },
                { new Regex(@"\bstack\s*overflow\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Stack Overflow" },

                // Common programming keywords
                { new Regex(@"\bfor\s*each\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "forEach" },
                { new Regex(@"\buse\s*state\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "useState" },
                { new Regex(@"\buse\s*effect\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "useEffect" },
                { new Regex(@"\buse\s*context\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "useContext" },
                { new Regex(@"\buse\s*reducer\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "useReducer" },
                { new Regex(@"\buse\s*memo\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "useMemo" },
                { new Regex(@"\buse\s*callback\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "useCallback" },
                { new Regex(@"\basync\s*await\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "async/await" },
                { new Regex(@"\btry\s*catch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "try/catch" },
                { new Regex(@"\bif\s*else\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "if/else" },

                // Common commands
                { new Regex(@"\bgit\s*add\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git add" },
                { new Regex(@"\bgit\s*commit\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git commit" },
                { new Regex(@"\bgit\s*push\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git push" },
                { new Regex(@"\bgit\s*pull\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git pull" },
                { new Regex(@"\bgit\s*clone\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git clone" },
                { new Regex(@"\bgit\s*checkout\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git checkout" },
                { new Regex(@"\bgit\s*merge\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git merge" },
                { new Regex(@"\bgit\s*rebase\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git rebase" },
                { new Regex(@"\bgit\s*status\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git status" },
                { new Regex(@"\bgit\s*log\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "git log" },
                { new Regex(@"\bnpm\s*run\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "npm run" },
                { new Regex(@"\bnpm\s*start\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "npm start" },
                { new Regex(@"\bnpm\s*test\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "npm test" },
                { new Regex(@"\bnpm\s*build\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "npm build" },
                { new Regex(@"\byarn\s*install\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "yarn install" },
                { new Regex(@"\byarn\s*add\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "yarn add" },
                { new Regex(@"\byarn\s*start\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "yarn start" },

                // File extensions
                { new Regex(@"\bdot\s*js\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".js" },
                { new Regex(@"\bdot\s*ts\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".ts" },
                { new Regex(@"\bdot\s*jsx\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".jsx" },
                { new Regex(@"\bdot\s*tsx\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".tsx" },
                { new Regex(@"\bdot\s*json\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".json" },
                { new Regex(@"\bdot\s*md\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".md" },
                { new Regex(@"\bdot\s*css\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".css" },
                { new Regex(@"\bdot\s*html\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".html" },
                { new Regex(@"\bdot\s*xml\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".xml" },
                { new Regex(@"\bdot\s*yaml\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".yaml" },
                { new Regex(@"\bdot\s*yml\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".yml" },
                { new Regex(@"\bdot\s*env\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".env" },
                { new Regex(@"\bdot\s*git\s*ignore\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".gitignore" },
                { new Regex(@"\bdot\s*docker\s*ignore\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ".dockerignore" },

                // Common misspellings
                { new Regex(@"\bconsole\s*dot\s*log\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "console.log" },
                { new Regex(@"\bvs\s*code\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "VS Code" },
                { new Regex(@"\bvisual\s*studio\s*code\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Visual Studio Code" },
                { new Regex(@"\bintelli\s*j\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "IntelliJ" },
                { new Regex(@"\bweb\s*pack\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "webpack" },
                { new Regex(@"\bbabel\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Babel" },
                { new Regex(@"\bes\s*lint\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ESLint" },
                { new Regex(@"\bprettier\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Prettier" }
            };
        }

        public static string ProcessTranscription(string transcription, bool useEnhancedDictionary = true, List<Models.DictionaryEntry>? customDictionary = null, Models.PostProcessingSettings? postProcessingSettings = null)
        {
            if (string.IsNullOrWhiteSpace(transcription))
                return transcription;

            var processed = transcription;

            // VOICESHORTCUTS FIX: Normalize spaced acronyms BEFORE custom dictionary
            // Whisper often transcribes "llm" as "L L M", breaking pattern matching
            // This normalizes "L L M" → "LLM", "A I" → "AI" so patterns match reliably
            processed = NormalizeSpacedAcronyms(processed);

            // Apply custom dictionary (now more reliable with normalized acronyms)
            if (customDictionary != null && customDictionary.Count > 0)
            {
                processed = ApplyCustomDictionary(processed, customDictionary);
            }

            // Apply filler word removal BEFORE corrections (order matters!)
            if (postProcessingSettings != null && postProcessingSettings.FillerRemovalIntensity != Models.FillerWordRemovalLevel.None)
            {
                processed = RemoveFillerWords(processed, postProcessingSettings);
            }

            // Apply technical corrections using pre-compiled regex
            foreach (var correction in TechnicalCorrections)
            {
                processed = correction.Key.Replace(processed, correction.Value);
            }

            // Apply enhanced corrections if enabled (default is true now)
            if (useEnhancedDictionary)
            {
                processed = ApplyEnhancedCorrections(processed);
            }

            // Handle contractions if configured
            if (postProcessingSettings != null && postProcessingSettings.ContractionHandling != Models.ContractionMode.LeaveAsIs)
            {
                processed = HandleContractions(processed, postProcessingSettings.ContractionHandling);
            }

            // Apply grammar fixes if enabled
            if (postProcessingSettings != null)
            {
                if (postProcessingSettings.FixHomophones)
                {
                    processed = FixHomophones(processed);
                }
                if (postProcessingSettings.FixDoubleNegatives)
                {
                    processed = FixDoubleNegatives(processed);
                }
                if (postProcessingSettings.FixSubjectVerbAgreement)
                {
                    processed = FixSubjectVerbAgreement(processed);
                }
            }

            // Fix common punctuation issues
            processed = FixPunctuation(processed, postProcessingSettings);

            // Fix spacing issues
            processed = FixSpacing(processed);

            return processed;
        }

        /// <summary>
        /// Normalize spaced acronyms to improve VoiceShortcuts reliability
        /// Whisper often transcribes "llm" as "L L M", which breaks pattern matching
        /// This fixes: "L L M" → "LLM", "A I" → "AI", "G P T" → "GPT"
        /// </summary>
        private static string NormalizeSpacedAcronyms(string text)
        {
            // Fix 4-letter spaced acronyms: "H T M L" → "HTML"
            text = Regex.Replace(text, @"\b([A-Za-z])\s+([A-Za-z])\s+([A-Za-z])\s+([A-Za-z])\b", "$1$2$3$4");

            // Fix 3-letter spaced acronyms: "L L M" → "LLM", "G P T" → "GPT"
            text = Regex.Replace(text, @"\b([A-Za-z])\s+([A-Za-z])\s+([A-Za-z])\b", "$1$2$3");

            // Fix 2-letter spaced acronyms: "A I" → "AI", "M L" → "ML"
            text = Regex.Replace(text, @"\b([A-Za-z])\s+([A-Za-z])\b", "$1$2");

            return text;
        }

        private static string ApplyCustomDictionary(string text, List<Models.DictionaryEntry> entries)
        {
            // Apply all enabled entries
            foreach (var entry in entries.Where(e => e.IsEnabled))
            {
                try
                {
                    var regex = entry.GetCompiledRegex();
                    text = regex.Replace(text, entry.Replacement);
                }
                catch (Exception)
                {
                    // Skip invalid regex patterns silently
                    continue;
                }
            }
            return text;
        }

        private static string FixPunctuation(string text, Models.PostProcessingSettings? settings)
        {
            var shouldCapitalize = settings?.EnableCapitalization ?? true;
            var capitalizeFirstLetter = settings?.CapitalizeFirstLetter ?? true;
            var capitalizeAfterPeriod = settings?.CapitalizeAfterPeriod ?? true;
            var capitalizeAfterQuestionExclamation = settings?.CapitalizeAfterQuestionExclamation ?? true;
            var shouldAddPeriod = settings?.EnableEndingPunctuation ?? true;
            var onlyAddIfMissing = settings?.OnlyAddIfMissing ?? true;
            var defaultPunctuation = settings?.DefaultPunctuation ?? Models.EndingPunctuationType.Period;
            var useSmartPunctuation = settings?.UseSmartPunctuation ?? false;

            // Add ending punctuation if enabled
            if (shouldAddPeriod && !string.IsNullOrWhiteSpace(text))
            {
                var trimmed = text.TrimEnd();
                var hasPunctuation = trimmed.EndsWith(".") || trimmed.EndsWith("!") || trimmed.EndsWith("?");

                if (!hasPunctuation || !onlyAddIfMissing)
                {
                    if (!hasPunctuation)
                    {
                        // Smart punctuation: detect if it looks like a question
                        if (useSmartPunctuation && IsQuestion(trimmed))
                        {
                            text = trimmed + "?";
                        }
                        else
                        {
                            // Use default punctuation
                            text = trimmed + GetPunctuationChar(defaultPunctuation);
                        }
                    }
                }
            }

            // Fix multiple spaces using pre-compiled regex
            text = MultipleSpacesRegex.Replace(text, " ");

            // Capitalization
            if (shouldCapitalize)
            {
                // Capitalize first letter
                if (capitalizeFirstLetter && text.Length > 0 && char.IsLower(text[0]))
                {
                    text = char.ToUpper(text[0]) + text.Substring(1);
                }

                // Capitalize after periods
                if (capitalizeAfterPeriod)
                {
                    text = CapitalizeAfterPeriodRegex.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value.ToUpper());
                }

                // Capitalize after question marks and exclamation points
                if (capitalizeAfterQuestionExclamation)
                {
                    text = Regex.Replace(text, @"([?!]\s+)([a-z])", m => m.Groups[1].Value + m.Groups[2].Value.ToUpper());
                }
            }

            return text;
        }

        private static bool IsQuestion(string text)
        {
            var lowerText = text.ToLower();
            var questionWords = new[] { "who", "what", "where", "when", "why", "how", "is", "are", "can", "could", "would", "should", "do", "does", "did" };
            return questionWords.Any(word => lowerText.StartsWith(word + " "));
        }

        private static string GetPunctuationChar(Models.EndingPunctuationType type)
        {
            return type switch
            {
                Models.EndingPunctuationType.Period => ".",
                Models.EndingPunctuationType.Question => "?",
                Models.EndingPunctuationType.Exclamation => "!",
                _ => ""
            };
        }

        private static string FixSpacing(string text)
        {
            // Remove space before punctuation using pre-compiled regex
            text = SpaceBeforePunctuationRegex.Replace(text, "$1");

            // Add space after punctuation if missing using pre-compiled regex
            text = SpaceAfterPunctuationRegex.Replace(text, "$1 $2");

            // Fix spacing around parentheses and brackets using pre-compiled regex
            text = SpaceAroundParenOpenRegex.Replace(text, " (");
            text = SpaceAroundParenCloseRegex.Replace(text, ") ");
            text = SpaceAroundBracketOpenRegex.Replace(text, " [");
            text = SpaceAroundBracketCloseRegex.Replace(text, "] ");

            // Clean up multiple spaces again using pre-compiled regex
            text = MultipleSpacesRegex.Replace(text, " ");

            return text.Trim();
        }

        private static string ApplyEnhancedCorrections(string text)
        {
            // Context-aware corrections for better accuracy
            var hasGitContext = HasContext(text, "repository", "commit", "branch", "merge", "push", "pull", "clone");
            var hasReactContext = HasContext(text, "component", "render", "props", "state", "hook", "React");
            var hasNodeContext = HasContext(text, "package", "install", "dependency", "module", "require", "import");
            var hasCodeContext = HasContext(text, "function", "variable", "class", "method", "return", "debug");

            // CRITICAL PERFORMANCE FIX: Use pre-compiled regex patterns instead of creating new ones
            // Apply context-aware corrections using pre-compiled patterns
            if (hasGitContext)
            {
                text = GetHubRegex.Replace(text, "GitHub");
                text = GetLabRegex.Replace(text, "GitLab");
                text = GitIgnoreRegex.Replace(text, ".gitignore");
            }

            if (hasReactContext)
            {
                text = YouStateRegex.Replace(text, "useState");
                text = YouEffectRegex.Replace(text, "useEffect");
                text = YouContextRegex.Replace(text, "useContext");
                text = YouReducerRegex.Replace(text, "useReducer");
                text = YouMemoRegex.Replace(text, "useMemo");
                text = YouCallbackRegex.Replace(text, "useCallback");
            }

            if (hasNodeContext)
            {
                text = PackageJasonRegex.Replace(text, "package.json");
                text = PackageLockRegex.Replace(text, "package-lock.json");
                text = YarnLockRegex.Replace(text, "yarn.lock");
            }

            if (hasCodeContext)
            {
                text = JaySonRegex.Replace(text, "JSON");
                text = ConsoleDotLogRegex.Replace(text, "console.log");
                text = DotThenRegex.Replace(text, ".then");
                text = DotCatchRegex.Replace(text, ".catch");
                text = DotMapRegex.Replace(text, ".map");
                text = DotFilterRegex.Replace(text, ".filter");
                text = DotReduceRegex.Replace(text, ".reduce");
            }

            // Additional common misrecognitions using pre-compiled patterns
            text = EndPointRegex.Replace(text, "endpoint");
            text = DataBaseRegex.Replace(text, "database");
            text = FrameWorkRegex.Replace(text, "framework");
            text = FrontEndRegex.Replace(text, "frontend");
            text = BackEndRegex.Replace(text, "backend");
            text = FullStackRegex.Replace(text, "fullstack");
            text = DevOpsRegex.Replace(text, "DevOps");
            text = CiCdRegex.Replace(text, "CI/CD");
            text = HttpsRegex.Replace(text, "HTTPS");
            text = SqlLightRegex.Replace(text, "SQLite");
            text = NoSqlRegex.Replace(text, "NoSQL");
            text = RestFullRegex.Replace(text, "RESTful");
            text = WebSocketRegex.Replace(text, "WebSocket");
            text = LocalHostRegex.Replace(text, "localhost");
            text = IdeRegex.Replace(text, "IDE");
            text = UrlRegex.Replace(text, "URL");
            text = UiRegex.Replace(text, "UI");
            text = UxRegex.Replace(text, "UX");
            text = SqlRegex.Replace(text, "SQL");
            text = XmlRegex.Replace(text, "XML");
            text = HtmlRegex.Replace(text, "HTML");
            text = CssRegex.Replace(text, "CSS");

            return text;
        }

        private static bool HasContext(string text, params string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static string RemoveFillerWords(string text, Models.PostProcessingSettings settings)
        {
            var wordsToRemove = new List<string>();

            // Build list based on intensity level and enabled lists
            if (settings.FillerRemovalIntensity == Models.FillerWordRemovalLevel.Custom)
            {
                // Custom mode: use only enabled lists + custom words
                if (settings.EnabledLists.Hesitations) wordsToRemove.AddRange(settings.EnabledLists.HesitationWords);
                if (settings.EnabledLists.VerbalTics) wordsToRemove.AddRange(settings.EnabledLists.VerbalTicWords);
                if (settings.EnabledLists.Qualifiers) wordsToRemove.AddRange(settings.EnabledLists.QualifierWords);
                if (settings.EnabledLists.Intensifiers) wordsToRemove.AddRange(settings.EnabledLists.IntensifierWords);
                if (settings.EnabledLists.Transitions) wordsToRemove.AddRange(settings.EnabledLists.TransitionWords);
                wordsToRemove.AddRange(settings.CustomFillerWords);
            }
            else
            {
                // Preset intensity levels
                switch (settings.FillerRemovalIntensity)
                {
                    case Models.FillerWordRemovalLevel.Light:
                        wordsToRemove.AddRange(settings.EnabledLists.HesitationWords);
                        break;
                    case Models.FillerWordRemovalLevel.Moderate:
                        wordsToRemove.AddRange(settings.EnabledLists.HesitationWords);
                        wordsToRemove.AddRange(settings.EnabledLists.VerbalTicWords);
                        break;
                    case Models.FillerWordRemovalLevel.Aggressive:
                        wordsToRemove.AddRange(settings.EnabledLists.HesitationWords);
                        wordsToRemove.AddRange(settings.EnabledLists.VerbalTicWords);
                        wordsToRemove.AddRange(settings.EnabledLists.QualifierWords);
                        wordsToRemove.AddRange(settings.EnabledLists.IntensifierWords);
                        wordsToRemove.AddRange(settings.EnabledLists.TransitionWords);
                        break;
                }
            }

            var distinctWords = wordsToRemove.Distinct().ToList();
            if (distinctWords.Count == 0)
                return text;

            // CONTEXT-AWARE REMOVAL: Handle ambiguous words that have legitimate grammatical uses
            // Words like "like", "right", "well" need special patterns to avoid false positives
            var contextAwareWords = new Dictionary<string, bool>();
            foreach (var word in new[] { "like", "right", "well", "actually", "literally", "I think" })
            {
                if (distinctWords.Contains(word, StringComparer.OrdinalIgnoreCase))
                    contextAwareWords[word] = true;
            }

            var simpleWords = distinctWords.Except(contextAwareWords.Keys, StringComparer.OrdinalIgnoreCase).ToList();

            var options = settings.CaseSensitiveFillerRemoval ? RegexOptions.None : RegexOptions.IgnoreCase;

            // CONTEXT-AWARE REMOVAL: Apply word-specific patterns
            // Each ambiguous word has tailored rules to preserve legitimate uses

            // 1. "like" - verb/preposition vs filler
            if (contextAwareWords.ContainsKey("like"))
            {
                // Comma-adjacent: ", like,", ", like", "like,"
                text = Regex.Replace(text,
                    @"(,\s*like\s*,|,\s*like\b|\blike\s*,)",
                    match => match.Value.StartsWith(",") && match.Value.EndsWith(",") ? "," : "",
                    options);
                // Quotative: "was/were like"
                text = Regex.Replace(text, @"\b(was|were)\s+like\b", "$1", options);
                // Isolated (not after similarity verbs, not before demonstratives)
                text = Regex.Replace(text,
                    @"(?<!looks|sounds|feels|seems|appears|smells|tastes)\s+like\s+(?!(this|that|it|he|she|they|we|you|a|an|the))",
                    " ",
                    options);
            }

            // 2. "right" - direction/correctness vs tag question filler
            if (contextAwareWords.ContainsKey("right"))
            {
                // Only remove when used as tag question: "right?" or ", right" at end of sentence
                text = Regex.Replace(text, @",\s*right\s*[?.]", match => match.Value.EndsWith("?") ? "?" : ".", options);
                text = Regex.Replace(text, @"\bright\s*\?", "?", options);
                // Preserve: "turn right", "right answer", "human rights", "right now"
            }

            // 3. "well" - health/quality vs transition filler
            if (contextAwareWords.ContainsKey("well"))
            {
                // Remove at sentence start with comma: "Well, I think" → "I think"
                text = Regex.Replace(text, @"^\s*well\s*,\s*", "", options);
                text = Regex.Replace(text, @"[.!?]\s+well\s*,\s*", match => match.Value.Substring(0, match.Value.IndexOf("well", StringComparison.OrdinalIgnoreCase)), options);
                // Preserve: "feel well", "well done", "as well", "well-known"
            }

            // 4. "actually" - meaningful emphasis vs filler
            if (contextAwareWords.ContainsKey("actually"))
            {
                // Remove when comma-surrounded (clear filler)
                text = Regex.Replace(text, @",\s*actually\s*,", ",", options);
                // Remove at sentence start with comma
                text = Regex.Replace(text, @"^\s*actually\s*,\s*", "", options);
                text = Regex.Replace(text, @"[.!?]\s+actually\s*,\s*", match => match.Value.Substring(0, match.Value.IndexOf("actually", StringComparison.OrdinalIgnoreCase)), options);
                // Preserve when used for correction: "actually correct", "actually works"
            }

            // 5. "literally" - literal meaning vs filler intensifier
            if (contextAwareWords.ContainsKey("literally"))
            {
                // Remove when comma-surrounded or before common hyperbole
                text = Regex.Replace(text, @",\s*literally\s*,", ",", options);
                text = Regex.Replace(text, @"\bliterally\s+(dying|dead|exploded|everything|anything|nothing)", "$1", options);
                // Preserve when used correctly: "literally translated", "literally true"
            }

            // 6. "I think" - opinion marker vs filler hedge
            if (contextAwareWords.ContainsKey("I think"))
            {
                // Remove when at start or comma-surrounded (hedging)
                text = Regex.Replace(text, @"^\s*I\s+think\s*,\s*", "", options);
                text = Regex.Replace(text, @",\s*I\s+think\s*,", ",", options);
                // Preserve when followed by "that" (intentional opinion): "I think that..."
            }

            // Remove simple filler words (um, uh, etc.) using combined regex
            if (simpleWords.Count > 0)
            {
                var escapedWords = simpleWords.Select(w => Regex.Escape(w));
                var combinedPattern = $@"\b({string.Join("|", escapedWords)})\b";
                text = Regex.Replace(text, combinedPattern, "", options);
            }

            // Clean up extra spaces and punctuation left by removal
            text = Regex.Replace(text, @"\s*,\s*,", ","); // Double commas
            text = Regex.Replace(text, @",\s*\.", "."); // Comma before period
            text = Regex.Replace(text, @"\s{2,}", " "); // Multiple spaces
            text = Regex.Replace(text, @"^\s*,\s*", ""); // Leading comma
            text = Regex.Replace(text, @"\s*,\s*$", ""); // Trailing comma

            return text.Trim();
        }

        private static string HandleContractions(string text, Models.ContractionMode mode)
        {
            if (mode == Models.ContractionMode.Expand)
            {
                // Expand contractions
                var expansions = new Dictionary<string, string>
                {
                    { "don't", "do not" },
                    { "doesn't", "does not" },
                    { "didn't", "did not" },
                    { "can't", "cannot" },
                    { "couldn't", "could not" },
                    { "won't", "will not" },
                    { "wouldn't", "would not" },
                    { "shouldn't", "should not" },
                    { "isn't", "is not" },
                    { "aren't", "are not" },
                    { "wasn't", "was not" },
                    { "weren't", "were not" },
                    { "haven't", "have not" },
                    { "hasn't", "has not" },
                    { "hadn't", "had not" },
                    { "I'm", "I am" },
                    { "you're", "you are" },
                    { "we're", "we are" },
                    { "they're", "they are" },
                    { "it's", "it is" },
                    { "that's", "that is" },
                    { "there's", "there is" },
                    { "I've", "I have" },
                    { "you've", "you have" },
                    { "we've", "we have" },
                    { "they've", "they have" },
                    { "I'll", "I will" },
                    { "you'll", "you will" },
                    { "we'll", "we will" },
                    { "they'll", "they will" },
                    { "I'd", "I would" },
                    { "you'd", "you would" },
                    { "we'd", "we would" },
                    { "they'd", "they would" }
                };

                foreach (var expansion in expansions)
                {
                    text = Regex.Replace(text, $@"\b{Regex.Escape(expansion.Key)}\b", expansion.Value, RegexOptions.IgnoreCase);
                }
            }
            else if (mode == Models.ContractionMode.Contract)
            {
                // Contract phrases
                var contractions = new Dictionary<string, string>
                {
                    { "do not", "don't" },
                    { "does not", "doesn't" },
                    { "did not", "didn't" },
                    { "cannot", "can't" },
                    { "could not", "couldn't" },
                    { "will not", "won't" },
                    { "would not", "wouldn't" },
                    { "should not", "shouldn't" },
                    { "is not", "isn't" },
                    { "are not", "aren't" },
                    { "was not", "wasn't" },
                    { "were not", "weren't" },
                    { "have not", "haven't" },
                    { "has not", "hasn't" },
                    { "had not", "hadn't" },
                    { "I am", "I'm" },
                    { "you are", "you're" },
                    { "we are", "we're" },
                    { "they are", "they're" },
                    { "it is", "it's" },
                    { "that is", "that's" },
                    { "there is", "there's" },
                    { "I have", "I've" },
                    { "you have", "you've" },
                    { "we have", "we've" },
                    { "they have", "they've" },
                    { "I will", "I'll" },
                    { "you will", "you'll" },
                    { "we will", "we'll" },
                    { "they will", "they'll" },
                    { "I would", "I'd" },
                    { "you would", "you'd" },
                    { "we would", "we'd" },
                    { "they would", "they'd" }
                };

                foreach (var contraction in contractions)
                {
                    text = Regex.Replace(text, $@"\b{Regex.Escape(contraction.Key)}\b", contraction.Value, RegexOptions.IgnoreCase);
                }
            }

            return text;
        }

        private static string FixHomophones(string text)
        {
            // Basic homophone fixes based on context
            // their/there/they're
            text = Regex.Replace(text, @"\bthey\'re\s+(\w+)", m =>
            {
                var nextWord = m.Groups[1].Value.ToLower();
                if (nextWord == "is" || nextWord == "was" || nextWord == "are")
                    return "there " + m.Groups[1].Value;
                return m.Value;
            }, RegexOptions.IgnoreCase);

            // your/you're
            text = Regex.Replace(text, @"\byour\s+(is|was|are|were)\b", "you're $1", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\byou\'re\s+(\w+)", m =>
            {
                var nextWord = m.Groups[1].Value.ToLower();
                if (nextWord != "is" && nextWord != "was" && nextWord != "are" && nextWord != "were")
                    return "your " + m.Groups[1].Value;
                return m.Value;
            }, RegexOptions.IgnoreCase);

            // its/it's
            text = Regex.Replace(text, @"\bits\s+(is|was)\b", "it's $1", RegexOptions.IgnoreCase);

            return text;
        }

        private static string FixDoubleNegatives(string text)
        {
            // Basic double negative fixes
            text = Regex.Replace(text, @"\bdon'?t\s+have\s+no\b", "don't have any", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bdon'?t\s+have\s+nothing\b", "don't have anything", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bcan'?t\s+find\s+no\b", "can't find any", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bcan'?t\s+see\s+no\b", "can't see any", RegexOptions.IgnoreCase);

            return text;
        }

        private static string FixSubjectVerbAgreement(string text)
        {
            // Basic subject-verb agreement fixes
            text = Regex.Replace(text, @"\bI\s+is\b", "I am", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bI\s+was\b", "I was", RegexOptions.IgnoreCase); // This is correct, no change needed
            text = Regex.Replace(text, @"\bhe\s+are\b", "he is", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bshe\s+are\b", "she is", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bit\s+are\b", "it is", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bthey\s+is\b", "they are", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bwe\s+is\b", "we are", RegexOptions.IgnoreCase);

            return text;
        }
    }
}