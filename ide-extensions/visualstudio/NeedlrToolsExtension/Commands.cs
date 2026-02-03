using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using Task = System.Threading.Tasks.Task;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Command to show the Needlr Services tool window.
    /// </summary>
    [Command(PackageGuids.CommandSetString, 0x0100)]
    internal sealed class ShowServicesWindowCommand : BaseCommand<ShowServicesWindowCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await NeedlrServicesToolWindow.ShowAsync();
        }
    }

    /// <summary>
    /// Command to refresh the Needlr graph.
    /// </summary>
    [Command(PackageGuids.CommandSetString, 0x0101)]
    internal sealed class RefreshGraphCommand : BaseCommand<RefreshGraphCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var graph = await NeedlrToolsPackage.GraphLoader?.FindAndLoadGraphAsync()!;
            
            if (graph != null)
            {
                await VS.StatusBar.ShowMessageAsync($"Needlr: Loaded {graph.Statistics.TotalServices} services");
            }
            else
            {
                await VS.StatusBar.ShowMessageAsync("Needlr: No graph found. Build your project with NeedlrExportGraph=true");
            }
        }
    }
}
