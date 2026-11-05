using PackageTracker.Entities;

namespace PackageTracker.UseCases;

/// <summary>Persistence port. Defined by the use-case layer, implemented outside it.</summary>
public interface IPackageGateway
{
    void Save(Package package);

    Package? FindByTrackingNumber(string trackingNumber);
}
