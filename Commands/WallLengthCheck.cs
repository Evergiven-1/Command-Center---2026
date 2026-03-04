using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class WallLengthCheck : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            try
            {
                // Check if we're in a valid view type
                if (activeView.ViewType == ViewType.DrawingSheet ||
                    activeView.ViewType == ViewType.DraftingView ||
                    activeView.ViewType == ViewType.Legend)
                {
                    TaskDialog.Show("Invalid View",
                        "Please open a plan, section, elevation, or RCP view.");
                    return Result.Failed;
                }

                // Collect all walls visible in the active view
                FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
                ICollection<Element> walls = collector.OfClass(typeof(Wall)).ToElements();

                if (walls.Count == 0)
                {
                    TaskDialog.Show("No Walls", "No walls found in this view.");
                    return Result.Succeeded;
                }

                // Find walls with fractional lengths smaller than 1/8"
                List<ElementId> wallsToHighlight = new List<ElementId>();
                double tolerance = 1.0 / 96.0; // 1/8" in feet (1/8 / 12)

                foreach (Element elem in walls)
                {
                    Wall wall = elem as Wall;
                    if (wall != null)
                    {
                        // Get wall length parameter
                        Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                        if (lengthParam != null)
                        {
                            double lengthInFeet = lengthParam.AsDouble();

                            // Check if the fractional remainder is less than 1/8"
                            if (HasFractionalRemainder(lengthInFeet, tolerance))
                            {
                                wallsToHighlight.Add(wall.Id);
                            }
                        }
                    }
                }

                // Apply visual override
                if (wallsToHighlight.Count > 0)
                {
                    using (Transaction trans = new Transaction(doc, "Highlight Non-Standard Walls"))
                    {
                        trans.Start();

                        // Create override settings (bright red/magenta)
                        OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
                        Color highlightColor = new Color(255, 0, 255); // Magenta
                        overrideSettings.SetProjectionLineColor(highlightColor);
                        overrideSettings.SetProjectionLineWeight(8);

                        // Apply override to each wall
                        foreach (ElementId wallId in wallsToHighlight)
                        {
                            activeView.SetElementOverrides(wallId, overrideSettings);
                        }

                        trans.Commit();
                    }

                    TaskDialog.Show("Walls Highlighted",
                        $"Highlighted {wallsToHighlight.Count} wall(s) with dimensions having fractional remainders less than 1/8\".\n\n" +
                        $"Total walls checked: {walls.Count}");
                }
                else
                {
                    TaskDialog.Show("No Issues Found",
                        $"All {walls.Count} walls have clean dimensions (1/8\" tolerance or greater).");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private bool HasFractionalRemainder(double lengthInFeet, double tolerance)
        {
            // Convert to inches for easier calculation
            double lengthInInches = lengthInFeet * 12.0;

            // Get the fractional part (remainder after whole inches)
            double fractionalInches = lengthInInches - Math.Floor(lengthInInches);

            // Check if there's a fractional remainder AND it's less than tolerance (1/8")
            if (fractionalInches > 0.0001) // Avoid floating point precision issues
            {
                // Round to nearest 1/8"
                double eighthsRemainder = fractionalInches % (1.0 / 8.0);

                // If the remainder when divided by 1/8" is significant, it's not a clean 1/8" increment
                if (eighthsRemainder > 0.0001 && eighthsRemainder < (1.0 / 8.0 - 0.0001))
                {
                    return true; // Has fractional remainder smaller than 1/8"
                }
            }

            return false;
        }
    }
}