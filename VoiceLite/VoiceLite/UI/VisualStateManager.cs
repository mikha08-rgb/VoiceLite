using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VoiceLite.UI
{
    /// <summary>
    /// Manages visual states and animations for the VoiceLite UI
    /// Provides smooth transitions and visual feedback for different recording states
    /// </summary>
    public class VisualStateManager
    {
        private readonly Border _statusIndicator;
        private readonly TextBlock _statusText;
        private readonly FrameworkElement _mainContainer;
        private Storyboard? _currentAnimation;

        public VisualStateManager(Border statusIndicator, TextBlock statusText, FrameworkElement mainContainer)
        {
            _statusIndicator = statusIndicator ?? throw new ArgumentNullException(nameof(statusIndicator));
            _statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
            _mainContainer = mainContainer ?? throw new ArgumentNullException(nameof(mainContainer));
        }

        /// <summary>
        /// Updates the visual state for recording status
        /// </summary>
        public void UpdateRecordingState(RecordingState state, string statusMessage = "")
        {
            _currentAnimation?.Stop();

            switch (state)
            {
                case RecordingState.Ready:
                    SetReadyState(statusMessage);
                    break;
                case RecordingState.Recording:
                    SetRecordingState(statusMessage);
                    break;
                case RecordingState.Processing:
                    SetProcessingState(statusMessage);
                    break;
                case RecordingState.Error:
                    SetErrorState(statusMessage);
                    break;
            }
        }

        private void SetReadyState(string message)
        {
            var readyBrush = Application.Current.Resources["ReadyBrush"] as SolidColorBrush
                            ?? new SolidColorBrush(Colors.Green);

            AnimateColorChange(_statusIndicator, "Background.Color", readyBrush.Color, TimeSpan.FromMilliseconds(300));
            AnimateColorChange(_statusText, "Foreground.Color", readyBrush.Color, TimeSpan.FromMilliseconds(300));

            _statusText.Text = !string.IsNullOrEmpty(message) ? message : "Ready";
        }

        private void SetRecordingState(string message)
        {
            var recordingBrush = Application.Current.Resources["RecordingBrush"] as SolidColorBrush
                               ?? new SolidColorBrush(Colors.Red);

            AnimateColorChange(_statusIndicator, "Background.Color", recordingBrush.Color, TimeSpan.FromMilliseconds(200));
            AnimateColorChange(_statusText, "Foreground.Color", recordingBrush.Color, TimeSpan.FromMilliseconds(200));

            _statusText.Text = !string.IsNullOrEmpty(message) ? message : "Recording...";

            // Start subtle pulse animation for recording indicator
            StartPulseAnimation();
        }

        private void SetProcessingState(string message)
        {
            var processingBrush = Application.Current.Resources["ProcessingBrush"] as SolidColorBrush
                                ?? new SolidColorBrush(Colors.Orange);

            AnimateColorChange(_statusIndicator, "Background.Color", processingBrush.Color, TimeSpan.FromMilliseconds(200));
            AnimateColorChange(_statusText, "Foreground.Color", processingBrush.Color, TimeSpan.FromMilliseconds(200));

            _statusText.Text = !string.IsNullOrEmpty(message) ? message : "Processing...";

            // Start subtle rotation animation for processing
            StartProcessingAnimation();
        }

        private void SetErrorState(string message)
        {
            var errorBrush = Application.Current.Resources["RecordingBrush"] as SolidColorBrush
                           ?? new SolidColorBrush(Colors.Red);

            AnimateColorChange(_statusIndicator, "Background.Color", errorBrush.Color, TimeSpan.FromMilliseconds(200));
            AnimateColorChange(_statusText, "Foreground.Color", errorBrush.Color, TimeSpan.FromMilliseconds(200));

            _statusText.Text = !string.IsNullOrEmpty(message) ? message : "Error";

            // Brief shake animation for error
            StartShakeAnimation();
        }

        private void AnimateColorChange(FrameworkElement element, string propertyPath, Color targetColor, TimeSpan duration)
        {
            var animation = new ColorAnimation
            {
                To = targetColor,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath(propertyPath));

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void StartPulseAnimation()
        {
            _currentAnimation = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            var pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.6,
                Duration = TimeSpan.FromSeconds(1),
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(pulseAnimation, _statusIndicator);
            Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath("Opacity"));

            _currentAnimation.Children.Add(pulseAnimation);
            _currentAnimation.Begin();
        }

        private void StartProcessingAnimation()
        {
            _currentAnimation = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            var scaleXAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1,
                Duration = TimeSpan.FromSeconds(0.8),
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            var scaleYAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1,
                Duration = TimeSpan.FromSeconds(0.8),
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            // Ensure transform exists
            if (_statusIndicator.RenderTransform == null || _statusIndicator.RenderTransform == Transform.Identity)
            {
                _statusIndicator.RenderTransform = new ScaleTransform();
                _statusIndicator.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            Storyboard.SetTarget(scaleXAnimation, _statusIndicator);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));

            Storyboard.SetTarget(scaleYAnimation, _statusIndicator);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));

            _currentAnimation.Children.Add(scaleXAnimation);
            _currentAnimation.Children.Add(scaleYAnimation);
            _currentAnimation.Begin();
        }

        private void StartShakeAnimation()
        {
            _currentAnimation = new Storyboard();

            var shakeAnimation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(400)
            };

            shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
            shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));

            // Ensure transform exists
            if (_statusIndicator.RenderTransform == null || _statusIndicator.RenderTransform == Transform.Identity)
            {
                _statusIndicator.RenderTransform = new TranslateTransform();
            }

            Storyboard.SetTarget(shakeAnimation, _statusIndicator);
            Storyboard.SetTargetProperty(shakeAnimation, new PropertyPath("RenderTransform.X"));

            _currentAnimation.Children.Add(shakeAnimation);
            _currentAnimation.Begin();
        }

        /// <summary>
        /// Animates content changes with a fade effect
        /// </summary>
        public void AnimateContentChange(FrameworkElement element, Action contentUpdateAction)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeIn = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                contentUpdateAction?.Invoke();

                Storyboard.SetTarget(fadeIn, element);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

                var fadeInStoryboard = new Storyboard();
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
            };

            Storyboard.SetTarget(fadeOut, element);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

            var fadeOutStoryboard = new Storyboard();
            fadeOutStoryboard.Children.Add(fadeOut);
            fadeOutStoryboard.Begin();
        }

        /// <summary>
        /// Stops all current animations
        /// </summary>
        public void StopAllAnimations()
        {
            _currentAnimation?.Stop();
            _currentAnimation = null;
        }
    }

    public enum RecordingState
    {
        Ready,
        Recording,
        Processing,
        Error
    }
}