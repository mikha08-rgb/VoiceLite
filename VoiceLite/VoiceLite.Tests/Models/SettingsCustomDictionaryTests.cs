using System.Text.Json;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    public class SettingsCustomDictionaryTests
    {
        [Fact]
        public void Settings_CustomDictionary_DefaultsToEmptyList()
        {
            var settings = new Settings();
            settings.CustomDictionary.Should().NotBeNull();
            settings.CustomDictionary.Should().BeEmpty();
        }

        [Fact]
        public void Settings_CustomDictionary_RoundTripsThroughJson()
        {
            var settings = new Settings();
            settings.CustomDictionary.Add(new CustomDictionaryEntry { Spoken = "medicare", Written = "Medicare" });
            settings.CustomDictionary.Add(new CustomDictionaryEntry { Spoken = "egfr", Written = "eGFR" });

            var json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<Settings>(json);

            deserialized.Should().NotBeNull();
            deserialized!.CustomDictionary.Should().HaveCount(2);
            deserialized.CustomDictionary[0].Spoken.Should().Be("medicare");
            deserialized.CustomDictionary[0].Written.Should().Be("Medicare");
            deserialized.CustomDictionary[1].Spoken.Should().Be("egfr");
            deserialized.CustomDictionary[1].Written.Should().Be("eGFR");
        }

        [Fact]
        public void Settings_CustomDictionary_MissingJsonKey_DeserializesToEmptyList()
        {
            // Old settings.json files (pre-v2.1) won't have the CustomDictionary key.
            // It must deserialize as an empty list, not null, so the UI binding survives.
            var json = "{}";
            var settings = JsonSerializer.Deserialize<Settings>(json);

            settings.Should().NotBeNull();
            settings!.CustomDictionary.Should().NotBeNull();
            settings.CustomDictionary.Should().BeEmpty();
        }
    }
}
