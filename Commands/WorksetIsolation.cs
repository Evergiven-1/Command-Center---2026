#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using CommandCenter.UI;        // Add this for WorksetIsolationWindow
#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class WorksetIsolation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Check if document is workshared
                if (!doc.IsWorkshared)
                {
                    TaskDialog.Show("Not Workshared",
                        "This document is not workshared. Workset isolation requires a workshared model.");
                    return Result.Succeeded;
                }

                // Check if active view is valid for isolation
                View activeView = doc.ActiveView;

                if (activeView == null ||
                    activeView.ViewType == ViewType.Schedule ||
                    activeView.ViewType == ViewType.DrawingSheet ||
                    activeView.ViewType == ViewType.ProjectBrowser)
                {
                    TaskDialog.Show("Invalid View",
                        "Please activate a 3D view, plan, section, or elevation to use workset isolation.");
                    return Result.Succeeded;
                }

                // Collect all worksets in the document
                FilteredWorksetCollector worksetCollector = new FilteredWorksetCollector(doc);
                IList<Workset> worksets = worksetCollector
                    .OfKind(WorksetKind.UserWorkset)
                    .ToWorksets()
                    .OrderBy(w => w.Name)
                    .ToList();

                if (worksets.Count == 0)
                {
                    TaskDialog.Show("No Worksets", "No user worksets found in this project.");
                    return Result.Succeeded;
                }

                // Prepare workset data
                List<WorksetData> worksetDataList = new List<WorksetData>();
                foreach (Workset workset in worksets)
                {
                    WorksetData data = new WorksetData
                    {
                        WorksetId = workset.Id.IntegerValue,
                        WorksetName = workset.Name,
                        IsVisible = workset.IsVisibleByDefault
                    };
                    worksetDataList.Add(data);
                }

                // Store the original view state before any isolation
                ViewIsolationState originalState = CaptureViewState(doc, activeView);

                // Open the WPF window
                WorksetIsolationWindow window = new WorksetIsolationWindow(
                    doc,
                    uidoc,
                    worksetDataList,
                    originalState);

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Captures the current state of the view before isolation
        /// </summary>
        private ViewIsolationState CaptureViewState(Document doc, View view)
        {
            ViewIsolationState state = new ViewIsolationState
            {
                ViewId = view.Id,
                WasInTemporaryMode = view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate)
            };

            // If already in temporary mode, capture the isolated elements
            if (state.WasInTemporaryMode)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType();

                state.IsolatedElementIds = new List<ElementId>();
                foreach (Element elem in collector)
                {
                    if (!elem.IsHidden(view))
                    {
                        state.IsolatedElementIds.Add(elem.Id);
                    }
                }
            }

            return state;
        }
    }
}