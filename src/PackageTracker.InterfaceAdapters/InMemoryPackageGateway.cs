using System.Collections.Generic;
using PackageTracker.Entities;
using PackageTracker.UseCases;

namespace PackageTracker.InterfaceAdapters;

public sealed class InMemoryPackageGateway : IPackageGateway
{
    private readonly Dictionary<string, Package> _store = new Dictionary<string, Package>();

    public void Save(Package package)
    {
        _store[package.TrackingNumber] = package.Clone();
    }

    public Package? FindByTrackingNumber(string trackingNumber)
    {
        return _store.TryGetValue(trackingNumber, out var found) ? found.Clone() : null;
    }
}
