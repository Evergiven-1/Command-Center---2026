#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion
// Note from claude regarding the transaction mode -
// ReadOnly - because it only shows a dialog and opens a website
// Manual - works fine too, just doesn't use any transactions

//The Add-in Manager sometimes has preferences about which mode to expect, which is why Manual often works better for avoiding those error messages.
//Bottom line: When in doubt, use Manual - it's more flexible and avoids compatibility issues!

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ColorOnCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get the application and document references
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            try
            {
                // Get current selection
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

                // Check if anything is selected
                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("No Selection", "Please select at least one element before running this command.");
                    return Result.Cancelled;
                }

                // Find solid fill pattern
                FillPatternElement solidPattern = null;
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> fillPatterns = collector.OfClass(typeof(FillPatternElement)).ToElements();

                foreach (FillPatternElement fp in fillPatterns.Cast<FillPatternElement>())
                {
                    if (fp.GetFillPattern().IsSolidFill)
                    {
                        solidPattern = fp;
                        break;
                    }
                }

                ElementId solidPatternId = solidPattern?.Id ?? ElementId.InvalidElementId;

                // Create red color
                Color redColor = new Color(255, 0, 0);

                // Create override settings for red
                OverrideGraphicSettings redOverride = new OverrideGraphicSettings();

                redOverride.SetSurfaceForegroundPatternId(solidPatternId);
                redOverride.SetCutForegroundPatternId(solidPatternId);

                redOverride.SetProjectionLineColor(redColor);
                redOverride.SetCutLineColor(redColor);

                // For fill colors, use these methods:
                redOverride.SetSurfaceForegroundPatternColor(redColor);
                redOverride.SetSurfaceBackgroundPatternColor(redColor);
                redOverride.SetCutForegroundPatternColor(redColor);
                redOverride.SetCutBackgroundPatternColor(redColor);

                // Apply overrides in a transaction
                using (Transaction trans = new Transaction(doc, "Override Elements Red"))
                {
                    trans.Start();

                    View activeView = doc.ActiveView;

                    foreach (ElementId elementId in selectedIds)
                    {
                        activeView.SetElementOverrides(elementId, redOverride);
                    }

                    trans.Commit();
                }

                // Show success message
                TaskDialog.Show("Success", $"Applied red override to {selectedIds.Count} element(s).");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
