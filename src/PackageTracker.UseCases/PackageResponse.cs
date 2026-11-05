using System;
using System.Collections.Generic;
using System.Linq;
using PackageTracker.Entities;

namespace PackageTracker.UseCases;

public sealed record EventResponse(string Status, string Location, DateTimeOffset OccurredAt, string Note);

public sealed record PackageResponse(
    string TrackingNumber,
    string Carrier,
    string Recipient,
    string Status,
    bool Delivered,
    IReadOnlyList<EventResponse> Events);

public static class PackageResponseMapper
{
    public static PackageResponse ToResponse(Package package)
    {
        var events = package.Events
            .Select(e => new EventResponse(TrackingStatusNames.ToWireName(e.Status), e.Location, e.OccurredAt, e.Note))
            .ToList();

        return new PackageResponse(
            package.TrackingNumber,
            package.Carrier,
            package.Recipient,
            TrackingStatusNames.ToWireName(package.Status),
            package.IsDelivered,
            events);
    }
}
