#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CommandCenter.UI;
#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class BulkViewEditor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Collect all views (excluding templates, schedules, and sheets)
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> allViews = collector.OfClass(typeof(View)).ToElements();

                // Filter to only include valid views (not templates, not schedules, not sheets)
                List<View> validViews = new List<View>();
                foreach (Element elem in allViews)
                {
                    View view = elem as View;
                    if (view != null && 
                        !view.IsTemplate && 
                        view.ViewType != ViewType.Schedule &&
                        view.ViewType != ViewType.DrawingSheet &&
                        view.ViewType != ViewType.ProjectBrowser &&
                        view.ViewType != ViewType.SystemBrowser &&
                        view.ViewType != ViewType.Undefined &&
                        view.ViewType != ViewType.Internal)
                    {
                        validViews.Add(view);
                    }
                }

                if (validViews.Count == 0)
                {
                    TaskDialog.Show("No Views", "No editable views found in this project.");
                    return Result.Succeeded;
                }

                // Collect all view templates for dropdown
                List<View> viewTemplates = new List<View>();
                foreach (Element elem in allViews)
                {
                    View view = elem as View;
                    if (view != null && view.IsTemplate)
                    {
                        viewTemplates.Add(view);
                    }
                }

                // Prepare view data
                List<ViewData> viewDataList = new List<ViewData>();
                List<string> existingDisciplines = new List<string>();

                foreach (View view in validViews)
                {
                    // Get discipline as string
                    string discipline = GetViewDiscipline(view);
                    if (!string.IsNullOrWhiteSpace(discipline))
                    {
                        existingDisciplines.Add(discipline);
                    }

                    // Get view type as string
                    string viewType = view.ViewType.ToString();

                    // Get scale (handle views that don't support scale)
                    int scale = 0;
                    try
                    {
                        scale = view.Scale;
                    }
                    catch
                    {
                        scale = 0; // View doesn't support scale
                    }

                    // Get view template name
                    string templateName = "";
                    ElementId templateId = view.ViewTemplateId;
                    if (templateId != null && templateId != ElementId.InvalidElementId)
                    {
                        View template = doc.GetElement(templateId) as View;
                        if (template != null)
                        {
                            templateName = template.Name;
                        }
                    }

                    ViewData viewData = new ViewData();
                    viewData.SetOriginalValues(
                        discipline,
                        viewType,
                        scale,
                        view.Name,
                        templateName,
                        templateId
                    );
                    viewData.ElementId = view.Id.ToString();
                    viewData.CanEditScale = CanViewHaveScale(view);

                    viewDataList.Add(viewData);
                }

                // Build template dictionary for the window
                Dictionary<string, ElementId> templateDict = new Dictionary<string, ElementId>();
                templateDict.Add("<None>", ElementId.InvalidElementId);
                foreach (View template in viewTemplates.OrderBy(t => t.Name))
                {
                    if (!templateDict.ContainsKey(template.Name))
                    {
                        templateDict.Add(template.Name, template.Id);
                    }
                }

                // Open the WPF window
                ViewEditorWindow window = new ViewEditorWindow(viewDataList, existingDisciplines, templateDict);
                window.ShowDialog();

                // If user clicked Apply, update only the changed views
                if (window.WasApplied)
                {
                    // Filter to only views that have changes
                    var changedViews = window.Views.Where(v => v.HasChanges).ToList();

                    if (changedViews.Count == 0)
                    {
                        TaskDialog.Show("No Changes", "No views were modified.");
                        return Result.Succeeded;
                    }

                    using (Transaction trans = new Transaction(doc, "Bulk Edit Views"))
                    {
                        trans.Start();

                        int successCount = 0;
                        List<string> errors = new List<string>();

                        foreach (ViewData viewData in changedViews)
                        {
                            try
                            {
                                ElementId id = new ElementId(long.Parse(viewData.ElementId));
                                View view = doc.GetElement(id) as View;

                                if (view != null)
                                {
                                    // Update Scale if changed and view supports it
                                    if (viewData.ScaleChanged && viewData.CanEditScale && viewData.ViewScale > 0)
                                    {
                                        try
                                        {
                                            view.Scale = viewData.ViewScale;
                                        }
                                        catch (Exception ex)
                                        {
                                            errors.Add($"View {viewData.ViewName}: Could not set scale - {ex.Message}");
                                        }
                                    }

                                    // Update Name if changed
                                    if (viewData.NameChanged)
                                    {
                                        try
                                        {
                                            view.Name = viewData.ViewName;
                                        }
                                        catch (Exception ex)
                                        {
                                            errors.Add($"View {viewData.ViewName}: Could not rename - {ex.Message}");
                                        }
                                    }

                                    // Update View Template if changed
                                    if (viewData.TemplateChanged)
                                    {
                                        try
                                        {
                                            view.ViewTemplateId = viewData.ViewTemplateId;
                                        }
                                        catch (Exception ex)
                                        {
                                            errors.Add($"View {viewData.ViewName}: Could not set template - {ex.Message}");
                                        }
                                    }

                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"View {viewData.ViewName}: {ex.Message}");
                            }
                        }

                        trans.Commit();

                        // Show detailed results
                        string resultMessage = $"Successfully updated {successCount} of {changedViews.Count} view(s).";

                        if (errors.Any())
                        {
                            resultMessage += $"\n\nErrors:\n" + string.Join("\n", errors.Take(5));
                            if (errors.Count > 5)
                            {
                                resultMessage += $"\n... and {errors.Count - 5} more error(s)";
                            }
                        }

                        TaskDialog.Show("Update Complete", resultMessage);
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

        private string GetViewDiscipline(View view)
        {
            try
            {
                ViewDiscipline discipline = view.Discipline;
                return discipline.ToString();
            }
            catch
            {
                return "";
            }
        }

        private bool CanViewHaveScale(View view)
        {
            // Check if the view type supports scale changes
            try
            {
                int testScale = view.Scale;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
