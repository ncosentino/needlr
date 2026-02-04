using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using Task = System.Threading.Tasks.Task;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Command to show the Needlr Services tool window.
    /// Placed in View > Other Windows menu by the toolkit.
    /// </summary>
    [Command("b69b7451-4333-4185-a9cb-e9e78c9be818", 0x0100)]
    internal sealed class ShowServicesWindowCommand : BaseCommand<ShowServicesWindowCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Visible = true;
            Command.Enabled = true;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            Debug.WriteLine("Needlr: ShowServicesWindowCommand.ExecuteAsync called");
            await VS.StatusBar.ShowMessageAsync("Needlr: Opening Services window...");
            
            try
            {
                await NeedlrServicesToolWindow.ShowAsync();
                Debug.WriteLine("Needlr: Tool window shown successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Needlr: Error showing tool window: {ex}");
                await VS.MessageBox.ShowErrorAsync("Needlr Error", $"Failed to open tool window: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Command to refresh the Needlr graph.
    /// </summary>
    [Command("b69b7451-4333-4185-a9cb-e9e78c9be818", 0x0101)]
    internal sealed class RefreshGraphCommand : BaseCommand<RefreshGraphCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Visible = true;
            Command.Enabled = true;
        }

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
