using System;
using System.Windows.Input;

namespace VoiceLite.Presentation.Commands
{
    /// <summary>
    /// A command implementation that relays its functionality via delegates
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Creates a new command
        /// </summary>
        /// <param name="execute">The execution logic</param>
        /// <param name="canExecute">The execution status logic</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute();
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
    /// A command implementation that relays its functionality via delegates with a parameter
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Creates a new command with parameter support
        /// </summary>
        /// <param name="execute">The execution logic</param>
        /// <param name="canExecute">The execution status logic</param>
        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke((T?)parameter) ?? true;
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
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