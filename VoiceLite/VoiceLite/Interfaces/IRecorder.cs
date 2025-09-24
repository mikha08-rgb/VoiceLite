using System;

namespace VoiceLite.Interfaces
{
    public interface IRecorder
    {
        void StartRecording();
        void StopRecording();
        bool IsRecording { get; }
        event EventHandler<string>? AudioFileReady;
    }
}