using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VoiceLite.Models
{
    public enum DictionaryCategory
    {
        General,
        Medical,
        Legal,
        Tech,
        Personal
    }

    public class DictionaryEntry
    {
        public string Pattern { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DictionaryCategory Category { get; set; } = DictionaryCategory.General;
        public bool CaseSensitive { get; set; } = false;
        public bool WholeWord { get; set; } = true; // Match whole words only (adds \b boundaries)

        // Cached compiled regex for performance
        private Regex? _compiledRegex;

        public Regex GetCompiledRegex()
        {
            if (_compiledRegex == null)
            {
                var pattern = WholeWord ? $@"\b{Regex.Escape(Pattern)}\b" : Regex.Escape(Pattern);
                var options = RegexOptions.Compiled;
                if (!CaseSensitive)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                _compiledRegex = new Regex(pattern, options);
            }
            return _compiledRegex;
        }

        public void InvalidateCache()
        {
            _compiledRegex = null;
        }
    }

    public static class CustomDictionaryTemplates
    {
        public static List<DictionaryEntry> GetMedicalTemplate()
        {
            return new List<DictionaryEntry>
            {
                // Vital Signs
                new DictionaryEntry { Pattern = "BP", Replacement = "blood pressure", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "HR", Replacement = "heart rate", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "RR", Replacement = "respiratory rate", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "temp", Replacement = "temperature", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "O2 sat", Replacement = "oxygen saturation", Category = DictionaryCategory.Medical },

                // Common Tests
                new DictionaryEntry { Pattern = "CBC", Replacement = "complete blood count", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "CMP", Replacement = "comprehensive metabolic panel", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "BMP", Replacement = "basic metabolic panel", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "UA", Replacement = "urinalysis", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "CT", Replacement = "computed tomography", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "MRI", Replacement = "magnetic resonance imaging", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "EKG", Replacement = "electrocardiogram", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "ECG", Replacement = "electrocardiogram", Category = DictionaryCategory.Medical },

                // Medications
                new DictionaryEntry { Pattern = "PO", Replacement = "by mouth", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "IV", Replacement = "intravenous", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "IM", Replacement = "intramuscular", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "PRN", Replacement = "as needed", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "BID", Replacement = "twice daily", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "TID", Replacement = "three times daily", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "QID", Replacement = "four times daily", Category = DictionaryCategory.Medical },

                // Common Abbreviations
                new DictionaryEntry { Pattern = "Hx", Replacement = "history", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "Dx", Replacement = "diagnosis", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "Tx", Replacement = "treatment", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "Rx", Replacement = "prescription", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "Sx", Replacement = "symptoms", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "Pt", Replacement = "patient", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "Dr", Replacement = "doctor", Category = DictionaryCategory.Medical },

                // Common Phrases
                new DictionaryEntry { Pattern = "WNL", Replacement = "within normal limits", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "NAD", Replacement = "no acute distress", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "SOB", Replacement = "shortness of breath", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "N/V", Replacement = "nausea and vomiting", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "c/o", Replacement = "complains of", Category = DictionaryCategory.Medical },
                new DictionaryEntry { Pattern = "s/p", Replacement = "status post", Category = DictionaryCategory.Medical },
            };
        }

        public static List<DictionaryEntry> GetLegalTemplate()
        {
            return new List<DictionaryEntry>
            {
                // Courts
                new DictionaryEntry { Pattern = "SCOTUS", Replacement = "Supreme Court of the United States", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "POTUS", Replacement = "President of the United States", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "USDC", Replacement = "United States District Court", Category = DictionaryCategory.Legal },

                // Legal Terms
                new DictionaryEntry { Pattern = "aka", Replacement = "also known as", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "a.k.a.", Replacement = "also known as", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "v.", Replacement = "versus", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "vs.", Replacement = "versus", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "et al", Replacement = "et alii", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "i.e.", Replacement = "that is", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "e.g.", Replacement = "for example", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "et seq", Replacement = "and the following", Category = DictionaryCategory.Legal },

                // Common Abbreviations
                new DictionaryEntry { Pattern = "LLC", Replacement = "Limited Liability Company", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "Inc", Replacement = "Incorporated", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "Corp", Replacement = "Corporation", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "Ltd", Replacement = "Limited", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "IP", Replacement = "intellectual property", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "TM", Replacement = "trademark", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "NDA", Replacement = "non-disclosure agreement", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "SLA", Replacement = "service level agreement", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "TOS", Replacement = "terms of service", Category = DictionaryCategory.Legal },
                new DictionaryEntry { Pattern = "EULA", Replacement = "end-user license agreement", Category = DictionaryCategory.Legal },
            };
        }

        public static List<DictionaryEntry> GetTechTemplate()
        {
            return new List<DictionaryEntry>
            {
                // Companies
                new DictionaryEntry { Pattern = "AWS", Replacement = "Amazon Web Services", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "GCP", Replacement = "Google Cloud Platform", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "MS", Replacement = "Microsoft", Category = DictionaryCategory.Tech },

                // Technologies
                new DictionaryEntry { Pattern = "k8s", Replacement = "Kubernetes", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "DB", Replacement = "database", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "API", Replacement = "Application Programming Interface", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "REST", Replacement = "Representational State Transfer", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "CRUD", Replacement = "Create Read Update Delete", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "CI/CD", Replacement = "Continuous Integration/Continuous Deployment", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "ML", Replacement = "machine learning", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "AI", Replacement = "artificial intelligence", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "UI", Replacement = "user interface", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "UX", Replacement = "user experience", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "SaaS", Replacement = "Software as a Service", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "PaaS", Replacement = "Platform as a Service", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "IaaS", Replacement = "Infrastructure as a Service", Category = DictionaryCategory.Tech },

                // Common Acronyms
                new DictionaryEntry { Pattern = "SQL", Replacement = "Structured Query Language", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "HTML", Replacement = "HyperText Markup Language", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "CSS", Replacement = "Cascading Style Sheets", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "JSON", Replacement = "JavaScript Object Notation", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "XML", Replacement = "eXtensible Markup Language", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "YAML", Replacement = "YAML Ain't Markup Language", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "SSH", Replacement = "Secure Shell", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "FTP", Replacement = "File Transfer Protocol", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "HTTP", Replacement = "HyperText Transfer Protocol", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "HTTPS", Replacement = "HyperText Transfer Protocol Secure", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "DNS", Replacement = "Domain Name System", Category = DictionaryCategory.Tech },
                new DictionaryEntry { Pattern = "VPN", Replacement = "Virtual Private Network", Category = DictionaryCategory.Tech },
            };
        }

        public static List<DictionaryEntry> GetAllTemplates()
        {
            var all = new List<DictionaryEntry>();
            all.AddRange(GetMedicalTemplate());
            all.AddRange(GetLegalTemplate());
            all.AddRange(GetTechTemplate());
            return all;
        }
    }
}
