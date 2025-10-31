using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using VoiceLite.Presentation.Commands;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for recording state and commands.
    /// Minimal extraction from MainWindow - MainWindow still orchestrates actual recording logic.
    /// </summary>
    public class RecordingViewModel : ViewModelBase
    {
        #region Fields

        private bool _isRecording;
        private bool _canRecord = true;
        private string _recordingElapsed = "0:00";
        private DateTime _recordingStartTime;
        private DispatcherTimer? _elapsedTimer;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether recording is currently active
        /// </summary>
        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (SetProperty(ref _isRecording, value))
                {
                    OnPropertyChanged(nameof(RecordingButtonText));
                    OnPropertyChanged(nameof(RecordingButtonToolTip));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether recording can be started
        /// </summary>
        public bool CanRecord
        {
            get => _canRecord;
            set
            {
                if (SetProperty(ref _canRecord, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets the elapsed recording time as formatted string (m:ss)
        /// </summary>
        public string RecordingElapsed
        {
            get => _recordingElapsed;
            private set => SetProperty(ref _recordingElapsed, value);
        }

        /// <summary>
        /// Gets the recording button text based on current state
        /// </summary>
        public string RecordingButtonText => IsRecording ? "Stop Recording" : "Start Recording";

        /// <summary>
        /// Gets the recording button tooltip
        /// </summary>
        public string RecordingButtonToolTip => IsRecording
            ? "Click to stop recording and transcribe"
            : "Click to start recording";

        #endregion

        #region Commands

        public ICommand ToggleRecordingCommand { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when user requests to start recording
        /// </summary>
        public event EventHandler? RecordingStartRequested;

        /// <summary>
        /// Raised when user requests to stop recording
        /// </summary>
        public event EventHandler? RecordingStopRequested;

        #endregion

        #region Constructor

        public RecordingViewModel()
        {
            ToggleRecordingCommand = new RelayCommand(ExecuteToggleRecording, () => CanRecord);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the recording elapsed timer
        /// Call this when recording actually starts in MainWindow
        /// </summary>
        public void StartRecordingTimer()
        {
            _recordingStartTime = DateTime.Now;
            RecordingElapsed = "0:00";

            if (_elapsedTimer == null)
            {
                _elapsedTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100) // Update 10x per second
                };
                _elapsedTimer.Tick += OnElapsedTimerTick;
            }

            _elapsedTimer.Start();
        }

        /// <summary>
        /// Stops the recording elapsed timer
        /// Call this when recording stops in MainWindow
        /// </summary>
        public void StopRecordingTimer()
        {
            _elapsedTimer?.Stop();
        }

        /// <summary>
        /// Resets recording state
        /// </summary>
        public void ResetRecording()
        {
            StopRecordingTimer();
            IsRecording = false;
            RecordingElapsed = "0:00";
        }

        #endregion

        #region Private Methods

        private void ExecuteToggleRecording()
        {
            if (IsRecording)
            {
                // Notify MainWindow to stop recording
                RecordingStopRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Notify MainWindow to start recording
                RecordingStartRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnElapsedTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _recordingStartTime;
            RecordingElapsed = $"{elapsed:m\\:ss}";
        }

        private void UpdateCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region Disposal

        protected override void DisposeCore()
        {
            if (_elapsedTimer != null)
            {
                _elapsedTimer.Stop();
                _elapsedTimer.Tick -= OnElapsedTimerTick;
                _elapsedTimer = null;
            }

            base.DisposeCore();
        }

        #endregion
    }
}
