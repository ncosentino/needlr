using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Tool window showing Needlr services grouped by lifetime.
    /// </summary>
    public class NeedlrServicesToolWindow : BaseToolWindow<NeedlrServicesToolWindow>
    {
        public override string GetTitle(int toolWindowId) => "Needlr Services";

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            return new NeedlrServicesControl();
        }

        [Guid("b69b7452-4333-4185-a9cb-e9e78c9be819")]
        internal class Pane : ToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.StatusInformation;
            }
        }
    }

    /// <summary>
    /// WPF control for the Needlr Services tool window.
    /// Uses VS theme colors for proper integration.
    /// </summary>
    public class NeedlrServicesControl : UserControl
    {
        private readonly TreeView _treeView;
        private readonly TextBlock _statusText;

        public NeedlrServicesControl()
        {
            // Use VS environment colors
            SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowBackgroundBrushKey);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Toolbar with VS styling
            var toolbar = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Margin = new Thickness(4, 4, 4, 2)
            };
            toolbar.SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarGradientBrushKey);
            
            // Create a button style with proper hover/pressed states
            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(8, 4, 8, 4)));
            buttonStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(2)));
            buttonStyle.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
            buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey)));
            buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarGradientBrushKey)));
            buttonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarBorderBrushKey)));
            
            // Hover trigger - use CommandBarTextHover for proper contrast
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarMouseOverBackgroundGradientBrushKey)));
            hoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarSelectedBorderBrushKey)));
            hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarTextHoverBrushKey)));
            buttonStyle.Triggers.Add(hoverTrigger);
            
            // Pressed trigger
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarMouseDownBackgroundGradientBrushKey)));
            pressedTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarMouseDownBorderBrushKey)));
            pressedTrigger.Setters.Add(new Setter(Button.ForegroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarTextMouseDownBrushKey)));
            buttonStyle.Triggers.Add(pressedTrigger);
            
            var refreshButton = new Button { Content = "↻ Refresh", Style = buttonStyle };
            refreshButton.Click += async (s, e) => await RefreshAsync();
            toolbar.Children.Add(refreshButton);
            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // Tree view with VS colors
            _treeView = new TreeView 
            { 
                Margin = new Thickness(4, 2, 4, 2),
                BorderThickness = new Thickness(0)
            };
            _treeView.SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowBackgroundBrushKey);
            _treeView.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);
            
            // Create a style for TreeViewItems - set ItemContainerStyle recursively
            var treeViewItemStyle = new Style(typeof(TreeViewItem));
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.ForegroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey)));
            // This makes nested items also use this style
            treeViewItemStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, 
                new DynamicResourceExtension(typeof(TreeViewItem))));
            _treeView.ItemContainerStyle = treeViewItemStyle;
            // Also add to resources so nested items can find it
            _treeView.Resources.Add(typeof(TreeViewItem), treeViewItemStyle);
            
            Grid.SetRow(_treeView, 1);
            grid.Children.Add(_treeView);

            // Status bar with VS colors
            var statusBorder = new Border
            {
                Padding = new Thickness(4, 2, 4, 2)
            };
            statusBorder.SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarGradientBrushKey);
            
            _statusText = new TextBlock 
            { 
                Text = "Loading...",
                FontSize = 11
            };
            _statusText.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);
            statusBorder.Child = _statusText;
            Grid.SetRow(statusBorder, 2);
            grid.Children.Add(statusBorder);

            Content = grid;

            // Subscribe to graph updates
            if (NeedlrToolsPackage.GraphLoader != null)
            {
                NeedlrToolsPackage.GraphLoader.GraphLoaded += OnGraphLoaded;
                NeedlrToolsPackage.GraphLoader.GraphCleared += OnGraphCleared;

                // Load current graph if available
                if (NeedlrToolsPackage.GraphLoader.CurrentGraph != null)
                {
                    PopulateTree(NeedlrToolsPackage.GraphLoader.CurrentGraph);
                }
                else
                {
                    _statusText.Text = "No graph loaded. Build your project with NeedlrExportGraph=true";
                }
            }
        }

        private void OnGraphLoaded(object? sender, NeedlrGraph graph)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                PopulateTree(graph);
            });
        }

        private void OnGraphCleared(object? sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _treeView.Items.Clear();
                _statusText.Text = "Graph cleared";
            });
        }

        private async Task RefreshAsync()
        {
            _statusText.Text = "Refreshing...";
            var graph = await NeedlrToolsPackage.GraphLoader?.FindAndLoadGraphAsync()!;
            
            if (graph == null)
            {
                _statusText.Text = "No graph found";
            }
        }

        private void PopulateTree(NeedlrGraph graph)
        {
            _treeView.Items.Clear();

            // Group by lifetime with icons
            var singletons = CreateLifetimeHeader($"🔷 Singletons ({graph.Statistics.Singletons})", true);
            var scoped = CreateLifetimeHeader($"🔶 Scoped ({graph.Statistics.Scoped})", false);
            var transient = CreateLifetimeHeader($"⚪ Transient ({graph.Statistics.Transient})", false);

            foreach (var service in graph.Services)
            {
                var serviceItem = CreateServiceItem(service);
                
                switch (service.Lifetime)
                {
                    case "Singleton":
                        singletons.Items.Add(serviceItem);
                        break;
                    case "Scoped":
                        scoped.Items.Add(serviceItem);
                        break;
                    case "Transient":
                        transient.Items.Add(serviceItem);
                        break;
                }
            }

            _treeView.Items.Add(singletons);
            _treeView.Items.Add(scoped);
            _treeView.Items.Add(transient);

            _statusText.Text = $"✓ Loaded {graph.Statistics.TotalServices} services from {graph.AssemblyName}";
        }

        private TreeViewItem CreateLifetimeHeader(string text, bool isExpanded)
        {
            var item = new TreeViewItem 
            { 
                Header = text,
                IsExpanded = isExpanded,
                FontWeight = FontWeights.SemiBold
            };
            return item;
        }

        private TreeViewItem CreateServiceItem(GraphService service)
        {
            // Create header with type name and dependency count
            var headerText = service.Dependencies.Count > 0 
                ? $"{service.TypeName} ({service.Dependencies.Count} deps)"
                : service.TypeName;
            
            // Check if navigation is available
            var canNavigate = !string.IsNullOrEmpty(service.Location?.FilePath) && service.Location.Line > 0;
            var tooltipText = canNavigate 
                ? $"{service.FullTypeName}\nDouble-click to navigate to source"
                : $"{service.FullTypeName}\n(Source location not available - from referenced assembly)";
            
            // Add assembly indicator for external types
            if (!string.IsNullOrEmpty(service.AssemblyName))
            {
                headerText = $"{headerText} [{service.AssemblyName}]";
            }
            
            var item = new TreeViewItem
            {
                Header = headerText,
                Tag = service,
                ToolTip = tooltipText,
                Cursor = canNavigate ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                FontStyle = canNavigate ? FontStyles.Normal : FontStyles.Italic
            };

            // Add interfaces
            if (service.Interfaces.Count > 0)
            {
                var interfacesItem = new TreeViewItem { Header = $"📋 Interfaces ({service.Interfaces.Count})" };
                foreach (var iface in service.Interfaces)
                {
                    interfacesItem.Items.Add(new TreeViewItem 
                    { 
                        Header = iface.Name,
                        ToolTip = iface.FullName,
                        FontStyle = FontStyles.Italic
                    });
                }
                item.Items.Add(interfacesItem);
            }

            // Add dependencies
            if (service.Dependencies.Count > 0)
            {
                var depsItem = new TreeViewItem { Header = $"🔗 Dependencies ({service.Dependencies.Count})" };
                foreach (var dep in service.Dependencies)
                {
                    var depText = dep.ResolvedTo != null 
                        ? $"{dep.ParameterName}: {dep.TypeName} → {GetSimpleTypeName(dep.ResolvedTo)}"
                        : $"{dep.ParameterName}: {dep.TypeName}";
                    depsItem.Items.Add(new TreeViewItem 
                    { 
                        Header = depText,
                        ToolTip = $"Resolved: {dep.ResolvedTo ?? "Unknown"}\nLifetime: {dep.ResolvedLifetime ?? "Unknown"}"
                    });
                }
                item.Items.Add(depsItem);
            }

            // Add decorators
            if (service.Decorators.Count > 0)
            {
                var decsItem = new TreeViewItem { Header = $"🎨 Decorators ({service.Decorators.Count})" };
                foreach (var dec in service.Decorators)
                {
                    decsItem.Items.Add(new TreeViewItem { Header = $"#{dec.Order}: {dec.TypeName}" });
                }
                item.Items.Add(decsItem);
            }

            // Double-click to navigate - use JoinableTaskFactory for proper async handling
            item.MouseDoubleClick += (s, e) =>
            {
                if (service.Location?.FilePath != null && service.Location.Line > 0)
                {
                    e.Handled = true;
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        try
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            System.Diagnostics.Debug.WriteLine($"Needlr: Navigating to {service.Location.FilePath}:{service.Location.Line}");
                            
                            var docView = await VS.Documents.OpenAsync(service.Location.FilePath);
                            if (docView?.TextView != null)
                            {
                                var textView = docView.TextView;
                                var lineNumber = Math.Max(0, Math.Min(service.Location.Line - 1, textView.TextSnapshot.LineCount - 1));
                                var snapshotLine = textView.TextSnapshot.GetLineFromLineNumber(lineNumber);
                                
                                // Move caret and center the line in view
                                textView.Caret.MoveTo(snapshotLine.Start);
                                textView.Caret.EnsureVisible();
                                textView.ViewScroller.EnsureSpanVisible(
                                    new Microsoft.VisualStudio.Text.SnapshotSpan(snapshotLine.Start, snapshotLine.End),
                                    EnsureSpanVisibleOptions.AlwaysCenter);
                                
                                System.Diagnostics.Debug.WriteLine($"Needlr: Navigated to line {lineNumber + 1}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Needlr: Failed to get TextView for {service.Location.FilePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Needlr: Navigation error: {ex.Message}");
                        }
                    });
                }
            };

            return item;
        }

        private static string GetSimpleTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return fullTypeName;
            var lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }
    }
}
