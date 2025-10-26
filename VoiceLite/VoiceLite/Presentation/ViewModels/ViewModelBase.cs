using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing INotifyPropertyChanged implementation
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        private bool _isDisposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if the value changed
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises PropertyChanged for multiple properties
        /// </summary>
        protected void RaisePropertyChanged(params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                DisposeCore();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Override this to dispose of managed resources
        /// </summary>
        protected virtual void DisposeCore()
        {
            // Override in derived classes to dispose managed resources
        }
    }
}