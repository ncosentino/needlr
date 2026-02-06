using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly TextBox _searchBox;
        private NeedlrGraph? _currentGraph;
        private string _searchFilter = "";

        public NeedlrServicesControl()
        {
            // Use VS environment colors
            SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowBackgroundBrushKey);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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
            
            // Hover trigger
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

            // Search box
            var searchPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 2, 4, 2)
            };
            
            var searchLabel = new TextBlock
            {
                Text = "🔍",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            searchLabel.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);
            searchPanel.Children.Add(searchLabel);
            
            _searchBox = new TextBox
            {
                Width = 200,
                Padding = new Thickness(4, 2, 4, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _searchBox.SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.SearchBoxBackgroundBrushKey);
            _searchBox.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);
            _searchBox.SetResourceReference(BorderBrushProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.SearchBoxBorderBrushKey);
            _searchBox.TextChanged += OnSearchTextChanged;
            
            // Add watermark text
            var watermark = new TextBlock
            {
                Text = "Filter services...",
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Opacity = 0.5
            };
            watermark.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);
            
            var searchContainer = new Grid();
            searchContainer.Children.Add(_searchBox);
            searchContainer.Children.Add(watermark);
            
            // Hide watermark when textbox has text
            _searchBox.TextChanged += (s, e) => watermark.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            
            searchPanel.Children.Add(searchContainer);
            
            var clearButton = new Button 
            { 
                Content = "✕", 
                Style = buttonStyle, 
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(2, 0, 0, 0),
                ToolTip = "Clear search"
            };
            clearButton.Click += (s, e) => { _searchBox.Text = ""; _searchBox.Focus(); };
            searchPanel.Children.Add(clearButton);
            
            Grid.SetRow(searchPanel, 1);
            grid.Children.Add(searchPanel);

            // Tree view with VS colors
            _treeView = new TreeView 
            { 
                Margin = new Thickness(4, 2, 4, 2),
                BorderThickness = new Thickness(0)
            };
            _treeView.SetResourceReference(BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowBackgroundBrushKey);
            _treeView.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);
            
            // Create a style for TreeViewItems
            var treeViewItemStyle = new Style(typeof(TreeViewItem));
            treeViewItemStyle.Setters.Add(new Setter(TreeViewItem.ForegroundProperty, 
                new DynamicResourceExtension(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey)));
            treeViewItemStyle.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, 
                new DynamicResourceExtension(typeof(TreeViewItem))));
            _treeView.ItemContainerStyle = treeViewItemStyle;
            _treeView.Resources.Add(typeof(TreeViewItem), treeViewItemStyle);
            
            Grid.SetRow(_treeView, 2);
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
            Grid.SetRow(statusBorder, 3);
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
                    _currentGraph = NeedlrToolsPackage.GraphLoader.CurrentGraph;
                    PopulateTree(_currentGraph);
                }
                else
                {
                    _statusText.Text = "No graph loaded. Build your project to load services.";
                }
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchFilter = _searchBox.Text.Trim();
            if (_currentGraph != null)
            {
                PopulateTree(_currentGraph);
            }
        }

        private void OnGraphLoaded(object? sender, NeedlrGraph graph)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _currentGraph = graph;
                PopulateTree(graph);
            });
        }

        private void OnGraphCleared(object? sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _currentGraph = null;
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

            // Filter services based on search
            var filteredServices = string.IsNullOrEmpty(_searchFilter)
                ? graph.Services
                : graph.Services.Where(s => MatchesFilter(s, _searchFilter)).ToList();

            // Group by lifetime with icons
            var singletonServices = filteredServices.Where(s => s.Lifetime == "Singleton").ToList();
            var scopedServices = filteredServices.Where(s => s.Lifetime == "Scoped").ToList();
            var transientServices = filteredServices.Where(s => s.Lifetime == "Transient").ToList();

            var singletons = CreateLifetimeHeader($"🔷 Singletons ({singletonServices.Count})", true);
            var scoped = CreateLifetimeHeader($"🔶 Scoped ({scopedServices.Count})", false);
            var transient = CreateLifetimeHeader($"⚪ Transient ({transientServices.Count})", false);

            foreach (var service in singletonServices)
            {
                singletons.Items.Add(CreateServiceItem(service));
            }
            foreach (var service in scopedServices)
            {
                scoped.Items.Add(CreateServiceItem(service));
            }
            foreach (var service in transientServices)
            {
                transient.Items.Add(CreateServiceItem(service));
            }

            _treeView.Items.Add(singletons);
            _treeView.Items.Add(scoped);
            _treeView.Items.Add(transient);

            // Update status
            if (string.IsNullOrEmpty(_searchFilter))
            {
                _statusText.Text = $"✓ Loaded {graph.Statistics.TotalServices} services from {graph.AssemblyName}";
            }
            else
            {
                _statusText.Text = $"🔍 Showing {filteredServices.Count} of {graph.Statistics.TotalServices} services";
            }
        }

        private static bool MatchesFilter(GraphService service, string filter)
        {
            var lowerFilter = filter.ToLowerInvariant();
            
            // Match type name
            if (service.TypeName.ToLowerInvariant().Contains(lowerFilter))
                return true;
            if (service.FullTypeName.ToLowerInvariant().Contains(lowerFilter))
                return true;
            
            // Match interfaces
            if (service.Interfaces.Any(i => 
                i.Name.ToLowerInvariant().Contains(lowerFilter) || 
                i.FullName.ToLowerInvariant().Contains(lowerFilter)))
                return true;
            
            // Match dependencies
            if (service.Dependencies.Any(d => 
                d.TypeName.ToLowerInvariant().Contains(lowerFilter) ||
                d.ParameterName.ToLowerInvariant().Contains(lowerFilter) ||
                (d.ResolvedTo?.ToLowerInvariant().Contains(lowerFilter) ?? false)))
                return true;
            
            // Match assembly name
            if (service.AssemblyName?.ToLowerInvariant().Contains(lowerFilter) ?? false)
                return true;
            
            return false;
        }

        private TreeViewItem CreateLifetimeHeader(string text, bool isExpanded)
        {
            var item = new TreeViewItem 
            { 
                Header = text,
                IsExpanded = isExpanded || !string.IsNullOrEmpty(_searchFilter), // Expand all when filtering
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
                FontStyle = canNavigate ? FontStyles.Normal : FontStyles.Italic,
                IsExpanded = !string.IsNullOrEmpty(_searchFilter) // Expand when filtering
            };

            // Add interfaces with navigation
            if (service.Interfaces.Count > 0)
            {
                var interfacesItem = new TreeViewItem { Header = $"📋 Interfaces ({service.Interfaces.Count})" };
                foreach (var iface in service.Interfaces)
                {
                    var ifaceCanNavigate = !string.IsNullOrEmpty(iface.Location?.FilePath) && iface.Location.Line > 0;
                    var ifaceItem = new TreeViewItem 
                    { 
                        Header = iface.Name,
                        ToolTip = ifaceCanNavigate 
                            ? $"{iface.FullName}\nDouble-click to navigate"
                            : iface.FullName,
                        FontStyle = FontStyles.Italic,
                        Cursor = ifaceCanNavigate ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                        Tag = iface
                    };
                    
                    if (ifaceCanNavigate)
                    {
                        ifaceItem.MouseDoubleClick += (s, e) =>
                        {
                            e.Handled = true;
                            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                            {
                                try
                                {
                                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                    var docView = await VS.Documents.OpenAsync(iface.Location!.FilePath!);
                                    if (docView?.TextView != null)
                                    {
                                        var textView = docView.TextView;
                                        var lineNumber = Math.Max(0, Math.Min(iface.Location.Line - 1, textView.TextSnapshot.LineCount - 1));
                                        var snapshotLine = textView.TextSnapshot.GetLineFromLineNumber(lineNumber);
                                        textView.Caret.MoveTo(snapshotLine.Start);
                                        textView.Caret.EnsureVisible();
                                        textView.ViewScroller.EnsureSpanVisible(
                                            new Microsoft.VisualStudio.Text.SnapshotSpan(snapshotLine.Start, snapshotLine.End),
                                            EnsureSpanVisibleOptions.AlwaysCenter);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Needlr: Interface navigation error: {ex.Message}");
                                }
                            });
                        };
                    }
                    
                    interfacesItem.Items.Add(ifaceItem);
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

            // Double-click to navigate
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
