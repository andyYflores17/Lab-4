using PackageTracker.Entities;

namespace PackageTracker.UseCases;

public sealed record RegisterPackageRequest(string TrackingNumber, string Carrier, string Recipient);

public sealed class RegisterPackage
{
    private readonly IPackageGateway _gateway;

    public RegisterPackage(IPackageGateway gateway)
    {
        _gateway = gateway;
    }

    public PackageResponse Execute(RegisterPackageRequest request)
    {
        if (_gateway.FindByTrackingNumber(request.TrackingNumber) != null)
        {
            throw new DuplicatePackageException(request.TrackingNumber);
        }

        var package = new Package(request.TrackingNumber, request.Carrier, request.Recipient);
        _gateway.Save(package);
        return PackageResponseMapper.ToResponse(package);
    }
}
