import re
import sys

# Read TranscriptionPostProcessor.cs
with open('VoiceLite/VoiceLite/Services/TranscriptionPostProcessor.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Add static pre-compiled contraction regexes at class level
static_contractions = '''
    // PERFORMANCE: Pre-compiled contraction regexes (76 total)
    private static readonly Dictionary<Regex, string> ContractionExpansions = new Dictionary<Regex, string>
    {
        { new Regex(@"\\bcan't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "cannot" },
        { new Regex(@"\\bwon't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "will not" },
        { new Regex(@"\\bshan't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "shall not" },
        { new Regex(@"\\bain't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "am not" },
        { new Regex(@"\\bI'm\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I am" },
        { new Regex(@"\\byou're\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you are" },
        { new Regex(@"\\bhe's\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "he is" },
        { new Regex(@"\\bshe's\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "she is" },
        { new Regex(@"\\bit's\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "it is" },
        { new Regex(@"\\bwe're\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we are" },
        { new Regex(@"\\bthey're\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they are" },
        { new Regex(@"\\bI've\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I have" },
        { new Regex(@"\\byou've\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you have" },
        { new Regex(@"\\bwe've\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we have" },
        { new Regex(@"\\bthey've\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they have" },
        { new Regex(@"\\bI'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I will" },
        { new Regex(@"\\byou'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you will" },
        { new Regex(@"\\bhe'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "he will" },
        { new Regex(@"\\bshe'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "she will" },
        { new Regex(@"\\bit'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "it will" },
        { new Regex(@"\\bwe'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we will" },
        { new Regex(@"\\bthey'll\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they will" },
        { new Regex(@"\\bI'd\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I would" },
        { new Regex(@"\\byou'd\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you would" },
        { new Regex(@"\\bhe'd\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "he would" },
        { new Regex(@"\\bshe'd\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "she would" },
        { new Regex(@"\\bwe'd\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we would" },
        { new Regex(@"\\bthey'd\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they would" },
        { new Regex(@"\\bisn't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "is not" },
        { new Regex(@"\\baren't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "are not" },
        { new Regex(@"\\bwasn't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "was not" },
        { new Regex(@"\\bweren't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "were not" },
        { new Regex(@"\\bhasn't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "has not" },
        { new Regex(@"\\bhaven't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "have not" },
        { new Regex(@"\\bhadn't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "had not" },
        { new Regex(@"\\bdoesn't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "does not" },
        { new Regex(@"\\bdon't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "do not" },
        { new Regex(@"\\bdidn't\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "did not" }
    };

    private static readonly Dictionary<Regex, string> Contractions = new Dictionary<Regex, string>
    {
        { new Regex(@"\\bcannot\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "can't" },
        { new Regex(@"\\bwill not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "won't" },
        { new Regex(@"\\bshall not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "shan't" },
        { new Regex(@"\\bI am\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I'm" },
        { new Regex(@"\\byou are\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you're" },
        { new Regex(@"\\bhe is\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "he's" },
        { new Regex(@"\\bshe is\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "she's" },
        { new Regex(@"\\bit is\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "it's" },
        { new Regex(@"\\bwe are\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we're" },
        { new Regex(@"\\bthey are\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they're" },
        { new Regex(@"\\bI have\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I've" },
        { new Regex(@"\\byou have\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you've" },
        { new Regex(@"\\bwe have\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we've" },
        { new Regex(@"\\bthey have\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they've" },
        { new Regex(@"\\bI will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I'll" },
        { new Regex(@"\\byou will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you'll" },
        { new Regex(@"\\bhe will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "he'll" },
        { new Regex(@"\\bshe will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "she'll" },
        { new Regex(@"\\bit will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "it'll" },
        { new Regex(@"\\bwe will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we'll" },
        { new Regex(@"\\bthey will\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they'll" },
        { new Regex(@"\\bI would\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "I'd" },
        { new Regex(@"\\byou would\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "you'd" },
        { new Regex(@"\\bhe would\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "he'd" },
        { new Regex(@"\\bshe would\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "she'd" },
        { new Regex(@"\\bwe would\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "we'd" },
        { new Regex(@"\\bthey would\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "they'd" },
        { new Regex(@"\\bis not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "isn't" },
        { new Regex(@"\\bare not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "aren't" },
        { new Regex(@"\\bwas not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wasn't" },
        { new Regex(@"\\bwere not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "weren't" },
        { new Regex(@"\\bhas not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "hasn't" },
        { new Regex(@"\\bhave not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "haven't" },
        { new Regex(@"\\bhad not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "hadn't" },
        { new Regex(@"\\bdoes not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "doesn't" },
        { new Regex(@"\\bdo not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "don't" },
        { new Regex(@"\\bdid not\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "didn't" }
    };
'''

# Insert static contractions after TechnicalCorrections dictionary (around line 181)
content = re.sub(
    r'(private static readonly Dictionary<Regex, string> TechnicalCorrections.*?\};)',
    r'\1\n' + static_contractions,
    content,
    flags=re.DOTALL
)

# 2. Add static grammar fix regexes
static_grammar_regexes = '''
    // PERFORMANCE: Pre-compiled grammar fix regexes
    private static readonly Regex TheyreFollowedByNoun = new Regex(@"\\bthey're\\s+(\\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TheirFollowedByVerb = new Regex(@"\\btheir\\s+(is|are|was|were)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DoubleNegative1 = new Regex(@"\\bdon't\\s+(\\w+)\\s+no\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DoubleNegative2 = new Regex(@"\\bcan't\\s+(\\w+)\\s+nothing\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SubjectVerbI = new Regex(@"\\bI\\s+was\\b", RegexOptions.Compiled);
    private static readonly Regex SubjectVerbYouWere = new Regex(@"\\byou\\s+was\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SubjectVerbWeWere = new Regex(@"\\bwe\\s+was\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SubjectVerbTheyWere = new Regex(@"\\bthey\\s+was\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
'''

# Insert static grammar regexes after contraction dictionaries
content = re.sub(
    r'(private static readonly Dictionary<Regex, string> Contractions.*?\};)',
    r'\1\n' + static_grammar_regexes,
    content,
    flags=re.DOTALL
)

# 3. Add static cleanup regexes
static_cleanup_regexes = '''
    // PERFORMANCE: Pre-compiled cleanup regexes
    private static readonly Regex MultipleSpaces = new Regex(@"\\s{2,}", RegexOptions.Compiled);
    private static readonly Regex SpaceBeforePunctuation = new Regex(@"\\s+([.,!?;:])", RegexOptions.Compiled);
    private static readonly Regex MultiplePeriodsNotEllipsis = new Regex(@"\\.{2}(?!\\.)", RegexOptions.Compiled);
    private static readonly Regex FixPunctuationCapitalization = new Regex(@"([?!]\\s+)([a-z])", RegexOptions.Compiled);
'''

# Insert static cleanup regexes after grammar regexes
content = re.sub(
    r'(private static readonly Regex SubjectVerbTheyWere.*?;)',
    r'\1\n' + static_cleanup_regexes,
    content,
    flags=re.DOTALL
)

# Write modified content
with open('VoiceLite/VoiceLite/Services/TranscriptionPostProcessor.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Step 1/2: Applied static regex definitions to TranscriptionPostProcessor.cs")

# Now modify MainWindow.xaml.cs for JSON indentation
with open('VoiceLite/VoiceLite/MainWindow.xaml.cs', 'r', encoding='utf-8') as f:
    mainwindow_content = f.read()

# Replace JSON serialization with conditional indentation
mainwindow_content = re.sub(
    r'string json = JsonSerializer\.Serialize\(settings, new JsonSerializerOptions \{ WriteIndented = true \}\);',
    '''#if DEBUG
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
#else
                var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
#endif
                string json = JsonSerializer.Serialize(settings, jsonOptions);''',
    mainwindow_content
)

with open('VoiceLite/VoiceLite/MainWindow.xaml.cs', 'w', encoding='utf-8') as f:
    f.write(mainwindow_content)

print("Step 2/2: Applied conditional JSON indentation to MainWindow.xaml.cs")
print("\nAll static regex definitions added successfully!")
print("Next: Need to update method implementations to use these pre-compiled regexes")
