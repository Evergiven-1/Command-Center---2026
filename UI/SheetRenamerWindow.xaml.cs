using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CommandCenter.UI
{
    public partial class SheetRenamerWindow : Window
    {
        public ObservableCollection<SheetData> Sheets { get; set; }
        public ObservableCollection<string> DisciplineOptions { get; set; }
        public bool WasApplied { get; private set; }

        // Sorting state tracking
        private string _currentSortColumn = "Number"; // Default sort
        private bool _isAscending = true;

        public SheetRenamerWindow(List<SheetData> sheets, List<string> existingDisciplines)
        {
            InitializeComponent();

            // Sort sheets alphabetically by Sheet Number (default)
            var sortedSheets = sheets.OrderBy(s => s.SheetNumber).ToList();
            Sheets = new ObservableCollection<SheetData>(sortedSheets);

            // Populate discipline dropdown options
            DisciplineOptions = new ObservableCollection<string>(
                existingDisciplines
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .OrderBy(d => d)
            );

            SheetItemsControl.ItemsSource = Sheets;
            WasApplied = false;

            // Set DataContext for binding
            this.DataContext = this;

            // Subscribe to property changes for duplicate detection AND change tracking
            foreach (var sheet in Sheets)
            {
                sheet.PropertyChanged += Sheet_PropertyChanged;
            }

            CheckForDuplicates();
            UpdateChangeCount();
            UpdateSortIndicators();
        }

        private void Sheet_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SheetNumber")
            {
                CheckForDuplicates();
            }

            if (e.PropertyName == "HasChanges")
            {
                UpdateChangeCount();
            }
        }

        private void CheckForDuplicates()
        {
            // Group by sheet number to find duplicates
            var duplicateGroups = Sheets
                .Where(s => !string.IsNullOrWhiteSpace(s.SheetNumber))
                .GroupBy(s => s.SheetNumber)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            // Reset all duplicate flags
            foreach (var sheet in Sheets)
            {
                sheet.HasDuplicate = false;
            }

            // Mark duplicates
            foreach (var sheet in duplicateGroups)
            {
                sheet.HasDuplicate = true;
            }

            // Update status message
            if (duplicateGroups.Any())
            {
                StatusTextBlock.Text = $"⚠ Warning: {duplicateGroups.Count} duplicate sheet number(s) detected!";
            }
            else
            {
                StatusTextBlock.Text = "";
            }
        }

        private void UpdateChangeCount()
        {
            int changeCount = Sheets.Count(s => s.HasChanges);

            if (changeCount > 0)
            {
                this.Title = $"Bulk Sheet Renamer ({changeCount} change{(changeCount == 1 ? "" : "s")})";
            }
            else
            {
                this.Title = "Bulk Sheet Renamer";
            }
        }

        private void UpdateSortIndicators()
        {
            // Clear all indicators
            var disciplineIndicator = FindVisualChild<TextBlock>(SortDisciplineButton, "DisciplineSortIndicator");
            var numberIndicator = FindVisualChild<TextBlock>(SortNumberButton, "NumberSortIndicator");
            var nameIndicator = FindVisualChild<TextBlock>(SortNameButton, "NameSortIndicator");

            if (disciplineIndicator != null) disciplineIndicator.Text = "";
            if (numberIndicator != null) numberIndicator.Text = "";
            if (nameIndicator != null) nameIndicator.Text = "";

            // Set the current sort indicator
            string indicator = _isAscending ? "▲" : "▼";

            switch (_currentSortColumn)
            {
                case "Discipline":
                    if (disciplineIndicator != null) disciplineIndicator.Text = indicator;
                    break;
                case "Number":
                    if (numberIndicator != null) numberIndicator.Text = indicator;
                    break;
                case "Name":
                    if (nameIndicator != null) nameIndicator.Text = indicator;
                    break;
            }
        }

        private void SortDisciplineButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle direction if clicking same column
            if (_currentSortColumn == "Discipline")
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _currentSortColumn = "Discipline";
                _isAscending = true;
            }

            ApplySort();
        }

        private void SortNumberButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle direction if clicking same column
            if (_currentSortColumn == "Number")
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _currentSortColumn = "Number";
                _isAscending = true;
            }

            ApplySort();
        }

        private void SortNameButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle direction if clicking same column
            if (_currentSortColumn == "Name")
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _currentSortColumn = "Name";
                _isAscending = true;
            }

            ApplySort();
        }

        private void ApplySort()
        {
            List<SheetData> sortedList;

            switch (_currentSortColumn)
            {
                case "Discipline":
                    sortedList = _isAscending
                        ? Sheets.OrderBy(s => s.SheetDiscipline).ToList()
                        : Sheets.OrderByDescending(s => s.SheetDiscipline).ToList();
                    break;
                case "Number":
                    sortedList = _isAscending
                        ? Sheets.OrderBy(s => s.SheetNumber).ToList()
                        : Sheets.OrderByDescending(s => s.SheetNumber).ToList();
                    break;
                case "Name":
                    sortedList = _isAscending
                        ? Sheets.OrderBy(s => s.SheetName).ToList()
                        : Sheets.OrderByDescending(s => s.SheetName).ToList();
                    break;
                default:
                    sortedList = Sheets.ToList();
                    break;
            }

            // Update the collection
            Sheets.Clear();
            foreach (var sheet in sortedList)
            {
                Sheets.Add(sheet);
            }

            UpdateSortIndicators();
        }

        // Helper method to find named elements in visual tree
        private T FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child != null && child is T && (child as FrameworkElement)?.Name == name)
                {
                    return (T)child;
                }

                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in Sheets)
            {
                sheet.IsSelected = false;
            }
        }

        private void ApplyBulkButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedSheets = Sheets.Where(s => s.IsSelected).ToList();

            if (!selectedSheets.Any())
            {
                MessageBox.Show("Please select at least one sheet to apply bulk edits.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Apply bulk updates only to non-empty fields
            foreach (var sheet in selectedSheets)
            {
                if (!string.IsNullOrWhiteSpace(BulkDisciplineCombo.Text))
                {
                    sheet.SheetDiscipline = BulkDisciplineCombo.Text;
                }

                if (!string.IsNullOrWhiteSpace(BulkSheetNumberText.Text))
                {
                    sheet.SheetNumber = BulkSheetNumberText.Text;
                }

                if (!string.IsNullOrWhiteSpace(BulkSheetNameText.Text))
                {
                    sheet.SheetName = BulkSheetNameText.Text;
                }
            }

            // Clear bulk edit fields
            BulkDisciplineCombo.Text = "";
            BulkSheetNumberText.Text = "";
            BulkSheetNameText.Text = "";

            CheckForDuplicates();

            MessageBox.Show($"Bulk edit applied to {selectedSheets.Count} sheet(s).",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Check for duplicates before applying
            var hasDuplicates = Sheets.Any(s => s.HasDuplicate);

            if (hasDuplicates)
            {
                var result = MessageBox.Show(
                    "There are duplicate sheet numbers. Do you want to continue anyway?",
                    "Duplicate Sheet Numbers",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            WasApplied = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            WasApplied = false;
            this.Close();
        }
    }

    // Data model for each sheet with INotifyPropertyChanged
    public class SheetData : INotifyPropertyChanged
    {
        private string _sheetDiscipline;
        private string _sheetNumber;
        private string _sheetName;
        private bool _isSelected;
        private bool _hasDuplicate;

        // Original values for comparison
        private string _originalSheetDiscipline;
        private string _originalSheetNumber;
        private string _originalSheetName;

        public string SheetDiscipline
        {
            get => _sheetDiscipline;
            set
            {
                _sheetDiscipline = value;
                OnPropertyChanged(nameof(SheetDiscipline));
                OnPropertyChanged(nameof(HasChanges));
            }
        }

        public string SheetNumber
        {
            get => _sheetNumber;
            set
            {
                _sheetNumber = value;
                OnPropertyChanged(nameof(SheetNumber));
                OnPropertyChanged(nameof(HasChanges));
            }
        }

        public string SheetName
        {
            get => _sheetName;
            set
            {
                _sheetName = value;
                OnPropertyChanged(nameof(SheetName));
                OnPropertyChanged(nameof(HasChanges));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public bool HasDuplicate
        {
            get => _hasDuplicate;
            set
            {
                _hasDuplicate = value;
                OnPropertyChanged(nameof(HasDuplicate));
            }
        }

        public string ElementId { get; set; }

        // Check if any values have changed
        public bool HasChanges
        {
            get
            {
                return _sheetDiscipline != _originalSheetDiscipline ||
                       _sheetNumber != _originalSheetNumber ||
                       _sheetName != _originalSheetName;
            }
        }

        // Initialize with original values
        public void SetOriginalValues(string discipline, string number, string name)
        {
            _originalSheetDiscipline = discipline;
            _originalSheetNumber = number;
            _originalSheetName = name;

            _sheetDiscipline = discipline;
            _sheetNumber = number;
            _sheetName = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}