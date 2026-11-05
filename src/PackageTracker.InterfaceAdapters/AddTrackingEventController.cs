using System;
using System.Globalization;
using PackageTracker.UseCases;

namespace PackageTracker.InterfaceAdapters;

/// <summary>Raw input (strings) to request model to input boundary. Returns the view model.</summary>
public sealed class AddTrackingEventController
{
    private readonly IAddTrackingEventInputBoundary _inputBoundary;
    private readonly Func<AddTrackingEventPresenter> _presenterFactory;

    public AddTrackingEventController(
        IAddTrackingEventInputBoundary inputBoundary,
        Func<AddTrackingEventPresenter>? presenterFactory = null)
    {
        _inputBoundary = inputBoundary;
        _presenterFactory = presenterFactory ?? (() => new AddTrackingEventPresenter());
    }

    public TrackingEventViewModel RecordEvent(
        string trackingNumber,
        string status,
        string location,
        string note = "",
        string? occurredAt = null)
    {
        var presenter = _presenterFactory(); // fresh per call: no shared state

        DateTimeOffset? when = null;
        if (!string.IsNullOrEmpty(occurredAt))
        {
            if (!DateTimeOffset.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                presenter.PresentEventRejected("occurred_at must be ISO 8601.");
                return presenter.ViewModel;
            }

            when = parsed;
        }

        _inputBoundary.Execute(
            new AddTrackingEventRequest(trackingNumber, status, location, note, when),
            presenter);
        return presenter.ViewModel;
    }
}
