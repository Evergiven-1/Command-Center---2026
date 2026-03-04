#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class WhaleCall : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // URL for a website about whales
                string url = "https://www.worldwildlife.org/species/whale";

                // Create a TaskDialog
                TaskDialog dialog = new TaskDialog("Whale Information");
                dialog.MainContent = "Do you want to visit the World Wildlife Fund website to learn about whales?";
                dialog.MainInstruction = "Open Whale Website";
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Yes, open the website");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "No, cancel");
                dialog.CommonButtons = TaskDialogCommonButtons.None;

                // Show the dialog and process the result
                TaskDialogResult result = dialog.Show();

                if (result == TaskDialogResult.CommandLink1)
                {
                    // Modern way to open a URL in the default browser
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User canceled the operation
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}