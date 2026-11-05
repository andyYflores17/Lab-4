using System.Collections.Generic;

namespace PackageTracker.InterfaceAdapters;

/// <summary>Display-ready data. No entities, no dates a view must interpret.</summary>
public sealed record TrackingEventViewModel(
    bool Ok,
    string Headline,
    string TrackingNumber,
    string Status,
    bool Delivered,
    IReadOnlyList<string> Timeline,
    string? ErrorKind); // null | "not_found" | "rejected"
