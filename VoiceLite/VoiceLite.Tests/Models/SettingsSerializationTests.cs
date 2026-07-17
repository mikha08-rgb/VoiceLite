using System.Text.Json;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    /// <summary>
    /// Guards the 2026-07-17 Whisper→Parakeet vocabulary rename: the C# property is now
    /// Settings.TranscriptionModel, but every existing user's settings.json on disk uses
    /// the legacy key "WhisperModel". [JsonPropertyName("WhisperModel")] must keep the
    /// serialized form byte-compatible in BOTH directions, forever.
    /// </summary>
    public class SettingsSerializationTests
    {
        [Fact]
        public void Settings_LegacyWhisperModelKey_DeserializesIntoTranscriptionModel()
        {
            // A minimal slice of a real pre-rename settings.json.
            var oldJson = """{"WhisperModel":"parakeet-tdt-0.6b-v3-int8","Language":"en"}""";

            var settings = JsonSerializer.Deserialize<Settings>(oldJson);

            settings.Should().NotBeNull();
            settings!.TranscriptionModel.Should().Be("parakeet-tdt-0.6b-v3-int8");
        }

        [Fact]
        public void Settings_LegacyGgmlValueUnderWhisperModelKey_StillDeserializes()
        {
            // Pre-v2.0 installs persisted GGML filenames; SettingsMigration rewrites the
            // VALUE later, but raw deserialization must surface it unchanged first.
            var oldJson = """{"WhisperModel":"ggml-small.bin"}""";

            var settings = JsonSerializer.Deserialize<Settings>(oldJson);

            settings.Should().NotBeNull();
            settings!.TranscriptionModel.Should().Be("ggml-small.bin");
        }

        [Fact]
        public void Settings_Serialize_WritesLegacyWhisperModelKey_NotTranscriptionModel()
        {
            var settings = new Settings();

            var json = JsonSerializer.Serialize(settings);

            json.Should().Contain("\"WhisperModel\"",
                "existing users' settings.json must keep the legacy key");
            json.Should().NotContain("\"TranscriptionModel\"",
                "the renamed property must never leak into the serialized form");
        }

        [Fact]
        public void Settings_OldSettingsJson_RoundTripsBothDirections()
        {
            var oldJson = """{"WhisperModel":"parakeet-tdt-0.6b-v3-int8"}""";

            var settings = JsonSerializer.Deserialize<Settings>(oldJson);
            var rewritten = JsonSerializer.Serialize(settings);
            var reloaded = JsonSerializer.Deserialize<Settings>(rewritten);

            rewritten.Should().Contain("\"WhisperModel\":\"parakeet-tdt-0.6b-v3-int8\"");
            reloaded!.TranscriptionModel.Should().Be("parakeet-tdt-0.6b-v3-int8");
        }
    }
}
