using System;
using System.Collections.Generic;

namespace PackageTracker.Entities;

public sealed class Package
{
    private readonly List<TrackingEvent> _events = new List<TrackingEvent>();

    public Package(string trackingNumber, string carrier, string recipient)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            throw new DomainException("Tracking number is required.");
        }

        if (string.IsNullOrWhiteSpace(carrier))
        {
            throw new DomainException("Carrier is required.");
        }

        TrackingNumber = trackingNumber;
        Carrier = carrier;
        Recipient = recipient;
    }

    public string TrackingNumber { get; }

    public string Carrier { get; }

    public string Recipient { get; }

    public IReadOnlyList<TrackingEvent> Events => _events.AsReadOnly();

    public TrackingStatus Status =>
        _events.Count == 0 ? TrackingStatus.Registered : _events[_events.Count - 1].Status;

    public bool IsDelivered => Status == TrackingStatus.Delivered;

    public void AddEvent(TrackingEvent trackingEvent)
    {
        if (IsDelivered)
        {
            throw new InvalidTrackingEventException("Package is already delivered.");
        }

        if (_events.Count > 0 && trackingEvent.OccurredAt < _events[_events.Count - 1].OccurredAt)
        {
            throw new InvalidTrackingEventException("Event is older than the latest event.");
        }

        _events.Add(trackingEvent);
    }

    public Package Clone()
    {
        var copy = new Package(TrackingNumber, Carrier, Recipient);
        copy._events.AddRange(_events);
        return copy;
    }
}
