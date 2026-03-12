#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ListUsedFillPatterns : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                // HashSet ensures unique pattern names only
                HashSet<string> usedPatternNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // --- 1. Collect from Filled Regions ---
                // FilledRegion elements reference a FilledRegionType which holds the pattern
                FilteredElementCollector filledRegionCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType));

                foreach (FilledRegionType regionType in filledRegionCollector)
                {

                    // Foreground fill pattern
                    AddPatternName(doc, regionType.ForegroundPatternId, usedPatternNames);

                    // Background fill pattern
                    AddPatternName(doc, regionType.BackgroundPatternId, usedPatternNames);
                }

                // --- 2. Collect from Materials ---
                FilteredElementCollector materialCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material));

                foreach (Material mat in materialCollector)
                {
                    // Surface foreground pattern
                    AddPatternName(doc, mat.SurfaceForegroundPatternId, usedPatternNames);

                    // Surface background pattern
                    AddPatternName(doc, mat.SurfaceBackgroundPatternId, usedPatternNames);

                    // Cut foreground pattern
                    AddPatternName(doc, mat.CutForegroundPatternId, usedPatternNames);

                    // Cut background pattern
                    AddPatternName(doc, mat.CutBackgroundPatternId, usedPatternNames);
                }

                // --- 3. Build output ---
                if (usedPatternNames.Count == 0)
                {
                    TaskDialog.Show("Fill Patterns In Use", "No fill patterns found in use.");
                    return Result.Succeeded;
                }

                List<string> sortedNames = usedPatternNames.OrderBy(n => n).ToList();
                string output = $"Fill Patterns In Use ({sortedNames.Count} unique):\n\n"
                                + string.Join("\n", sortedNames);

                TaskDialog dialog = new TaskDialog("Fill Patterns In Use");
                dialog.MainInstruction = $"{sortedNames.Count} unique fill pattern(s) found";
                dialog.MainContent = string.Join("\n", sortedNames);
                dialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Unexpected error: {ex.Message}";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Resolves a FillPattern ElementId to its name and adds it to the set.
        /// Skips invalid ids (no pattern assigned) and the "<Solid fill>" pattern.
        /// </summary>
        private void AddPatternName(Document doc, ElementId patternId, HashSet<string> names)
        {
            if (patternId == null || patternId == ElementId.InvalidElementId)
                return;

            Element patternElement = doc.GetElement(patternId);
            if (patternElement == null)
                return;

            FillPatternElement fillPatternElement = patternElement as FillPatternElement;
            if (fillPatternElement == null)
                return;

            string name = fillPatternElement.Name;
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }
}