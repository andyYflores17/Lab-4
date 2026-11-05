using System;
using PackageTracker.UseCases;

namespace PackageTracker.FrameworksDrivers;

public static class PackageTrackingConsole
{
    private sealed record Step(
        string Title,
        string TrackingNumber,
        string Status,
        string Location,
        string Note,
        string? OccurredAt);

    private static readonly Step[] Script = new[]
    {
        new Step("Leaves the origin facility", "T-1001", "in_transit", "Jacksonville, FL", "Departed origin facility", "2026-11-02T08:15:00+00:00"),
        new Step("Scanned at a hub", "T-1001", "in_transit", "Atlanta, GA", "", "2026-11-02T19:40:00+00:00"),
        new Step("Out for delivery", "T-1001", "out_for_delivery", "Savannah, GA", "", "2026-11-03T07:05:00+00:00"),
        new Step("Rejected: unknown status", "T-1001", "teleported", "Mars", "", null),
        new Step("Rejected by the entity: older than the latest event", "T-1001", "in_transit", "Macon, GA", "", "2026-11-01T10:00:00+00:00"),
        new Step("Unknown package", "T-9999", "in_transit", "Atlanta, GA", "", null),
        new Step("Delivered", "T-1001", "delivered", "Savannah, GA", "Left at front door", "2026-11-03T13:22:00+00:00"),
        new Step("Rejected by the entity: already delivered", "T-1001", "in_transit", "Savannah, GA", "", "2026-11-03T14:00:00+00:00")
    };

    public static void RunDemo()
    {
        var app = ApplicationComposition.Build();
        var view = new ConsoleView();
        app.RegisterPackage.Execute(new RegisterPackageRequest("T-1001", "UPS", "Ada Lovelace"));

        var number = 1;
        foreach (var step in Script)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {number}. {step.Title}");
            view.Render(app.TrackingEventController.RecordEvent(
                step.TrackingNumber,
                step.Status,
                step.Location,
                step.Note,
                step.OccurredAt));
            number++;
        }
    }
}
