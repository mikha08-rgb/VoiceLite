using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VoiceLite.Models;
using VoiceLite.Presentation.Commands;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for transcription history display and management.
    /// Minimal extraction - MainWindow still handles UI rendering (CreateHistoryCard, etc.)
    /// </summary>
    public class HistoryViewModel : ViewModelBase
    {
        #region Fields

        private string _searchText = "";
        private bool _isSearchVisible;
        private TranscriptionHistoryItem? _selectedItem;
        private ObservableCollection<TranscriptionHistoryItem> _displayedItems;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the search text for filtering history
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(HasSearchText));
                    // Notify MainWindow to apply filter
                    SearchTextChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Gets whether search text is not empty
        /// </summary>
        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        /// <summary>
        /// Gets or sets whether search box is visible
        /// </summary>
        public bool IsSearchVisible
        {
            get => _isSearchVisible;
            set => SetProperty(ref _isSearchVisible, value);
        }

        /// <summary>
        /// Gets or sets the currently selected history item
        /// </summary>
        public TranscriptionHistoryItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets the collection of displayed history items (after filtering)
        /// </summary>
        public ObservableCollection<TranscriptionHistoryItem> DisplayedItems
        {
            get => _displayedItems;
            set => SetProperty(ref _displayedItems, value);
        }

        /// <summary>
        /// Gets whether history has any items
        /// </summary>
        public bool HasHistory => DisplayedItems?.Count > 0;

        /// <summary>
        /// Gets whether history is empty
        /// </summary>
        public bool IsHistoryEmpty => DisplayedItems?.Count == 0;

        #endregion

        #region Commands

        public ICommand ClearHistoryCommand { get; }
        public ICommand ClearAllHistoryCommand { get; }
        public ICommand CopyToClipboardCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand ReInjectCommand { get; }
        public ICommand ToggleSearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when user requests to clear history
        /// </summary>
        public event EventHandler? ClearHistoryRequested;

        /// <summary>
        /// Raised when user requests to clear all history (including pinned)
        /// </summary>
        public event EventHandler? ClearAllHistoryRequested;

        /// <summary>
        /// Raised when user requests to copy item to clipboard
        /// </summary>
        public event EventHandler<TranscriptionHistoryItem>? CopyToClipboardRequested;

        /// <summary>
        /// Raised when user requests to delete an item
        /// </summary>
        public event EventHandler<TranscriptionHistoryItem>? DeleteItemRequested;

        /// <summary>
        /// Raised when user requests to re-inject an item
        /// </summary>
        public event EventHandler<TranscriptionHistoryItem>? ReInjectRequested;

        /// <summary>
        /// Raised when search text changes
        /// </summary>
        public event EventHandler<string>? SearchTextChanged;

        #endregion

        #region Constructor

        public HistoryViewModel()
        {
            _displayedItems = new ObservableCollection<TranscriptionHistoryItem>();

            // Initialize commands
            ClearHistoryCommand = new RelayCommand(ExecuteClearHistory, () => HasHistory);
            ClearAllHistoryCommand = new RelayCommand(ExecuteClearAllHistory, () => HasHistory);
            CopyToClipboardCommand = new RelayCommand<TranscriptionHistoryItem>(ExecuteCopyToClipboard);
            DeleteItemCommand = new RelayCommand<TranscriptionHistoryItem>(ExecuteDeleteItem);
            ReInjectCommand = new RelayCommand<TranscriptionHistoryItem>(ExecuteReInject);
            ToggleSearchCommand = new RelayCommand(ExecuteToggleSearch);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the displayed items collection
        /// Call this from MainWindow when history changes
        /// </summary>
        public void UpdateDisplayedItems(ObservableCollection<TranscriptionHistoryItem> items)
        {
            DisplayedItems = items;
            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(IsHistoryEmpty));
            UpdateCommandStates();
        }

        #endregion

        #region Command Implementations

        private void ExecuteClearHistory()
        {
            ClearHistoryRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteClearAllHistory()
        {
            ClearAllHistoryRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteCopyToClipboard(TranscriptionHistoryItem? item)
        {
            if (item != null)
            {
                CopyToClipboardRequested?.Invoke(this, item);
            }
        }

        private void ExecuteDeleteItem(TranscriptionHistoryItem? item)
        {
            if (item != null)
            {
                DeleteItemRequested?.Invoke(this, item);
            }
        }

        private void ExecuteReInject(TranscriptionHistoryItem? item)
        {
            if (item != null)
            {
                ReInjectRequested?.Invoke(this, item);
            }
        }

        private void ExecuteToggleSearch()
        {
            IsSearchVisible = !IsSearchVisible;
            if (!IsSearchVisible)
            {
                SearchText = ""; // Clear search when hiding
            }
        }

        private void ExecuteClearSearch()
        {
            SearchText = "";
        }

        private void UpdateCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region Disposal

        protected override void DisposeCore()
        {
            DisplayedItems?.Clear();
            base.DisposeCore();
        }

        #endregion
    }
}
