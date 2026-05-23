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

        [Theory]
        [InlineData("what time is it")]
        [InlineData("how are you")]
        [InlineData("can you help me")]
        [InlineData("are you there")]
        public void Process_AddsQuestionMark_ToInterrogatives(string input)
        {
            var result = TextPostProcessor.Process(input);
            result.Should().EndWith("?");
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

        [Theory]
        [InlineData("i use github every day", "GitHub")]
        [InlineData("running python tests", "Python")]
        [InlineData("on windows ten", "Windows")]
        public void Process_CapitalizesTechProperNouns(string input, string expected)
        {
            var result = TextPostProcessor.Process(input);
            result.Should().Contain(expected);
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
            result.Should().EndWith("?");
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
