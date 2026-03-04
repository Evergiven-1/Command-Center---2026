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
    public class BulkSheetRenamer : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Collect all sheets
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> sheets = collector.OfClass(typeof(ViewSheet)).ToElements();

                if (sheets.Count == 0)
                {
                    TaskDialog.Show("No Sheets", "No sheets found in this project.");
                    return Result.Succeeded;
                }

                // Prepare sheet data and collect existing disciplines
                List<SheetData> sheetDataList = new List<SheetData>();
                List<string> existingDisciplines = new List<string>();

                foreach (Element elem in sheets)
                {
                    ViewSheet sheet = elem as ViewSheet;
                    if (sheet != null)
                    {
                        // Get the Sheet Discipline # parameter
                        string sheetDiscipline = "";
                        Parameter disciplineParam = sheet.LookupParameter("Sheet Discipline #");
                        if (disciplineParam != null)
                        {
                            sheetDiscipline = disciplineParam.AsString() ?? "";
                            if (!string.IsNullOrWhiteSpace(sheetDiscipline))
                            {
                                existingDisciplines.Add(sheetDiscipline);
                            }
                        }

                        SheetData sheetData = new SheetData();
                        sheetData.SetOriginalValues(
                            sheetDiscipline,
                            sheet.SheetNumber,
                            sheet.Name
                        );
                        sheetData.ElementId = sheet.Id.ToString();

                        sheetDataList.Add(sheetData);
                    }
                }

                // Open the WPF window with existing disciplines for dropdown
                SheetRenamerWindow window = new SheetRenamerWindow(sheetDataList, existingDisciplines);
                window.ShowDialog();

                // If user clicked Apply, update only the changed sheets
                if (window.WasApplied)
                {
                    // Filter to only sheets that have changes
                    var changedSheets = window.Sheets.Where(s => s.HasChanges).ToList();

                    if (changedSheets.Count == 0)
                    {
                        TaskDialog.Show("No Changes", "No sheets were modified.");
                        return Result.Succeeded;
                    }

                    using (Transaction trans = new Transaction(doc, "Bulk Rename Sheets"))
                    {
                        trans.Start();

                        int successCount = 0;
                        List<string> errors = new List<string>();

                        foreach (SheetData sheetData in changedSheets)
                        {
                            try
                            {
                                ElementId id = new ElementId(long.Parse(sheetData.ElementId));
                                ViewSheet sheet = doc.GetElement(id) as ViewSheet;

                                if (sheet != null)
                                {
                                    // Update Sheet Discipline # if changed
                                    Parameter disciplineParam = sheet.LookupParameter("Sheet Discipline #");
                                    if (disciplineParam != null && !disciplineParam.IsReadOnly)
                                    {
                                        disciplineParam.Set(sheetData.SheetDiscipline);
                                    }

                                    // Update Sheet Number and Name
                                    sheet.SheetNumber = sheetData.SheetNumber;
                                    sheet.Name = sheetData.SheetName;

                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Sheet {sheetData.SheetNumber}: {ex.Message}");
                            }
                        }

                        trans.Commit();

                        // Show detailed results
                        string resultMessage = $"Successfully updated {successCount} of {changedSheets.Count} sheet(s).";

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
    }
}
