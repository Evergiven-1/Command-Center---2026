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

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ColorCLEAR : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get the UIApplication and UIDocument
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            try
            {
                // Get the active view
                View activeView = doc.ActiveView;

                // Check if the active view supports graphical overrides
                if (!activeView.AreGraphicsOverridesAllowed())
                {
                    TaskDialog.Show("Error", "The active view does not support graphical overrides.");
                    return Result.Failed;
                }

                // Collect all elements in the active view
                FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
                ICollection<Element> elementsInView = collector.WhereElementIsNotElementType().ToElements();

                int clearedCount = 0;

                // Start a transaction to clear overrides
                using (Transaction trans = new Transaction(doc, "Clear All Graphical Overrides"))
                {
                    trans.Start();

                    try
                    {
                        // Create an empty OverrideGraphicSettings to clear overrides
                        OverrideGraphicSettings clearOverrides = new OverrideGraphicSettings();

                        // Iterate through all elements and clear their overrides
                        foreach (Element element in elementsInView)
                        {
                            try
                            {
                                // Check if the element has any overrides before clearing
                                OverrideGraphicSettings currentOverrides = activeView.GetElementOverrides(element.Id);

                                // Only clear if there are actually overrides (optimization)
                                if (HasOverrides(currentOverrides))
                                {
                                    activeView.SetElementOverrides(element.Id, clearOverrides);
                                    clearedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log individual element failures but continue processing others
                                Debug.WriteLine($"Failed to clear overrides for element {element.Id}: {ex.Message}");
                            }
                        }

                        // Also clear category overrides
                        ClearCategoryOverrides(activeView, doc);

                        trans.Commit();

                        // Show success message
                        TaskDialog.Show("Success",
                            $"Cleared graphical overrides from {clearedCount} elements in the active view.\n" +
                            $"Category overrides have also been cleared.");

                        return Result.Succeeded;
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        message = $"Error during transaction: {ex.Message}";
                        return Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                message = $"Unexpected error: {ex.Message}";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Checks if an OverrideGraphicSettings object has any overrides applied
        /// </summary>
        /// <param name="overrides">The OverrideGraphicSettings to check</param>
        /// <returns>True if overrides exist, false otherwise</returns>
        private bool HasOverrides(OverrideGraphicSettings overrides)
        {
            // Check various override properties to see if any are set
            try
            {
                // Check if projection line color is overridden
                if (overrides.ProjectionLineColor.IsValid)
                    return true;

                // Check if cut line color is overridden
                if (overrides.CutLineColor.IsValid)
                    return true;

                // Check if surface foreground pattern color is overridden
                if (overrides.SurfaceForegroundPatternColor.IsValid)
                    return true;

                // Check if surface background pattern color is overridden
                if (overrides.SurfaceBackgroundPatternColor.IsValid)
                    return true;

                // Check if cut foreground pattern color is overridden
                if (overrides.CutForegroundPatternColor.IsValid)
                    return true;

                // Check if cut background pattern color is overridden
                if (overrides.CutBackgroundPatternColor.IsValid)
                    return true;

                // Check if projection line weight is overridden
                if (overrides.ProjectionLineWeight != -1)
                    return true;

                // Check if cut line weight is overridden
                if (overrides.CutLineWeight != -1)
                    return true;

                // Check if transparency is overridden
                if (overrides.Transparency != 0)
                    return true;

                // Check if halftone is applied
                if (overrides.Halftone)
                    return true;

                // Check if surface patterns are overridden
                if (overrides.SurfaceForegroundPatternId != ElementId.InvalidElementId)
                    return true;

                if (overrides.SurfaceBackgroundPatternId != ElementId.InvalidElementId)
                    return true;

                // Check if cut patterns are overridden
                if (overrides.CutForegroundPatternId != ElementId.InvalidElementId)
                    return true;

                if (overrides.CutBackgroundPatternId != ElementId.InvalidElementId)
                    return true;

                return false;
            }
            catch
            {
                // If we can't determine, assume there are overrides to be safe
                return true;
            }
        }

        /// <summary>
        /// Clears category overrides from the view
        /// </summary>
        /// <param name="view">The view to clear category overrides from</param>
        /// <param name="doc">The document</param>
        private void ClearCategoryOverrides(View view, Document doc)
        {
            try
            {
                // Get all categories in the document
                Categories categories = doc.Settings.Categories;
                OverrideGraphicSettings clearOverrides = new OverrideGraphicSettings();

                foreach (Category category in categories)
                {
                    try
                    {
                        if (category != null && category.Id != null)
                        {
                            // Check if the category has overrides in this view
                            OverrideGraphicSettings categoryOverrides = view.GetCategoryOverrides(category.Id);

                            if (HasOverrides(categoryOverrides))
                            {
                                // Clear the category overrides
                                view.SetCategoryOverrides(category.Id, clearOverrides);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log category override clearing failures but continue
                        Debug.WriteLine($"Failed to clear category overrides for {category?.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing category overrides: {ex.Message}");
            }
        }
    }
}