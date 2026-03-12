#region Namespaces
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ListActiveFillPatterns : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                // --- 1. Collect ALL fill patterns loaded in the project ---
                List<string> allDrafting = new List<string>();
                List<string> allModel = new List<string>();

                FilteredElementCollector allPatternsCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                foreach (FillPatternElement fpe in allPatternsCollector)
                {
                    FillPattern pattern = fpe.GetFillPattern();
                    if (pattern == null) continue;

                    if (pattern.Target == FillPatternTarget.Drafting)
                        allDrafting.Add(fpe.Name);
                    else if (pattern.Target == FillPatternTarget.Model)
                        allModel.Add(fpe.Name);
                }

                allDrafting.Sort(StringComparer.OrdinalIgnoreCase);
                allModel.Sort(StringComparer.OrdinalIgnoreCase);

                // --- 2. Collect patterns IN USE (Filled Region Types + Materials) ---
                HashSet<string> usedPatternNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // From all FilledRegionTypes (loaded, not just placed)
                FilteredElementCollector regionTypeCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType));

                foreach (FilledRegionType regionType in regionTypeCollector)
                {
                    AddPatternName(doc, regionType.ForegroundPatternId, usedPatternNames);
                    AddPatternName(doc, regionType.BackgroundPatternId, usedPatternNames);
                }

                // From all Materials
                FilteredElementCollector materialCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material));

                foreach (Material mat in materialCollector)
                {
                    AddPatternName(doc, mat.SurfaceForegroundPatternId, usedPatternNames);
                    AddPatternName(doc, mat.SurfaceBackgroundPatternId, usedPatternNames);
                    AddPatternName(doc, mat.CutForegroundPatternId, usedPatternNames);
                    AddPatternName(doc, mat.CutBackgroundPatternId, usedPatternNames);
                }

                // --- 3. Split into used / unused sub-lists ---
                List<string> draftingUsed = allDrafting.Where(n => usedPatternNames.Contains(n)).ToList();
                List<string> draftingUnused = allDrafting.Where(n => !usedPatternNames.Contains(n)).ToList();
                List<string> modelUsed = allModel.Where(n => usedPatternNames.Contains(n)).ToList();
                List<string> modelUnused = allModel.Where(n => !usedPatternNames.Contains(n)).ToList();

                // --- 4. Build output ---
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                // Drafting Patterns
                sb.AppendLine($"── DRAFTING PATTERNS ({allDrafting.Count} loaded) ──");
                sb.AppendLine($"  IN USE ({draftingUsed.Count}):");
                foreach (string name in draftingUsed)
                    sb.AppendLine($"    {name}");

                sb.AppendLine($"  NOT IN USE ({draftingUnused.Count}):");
                foreach (string name in draftingUnused)
                    sb.AppendLine($"    {name}");

                sb.AppendLine();

                // Model Patterns
                sb.AppendLine($"── MODEL PATTERNS ({allModel.Count} loaded) ──");
                sb.AppendLine($"  IN USE ({modelUsed.Count}):");
                foreach (string name in modelUsed)
                    sb.AppendLine($"    {name}");

                sb.AppendLine($"  NOT IN USE ({modelUnused.Count}):");
                foreach (string name in modelUnused)
                    sb.AppendLine($"    {name}");

                sb.AppendLine();

                // Summary
                int totalLoaded = allDrafting.Count + allModel.Count;
                int totalUsed = draftingUsed.Count + modelUsed.Count;
                int totalUnused = draftingUnused.Count + modelUnused.Count;

                sb.AppendLine($"── SUMMARY ──");
                sb.AppendLine($"  Total loaded:  {totalLoaded}");
                sb.AppendLine($"  In use:        {totalUsed}");
                sb.AppendLine($"  Not in use:    {totalUnused}");

                TaskDialog dialog = new TaskDialog("Fill Pattern Audit");
                dialog.MainInstruction = $"{totalLoaded} patterns loaded | {totalUsed} in use | {totalUnused} unused";
                dialog.MainContent = sb.ToString();
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

        private void AddPatternName(Document doc, ElementId patternId, HashSet<string> names)
        {
            if (patternId == null || patternId == ElementId.InvalidElementId)
                return;

            FillPatternElement fpe = doc.GetElement(patternId) as FillPatternElement;
            if (fpe == null) return;

            string name = fpe.Name;
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }
}