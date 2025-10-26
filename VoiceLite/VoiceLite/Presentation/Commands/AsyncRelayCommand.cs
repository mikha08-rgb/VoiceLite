using System;
using System.Threading.Tasks;
using System.Windows.Input;
using VoiceLite.Helpers;

namespace VoiceLite.Presentation.Commands
{
    /// <summary>
    /// An async command implementation that relays its functionality via async delegates
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Creates a new async command
        /// </summary>
        /// <param name="execute">The async execution logic</param>
        /// <param name="canExecute">The execution status logic</param>
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        /// <summary>
        /// Executes the command asynchronously
        /// </summary>
        public async void Execute(object? parameter)
        {
            if (_isExecuting)
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                // Use AsyncHelper to safely handle exceptions in async void
                AsyncHelper.SafeFireAndForget(
                    Task.FromException(ex),
                    "AsyncRelayCommand execution failed");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// An async command implementation with parameter support
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Predicate<T?>? _canExecute;
        private bool _isExecuting;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Creates a new async command with parameter support
        /// </summary>
        /// <param name="execute">The async execution logic</param>
        /// <param name="canExecute">The execution status logic</param>
        public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);
        }

        /// <summary>
        /// Executes the command asynchronously
        /// </summary>
        public async void Execute(object? parameter)
        {
            if (_isExecuting)
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute((T?)parameter);
            }
            catch (Exception ex)
            {
                // Use AsyncHelper to safely handle exceptions in async void
                AsyncHelper.SafeFireAndForget(
                    Task.FromException(ex),
                    $"AsyncRelayCommand<{typeof(T).Name}> execution failed");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}