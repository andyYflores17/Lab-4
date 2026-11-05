using System;
using PackageTracker.InterfaceAdapters;

namespace PackageTracker.FrameworksDrivers;

/// <summary>Humble view: prints the view model, decides nothing.</summary>
public sealed class ConsoleView
{
    public void Render(TrackingEventViewModel viewModel)
    {
        var mark = viewModel.Ok ? "OK  " : "FAIL";
        Console.WriteLine($"[{mark}] {viewModel.Headline}");
        foreach (var line in viewModel.Timeline)
        {
            Console.WriteLine($"         {line}");
        }
    }
}
