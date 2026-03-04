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
using System.Text.Json;
using System.Collections.Generic;
using System.IO;

#endregion
// Note from claude regarding the transaction mode -
// ReadOnly - because it only shows a dialog and opens a website
// Manual - works fine too, just doesn't use any transactions

//The Add-in Manager sometimes has preferences about which mode to expect, which is why Manual often works better for avoiding those error messages.
//Bottom line: When in doubt, use Manual - it's more flexible and avoids compatibility issues!


namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ElementInfoCommand : IExternalCommand
    {
        private string ClaudeApiKey;
        private const string ClaudeAPIUrl = "https://api.anthropic.com/v1/messages";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Load the config HERE, inside the method
            try
            {
                string configText = File.ReadAllText("secrets.json");
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(configText);
                ClaudeApiKey = config["CLAUDE_API_KEY"];
            }

            catch (Exception ex)
            {
                message = "Failed to load API key: " + ex.Message;
                return Result.Failed;
            }

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            try
            {
                // Prompt user to select an element
                Reference pickedRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    "Please select an element");
                // Get the element
                Element elem = doc.GetElement(pickedRef);
                // Get element name
                string elemName = elem.Name;
                if (string.IsNullOrEmpty(elemName))
                {
                    elemName = elem.Id.ToString() + " (No name)";
                }

                // Get element category name
                string categoryName = elem.Category?.Name ?? "Unknown Category";

                // Get element type information
                string typeName = "Unknown Type";
                if (elem is FamilyInstance familyInstance)
                {
                    FamilySymbol symbol = familyInstance.Symbol;
                    if (symbol != null)
                    {
                        typeName = symbol.Family.Name + " - " + symbol.Name;
                    }
                }
                else if (elem.GetType().Name != null)
                {
                    typeName = elem.GetType().Name;
                }

                // Call Claude API to get element description and usage suggestions
                string elementInfo = "Loading information...";
                TaskDialog progressDialog = new TaskDialog("Getting Info")
                {
                    MainInstruction = "Retrieving information about this element...",
                    MainContent = "Please wait while we fetch details about: " + elemName,
                    CommonButtons = TaskDialogCommonButtons.None,
                    AllowCancellation = true
                };

                // Use Task.Run to make the API call asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        elementInfo = await GetClaudeInfoAsync(elemName, categoryName, typeName);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        elementInfo = "Error retrieving information: " + ex.Message;
                        return false;
                    }
                }).Wait();

                // Show element info in a dialog
                TaskDialog infoDialog = new TaskDialog("Element Information");
                infoDialog.MainInstruction = "Selected Element: " + elemName;
                infoDialog.MainContent = elementInfo;
                infoDialog.Show();

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

        private async Task<string> GetClaudeInfoAsync(string elementName, string categoryName, string typeName)
        {
            using (HttpClient client = new HttpClient())
            {
                // Set up headers
                client.DefaultRequestHeaders.Add("x-api-key", ClaudeApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                // Create the message request
                var requestBody = new
                {
                    model = "claude-3-haiku-20240307",
                    max_tokens = 300,
                    messages = new[]
                    {
                    new
                    {
                        role = "user",
                        content = $"I'm working in Revit and have selected a '{elementName}' element. It has category '{categoryName}' and type '{typeName}'. Please provide a brief description (2-3 sentences) of what this element represents in building design, followed by a very short suggestion (1-2 sentences) on how it's typically used in BIM workflows."
                    }
                }
                };

                // Serialize and send request
                string jsonRequest = JsonConvert.SerializeObject(requestBody);
                StringContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(ClaudeAPIUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response to extract Claude's message
                    dynamic responseObject = JsonConvert.DeserializeObject(jsonResponse);
                    return responseObject.content[0].text;
                }
                else
                {
                    return $"Error retrieving information: {response.StatusCode} - {jsonResponse}";
                }
            }
        }
    }
}