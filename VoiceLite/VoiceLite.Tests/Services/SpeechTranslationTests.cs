using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public sealed class SpeechTranslationTests : IDisposable
    {
        private readonly string modelDir = Path.Combine(
            Path.GetTempPath(),
            $"VoiceLite-CanaryConfig-{Guid.NewGuid():N}");

        [Fact]
        public void Settings_DefaultsKeepExistingTranscriptionBehavior()
        {
            var settings = new Settings();

            settings.TranslateToEnglish.Should().BeFalse();
            settings.TranslationSourceLanguage.Should().Be("es");
        }

        [Fact]
        public void SettingsValidator_RepairsUnsupportedTranslationLanguage()
        {
            var settings = new Settings { TranslationSourceLanguage = "it" };

            SettingsValidator.ValidateAndRepair(settings)
                .TranslationSourceLanguage.Should().Be("es");
        }

        [Theory]
        [InlineData("es")]
        [InlineData("fr")]
        [InlineData("de")]
        public void SettingsValidator_PreservesSupportedTranslationLanguage(string language)
        {
            var settings = new Settings { TranslationSourceLanguage = language };

            SettingsValidator.ValidateAndRepair(settings)
                .TranslationSourceLanguage.Should().Be(language);
        }

        [Fact]
        public void OldSettingsJson_LeavesTranslationDisabled()
        {
            var settings = JsonSerializer.Deserialize<Settings>(
                """{"WhisperModel":"parakeet-tdt-0.6b-v3-int8"}""");

            settings.Should().NotBeNull();
            settings!.TranslateToEnglish.Should().BeFalse();
            settings.TranslationSourceLanguage.Should().Be("es");
        }

        [Fact]
        public void TranslationSettings_RoundTripThroughSettingsJson()
        {
            var original = new Settings
            {
                TranslateToEnglish = true,
                TranslationSourceLanguage = "fr"
            };

            var json = JsonSerializer.Serialize(original);
            var reloaded = JsonSerializer.Deserialize<Settings>(json);

            reloaded.Should().NotBeNull();
            reloaded!.TranslateToEnglish.Should().BeTrue();
            reloaded.TranslationSourceLanguage.Should().Be("fr");
        }

        [Fact]
        public void EffectiveTranslateToEnglish_FreeUser_IsFalse_EvenWhenEnabledInSettings()
        {
            var settings = new Settings
            {
                IsProLicense = false,
                TranslateToEnglish = true
            };
            using var service = new TranscriptionService(settings);

            service.EffectiveTranslateToEnglish.Should().BeFalse(
                "Free tier must fall back to normal Parakeet transcription");
        }

        [Fact]
        public void EffectiveTranslateToEnglish_ProUser_ReflectsTheSetting()
        {
            var settings = new Settings
            {
                IsProLicense = true,
                TranslateToEnglish = true
            };
            using var service = new TranscriptionService(settings);

            service.EffectiveTranslateToEnglish.Should().BeTrue();
        }

        [Fact]
        public void DownloadEndpoint_ResolvesTranslationModelBundle()
        {
            DownloadEndpoints.GetUrlForFileName(TranslationModelResolverService.ModelId)
                .Should().Be(DownloadEndpoints.CanaryTranslationInt8);
        }

        [Theory]
        [InlineData("es", "es")]
        [InlineData("fr", "fr")]
        [InlineData("de", "de")]
        [InlineData("ES", "es")]
        public void CreateCanaryRecognizerConfig_TargetsEnglishLocally(
            string sourceLanguage,
            string expectedSourceLanguage)
        {
            WriteRequiredFiles();

            var config = TranscriptionService.CreateCanaryRecognizerConfig(
                modelDir,
                sourceLanguage);

            config.ModelConfig.Canary.Encoder.Should().Be(
                Path.Combine(modelDir, "encoder.int8.onnx"));
            config.ModelConfig.Canary.Decoder.Should().Be(
                Path.Combine(modelDir, "decoder.int8.onnx"));
            config.ModelConfig.Canary.SrcLang.Should().Be(expectedSourceLanguage);
            config.ModelConfig.Canary.TgtLang.Should().Be("en");
            config.ModelConfig.Canary.UsePnc.Should().Be(1);
            config.DecodingMethod.Should().Be("greedy_search");
        }

        [Fact]
        public void CreateCanaryRecognizerConfig_RejectsUnsupportedSourceLanguage()
        {
            var act = () => TranscriptionService.CreateCanaryRecognizerConfig(modelDir, "it");

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unsupported translation source language*");
        }

        [Fact]
        public async Task ForeignLanguageSpeech_ProducesEnglishTranslation_WhenFunctionalModelIsAvailable()
        {
            // Optional local functional coverage, matching the repository's established
            // model-dependent test pattern. CI does not download ~154MB just to run it.
            var testRoot = Environment.GetEnvironmentVariable("VOICELITE_CANARY_TEST_ROOT");
            var sourceWav = Environment.GetEnvironmentVariable("VOICELITE_CANARY_TEST_WAV");
            var sourceLanguage = Environment.GetEnvironmentVariable("VOICELITE_CANARY_TEST_LANGUAGE") ?? "es";
            if (string.IsNullOrWhiteSpace(testRoot) ||
                string.IsNullOrWhiteSpace(sourceWav) ||
                !File.Exists(sourceWav))
            {
                return;
            }

            var settings = new Settings
            {
                IsProLicense = true, // Translation is Pro-gated
                TranslateToEnglish = true,
                TranslationSourceLanguage = sourceLanguage
            };
            var resolver = new TranslationModelResolverService(
                testRoot,
                Path.Combine(testRoot, "unused-local-app-data"));

            using var service = new TranscriptionService(
                settings,
                translationModelResolver: resolver);

            var result = await service.TranscribeAsync(sourceWav);

            result.Should().NotBeNullOrWhiteSpace();
            result.Should().ContainAny(
                "the", "The", "you", "You", "is", "are", "has", "have");
        }

        private void WriteRequiredFiles()
        {
            Directory.CreateDirectory(modelDir);
            foreach (var fileName in TranslationModelResolverService.RequiredModelFiles)
            {
                File.WriteAllText(Path.Combine(modelDir, fileName), "test");
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(modelDir))
                Directory.Delete(modelDir, recursive: true);
        }
    }
}
