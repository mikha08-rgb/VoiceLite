using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    public class CustomDictionaryTests
    {
        [Fact]
        public void DictionaryEntry_GetCompiledRegex_ReturnsValidRegex()
        {
            var entry = new DictionaryEntry
            {
                Pattern = "test",
                Replacement = "result"
            };

            var regex = entry.GetCompiledRegex();

            regex.Should().NotBeNull();
            regex.Match("this is a test").Success.Should().BeTrue();
        }

        [Fact]
        public void DictionaryEntry_GetCompiledRegex_CachesRegex()
        {
            var entry = new DictionaryEntry { Pattern = "test", Replacement = "result" };

            var regex1 = entry.GetCompiledRegex();
            var regex2 = entry.GetCompiledRegex();

            // Should return the same instance (cached)
            regex1.Should().BeSameAs(regex2);
        }

        [Fact]
        public void DictionaryEntry_InvalidateCache_ClearsRegex()
        {
            var entry = new DictionaryEntry { Pattern = "test", Replacement = "result" };

            var regex1 = entry.GetCompiledRegex();
            entry.InvalidateCache();
            var regex2 = entry.GetCompiledRegex();

            // Should return a new instance after invalidation
            regex1.Should().NotBeSameAs(regex2);
        }

        [Fact]
        public void DictionaryEntry_WholeWord_MatchesOnlyWholeWords()
        {
            var entry = new DictionaryEntry
            {
                Pattern = "test",
                Replacement = "result",
                WholeWord = true
            };

            var regex = entry.GetCompiledRegex();

            regex.Match("this is a test").Success.Should().BeTrue();
            regex.Match("testing").Success.Should().BeFalse();
            regex.Match("attest").Success.Should().BeFalse();
        }

        [Fact]
        public void DictionaryEntry_NonWholeWord_MatchesPartial()
        {
            var entry = new DictionaryEntry
            {
                Pattern = "test",
                Replacement = "result",
                WholeWord = false
            };

            var regex = entry.GetCompiledRegex();

            regex.Match("this is a test").Success.Should().BeTrue();
            regex.Match("testing").Success.Should().BeTrue();
            regex.Match("attest").Success.Should().BeTrue();
        }

        [Fact]
        public void DictionaryEntry_CaseSensitive_MatchesCase()
        {
            var entry = new DictionaryEntry
            {
                Pattern = "Test",
                Replacement = "result",
                CaseSensitive = true
            };

            var regex = entry.GetCompiledRegex();

            regex.Match("Test").Success.Should().BeTrue();
            regex.Match("test").Success.Should().BeFalse();
            regex.Match("TEST").Success.Should().BeFalse();
        }

        [Fact]
        public void DictionaryEntry_CaseInsensitive_MatchesAnyCase()
        {
            var entry = new DictionaryEntry
            {
                Pattern = "Test",
                Replacement = "result",
                CaseSensitive = false
            };

            var regex = entry.GetCompiledRegex();

            regex.Match("Test").Success.Should().BeTrue();
            regex.Match("test").Success.Should().BeTrue();
            regex.Match("TEST").Success.Should().BeTrue();
            regex.Match("TeSt").Success.Should().BeTrue();
        }

        [Fact]
        public void DictionaryEntry_DefaultsToEnabled()
        {
            var entry = new DictionaryEntry();
            entry.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void DictionaryEntry_DefaultsToGeneral()
        {
            var entry = new DictionaryEntry();
            entry.Category.Should().Be(DictionaryCategory.General);
        }

        [Fact]
        public void DictionaryEntry_DefaultsToWholeWord()
        {
            var entry = new DictionaryEntry();
            entry.WholeWord.Should().BeTrue();
        }

        [Fact]
        public void DictionaryEntry_DefaultsToNonCaseSensitive()
        {
            var entry = new DictionaryEntry();
            entry.CaseSensitive.Should().BeFalse();
        }

        [Fact]
        public void GetMedicalTemplate_ReturnsEntries()
        {
            var entries = CustomDictionaryTemplates.GetMedicalTemplate();

            entries.Should().NotBeEmpty();
            entries.Should().AllSatisfy(e => e.Category.Should().Be(DictionaryCategory.Medical));
        }

        [Fact]
        public void GetMedicalTemplate_ContainsBPEntry()
        {
            var entries = CustomDictionaryTemplates.GetMedicalTemplate();

            var bpEntry = entries.FirstOrDefault(e => e.Pattern == "BP");
            bpEntry.Should().NotBeNull();
            bpEntry!.Replacement.Should().Be("blood pressure");
        }

        [Fact]
        public void GetLegalTemplate_ReturnsEntries()
        {
            var entries = CustomDictionaryTemplates.GetLegalTemplate();

            entries.Should().NotBeEmpty();
            entries.Should().AllSatisfy(e => e.Category.Should().Be(DictionaryCategory.Legal));
        }

        [Fact]
        public void GetLegalTemplate_ContainsLLCEntry()
        {
            var entries = CustomDictionaryTemplates.GetLegalTemplate();

            var llcEntry = entries.FirstOrDefault(e => e.Pattern == "LLC");
            llcEntry.Should().NotBeNull();
            llcEntry!.Replacement.Should().Be("Limited Liability Company");
        }

        [Fact]
        public void GetTechTemplate_ReturnsEntries()
        {
            var entries = CustomDictionaryTemplates.GetTechTemplate();

            entries.Should().NotBeEmpty();
            entries.Should().AllSatisfy(e => e.Category.Should().Be(DictionaryCategory.Tech));
        }

        [Fact]
        public void GetTechTemplate_ContainsAPIEntry()
        {
            var entries = CustomDictionaryTemplates.GetTechTemplate();

            var apiEntry = entries.FirstOrDefault(e => e.Pattern == "API");
            apiEntry.Should().NotBeNull();
            apiEntry!.Replacement.Should().Be("Application Programming Interface");
        }

        [Fact]
        public void GetAllTemplates_CombinesAllCategories()
        {
            var all = CustomDictionaryTemplates.GetAllTemplates();

            all.Should().NotBeEmpty();
            all.Should().Contain(e => e.Category == DictionaryCategory.Medical);
            all.Should().Contain(e => e.Category == DictionaryCategory.Legal);
            all.Should().Contain(e => e.Category == DictionaryCategory.Tech);
        }

        [Fact]
        public void GetAllTemplates_CountEqualsSum()
        {
            var medical = CustomDictionaryTemplates.GetMedicalTemplate();
            var legal = CustomDictionaryTemplates.GetLegalTemplate();
            var tech = CustomDictionaryTemplates.GetTechTemplate();
            var all = CustomDictionaryTemplates.GetAllTemplates();

            all.Count.Should().Be(medical.Count + legal.Count + tech.Count);
        }

        [Fact]
        public void AllTemplateEntries_HaveValidProperties()
        {
            var all = CustomDictionaryTemplates.GetAllTemplates();

            foreach (var entry in all)
            {
                entry.Pattern.Should().NotBeNullOrEmpty();
                entry.Replacement.Should().NotBeNullOrEmpty();
                entry.IsEnabled.Should().BeTrue();
            }
        }

        [Fact]
        public void DictionaryEntry_EscapesRegexSpecialCharacters()
        {
            var entry = new DictionaryEntry
            {
                Pattern = "C++",
                Replacement = "C plus plus",
                WholeWord = false
            };

            var regex = entry.GetCompiledRegex();

            // Should match literally, not as regex
            regex.Match("C++").Success.Should().BeTrue();
            regex.Match("C").Success.Should().BeFalse();
        }
    }
}
