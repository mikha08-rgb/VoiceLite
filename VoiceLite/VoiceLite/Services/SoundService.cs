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
        private readonly object _disposeLock = new object();

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
            if (_disposed) return;

            try
            {
                if (!File.Exists(_soundFilePath))
                {
                    // Fallback to system beep if file not found
                    SystemSounds.Beep.Play();
                    return;
                }

                lock (_disposeLock)
                {
                    if (_disposed) return;

                    // Dispose previous instances if still playing
                    CleanupAudioResources();

                    // Create new instances - use VorbisWaveReader for .ogg files
                    _audioFile = new VorbisWaveReader(_soundFilePath);
                    _outputDevice = new WaveOutEvent();

                    _outputDevice.Init(_audioFile);
                    _outputDevice.PlaybackStopped += OnPlaybackStopped;

                    _outputDevice.Play();
                }
            }
            catch (Exception)
            {
                // Silently fallback to system beep on any error
                SystemSounds.Beep.Play();
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // Clean up resources after playback completes
            lock (_disposeLock)
            {
                if (_disposed) return;
                CleanupAudioResources();
            }
        }

        private void CleanupAudioResources()
        {
            // IMPORTANT: No lock here - caller must hold _disposeLock
            try
            {
                if (_outputDevice != null)
                {
                    _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                    _outputDevice.Stop();
                    _outputDevice.Dispose();
                    _outputDevice = null;
                }
            }
            catch { /* Ignore disposal errors */ }

            try
            {
                if (_audioFile != null)
                {
                    _audioFile.Dispose();
                    _audioFile = null;
                }
            }
            catch { /* Ignore disposal errors */ }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;

                CleanupAudioResources();
                _disposed = true;
            }
        }
    }
}
