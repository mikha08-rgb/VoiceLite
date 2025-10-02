using System;
using System.IO;
using System.Media;
using NAudio.Wave;
using NAudio.Vorbis;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service for playing custom UI sounds
    /// </summary>
    public class SoundService : IDisposable
    {
        private WaveOutEvent? _outputDevice;
        private VorbisWaveReader? _audioFile;
        private readonly string _soundFilePath;
        private bool _disposed = false;

        public SoundService()
        {
            // Look for sound file in Resources folder relative to executable
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _soundFilePath = Path.Combine(exeDir, "Resources", "wood-tap-click.ogg");
        }

        /// <summary>
        /// Play the UI sound effect
        /// </summary>
        public void PlaySound()
        {
            try
            {
                if (!File.Exists(_soundFilePath))
                {
                    // Fallback to system beep if file not found
                    SystemSounds.Beep.Play();
                    return;
                }

                // Dispose previous instances if still playing
                _outputDevice?.Stop();
                _outputDevice?.Dispose();
                _audioFile?.Dispose();

                // Create new instances - use VorbisWaveReader for .ogg files
                _audioFile = new VorbisWaveReader(_soundFilePath);
                _outputDevice = new WaveOutEvent();

                _outputDevice.Init(_audioFile);
                _outputDevice.PlaybackStopped += (sender, args) =>
                {
                    _outputDevice?.Dispose();
                    _audioFile?.Dispose();
                };

                _outputDevice.Play();
            }
            catch (Exception)
            {
                // Silently fallback to system beep on any error
                SystemSounds.Beep.Play();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _audioFile?.Dispose();

            _disposed = true;
        }
    }
}
