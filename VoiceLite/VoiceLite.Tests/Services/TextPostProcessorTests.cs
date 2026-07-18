using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class TextPostProcessorTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void Process_ReturnsInputUnchanged_ForEmptyOrWhitespace(string input)
        {
            TextPostProcessor.Process(input).Should().Be(input);
        }

        [Fact]
        public void Process_AddsPeriod_ToStatementWithoutTerminalPunctuation()
        {
            var result = TextPostProcessor.Process("hello world");
            result.Should().EndWith(".");
        }

        // NEW CONTRACT (2026-07-17): Parakeet emits its own punctuation. We never strip
        // or re-guess terminal punctuation — question-word guessing is gone. A period is
        // appended only when the model produced no terminal punctuation at all.

        [Theory]
        [InlineData("Has he arrived?")]
        [InlineData("What time is it?")]
        [InlineData("Stop right there!")]
        [InlineData("Note the following:")]
        [InlineData("First; second.")]
        public void Process_PreservesModelTerminalPunctuation(string input)
        {
            var result = TextPostProcessor.Process(input);
            result.Should().EndWith(input[input.Length - 1].ToString());
        }

        [Theory]
        [InlineData("what time is it")]
        [InlineData("how are you")]
        [InlineData("can you help me")]
        public void Process_DoesNotGuessQuestionMarks_AppendsPeriodInstead(string input)
        {
            // Question-word guessing was deliberately removed — without model punctuation
            // we can't know intonation, so a period is the only honest default.
            var result = TextPostProcessor.Process(input);
            result.Should().EndWith(".");
            result.Should().NotEndWith("?");
        }

        [Fact]
        public void Process_DropsDanglingTrailingComma_BeforeAppendingPeriod()
        {
            var result = TextPostProcessor.Process("hello world,");
            result.Should().Be("Hello world.");
        }

        [Fact]
        public void Process_CapitalizesFirstWord()
        {
            var result = TextPostProcessor.Process("hello world");
            result.Should().StartWith("H");
        }

        [Fact]
        public void Process_CapitalizesStandaloneI()
        {
            var result = TextPostProcessor.Process("yesterday i went home");
            result.Should().Contain(" I ");
        }

        // NEW CONTRACT (2026-07-17): never lowercase what the model produced. The old
        // behavior force-lowercased every word outside a ~20-word allowlist, destroying
        // names, acronyms, and brands that Parakeet capitalizes correctly.

        [Fact]
        public void Process_PreservesModelCapitalization_NamesAndAcronyms()
        {
            var result = TextPostProcessor.Process("the MRI results for Sarah Chen came back clear");
            result.Should().Contain("MRI");
            result.Should().Contain("Sarah Chen");
        }

        [Fact]
        public void Process_PreservesMidSentenceCapitalization_AfterSentenceBreaks()
        {
            var result = TextPostProcessor.Process("see Dr. Adams. Book the CT scan");
            result.Should().Contain("Adams");
            result.Should().Contain("CT");
        }

        [Fact]
        public void Process_SentenceInitialCapitalization_PreservesRestOfWord()
        {
            // CapitalizeFirst must not lowercase the tail of the word.
            var result = TextPostProcessor.Process("mRNA vaccines work");
            result.Should().StartWith("MRNA");
        }

        [Theory]
        [InlineData("i use github every day", "GitHub")]
        [InlineData("running python tests", "Python")]
        [InlineData("on windows ten", "Windows")]
        public void Process_CapitalizesTechProperNouns(string input, string expected)
        {
            var result = TextPostProcessor.Process(input);
            result.Should().Contain(expected);
        }

        // Dev-term dictionary tests — canonical casing for terms Parakeet won't get
        // right out of the box (it has no initial-prompt vocab biasing like Whisper).

        [Theory]
        [InlineData("i love javascript", "JavaScript")]
        [InlineData("write typescript code", "TypeScript")]
        [InlineData("the api returns json", "API")]
        [InlineData("the api returns json", "JSON")]
        [InlineData("run sql queries", "SQL")]
        [InlineData("voicelite is great", "VoiceLite")]
        [InlineData("we use .net for the desktop", ".NET")]
        [InlineData("node.js powers the backend", "Node.js")]
        public void Process_AppliesCanonicalDevTermCasing(string input, string expected)
        {
            var result = TextPostProcessor.Process(input);
            result.Should().Contain(expected);
        }

        [Fact]
        public void Process_DevTermsAreCaseInsensitiveOnInput()
        {
            TextPostProcessor.Process("use GITHUB daily").Should().Contain("GitHub");
            TextPostProcessor.Process("use GitHub daily").Should().Contain("GitHub");
            TextPostProcessor.Process("use github daily").Should().Contain("GitHub");
        }

        [Fact]
        public void Process_DevTermsRespectWordBoundaries()
        {
            // "github" inside a longer word ("githubactions") should NOT be replaced.
            var result = TextPostProcessor.Process("we use githubactions for CI");
            result.Should().NotContain("GitHubactions");
        }

        [Fact]
        public void Process_DoesNotDoubleUpPunctuation()
        {
            var result = TextPostProcessor.Process("hello world.");
            result.Should().Be("Hello world.");
        }

        [Fact]
        public void Process_AddsCommaBeforeAndInLongSentence()
        {
            // Sentence > 30 chars triggers comma insertion before coordinating conjunctions
            var result = TextPostProcessor.Process("I went to the store and bought some bread for dinner");
            result.Should().Contain(", and ");
        }

        [Fact]
        public void Process_DoesNotAddCommaInShortSentence()
        {
            // Short sentence (< 30 chars) shouldn't get extra commas
            var result = TextPostProcessor.Process("you and me");
            result.Should().NotContain(", and ");
        }

        [Fact]
        public void Process_WithPunctuationDisabled_DoesNotAddPunctuation()
        {
            var result = TextPostProcessor.Process("hello world", enablePunctuation: false);
            result.Should().NotEndWith(".");
            result.Should().NotEndWith("?");
        }

        [Fact]
        public void Process_WithCapitalizationDisabled_DoesNotCapitalize()
        {
            var result = TextPostProcessor.Process("hello world", enableCapitalization: false);
            result.Should().StartWith("h");
        }

        [Fact]
        public void Process_TrimsLeadingAndTrailingWhitespace()
        {
            var result = TextPostProcessor.Process("   hello world   ");
            result.Should().StartWith("H");
            result.Should().EndWith(".");
        }

        [Fact]
        public void Process_HandlesSingleWord()
        {
            var result = TextPostProcessor.Process("hello");
            result.Should().Be("Hello.");
        }

        [Fact]
        public void Process_PreservesInternalPunctuation()
        {
            var result = TextPostProcessor.Process("hello, world how are you");
            result.Should().Contain(",");
        }

        [Fact]
        public void Process_IsThreadSafe()
        {
            // Pre-compiled regex + static methods → safe to call concurrently.
            const string input = "this is a test sentence we want to process concurrently";
            var results = new string[100];
            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                results[i] = TextPostProcessor.Process(input);
            });

            // All concurrent calls should produce identical output.
            results.Should().AllBe(results[0]);
        }
    }
}
