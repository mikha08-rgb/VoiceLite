using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class TextPostProcessorCustomDictionaryTests
    {
        private static List<CustomDictionaryEntry> Dict(params (string Spoken, string Written)[] entries)
        {
            var list = new List<CustomDictionaryEntry>();
            foreach (var (spoken, written) in entries)
            {
                list.Add(new CustomDictionaryEntry { Spoken = spoken, Written = written });
            }
            return list;
        }

        [Fact]
        public void Process_WithNullDictionary_DoesNotChangeNonDictWords()
        {
            var result = TextPostProcessor.Process("the medicare claim was approved", customDictionary: null);
            // No dictionary entry, so "medicare" stays lowercase (no dev-term match either).
            result.Should().Contain("medicare");
        }

        [Fact]
        public void Process_WithEmptyDictionary_DoesNotChangeText()
        {
            var dict = new List<CustomDictionaryEntry>();
            var result = TextPostProcessor.Process("the medicare claim was approved", customDictionary: dict);
            result.Should().Contain("medicare");
        }

        [Fact]
        public void Process_AppliesSingleEntry_CaseInsensitive()
        {
            var dict = Dict(("medicare", "Medicare"));
            var result = TextPostProcessor.Process("apply for medicare today", customDictionary: dict);
            result.Should().Contain("Medicare");
            result.Should().NotContain("medicare ");
        }

        [Fact]
        public void Process_RespectsWordBoundaries()
        {
            // "medicareadvantage" is one word, should not be replaced
            var dict = Dict(("medicare", "Medicare"));
            var result = TextPostProcessor.Process("the medicareadvantage plan", customDictionary: dict);
            result.Should().Contain("medicareadvantage");
        }

        [Fact]
        public void Process_AppliesMultipleEntries()
        {
            var dict = Dict(
                ("egfr", "eGFR"),
                ("medicare", "Medicare"),
                ("ckd", "CKD"));

            var result = TextPostProcessor.Process(
                "the patient has medicare and ckd with low egfr",
                customDictionary: dict);

            result.Should().Contain("Medicare");
            result.Should().Contain("CKD");
            result.Should().Contain("eGFR");
        }

        [Fact]
        public void Process_LongerEntryWinsOverShorter()
        {
            // Both "north" and "north shore" could match; longer must win.
            var dict = Dict(
                ("north", "N"),
                ("north shore", "North Shore Hospital"));

            var result = TextPostProcessor.Process("admitted to north shore yesterday", customDictionary: dict);
            result.Should().Contain("North Shore Hospital");
            result.Should().NotContain("N shore");
        }

        [Fact]
        public void Process_CustomDictionaryAppliesAfterDevTerm()
        {
            // Dev-term maps "github" → "GitHub"; user override should win on top of that.
            var dict = Dict(("github", "Github (legacy)"));
            var result = TextPostProcessor.Process("push to github please", customDictionary: dict);
            result.Should().Contain("Github (legacy)");
            result.Should().NotContain("GitHub ");
        }

        [Fact]
        public void Process_SkipsEntriesWithBlankSpoken()
        {
            // Blank Spoken keys must not crash the regex builder.
            var dict = Dict(
                ("", "should-be-ignored"),
                ("   ", "also-ignored"),
                ("foo", "Foo"));

            var result = TextPostProcessor.Process("the foo bar", customDictionary: dict);
            result.Should().Contain("Foo");
        }

        [Fact]
        public void Process_HandlesNullWrittenAsEmptyString()
        {
            var dict = new List<CustomDictionaryEntry>
            {
                new CustomDictionaryEntry { Spoken = "umm", Written = null! },
            };

            var result = TextPostProcessor.Process("well umm yeah", customDictionary: dict);
            // Replacement with empty string leaves "well  yeah" — verify it doesn't crash + the filler is gone.
            result.Should().NotContain("umm");
        }

        [Fact]
        public async Task Process_IsThreadSafe_WithCustomDictionary()
        {
            var dict = Dict(("medicare", "Medicare"), ("egfr", "eGFR"));
            var input = "the patient has medicare and a low egfr value";

            var results = new string[100];
            await Task.Run(() => Parallel.For(0, 100, i =>
            {
                results[i] = TextPostProcessor.Process(input, customDictionary: dict);
            }));

            results.Should().AllBe(results[0]);
            results[0].Should().Contain("Medicare");
            results[0].Should().Contain("eGFR");
        }
    }
}
