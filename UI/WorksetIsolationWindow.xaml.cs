using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CommandCenter.UI
{
    public partial class WorksetIsolationWindow : Window
    {
        private Document _doc;
        private UIDocument _uidoc;
        private ViewIsolationState _originalState;
        private bool _hasIsolated = false;

        public ObservableCollection<WorksetData> Worksets { get; set; }

        public WorksetIsolationWindow(
            Document doc,
            UIDocument uidoc,
            List<WorksetData> worksets,
            ViewIsolationState originalState)
        {
            InitializeComponent();

            _doc = doc;
            _uidoc = uidoc;
            _originalState = originalState;

            Worksets = new ObservableCollection<WorksetData>(worksets);
            this.DataContext = this;
        }

        private void IsolateButton_Click(object sender, RoutedEventArgs e)
        {
            WorksetData selectedWorkset = WorksetComboBox.SelectedItem as WorksetData;

            if (selectedWorkset == null)
            {
                MessageBox.Show("Please select a workset first.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                View activeView = _doc.ActiveView;

                // Collect all elements in the active view that belong to the selected workset
                FilteredElementCollector collector = new FilteredElementCollector(_doc, activeView.Id)
                    .WhereElementIsNotElementType();

                List<ElementId> elementsToIsolate = new List<ElementId>();

                foreach (Element elem in collector)
                {
                    if (elem.WorksetId.IntegerValue == selectedWorkset.WorksetId)
                    {
                        elementsToIsolate.Add(elem.Id);
                    }
                }

                if (elementsToIsolate.Count == 0)
                {
                    MessageBox.Show($"No elements found in workset '{selectedWorkset.WorksetName}' in this view.",
                        "No Elements", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Apply isolation
                using (Transaction trans = new Transaction(_doc, "Isolate Workset"))
                {
                    trans.Start();

                    // If already in temporary mode, disable it first
                    if (activeView.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                    {
                        activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    }

                    // Isolate the elements
                    activeView.IsolateElementsTemporary(elementsToIsolate);

                    trans.Commit();
                }

                // Update UI
                _hasIsolated = true;
                ResetButton.IsEnabled = true;
                KeepButton.IsEnabled = true;

                StatusTextBlock.Text = $"Isolated workset: {selectedWorkset.WorksetName}";
                ElementCountTextBlock.Text = $"{elementsToIsolate.Count} element(s) isolated";
                ElementCountTextBlock.Visibility = System.Windows.Visibility.Visible;

                // Refresh the view
                _uidoc.RefreshActiveView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error isolating workset: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                View activeView = _doc.ActiveView;

                using (Transaction trans = new Transaction(_doc, "Reset Isolation"))
                {
                    trans.Start();

                    if (activeView.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                    {
                        activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    }

                    trans.Commit();
                }

                // Update UI
                _hasIsolated = false;
                ResetButton.IsEnabled = false;
                KeepButton.IsEnabled = false;

                StatusTextBlock.Text = "Isolation reset. Select another workset to isolate.";
                ElementCountTextBlock.Visibility = System.Windows.Visibility.Collapsed;

                _uidoc.RefreshActiveView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting isolation: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KeepButton_Click(object sender, RoutedEventArgs e)
        {
            // Keep the current isolation and close
            this.Close();
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                View activeView = _doc.ActiveView;

                using (Transaction trans = new Transaction(_doc, "Restore View State"))
                {
                    trans.Start();

                    // Disable temporary mode
                    if (activeView.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                    {
                        activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    }

                    // Restore original isolation if it existed
                    if (_originalState.WasInTemporaryMode &&
                        _originalState.IsolatedElementIds != null &&
                        _originalState.IsolatedElementIds.Count > 0)
                    {
                        activeView.IsolateElementsTemporary(_originalState.IsolatedElementIds);
                    }

                    trans.Commit();
                }

                _uidoc.RefreshActiveView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring view state: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            this.Close();
        }
    }

    /// <summary>
    /// Data model for workset information
    /// </summary>
    public class WorksetData
    {
        public int WorksetId { get; set; }
        public string WorksetName { get; set; }
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// Stores the original view state for restoration
    /// </summary>
    public class ViewIsolationState
    {
        public ElementId ViewId { get; set; }
        public bool WasInTemporaryMode { get; set; }
        public List<ElementId> IsolatedElementIds { get; set; }
    }
}