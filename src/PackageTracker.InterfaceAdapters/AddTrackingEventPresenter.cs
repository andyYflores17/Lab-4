using System;
using System.Globalization;
using System.Linq;
using PackageTracker.UseCases;

namespace PackageTracker.InterfaceAdapters;

public sealed class AddTrackingEventPresenter : IAddTrackingEventOutputBoundary
{
    private TrackingEventViewModel? _viewModel;

    public TrackingEventViewModel ViewModel =>
        _viewModel ?? throw new InvalidOperationException("The use case has not presented anything yet.");

    public void PresentEventAdded(PackageResponse response)
    {
        var latest = response.Events[response.Events.Count - 1];
        var timeline = response.Events.Select(e => FormatLine(e)).ToList();
        var headline = $"{response.TrackingNumber} is now {Label(response.Status)} - latest scan: {latest.Location}";

        _viewModel = new TrackingEventViewModel(
            true,
            headline,
            response.TrackingNumber,
            response.Status,
            response.Delivered,
            timeline,
            null);
    }

    public void PresentPackageNotFound(string trackingNumber)
    {
        _viewModel = new TrackingEventViewModel(
            false,
            $"No package with tracking number {trackingNumber}.",
            trackingNumber,
            "",
            false,
            Array.Empty<string>(),
            "not_found");
    }

    public void PresentEventRejected(string reason)
    {
        _viewModel = new TrackingEventViewModel(
            false,
            $"Event not recorded: {reason}",
            "",
            "",
            false,
            Array.Empty<string>(),
            "rejected");
    }

    private static string Label(string status)
    {
        var text = status.Replace('_', ' ');
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static string FormatLine(EventResponse e)
    {
        var when = e.OccurredAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var line = $"{when}  {Label(e.Status).PadRight(17)} {e.Location}";
        return e.Note.Length > 0 ? $"{line} ({e.Note})" : line;
    }
}
