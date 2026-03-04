#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ColorOnCommandv2 : IExternalCommand
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

                View activeView = doc.ActiveView;

                // Persistent selection loop
                while (true)
                {
                    try
                    {
                        // Let user pick elements
                        IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            "Select elements to make red (press ESC to finish)");

                        // Apply red override to selected elements
                        using (Transaction trans = new Transaction(doc, "Override Elements Red"))
                        {
                            trans.Start();

                            foreach (Reference refElement in selectedRefs)
                            {
                                activeView.SetElementOverrides(refElement.ElementId, redOverride);
                            }

                            trans.Commit();
                        }

                        // Loop continues - user can select more elements
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // User pressed ESC - exit gracefully
                        break;
                    }
                }

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