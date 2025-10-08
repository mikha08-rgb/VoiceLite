using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for RecordingStateMachine - Week 1, Day 3-4 implementation.
    /// Validates state transitions, thread safety, and error recovery.
    /// </summary>
    public class RecordingStateMachineTests
    {
        [Fact]
        public void Constructor_InitializesToIdleState()
        {
            // Arrange & Act
            var stateMachine = new RecordingStateMachine();

            // Assert
            stateMachine.CurrentState.Should().Be(RecordingState.Idle);
            stateMachine.PreviousState.Should().Be(RecordingState.Idle);
            stateMachine.TimeInCurrentState.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void TryTransition_ValidTransition_ReturnsTrue()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Act
            bool result = stateMachine.TryTransition(RecordingState.Recording);

            // Assert
            result.Should().BeTrue();
            stateMachine.CurrentState.Should().Be(RecordingState.Recording);
            stateMachine.PreviousState.Should().Be(RecordingState.Idle);
        }

        [Fact]
        public void TryTransition_InvalidTransition_ReturnsFalse()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Act - Try to jump directly from Idle to Transcribing (invalid!)
            bool result = stateMachine.TryTransition(RecordingState.Transcribing);

            // Assert
            result.Should().BeFalse();
            stateMachine.CurrentState.Should().Be(RecordingState.Idle, "state should not change on invalid transition");
            stateMachine.PreviousState.Should().Be(RecordingState.Idle);
        }

        [Fact]
        public void TryTransition_NormalWorkflow_Succeeds()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Act - Execute normal workflow
            var transitions = new[]
            {
                RecordingState.Recording,
                RecordingState.Stopping,
                RecordingState.Transcribing,
                RecordingState.Injecting,
                RecordingState.Complete,
                RecordingState.Idle
            };

            // Assert - All transitions should succeed
            foreach (var targetState in transitions)
            {
                bool result = stateMachine.TryTransition(targetState);
                result.Should().BeTrue($"transition to {targetState} should be valid");
                stateMachine.CurrentState.Should().Be(targetState);
            }

            stateMachine.CurrentState.Should().Be(RecordingState.Idle, "workflow should end in Idle state");
        }

        [Fact]
        public void TryTransition_CancelDuringRecording_Succeeds()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            stateMachine.TryTransition(RecordingState.Recording);

            // Act - Cancel during recording
            bool cancelResult = stateMachine.TryTransition(RecordingState.Cancelled);
            bool idleResult = stateMachine.TryTransition(RecordingState.Idle);

            // Assert
            cancelResult.Should().BeTrue();
            idleResult.Should().BeTrue();
            stateMachine.CurrentState.Should().Be(RecordingState.Idle);
        }

        [Fact]
        public void TryTransition_ErrorDuringTranscription_Succeeds()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            stateMachine.TryTransition(RecordingState.Recording);
            stateMachine.TryTransition(RecordingState.Stopping);
            stateMachine.TryTransition(RecordingState.Transcribing);

            // Act - Error during transcription
            bool errorResult = stateMachine.TryTransition(RecordingState.Error);
            bool idleResult = stateMachine.TryTransition(RecordingState.Idle);

            // Assert
            errorResult.Should().BeTrue();
            idleResult.Should().BeTrue();
            stateMachine.CurrentState.Should().Be(RecordingState.Idle);
        }

        [Fact]
        public void TryTransition_ErrorDuringInjection_Succeeds()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            stateMachine.TryTransition(RecordingState.Recording);
            stateMachine.TryTransition(RecordingState.Stopping);
            stateMachine.TryTransition(RecordingState.Transcribing);
            stateMachine.TryTransition(RecordingState.Injecting);

            // Act - Error during injection
            bool errorResult = stateMachine.TryTransition(RecordingState.Error);
            bool idleResult = stateMachine.TryTransition(RecordingState.Idle);

            // Assert
            errorResult.Should().BeTrue();
            idleResult.Should().BeTrue();
            stateMachine.CurrentState.Should().Be(RecordingState.Idle);
        }

        [Fact]
        public void TryTransition_CancelDuringStopping_Succeeds()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            stateMachine.TryTransition(RecordingState.Recording);
            stateMachine.TryTransition(RecordingState.Stopping);

            // Act - Cancel during stopping
            bool cancelResult = stateMachine.TryTransition(RecordingState.Cancelled);
            bool idleResult = stateMachine.TryTransition(RecordingState.Idle);

            // Assert
            cancelResult.Should().BeTrue();
            idleResult.Should().BeTrue();
        }

        [Fact]
        public void TryTransition_CancelDuringTranscribing_Succeeds()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            stateMachine.TryTransition(RecordingState.Recording);
            stateMachine.TryTransition(RecordingState.Stopping);
            stateMachine.TryTransition(RecordingState.Transcribing);

            // Act - Cancel during transcription
            bool cancelResult = stateMachine.TryTransition(RecordingState.Cancelled);
            bool idleResult = stateMachine.TryTransition(RecordingState.Idle);

            // Assert
            cancelResult.Should().BeTrue();
            idleResult.Should().BeTrue();
        }

        [Fact]
        public void CanTransitionTo_ValidTransition_ReturnsTrue()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Act
            bool canTransition = stateMachine.CanTransitionTo(RecordingState.Recording);

            // Assert
            canTransition.Should().BeTrue();
            stateMachine.CurrentState.Should().Be(RecordingState.Idle, "CanTransitionTo should not modify state");
        }

        [Fact]
        public void CanTransitionTo_InvalidTransition_ReturnsFalse()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Act
            bool canTransition = stateMachine.CanTransitionTo(RecordingState.Injecting);

            // Assert
            canTransition.Should().BeFalse();
            stateMachine.CurrentState.Should().Be(RecordingState.Idle, "CanTransitionTo should not modify state");
        }

        [Fact]
        public void Reset_FromAnyState_ReturnsToIdle()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            stateMachine.TryTransition(RecordingState.Recording);
            stateMachine.TryTransition(RecordingState.Stopping);
            stateMachine.TryTransition(RecordingState.Transcribing);

            // Act
            stateMachine.Reset();

            // Assert
            stateMachine.CurrentState.Should().Be(RecordingState.Idle);
            stateMachine.PreviousState.Should().Be(RecordingState.Transcribing);
        }

        [Fact]
        public void StateChanged_ValidTransition_FiresEvent()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            RecordingState? capturedFromState = null;
            RecordingState? capturedToState = null;
            var eventFired = new ManualResetEventSlim(false);

            stateMachine.StateChanged += (sender, args) =>
            {
                capturedFromState = args.FromState;
                capturedToState = args.ToState;
                eventFired.Set();
            };

            // Act
            stateMachine.TryTransition(RecordingState.Recording);

            // Assert
            bool signaled = eventFired.Wait(TimeSpan.FromSeconds(1));
            signaled.Should().BeTrue("StateChanged event should fire");
            capturedFromState.Should().Be(RecordingState.Idle);
            capturedToState.Should().Be(RecordingState.Recording);
        }

        [Fact]
        public void StateChanged_InvalidTransition_DoesNotFireEvent()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            bool eventFired = false;

            stateMachine.StateChanged += (sender, args) =>
            {
                eventFired = true;
            };

            // Act - Invalid transition
            stateMachine.TryTransition(RecordingState.Transcribing);
            Thread.Sleep(100); // Give event time to fire if it would

            // Assert
            eventFired.Should().BeFalse("StateChanged should not fire on invalid transition");
        }

        [Fact]
        public void TimeInCurrentState_TracksCorrectly()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Act
            Thread.Sleep(50);
            var elapsed = stateMachine.TimeInCurrentState;

            // Assert
            elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(40));
            elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        public async Task ThreadSafety_ConcurrentTransitions_NoCorruption()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            var errors = new List<Exception>();
            var completedCount = 0;

            // Act - 100 concurrent attempts to transition
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                try
                {
                    // Each thread tries to execute the full workflow
                    if (stateMachine.TryTransition(RecordingState.Recording))
                    {
                        stateMachine.TryTransition(RecordingState.Stopping);
                        stateMachine.TryTransition(RecordingState.Transcribing);
                        stateMachine.TryTransition(RecordingState.Injecting);
                        stateMachine.TryTransition(RecordingState.Complete);
                        stateMachine.TryTransition(RecordingState.Idle);
                        Interlocked.Increment(ref completedCount);
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
            })).ToArray();

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            errors.Should().BeEmpty("no exceptions should occur during concurrent transitions");
            completedCount.Should().BeGreaterThan(0, "at least one thread should complete workflow");
            completedCount.Should().BeLessOrEqualTo(100, "sanity check");

            // State machine should be in valid state (either Idle or somewhere in a partial workflow)
            var finalState = stateMachine.CurrentState;
            finalState.Should().BeOneOf(
                RecordingState.Idle,
                RecordingState.Recording,
                RecordingState.Stopping,
                RecordingState.Transcribing,
                RecordingState.Injecting,
                RecordingState.Complete,
                RecordingState.Cancelled,
                RecordingState.Error
            );
        }

        [Fact]
        public async Task ThreadSafety_ConcurrentReadsAndWrites_NoDeadlock()
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var exceptions = new List<Exception>();

            // Act - Readers and writers running concurrently
            var readerTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var state = stateMachine.CurrentState;
                        var prevState = stateMachine.PreviousState;
                        var time = stateMachine.TimeInCurrentState;
                        var canTransition = stateMachine.CanTransitionTo(RecordingState.Recording);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            });

            var writerTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        stateMachine.TryTransition(RecordingState.Recording);
                        stateMachine.TryTransition(RecordingState.Stopping);
                        stateMachine.TryTransition(RecordingState.Transcribing);
                        stateMachine.TryTransition(RecordingState.Injecting);
                        stateMachine.TryTransition(RecordingState.Complete);
                        stateMachine.TryTransition(RecordingState.Idle);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            });

            await Task.WhenAll(readerTask, writerTask).WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exceptions.Should().BeEmpty("no deadlocks or exceptions should occur");
        }

        [Theory]
        [InlineData(RecordingState.Idle, RecordingState.Recording, true)]
        [InlineData(RecordingState.Idle, RecordingState.Transcribing, false)]
        [InlineData(RecordingState.Recording, RecordingState.Stopping, true)]
        [InlineData(RecordingState.Recording, RecordingState.Injecting, false)]
        [InlineData(RecordingState.Stopping, RecordingState.Transcribing, true)]
        [InlineData(RecordingState.Transcribing, RecordingState.Injecting, true)]
        [InlineData(RecordingState.Transcribing, RecordingState.Error, true)]
        [InlineData(RecordingState.Injecting, RecordingState.Complete, true)]
        [InlineData(RecordingState.Complete, RecordingState.Idle, true)]
        [InlineData(RecordingState.Error, RecordingState.Idle, true)]
        [InlineData(RecordingState.Cancelled, RecordingState.Idle, true)]
        public void TransitionValidation_VariousPaths_ExpectedResults(
            RecordingState from,
            RecordingState to,
            bool expectedValid)
        {
            // Arrange
            var stateMachine = new RecordingStateMachine();

            // Navigate to 'from' state via valid path
            NavigateToState(stateMachine, from);

            // Act
            bool result = stateMachine.TryTransition(to);

            // Assert
            result.Should().Be(expectedValid, $"transition {from} → {to} should be {(expectedValid ? "valid" : "invalid")}");
        }

        /// <summary>
        /// Helper method to navigate to a specific state via a valid path.
        /// </summary>
        private void NavigateToState(RecordingStateMachine stateMachine, RecordingState targetState)
        {
            switch (targetState)
            {
                case RecordingState.Idle:
                    // Already in Idle
                    break;
                case RecordingState.Recording:
                    stateMachine.TryTransition(RecordingState.Recording);
                    break;
                case RecordingState.Stopping:
                    stateMachine.TryTransition(RecordingState.Recording);
                    stateMachine.TryTransition(RecordingState.Stopping);
                    break;
                case RecordingState.Transcribing:
                    stateMachine.TryTransition(RecordingState.Recording);
                    stateMachine.TryTransition(RecordingState.Stopping);
                    stateMachine.TryTransition(RecordingState.Transcribing);
                    break;
                case RecordingState.Injecting:
                    stateMachine.TryTransition(RecordingState.Recording);
                    stateMachine.TryTransition(RecordingState.Stopping);
                    stateMachine.TryTransition(RecordingState.Transcribing);
                    stateMachine.TryTransition(RecordingState.Injecting);
                    break;
                case RecordingState.Complete:
                    stateMachine.TryTransition(RecordingState.Recording);
                    stateMachine.TryTransition(RecordingState.Stopping);
                    stateMachine.TryTransition(RecordingState.Transcribing);
                    stateMachine.TryTransition(RecordingState.Injecting);
                    stateMachine.TryTransition(RecordingState.Complete);
                    break;
                case RecordingState.Error:
                    stateMachine.TryTransition(RecordingState.Recording);
                    stateMachine.TryTransition(RecordingState.Stopping);
                    stateMachine.TryTransition(RecordingState.Transcribing);
                    stateMachine.TryTransition(RecordingState.Error);
                    break;
                case RecordingState.Cancelled:
                    stateMachine.TryTransition(RecordingState.Recording);
                    stateMachine.TryTransition(RecordingState.Cancelled);
                    break;
            }
        }
    }
}
