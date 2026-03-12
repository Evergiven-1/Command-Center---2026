#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using WpfColor = System.Windows.Media.Color;
using WpfGrid = System.Windows.Controls.Grid;
using WpfTextBox = System.Windows.Controls.TextBox;
#endregion

namespace CommandCenter.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FillPatternAudit : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
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

                HashSet<string> usedPatternNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (FilledRegionType regionType in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)))
                {
                    AddPatternName(doc, regionType.ForegroundPatternId, usedPatternNames);
                    AddPatternName(doc, regionType.BackgroundPatternId, usedPatternNames);
                }

                foreach (Material mat in new FilteredElementCollector(doc).OfClass(typeof(Material)))
                {
                    AddPatternName(doc, mat.SurfaceForegroundPatternId, usedPatternNames);
                    AddPatternName(doc, mat.SurfaceBackgroundPatternId, usedPatternNames);
                    AddPatternName(doc, mat.CutForegroundPatternId, usedPatternNames);
                    AddPatternName(doc, mat.CutBackgroundPatternId, usedPatternNames);
                }

                List<string> draftingUsed = allDrafting.Where(n => usedPatternNames.Contains(n)).ToList();
                List<string> draftingUnused = allDrafting.Where(n => !usedPatternNames.Contains(n)).ToList();
                List<string> modelUsed = allModel.Where(n => usedPatternNames.Contains(n)).ToList();
                List<string> modelUnused = allModel.Where(n => !usedPatternNames.Contains(n)).ToList();

                FillPatternAuditWindow window = new FillPatternAuditWindow(
                    draftingUsed, draftingUnused,
                    modelUsed, modelUnused);

                window.Show();

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
            if (patternId == null || patternId == ElementId.InvalidElementId) return;
            FillPatternElement fpe = doc.GetElement(patternId) as FillPatternElement;
            if (fpe == null) return;
            string name = fpe.Name;
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }

    public class FillPatternAuditWindow : Window
    {
        private readonly List<string> _draftingUsed;
        private readonly List<string> _draftingUnused;
        private readonly List<string> _modelUsed;
        private readonly List<string> _modelUnused;

        private static readonly SolidColorBrush BgDark = new SolidColorBrush(WpfColor.FromRgb(30, 30, 30));
        private static readonly SolidColorBrush BgPanel = new SolidColorBrush(WpfColor.FromRgb(45, 45, 48));
        private static readonly SolidColorBrush BgHeader = new SolidColorBrush(WpfColor.FromRgb(210, 100, 0));
        private static readonly SolidColorBrush BgUsed = new SolidColorBrush(WpfColor.FromRgb(37, 37, 38));
        private static readonly SolidColorBrush AccentGreen = new SolidColorBrush(WpfColor.FromRgb(34, 139, 34));
        private static readonly SolidColorBrush AccentRed = new SolidColorBrush(WpfColor.FromRgb(139, 0, 0));
        private static readonly SolidColorBrush TextLight = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush TextMuted = new SolidColorBrush(WpfColor.FromRgb(180, 180, 180));

        public FillPatternAuditWindow(
            List<string> draftingUsed, List<string> draftingUnused,
            List<string> modelUsed, List<string> modelUnused)
        {
            _draftingUsed = draftingUsed;
            _draftingUnused = draftingUnused;
            _modelUsed = modelUsed;
            _modelUnused = modelUnused;

            Title = "Fill Pattern Audit";
            Width = 900;
            Height = 700;
            MinWidth = 600;
            MinHeight = 400;
            Background = BgDark;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            int totalLoaded = _draftingUsed.Count + _draftingUnused.Count
                            + _modelUsed.Count + _modelUnused.Count;
            int totalUsed = _draftingUsed.Count + _modelUsed.Count;
            int totalUnused = _draftingUnused.Count + _modelUnused.Count;

            DockPanel root = new DockPanel { Background = BgDark };

            // Summary bar
            Border summaryBar = new Border
            {
                Background = BgHeader,
                Padding = new Thickness(16, 10, 16, 10)
            };
            StackPanel summaryContent = new StackPanel { Orientation = Orientation.Horizontal };
            summaryContent.Children.Add(SummaryChip("TOTAL LOADED", totalLoaded.ToString(), TextLight));
            summaryContent.Children.Add(SummaryChip("IN USE", totalUsed.ToString(), AccentGreen));
            summaryContent.Children.Add(SummaryChip("NOT IN USE", totalUnused.ToString(), AccentRed));
            summaryBar.Child = summaryContent;
            DockPanel.SetDock(summaryBar, Dock.Top);
            root.Children.Add(summaryBar);

            // Copy button bar
            Border copyBar = new Border
            {
                Background = BgPanel,
                Padding = new Thickness(16, 6, 16, 6),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(60, 60, 60))
            };
            StackPanel copyRow = new StackPanel { Orientation = Orientation.Horizontal };
            copyRow.Children.Add(MakeCopyButton("Copy All", () => BuildCopyText(all: true)));
            copyRow.Children.Add(MakeCopyButton("Copy In Use", () => BuildCopyText(inUseOnly: true)));
            copyRow.Children.Add(MakeCopyButton("Copy Not In Use", () => BuildCopyText(unusedOnly: true)));
            copyBar.Child = copyRow;
            DockPanel.SetDock(copyBar, Dock.Top);
            root.Children.Add(copyBar);

            // Two-column grid
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(12)
            };

            WpfGrid grid = new WpfGrid { Margin = new Thickness(4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            UIElement draftingPanel = BuildPatternPanel("DRAFTING PATTERNS", _draftingUsed, _draftingUnused);
            UIElement modelPanel = BuildPatternPanel("MODEL PATTERNS", _modelUsed, _modelUnused);

            WpfGrid.SetColumn(draftingPanel, 0);
            WpfGrid.SetColumn(modelPanel, 2);
            grid.Children.Add(draftingPanel);
            grid.Children.Add(modelPanel);

            scroll.Content = grid;
            root.Children.Add(scroll);

            return root;
        }

        private UIElement BuildPatternPanel(string title, List<string> used, List<string> unused)
        {
            StackPanel panel = new StackPanel();

            Border header = new Border
            {
                Background = BgPanel,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 2)
            };
            header.Child = new TextBlock
            {
                Text = $"{title}  ({used.Count + unused.Count})",
                Foreground = TextLight,
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            panel.Children.Add(header);
            panel.Children.Add(BuildSubSection("IN USE", used, AccentGreen));
            panel.Children.Add(BuildSubSection("NOT IN USE", unused, AccentRed));

            return panel;
        }

        private UIElement BuildSubSection(string label, List<string> names, SolidColorBrush accentColor)
        {
            StackPanel section = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            Border subHeader = new Border
            {
                Background = new SolidColorBrush(WpfColor.FromArgb(
                    40,
                    accentColor.Color.R,
                    accentColor.Color.G,
                    accentColor.Color.B)),
                Padding = new Thickness(12, 5, 12, 5)
            };
            StackPanel subHeaderRow = new StackPanel { Orientation = Orientation.Horizontal };
            subHeaderRow.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = accentColor,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            });
            subHeaderRow.Children.Add(new TextBlock
            {
                Text = $"  ({names.Count})",
                Foreground = TextMuted,
                FontSize = 11
            });
            subHeader.Child = subHeaderRow;
            section.Children.Add(subHeader);

            Border listBorder = new Border
            {
                Background = BgUsed,
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0)
            };

            if (names.Count == 0)
            {
                listBorder.Child = new TextBlock
                {
                    Text = "  —  none",
                    Foreground = TextMuted,
                    FontStyle = FontStyles.Italic,
                    Padding = new Thickness(12, 6, 12, 6)
                };
            }
            else
            {
                WpfTextBox tb = new WpfTextBox
                {
                    Text = string.Join(Environment.NewLine, names),
                    Background = Brushes.Transparent,
                    Foreground = TextMuted,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.NoWrap,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Padding = new Thickness(12, 6, 12, 6),
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
                listBorder.Child = tb;
            }

            section.Children.Add(listBorder);
            return section;
        }

        private UIElement SummaryChip(string label, string value, SolidColorBrush valueColor)
        {
            StackPanel chip = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 32, 0)
            };
            chip.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(180, 210, 240)),
                FontSize = 9,
                FontWeight = FontWeights.Bold
            });
            chip.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = valueColor,
                FontSize = 22,
                FontWeight = FontWeights.Bold
            });
            return chip;
        }

        private UIElement MakeCopyButton(string label, Func<string> getText)
        {
            Button btn = new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 4, 12, 4),
                Background = BgPanel,
                Foreground = TextLight,
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, e) =>
            {
                try { Clipboard.SetText(getText()); }
                catch { /* clipboard unavailable */ }
            };
            return btn;
        }

        private string BuildCopyText(bool all = false, bool inUseOnly = false, bool unusedOnly = false)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            void AppendSection(string title, List<string> used, List<string> unused)
            {
                sb.AppendLine($"── {title} ──");
                if (all || inUseOnly)
                {
                    sb.AppendLine($"  IN USE ({used.Count}):");
                    foreach (string n in used) sb.AppendLine($"    {n}");
                }
                if (all || unusedOnly)
                {
                    sb.AppendLine($"  NOT IN USE ({unused.Count}):");
                    foreach (string n in unused) sb.AppendLine($"    {n}");
                }
                sb.AppendLine();
            }

            AppendSection("DRAFTING PATTERNS", _draftingUsed, _draftingUnused);
            AppendSection("MODEL PATTERNS", _modelUsed, _modelUnused);

            return sb.ToString();
        }
    }
}