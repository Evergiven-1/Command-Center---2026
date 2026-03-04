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

            // Use typeof() instead of GetExecutingAssembly() - more reliable in Revit
            string assemblyLocation = typeof(WhaleCall).Assembly.Location;
            string envPath = @"C:\Users\aaliaj\Desktop\00_CODING\Command-Center---2026\bin\Debug\net8.0-windows\.env";
            
            // Show us the path BEFORE trying to load
            TaskDialog debugDialog = new TaskDialog("Debug - Path Check");
            debugDialog.MainInstruction = "Looking for .env here:";
            debugDialog.MainContent = envPath;
            debugDialog.Show();

            try
            {
                DotNetEnv.Env.Load(envPath);

                string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

                TaskDialog dialog = new TaskDialog("API Key Check");
                dialog.MainInstruction = "Environment Variable Test";

                if (string.IsNullOrEmpty(apiKey))
                {
                    dialog.MainContent = "API key not found! Check your .env file location.";
                }
                else
                {
                    dialog.MainContent = $"API key loaded!\n\nKey preview: {apiKey.Substring(0, 6)}...";
                }

                dialog.Show();

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