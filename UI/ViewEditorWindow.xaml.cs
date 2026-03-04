using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace CommandCenter.UI
{
    public partial class ViewEditorWindow : Window
    {
        public ObservableCollection<ViewData> Views { get; set; }
        public ObservableCollection<string> DisciplineOptions { get; set; }
        public ObservableCollection<string> TemplateOptions { get; set; }
        public Dictionary<string, ElementId> TemplateDict { get; set; }
        public bool WasApplied { get; private set; }

        // Sorting state tracking
        private string _currentSortColumn = "Name"; // Default sort
        private bool _isAscending = true;

        public ViewEditorWindow(List<ViewData> views, List<string> existingDisciplines, Dictionary<string, ElementId> templateDict)
        {
            InitializeComponent();

            // Store template dictionary
            TemplateDict = templateDict;

            // Sort views alphabetically by Name (default)
            var sortedViews = views.OrderBy(v => v.ViewName).ToList();
            Views = new ObservableCollection<ViewData>(sortedViews);

            // Populate discipline dropdown options
            DisciplineOptions = new ObservableCollection<string>(
                existingDisciplines
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .OrderBy(d => d)
            );

            // Populate template dropdown options
            TemplateOptions = new ObservableCollection<string>(templateDict.Keys.OrderBy(k => k == "<None>" ? "" : k));

            ViewItemsControl.ItemsSource = Views;
            WasApplied = false;

            // Set DataContext for binding
            this.DataContext = this;

            // Subscribe to property changes for duplicate detection AND change tracking
            foreach (var view in Views)
            {
                view.TemplateDict = TemplateDict;
                view.PropertyChanged += View_PropertyChanged;
            }

            CheckForDuplicates();
            UpdateChangeCount();
            UpdateSortIndicators();
        }

        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ViewName")
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
            // Group by view name to find duplicates
            var duplicateGroups = Views
                .Where(v => !string.IsNullOrWhiteSpace(v.ViewName))
                .GroupBy(v => v.ViewName)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            // Reset all duplicate flags
            foreach (var view in Views)
            {
                view.HasDuplicate = false;
            }

            // Mark duplicates
            foreach (var view in duplicateGroups)
            {
                view.HasDuplicate = true;
            }

            // Update status message
            if (duplicateGroups.Any())
            {
                StatusTextBlock.Text = $"⚠ Warning: {duplicateGroups.Count} duplicate view name(s) detected!";
            }
            else
            {
                StatusTextBlock.Text = "";
            }
        }

        private void UpdateChangeCount()
        {
            int changeCount = Views.Count(v => v.HasChanges);

            if (changeCount > 0)
            {
                this.Title = $"Bulk View Editor ({changeCount} change{(changeCount == 1 ? "" : "s")})";
            }
            else
            {
                this.Title = "Bulk View Editor";
            }
        }

        private void UpdateSortIndicators()
        {
            // Clear all indicators
            var disciplineIndicator = FindVisualChild<TextBlock>(SortDisciplineButton, "DisciplineSortIndicator");
            var typeIndicator = FindVisualChild<TextBlock>(SortTypeButton, "TypeSortIndicator");
            var scaleIndicator = FindVisualChild<TextBlock>(SortScaleButton, "ScaleSortIndicator");
            var nameIndicator = FindVisualChild<TextBlock>(SortNameButton, "NameSortIndicator");
            var templateIndicator = FindVisualChild<TextBlock>(SortTemplateButton, "TemplateSortIndicator");

            if (disciplineIndicator != null) disciplineIndicator.Text = "";
            if (typeIndicator != null) typeIndicator.Text = "";
            if (scaleIndicator != null) scaleIndicator.Text = "";
            if (nameIndicator != null) nameIndicator.Text = "";
            if (templateIndicator != null) templateIndicator.Text = "";

            // Set the current sort indicator
            string indicator = _isAscending ? "▲" : "▼";

            switch (_currentSortColumn)
            {
                case "Discipline":
                    if (disciplineIndicator != null) disciplineIndicator.Text = indicator;
                    break;
                case "Type":
                    if (typeIndicator != null) typeIndicator.Text = indicator;
                    break;
                case "Scale":
                    if (scaleIndicator != null) scaleIndicator.Text = indicator;
                    break;
                case "Name":
                    if (nameIndicator != null) nameIndicator.Text = indicator;
                    break;
                case "Template":
                    if (templateIndicator != null) templateIndicator.Text = indicator;
                    break;
            }
        }

        private void SortDisciplineButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void SortTypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSortColumn == "Type")
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _currentSortColumn = "Type";
                _isAscending = true;
            }
            ApplySort();
        }

        private void SortScaleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSortColumn == "Scale")
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _currentSortColumn = "Scale";
                _isAscending = true;
            }
            ApplySort();
        }

        private void SortNameButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void SortTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSortColumn == "Template")
            {
                _isAscending = !_isAscending;
            }
            else
            {
                _currentSortColumn = "Template";
                _isAscending = true;
            }
            ApplySort();
        }

        private void ApplySort()
        {
            List<ViewData> sortedList;

            switch (_currentSortColumn)
            {
                case "Discipline":
                    sortedList = _isAscending
                        ? Views.OrderBy(v => v.ViewDiscipline).ToList()
                        : Views.OrderByDescending(v => v.ViewDiscipline).ToList();
                    break;
                case "Type":
                    sortedList = _isAscending
                        ? Views.OrderBy(v => v.ViewType).ToList()
                        : Views.OrderByDescending(v => v.ViewType).ToList();
                    break;
                case "Scale":
                    sortedList = _isAscending
                        ? Views.OrderBy(v => v.ViewScale).ToList()
                        : Views.OrderByDescending(v => v.ViewScale).ToList();
                    break;
                case "Name":
                    sortedList = _isAscending
                        ? Views.OrderBy(v => v.ViewName).ToList()
                        : Views.OrderByDescending(v => v.ViewName).ToList();
                    break;
                case "Template":
                    sortedList = _isAscending
                        ? Views.OrderBy(v => v.ViewTemplateName).ToList()
                        : Views.OrderByDescending(v => v.ViewTemplateName).ToList();
                    break;
                default:
                    sortedList = Views.ToList();
                    break;
            }

            // Update the collection
            Views.Clear();
            foreach (var view in sortedList)
            {
                Views.Add(view);
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
            foreach (var view in Views)
            {
                view.IsSelected = false;
            }
        }

        private void ApplyBulkButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedViews = Views.Where(v => v.IsSelected).ToList();

            if (!selectedViews.Any())
            {
                MessageBox.Show("Please select at least one view to apply bulk edits.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Apply bulk updates only to non-empty fields
            foreach (var view in selectedViews)
            {
                // Scale
                if (!string.IsNullOrWhiteSpace(BulkScaleText.Text))
                {
                    if (int.TryParse(BulkScaleText.Text, out int scale) && scale > 0 && view.CanEditScale)
                    {
                        view.ViewScale = scale;
                    }
                }

                // Name
                if (!string.IsNullOrWhiteSpace(BulkNameText.Text))
                {
                    view.ViewName = BulkNameText.Text;
                }

                // Template
                if (BulkTemplateCombo.SelectedItem != null)
                {
                    string templateName = BulkTemplateCombo.SelectedItem.ToString();
                    view.ViewTemplateName = templateName;
                }
            }

            // Clear bulk edit fields
            BulkScaleText.Text = "";
            BulkNameText.Text = "";
            BulkTemplateCombo.SelectedItem = null;

            CheckForDuplicates();

            MessageBox.Show($"Bulk edit applied to {selectedViews.Count} view(s).",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Check for duplicates before applying
            var hasDuplicates = Views.Any(v => v.HasDuplicate);

            if (hasDuplicates)
            {
                var result = MessageBox.Show(
                    "There are duplicate view names. Do you want to continue anyway?",
                    "Duplicate View Names",
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

    // Data model for each view with INotifyPropertyChanged
    public class ViewData : INotifyPropertyChanged
    {
        private string _viewDiscipline;
        private string _viewType;
        private int _viewScale;
        private string _viewName;
        private string _viewTemplateName;
        private ElementId _viewTemplateId;
        private bool _isSelected;
        private bool _hasDuplicate;
        private bool _canEditScale;

        // Original values for comparison
        private string _originalViewDiscipline;
        private string _originalViewType;
        private int _originalViewScale;
        private string _originalViewName;
        private string _originalViewTemplateName;
        private ElementId _originalViewTemplateId;

        // Reference to template dictionary
        public Dictionary<string, ElementId> TemplateDict { get; set; }

        public string ViewDiscipline
        {
            get => _viewDiscipline;
            set
            {
                _viewDiscipline = value;
                OnPropertyChanged(nameof(ViewDiscipline));
                OnPropertyChanged(nameof(HasChanges));
            }
        }

        public string ViewType
        {
            get => _viewType;
            set
            {
                _viewType = value;
                OnPropertyChanged(nameof(ViewType));
            }
        }

        public int ViewScale
        {
            get => _viewScale;
            set
            {
                _viewScale = value;
                OnPropertyChanged(nameof(ViewScale));
                OnPropertyChanged(nameof(ViewScaleDisplay));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(ScaleChanged));
            }
        }

        public string ViewScaleDisplay
        {
            get => _viewScale > 0 ? _viewScale.ToString() : "N/A";
            set
            {
                if (int.TryParse(value, out int scale) && scale > 0)
                {
                    ViewScale = scale;
                }
            }
        }

        public string ViewName
        {
            get => _viewName;
            set
            {
                _viewName = value;
                OnPropertyChanged(nameof(ViewName));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(NameChanged));
            }
        }

        public string ViewTemplateName
        {
            get => _viewTemplateName;
            set
            {
                _viewTemplateName = value;
                // Update the ElementId based on the template name
                if (TemplateDict != null && TemplateDict.ContainsKey(value))
                {
                    _viewTemplateId = TemplateDict[value];
                }
                else if (value == "<None>" || System.String.IsNullOrEmpty(value))
                    
                OnPropertyChanged(nameof(ViewTemplateName));
                OnPropertyChanged(nameof(ViewTemplateId));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(TemplateChanged));
            }
        }

        public ElementId ViewTemplateId
        {
            get => _viewTemplateId;
            set
            {
                _viewTemplateId = value;
                OnPropertyChanged(nameof(ViewTemplateId));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(TemplateChanged));
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

        public bool CanEditScale
        {
            get => _canEditScale;
            set
            {
                _canEditScale = value;
                OnPropertyChanged(nameof(CanEditScale));
            }
        }

        public string ElementId { get; set; }

        // Check if any values have changed
        public bool HasChanges
        {
            get
            {
                return ScaleChanged || NameChanged || TemplateChanged;
            }
        }

        public bool ScaleChanged => _viewScale != _originalViewScale;
        public bool NameChanged => _viewName != _originalViewName;
        public bool TemplateChanged => _viewTemplateId != _originalViewTemplateId;

        // Initialize with original values
        public void SetOriginalValues(string discipline, string type, int scale, string name, string templateName, ElementId templateId)
        {
            _originalViewDiscipline = discipline;
            _originalViewType = type;
            _originalViewScale = scale;
            _originalViewName = name;
            _originalViewTemplateName = templateName;
            _originalViewTemplateId = templateId;

            _viewDiscipline = discipline;
            _viewType = type;
            _viewScale = scale;
            _viewName = name;
            _viewTemplateName = string.IsNullOrEmpty(templateName) ? "<None>" : templateName;
            _viewTemplateId = templateId;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
