using System;
using VoiceLite.Utilities;

namespace VoiceLite.Services
{
    /// <summary>
    /// State machine for recording workflow coordination.
    /// Enforces valid state transitions to prevent desync bugs.
    /// Single source of truth for recording state across MainWindow, RecordingCoordinator, AudioRecorder.
    ///
    /// Week 1, Day 3-4: Created to eliminate 3 separate isRecording flags that caused race conditions.
    /// </summary>
    public class RecordingStateMachine
    {
        private RecordingState _state = RecordingState.Idle;
        private readonly object _lock = new object();
        private DateTime _stateEnteredAt;
        private RecordingState _previousState = RecordingState.Idle;

        /// <summary>
        /// Event fired when state transitions successfully.
        /// Subscribers can react to state changes (UI updates, logging, etc.).
        /// </summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Current state of the recording workflow.
        /// Thread-safe read operation.
        /// </summary>
        public RecordingState CurrentState
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// Previous state (before current transition).
        /// Useful for logging and debugging state flow.
        /// </summary>
        public RecordingState PreviousState
        {
            get
            {
                lock (_lock)
                {
                    return _previousState;
                }
            }
        }

        /// <summary>
        /// How long the state machine has been in the current state.
        /// </summary>
        public TimeSpan TimeInCurrentState
        {
            get
            {
                lock (_lock)
                {
                    return DateTime.Now - _stateEnteredAt;
                }
            }
        }

        /// <summary>
        /// Initialize state machine in Idle state.
        /// </summary>
        public RecordingStateMachine()
        {
            _stateEnteredAt = DateTime.Now;
        }

        /// <summary>
        /// Attempt to transition to a new state.
        /// Returns true if transition is valid and succeeded.
        /// Returns false if transition is invalid (logs warning).
        /// Thread-safe operation.
        /// </summary>
        public bool TryTransition(RecordingState toState)
        {
            lock (_lock)
            {
                if (!IsValidTransition(_state, toState))
                {
                    ErrorLogger.LogWarning($"RecordingStateMachine: Invalid transition {_state} → {toState}");
                    return false;
                }

                RecordingState fromState = _state;
                _previousState = fromState;
                _state = toState;
                _stateEnteredAt = DateTime.Now;

                ErrorLogger.LogDebug($"RecordingStateMachine: Transitioned {fromState} → {toState}");

                // Fire event outside lock to avoid deadlocks
                var handler = StateChanged;
                if (handler != null)
                {
                    var args = new StateChangedEventArgs(fromState, toState);
                    // Fire event on ThreadPool to avoid blocking caller
                    System.Threading.Tasks.Task.Run(() => handler(this, args));
                }

                return true;
            }
        }

        /// <summary>
        /// Check if a transition to the target state is valid WITHOUT modifying state.
        /// Useful for enabling/disabling UI buttons based on valid transitions.
        /// </summary>
        public bool CanTransitionTo(RecordingState toState)
        {
            lock (_lock)
            {
                return IsValidTransition(_state, toState);
            }
        }

        /// <summary>
        /// Force reset to Idle state (emergency recovery).
        /// Use this for error recovery or disposal cleanup.
        /// Always succeeds regardless of current state.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                RecordingState fromState = _state;
                _previousState = fromState;
                _state = RecordingState.Idle;
                _stateEnteredAt = DateTime.Now;

                ErrorLogger.LogDebug($"RecordingStateMachine: RESET from {fromState} → Idle");

