using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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
    /// </summary>
    public class NeedlrServicesControl : UserControl
    {
        private readonly TreeView _treeView;
        private readonly TextBlock _statusText;

        public NeedlrServicesControl()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Toolbar
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            var refreshButton = new Button { Content = "Refresh", Padding = new Thickness(10, 2, 10, 2) };
            refreshButton.Click += async (s, e) => await RefreshAsync();
            toolbar.Children.Add(refreshButton);
            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // Tree view
            _treeView = new TreeView { Margin = new Thickness(5) };
            Grid.SetRow(_treeView, 1);
            grid.Children.Add(_treeView);

            // Status bar
            _statusText = new TextBlock { Margin = new Thickness(5), Text = "Loading..." };
            Grid.SetRow(_statusText, 2);
            grid.Children.Add(_statusText);

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

            // Group by lifetime
            var singletons = new TreeViewItem { Header = $"Singletons ({graph.Statistics.Singletons})" };
            var scoped = new TreeViewItem { Header = $"Scoped ({graph.Statistics.Scoped})" };
            var transient = new TreeViewItem { Header = $"Transient ({graph.Statistics.Transient})" };

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

            _statusText.Text = $"Loaded {graph.Statistics.TotalServices} services";
        }

        private TreeViewItem CreateServiceItem(GraphService service)
        {
            var item = new TreeViewItem
            {
                Header = service.TypeName,
                Tag = service
            };

            // Add interfaces
            if (service.Interfaces.Count > 0)
            {
                var interfacesItem = new TreeViewItem { Header = $"Interfaces ({service.Interfaces.Count})" };
                foreach (var iface in service.Interfaces)
                {
                    interfacesItem.Items.Add(new TreeViewItem { Header = iface.Name });
                }
                item.Items.Add(interfacesItem);
            }

            // Add dependencies
            if (service.Dependencies.Count > 0)
            {
                var depsItem = new TreeViewItem { Header = $"Dependencies ({service.Dependencies.Count})" };
                foreach (var dep in service.Dependencies)
                {
                    depsItem.Items.Add(new TreeViewItem { Header = $"{dep.ParameterName}: {dep.TypeName}" });
                }
                item.Items.Add(depsItem);
            }

            // Add decorators
            if (service.Decorators.Count > 0)
            {
                var decsItem = new TreeViewItem { Header = $"Decorators ({service.Decorators.Count})" };
                foreach (var dec in service.Decorators)
                {
                    decsItem.Items.Add(new TreeViewItem { Header = $"{dec.Order}: {dec.TypeName}" });
                }
                item.Items.Add(decsItem);
            }

            // Double-click to navigate
            item.MouseDoubleClick += async (s, e) =>
            {
                if (service.Location?.FilePath != null)
                {
                    await VS.Documents.OpenAsync(service.Location.FilePath);
                    e.Handled = true;
                }
            };

            return item;
        }
    }
}
