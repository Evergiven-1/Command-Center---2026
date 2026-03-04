#region Namespaces
using System;
using IOPath = System.IO.Path;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;

#endregion

namespace CommandCenter
{
    /// <summary>
    /// Implements the Revit add-in interface IExternalApplication
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    internal class App : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            // Establish Common Parameters
            string masterPanel = "Command Center";
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            // Call Ribbon Panel Function
            RibbonPanel panelCommand = RibbonPanelGenerator(application, masterPanel, "Command Structures");// Ribbon Tab is First, Tab is Second, second corelates to text underneath pushbuttons

            string pgNameCommand01 = "UN Command";
            string pgNameCommand02 = "Whale Command";
            string pgComBLOCK_Command1 = "CommandCenter.ElementInfoCommand";
            string pgComBLOCK_Command2 = "CommandCenter.WhaleCall";

            /// Initialize Button
            if (panelCommand.AddItem(new PushButtonData(pgNameCommand01, pgNameCommand01, thisAssemblyPath, pgComBLOCK_Command1)) is PushButton commandButton1)
            {
                Uri uri = new Uri(@"C:\Users\aaliaj\OneDrive - Amenta Emma Architects\Desktop\00_CODING\Command-Center---2025\Resources\UN_favicon.ico");
                //BitmapImage bitmapImage = new BitmapImage(uri);
                commandButton1.ToolTip = "Strikegroup - Standing by"; // string played when cursor hangs over tab
                //commandButton1.LargeImage = bitmapImage;
            }
            // Initialize Button 2
            if (panelCommand.AddItem(new PushButtonData(pgNameCommand02, pgNameCommand02, thisAssemblyPath, pgComBLOCK_Command2)) is PushButton commandButton2)
            {
                Uri uri = new Uri(@"C:\Users\aaliaj\OneDrive - Amenta Emma Architects\Desktop\00_CODING\Command-Center---2025\Resources\Whaley.ico");
                //BitmapImage bitmapImage = new BitmapImage(uri);
                commandButton2.ToolTip = "Poopoo Peepee"; // string played when cursor hangs over tab
                //commandButton2.LargeImage = bitmapImage;
            }

            // close function
            return Result.Succeeded;
        }


        // Initialize Ribbon Panel
        // May not need Debug catches
        public RibbonPanel RibbonPanelGenerator(UIControlledApplication application, String tab, String panelName)
        {
            RibbonPanel ribbonPanel = null;

            try
            {
                application.CreateRibbonTab(tab);
            }
            catch (Exception ex)
            { Debug.WriteLine(ex.Message); }

            try
            {
                RibbonPanel panel = application.CreateRibbonPanel(tab, panelName);
            }
            catch (Exception ex)
            { Debug.WriteLine(ex.Message); }

            List<RibbonPanel> panels = application.GetRibbonPanels(tab);
            foreach (RibbonPanel rP in panels.Where(rP => rP.Name == panelName))
            { ribbonPanel = rP; }
            return ribbonPanel;
        }
    }
}
