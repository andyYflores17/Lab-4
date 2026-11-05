using System;
using PackageTracker.Entities;

namespace PackageTracker.UseCases;

public sealed record AddTrackingEventRequest(
    string TrackingNumber,
    string Status,
    string Location,
    string Note = "",
    DateTimeOffset? OccurredAt = null);

/// <summary>How the interactor reports outcomes. Implemented by a presenter (outer layer).</summary>
public interface IAddTrackingEventOutputBoundary
{
    void PresentEventAdded(PackageResponse response);

    void PresentPackageNotFound(string trackingNumber);

    void PresentEventRejected(string reason);
}

/// <summary>How the outside asks for this use case. Called by a controller (outer layer).</summary>
public interface IAddTrackingEventInputBoundary
{
    void Execute(AddTrackingEventRequest request, IAddTrackingEventOutputBoundary output);
}

public sealed class AddTrackingEventInteractor : IAddTrackingEventInputBoundary
{
    private readonly IPackageGateway _gateway;

    public AddTrackingEventInteractor(IPackageGateway gateway)
    {
        _gateway = gateway;
    }

    public void Execute(AddTrackingEventRequest request, IAddTrackingEventOutputBoundary output)
    {
        var package = _gateway.FindByTrackingNumber(request.TrackingNumber);
        if (package is null)
        {
            output.PresentPackageNotFound(request.TrackingNumber);
            return;
        }

        if (!TrackingStatusNames.TryParse(request.Status, out var status))
        {
            output.PresentEventRejected($"Unknown status: '{request.Status}'");
            return;
        }

        try
        {
            package.AddEvent(new TrackingEvent(
                status,
                request.Location,
                request.OccurredAt ?? DateTimeOffset.UtcNow,
                request.Note));
        }
        catch (DomainException error) // business rule enforced by the entity
        {
            output.PresentEventRejected(error.Message);
            return;
        }

        _gateway.Save(package);
        output.PresentEventAdded(PackageResponseMapper.ToResponse(package));
    }
}
