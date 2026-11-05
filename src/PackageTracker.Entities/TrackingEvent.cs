using System;

namespace PackageTracker.Entities;

public sealed record TrackingEvent(TrackingStatus Status, string Location, DateTimeOffset OccurredAt, string Note = "");