                var handler = StateChanged;
                if (handler != null)
                {
                    var args = new StateChangedEventArgs(fromState, RecordingState.Idle);
                    System.Threading.Tasks.Task.Run(() => handler(this, args));
                }
            }
        }

        /// <summary>
        /// Validate state transitions according to workflow rules.
        ///
        /// Valid workflow paths:
        /// 1. Normal: Idle → Recording → Stopping → Transcribing → Injecting → Complete → Idle
        /// 2. Cancel during recording: Recording → Cancelled → Idle
        /// 3. Cancel during stopping: Stopping → Cancelled → Idle
        /// 4. Error during transcription: Transcribing → Error → Idle
        /// 5. Error during injection: Injecting → Error → Idle
        /// </summary>
        private bool IsValidTransition(RecordingState from, RecordingState to)
        {
            return (from, to) switch
            {
                // Normal workflow: Idle → Recording
                (RecordingState.Idle, RecordingState.Recording) => true,

                // Recording → Stopping (user released hotkey)
                (RecordingState.Recording, RecordingState.Stopping) => true,

                // Recording → Cancelled (user cancelled during recording)
                (RecordingState.Recording, RecordingState.Cancelled) => true,

                // Recording → Error (error during recording - mic disconnect, etc.)
                (RecordingState.Recording, RecordingState.Error) => true,

                // Stopping → Transcribing (audio file ready, starting transcription)
                (RecordingState.Stopping, RecordingState.Transcribing) => true,

                // Stopping → Cancelled (user cancelled during stop)
                (RecordingState.Stopping, RecordingState.Cancelled) => true,

                // Stopping → Error (error during audio finalization or file save)
                (RecordingState.Stopping, RecordingState.Error) => true,

                // Transcribing → Injecting (transcription complete, injecting text)
                (RecordingState.Transcribing, RecordingState.Injecting) => true,

                // Transcribing → Error (Whisper failed or timeout)
                (RecordingState.Transcribing, RecordingState.Error) => true,

                // Transcribing → Cancelled (user cancelled during transcription)
                (RecordingState.Transcribing, RecordingState.Cancelled) => true,

                // Injecting → Complete (text injection succeeded)
                (RecordingState.Injecting, RecordingState.Complete) => true,

                // Injecting → Error (text injection failed)
                (RecordingState.Injecting, RecordingState.Error) => true,

                // Terminal states → Idle (reset for next recording)
                (RecordingState.Complete, RecordingState.Idle) => true,
                (RecordingState.Cancelled, RecordingState.Idle) => true,
                (RecordingState.Error, RecordingState.Idle) => true,

                // All other transitions are invalid
                _ => false
            };
        }
    }

    /// <summary>
    /// Recording workflow states.
    ///
    /// NOTE: This is DIFFERENT from UI.VisualStateManager.RecordingState which only has 4 states (Ready/Recording/Processing/Error).
    /// This enum is for workflow coordination, not UI display.
    /// </summary>
    public enum RecordingState
    {
        /// <summary>
        /// Not recording, waiting for user input. Initial state.
        /// </summary>
        Idle,

        /// <summary>
        /// Audio recording in progress (microphone capturing audio).
        /// </summary>
        Recording,

        /// <summary>
        /// Recording stopped, finalizing audio file.
        /// Transition state between Recording and Transcribing.
        /// </summary>
        Stopping,

        /// <summary>
        /// Whisper AI transcription in progress.
        /// </summary>
        Transcribing,

        /// <summary>
        /// Injecting transcribed text into target application (clipboard or typing).
        /// </summary>
        Injecting,

        /// <summary>
        /// Workflow completed successfully (terminal state before returning to Idle).
        /// </summary>
        Complete,

        /// <summary>
        /// User cancelled the operation (terminal state before returning to Idle).
        /// </summary>
        Cancelled,

        /// <summary>
        /// Error occurred during workflow (terminal state before returning to Idle).
        /// </summary>
        Error
    }

    /// <summary>
    /// Event args for state machine transitions.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        public RecordingState FromState { get; }
        public RecordingState ToState { get; }
        public DateTime TransitionTime { get; }

        public StateChangedEventArgs(RecordingState fromState, RecordingState toState)
        {
            FromState = fromState;
            ToState = toState;
            TransitionTime = DateTime.Now;
        }
    }
}
