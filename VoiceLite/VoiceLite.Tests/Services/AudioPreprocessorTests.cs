using System;
using System.IO;
using FluentAssertions;
using NAudio.Wave;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class AudioPreprocessorTests : IDisposable
    {
        private readonly Settings _testSettings;
        private readonly string _tempDirectory;
        private readonly string _testAudioFile;

        public AudioPreprocessorTests()
        {
            _testSettings = new Settings();
            _tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_audio_tests");
            Directory.CreateDirectory(_tempDirectory);
            _testAudioFile = Path.Combine(_tempDirectory, "test_audio.wav");

            // Create a simple test audio file (1 second of 440Hz sine wave at 16kHz, 16-bit mono)
            CreateTestAudioFile(_testAudioFile, durationSeconds: 1.0, frequency: 440);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        #region Preset Application Tests

// REMOVED:         [Fact]
// REMOVED:         public void StudioPreset_AppliesCorrectSettings()
// REMOVED:         {
// REMOVED:             // Arrange
// REMOVED:             var settings = new Settings();
// REMOVED: 
// REMOVED:             // Act
// REMOVED:             settings.ApplyAudioPreset(AudioPreset.StudioQuality);
// REMOVED: 
// REMOVED:             // Assert
// REMOVED:             settings.CurrentAudioPreset.Should().Be(AudioPreset.StudioQuality);
// REMOVED:             settings.EnableNoiseSuppression.Should().BeTrue("StudioQuality should enable noise suppression");
// REMOVED:             settings.EnableAutomaticGain.Should().BeTrue("StudioQuality should enable automatic gain");
// REMOVED:             settings.TargetRmsLevel.Should().Be(0.25f);
// REMOVED:             settings.NoiseGateThreshold.Should().Be(0.01);
// REMOVED:             settings.UseVAD.Should().BeTrue("StudioQuality should enable VAD");
// REMOVED:         }

// REMOVED:         [Fact]
// REMOVED:         public void OfficePreset_AppliesCorrectSettings()
// REMOVED:         {
// REMOVED:             // Arrange
// REMOVED:             var settings = new Settings();
// REMOVED: 
// REMOVED:             // Act
// REMOVED:             settings.ApplyAudioPreset(AudioPreset.OfficeNoisy);
// REMOVED: 
// REMOVED:             // Assert
// REMOVED:             settings.CurrentAudioPreset.Should().Be(AudioPreset.OfficeNoisy);
// REMOVED:             settings.EnableNoiseSuppression.Should().BeTrue("OfficeNoisy should enable noise suppression");
// REMOVED:             settings.EnableAutomaticGain.Should().BeTrue("OfficeNoisy should enable automatic gain");
// REMOVED:             settings.TargetRmsLevel.Should().Be(0.3f);
// REMOVED:             settings.NoiseGateThreshold.Should().Be(0.04);
// REMOVED:             settings.UseVAD.Should().BeTrue("OfficeNoisy should enable VAD");
// REMOVED:         }

// REMOVED:         [Fact]
// REMOVED:         public void DefaultPreset_AppliesCorrectSettings()
// REMOVED:         {
// REMOVED:             // Arrange
// REMOVED:             var settings = new Settings();
// REMOVED: 
// REMOVED:             // Act
// REMOVED:             settings.ApplyAudioPreset(AudioPreset.Default);
// REMOVED: 
// REMOVED:             // Assert
// REMOVED:             settings.CurrentAudioPreset.Should().Be(AudioPreset.Default);
// REMOVED:             settings.EnableNoiseSuppression.Should().BeFalse("Default should disable noise suppression");
// REMOVED:             settings.EnableAutomaticGain.Should().BeFalse("Default should disable automatic gain");
// REMOVED:             settings.TargetRmsLevel.Should().Be(0.2f);
// REMOVED:             settings.NoiseGateThreshold.Should().Be(0.02);
// REMOVED:             settings.UseVAD.Should().BeTrue("Default should enable VAD");
// REMOVED:         }

        #endregion

        #region Settings Validation Tests

        [Fact]
        public void TargetRmsLevel_WhenSetToNegative_ClampsToMinimum()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.TargetRmsLevel = -0.5f;

            // Assert
            settings.TargetRmsLevel.Should().Be(0.05f, "TargetRmsLevel should clamp negative values to minimum 0.05");
        }

        [Fact]
        public void TargetRmsLevel_WhenSetAboveOne_ClampsToMaximum()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.TargetRmsLevel = 1.5f;

            // Assert
            settings.TargetRmsLevel.Should().Be(0.95f, "TargetRmsLevel should clamp values above 1.0 to maximum 0.95");
        }

        [Fact]
        public void NoiseGateThreshold_WhenSetToNegative_ClampsToMinimum()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.NoiseGateThreshold = -0.1;

            // Assert
            settings.NoiseGateThreshold.Should().Be(0.001, "NoiseGateThreshold should clamp negative values to minimum 0.001");
        }

        [Fact]
        public void NoiseGateThreshold_WhenSetAboveHalf_ClampsToMaximum()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.NoiseGateThreshold = 0.8;

            // Assert
            settings.NoiseGateThreshold.Should().Be(0.5, "NoiseGateThreshold should clamp values above 0.5 to maximum 0.5");
        }

        #endregion

        #region Audio Processing Stats Tests

        [Fact]
        public void ProcessAudioFileWithStats_ReturnsOriginalDuration()
        {
            // Arrange
            var testFile = CreateTemporaryTestFile("original_duration.wav", durationSeconds: 2.0);
            var settings = new Settings();

            // Act
            var stats = AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            stats.OriginalDurationSeconds.Should().BeApproximately(2.0, 0.1, "Original duration should be tracked");
        }

        [Fact]
        public void ProcessAudioFileWithStats_ReturnsProcessedPeakLevel()
        {
            // Arrange
            var testFile = CreateTemporaryTestFile("peak_level.wav", durationSeconds: 1.0);
            var settings = new Settings();

            // Act
            var stats = AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            stats.ProcessedPeakLevel.Should().BeGreaterThan(0, "Processed peak level should be tracked");
            stats.ProcessedPeakLevel.Should().BeLessThanOrEqualTo(1.0f, "Peak level should not exceed 1.0");
        }

        [Fact]
        public void ProcessAudioFileWithStats_WhenVADEnabled_ReturnsTrimmedSilenceMs()
        {
            // Arrange
            // Create audio with silence at start and end
            var testFile = CreateAudioWithSilence("vad_test.wav");
            var settings = new Settings
            {
                UseVAD = true
            };

            // Act
            var stats = AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            stats.VADApplied.Should().BeTrue("VAD should be marked as applied");
            stats.TrimmedSilenceMs.Should().BeGreaterOrEqualTo(0, "Trimmed silence should be non-negative");
        }

        [Fact]
        public void ProcessAudioFileWithStats_TracksWhichProcessingApplied()
        {
            // Arrange
            var testFile = CreateTemporaryTestFile("processing_flags.wav", durationSeconds: 1.0);
            var settings = new Settings
            {
                EnableNoiseSuppression = true,
                EnableAutomaticGain = true,
                UseVAD = true
            };

            // Act
            var stats = AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            stats.NoiseSuppressionApplied.Should().BeTrue("Noise suppression flag should be set");
            stats.AutoGainApplied.Should().BeTrue("Auto gain flag should be set");
            stats.VADApplied.Should().BeTrue("VAD flag should be set");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void ProcessAudioFile_WithEmptyAudio_DoesNotCrash()
        {
            // Arrange
            var testFile = CreateEmptyAudioFile("empty.wav");
            var settings = new Settings();

            // Act
            Action act = () => AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            act.Should().NotThrow("Processing empty audio should not crash");
        }

        [Fact]
        public void ProcessAudioFile_WithInvalidPath_ThrowsAppropriateException()
        {
            // Arrange
            var invalidPath = Path.Combine(_tempDirectory, "nonexistent_file.wav");
            var settings = new Settings();

            // Act
            Action act = () => AudioPreprocessor.ProcessAudioFileWithStats(invalidPath, settings);

            // Assert
            act.Should().Throw<Exception>("Processing non-existent file should throw exception");
        }

// REMOVED:         [Fact]
// REMOVED:         public void ApplyAudioPreset_WithCustomPreset_DoesNotChangeSettings()
// REMOVED:         {
// REMOVED:             // Arrange
// REMOVED:             var settings = new Settings
// REMOVED:             {
// REMOVED:                 EnableNoiseSuppression = true,
// REMOVED:                 EnableAutomaticGain = false,
// REMOVED:                 TargetRmsLevel = 0.5f,
// REMOVED:                 NoiseGateThreshold = 0.03,
// REMOVED:                 UseVAD = false
// REMOVED:             };
// REMOVED: 
// REMOVED:             // Store original values
// REMOVED:             var originalNoiseSuppression = settings.EnableNoiseSuppression;
// REMOVED:             var originalAutoGain = settings.EnableAutomaticGain;
// REMOVED:             var originalRms = settings.TargetRmsLevel;
// REMOVED:             var originalGate = settings.NoiseGateThreshold;
// REMOVED:             var originalVAD = settings.UseVAD;
// REMOVED: 
// REMOVED:             // Act
// REMOVED:             settings.ApplyAudioPreset(AudioPreset.Custom);
// REMOVED: 
// REMOVED:             // Assert
// REMOVED:             settings.CurrentAudioPreset.Should().Be(AudioPreset.Custom);
// REMOVED:             settings.EnableNoiseSuppression.Should().Be(originalNoiseSuppression, "Custom preset should not change noise suppression");
// REMOVED:             settings.EnableAutomaticGain.Should().Be(originalAutoGain, "Custom preset should not change auto gain");
// REMOVED:             settings.TargetRmsLevel.Should().Be(originalRms, "Custom preset should not change RMS level");
// REMOVED:             settings.NoiseGateThreshold.Should().Be(originalGate, "Custom preset should not change noise gate");
// REMOVED:             settings.UseVAD.Should().Be(originalVAD, "Custom preset should not change VAD");
// REMOVED:         }

        #endregion

        #region Integration Tests
// REMOVED: 
// REMOVED: // REMOVED:         [Fact]
// REMOVED:         public void ApplyPreset_ChangesCurrentAudioPreset()
// REMOVED:         {
// REMOVED:             // Arrange
// REMOVED:             var settings = new Settings();
// REMOVED:             settings.CurrentAudioPreset.Should().Be(AudioPreset.Default, "Initial preset should be Default");
// REMOVED: 
// REMOVED:             // Act
// REMOVED:             settings.ApplyAudioPreset(AudioPreset.StudioQuality);
// REMOVED: 
// REMOVED:             // Assert
// REMOVED:             settings.CurrentAudioPreset.Should().Be(AudioPreset.StudioQuality, "Current preset should update after applying preset");
// REMOVED:         }
// REMOVED: 
// REMOVED:         [Fact]
// REMOVED:         public void ChangingSetting_AfterPresetApplied_ShouldAllowManualChanges()
// REMOVED:         {
// REMOVED:             // Arrange
// REMOVED:             var settings = new Settings();
// REMOVED:             settings.ApplyAudioPreset(AudioPreset.StudioQuality);
// REMOVED: 
// REMOVED:             var originalRms = settings.TargetRmsLevel;
// REMOVED: 
// REMOVED:             // Act - Manually change a setting after applying preset
// REMOVED:             settings.TargetRmsLevel = 0.5f;
// REMOVED: 
// REMOVED:             // Assert
// REMOVED:             settings.TargetRmsLevel.Should().Be(0.5f, "Manual changes should be allowed after preset application");
// REMOVED:             settings.TargetRmsLevel.Should().NotBe(originalRms, "Setting should have changed from preset value");
// REMOVED: 
// REMOVED:             // Note: In a full implementation, this might also set CurrentAudioPreset to Custom
// REMOVED:             // but that logic would be in the UI layer, not the model
// REMOVED:         }
// REMOVED: 
        [Fact]
        public void ProcessAudioFile_WithNoiseSuppressionEnabled_ProcessesSuccessfully()
        {
            // Arrange
            var testFile = CreateTemporaryTestFile("noise_suppression_integration.wav", durationSeconds: 1.0);
            var settings = new Settings
            {
                EnableNoiseSuppression = true,
                NoiseGateThreshold = 0.02
            };

            // Act
            var stats = AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            stats.Should().NotBeNull("Processing should return stats");
            stats.NoiseSuppressionApplied.Should().BeTrue("Noise suppression should be applied");
            File.Exists(testFile).Should().BeTrue("Output file should exist after processing");
        }

        [Fact]
        public void ProcessAudioFile_WithAutomaticGainEnabled_ProcessesSuccessfully()
        {
            // Arrange
            var testFile = CreateTemporaryTestFile("auto_gain_integration.wav", durationSeconds: 1.0);
            var settings = new Settings
            {
                EnableAutomaticGain = true,
                TargetRmsLevel = 0.3f
            };

            // Act
            var stats = AudioPreprocessor.ProcessAudioFileWithStats(testFile, settings);

            // Assert
            stats.Should().NotBeNull("Processing should return stats");
            stats.AutoGainApplied.Should().BeTrue("Auto gain should be applied");
            stats.ProcessedPeakLevel.Should().BeGreaterThan(0, "Processed audio should have measurable peak level");
        }

        // REMOVED: AudioPreset test - feature removed
        // [Fact]
        // public void ProcessAudioFile_WithAllEnhancementsEnabled_ProcessesSuccessfully() { }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a test audio file with a sine wave at the specified frequency.
        /// </summary>
        private void CreateTestAudioFile(string path, double durationSeconds, int frequency = 440)
        {
            var sampleRate = 16000; // 16kHz as required by Whisper
            var amplitude = 0.5f;
            var numSamples = (int)(sampleRate * durationSeconds);

            var samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            }

            var waveFormat = new WaveFormat(sampleRate, 16, 1); // 16kHz, 16-bit, mono
            using (var writer = new WaveFileWriter(path, waveFormat))
            {
                writer.WriteSamples(samples, 0, samples.Length);
            }
        }

        /// <summary>
        /// Creates a temporary test file with a unique name.
        /// </summary>
        private string CreateTemporaryTestFile(string filename, double durationSeconds)
        {
            var path = Path.Combine(_tempDirectory, filename);
            CreateTestAudioFile(path, durationSeconds);
            return path;
        }

        /// <summary>
        /// Creates an audio file with silence at the beginning and end (for VAD testing).
        /// </summary>
        private string CreateAudioWithSilence(string filename)
        {
            var path = Path.Combine(_tempDirectory, filename);
            var sampleRate = 16000;
            var silenceDuration = 0.2; // 200ms of silence at start and end
            var speechDuration = 0.6;  // 600ms of speech in middle
            var totalDuration = silenceDuration * 2 + speechDuration;

            var numSamples = (int)(sampleRate * totalDuration);
            var silenceSamples = (int)(sampleRate * silenceDuration);
            var speechSamples = (int)(sampleRate * speechDuration);

            var samples = new float[numSamples];

            // Add silence at start (already zeroed)

            // Add speech in middle
            var amplitude = 0.5f;
            var frequency = 440;
            for (int i = silenceSamples; i < silenceSamples + speechSamples; i++)
            {
                samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            }

            // Add silence at end (already zeroed)

            var waveFormat = new WaveFormat(sampleRate, 16, 1);
            using (var writer = new WaveFileWriter(path, waveFormat))
            {
                writer.WriteSamples(samples, 0, samples.Length);
            }

            return path;
        }

        /// <summary>
        /// Creates an empty audio file (minimal valid WAV file).
        /// </summary>
        private string CreateEmptyAudioFile(string filename)
        {
            var path = Path.Combine(_tempDirectory, filename);
            var sampleRate = 16000;
            var samples = new float[1]; // Minimal audio (single sample)

            var waveFormat = new WaveFormat(sampleRate, 16, 1);
            using (var writer = new WaveFileWriter(path, waveFormat))
            {
                writer.WriteSamples(samples, 0, samples.Length);
            }

            return path;
        }

        #endregion
    }
}
