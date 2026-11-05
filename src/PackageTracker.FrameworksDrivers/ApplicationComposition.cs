using PackageTracker.InterfaceAdapters;
using PackageTracker.UseCases;

namespace PackageTracker.FrameworksDrivers;

public sealed record Application(
    RegisterPackage RegisterPackage,
    AddTrackingEventController TrackingEventController,
    PackageTrackingController PackageController);

public static class ApplicationComposition
{
    /// <summary>Composition root: the only place that picks concrete implementations.</summary>
    public static Application Build()
    {
        var gateway = new InMemoryPackageGateway();
        var register = new RegisterPackage(gateway);
        var eventController = new AddTrackingEventController(new AddTrackingEventInteractor(gateway));
        var packageController = new PackageTrackingController(
            register,
            eventController,
            new GetPackageStatus(gateway),
            new PackagePresenter());
        return new Application(register, eventController, packageController);
    }
}
