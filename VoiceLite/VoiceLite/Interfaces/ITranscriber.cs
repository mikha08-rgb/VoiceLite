using System;
using System.Threading.Tasks;

namespace VoiceLite.Interfaces
{
    public interface ITranscriber : IDisposable
    {
        Task<string> TranscribeAsync(string audioFilePath);
        Task<string> TranscribeFromMemoryAsync(byte[] audioData);
    }
}