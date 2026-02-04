using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Needlr Tools Extension Package.
    /// Provides dependency injection visualization and diagnostics for Needlr-based projects.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.NeedlrToolsPackageString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(NeedlrServicesToolWindow.Pane), Style = VsDockStyle.Tabbed, DockedWidth = 300, Window = "DocumentWell", Orientation = ToolWindowOrientation.Left)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class NeedlrToolsPackage : ToolkitPackage
    {
        public static GraphLoader? GraphLoader { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            
            // Initialize the graph loader
            GraphLoader = new GraphLoader();
            
            // Register tool windows - this is required for BaseToolWindow to work
            this.RegisterToolWindows();
            
            // Register commands - this must come AFTER base.InitializeAsync
            await this.RegisterCommandsAsync();
            
            // Initialize graph loader after solution loads
            VS.Events.SolutionEvents.OnAfterOpenSolution += OnSolutionOpened;
        }

        private void OnSolutionOpened(Community.VisualStudio.Toolkit.Solution? solution)
        {
            _ = GraphLoader?.FindAndLoadGraphAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GraphLoader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Package GUIDs for the extension.
    /// </summary>
    public static class PackageGuids
    {
        public const string NeedlrToolsPackageString = "b69b7450-4333-4185-a9cb-e9e78c9be817";
        public static readonly Guid NeedlrToolsPackage = new(NeedlrToolsPackageString);
        
        public const string CommandSetString = "b69b7451-4333-4185-a9cb-e9e78c9be818";
        public static readonly Guid CommandSet = new(CommandSetString);
    }
}
