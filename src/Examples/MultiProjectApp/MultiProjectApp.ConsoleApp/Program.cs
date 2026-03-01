using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using MultiProjectApp.Features.Notifications;
using MultiProjectApp.Features.Reporting;

// All plugins from Bootstrap and the feature projects are discovered via the source-generated
// TypeRegistry. Each referenced assembly's module initializer registers its types at startup.
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

var notifier = provider.GetRequiredService<INotificationService>();
var reporter = provider.GetRequiredService<IReportService>();

notifier.Send("team@example.com", "MultiProjectApp is running.");

var report = reporter.Generate("Startup Report", ["Notifications: OK", "Reporting: OK"]);
Console.WriteLine(report);
