namespace PackageTracker.UseCases;

public sealed class GetPackageStatus
{
    private readonly IPackageGateway _gateway;

    public GetPackageStatus(IPackageGateway gateway)
    {
        _gateway = gateway;
    }

    public PackageResponse Execute(string trackingNumber)
    {
        var package = _gateway.FindByTrackingNumber(trackingNumber);
        if (package is null)
        {
            throw new PackageNotFoundException(trackingNumber);
        }

        return PackageResponseMapper.ToResponse(package);
    }
}
