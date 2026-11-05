using System;
using System.Collections.Generic;
using PackageTracker.Entities;
using PackageTracker.UseCases;

namespace PackageTracker.InterfaceAdapters;

public sealed record ControllerResult(int StatusCode, IReadOnlyDictionary<string, object?> Body);

public sealed class PackageTrackingController
{
    private readonly RegisterPackage _register;
    private readonly AddTrackingEventController _addEvent;
    private readonly GetPackageStatus _getStatus;
    private readonly PackagePresenter _presenter;

    public PackageTrackingController(
        RegisterPackage register,
        AddTrackingEventController addEvent,
        GetPackageStatus getStatus,
        PackagePresenter presenter)
    {
        _register = register;
        _addEvent = addEvent;
        _getStatus = getStatus;
        _presenter = presenter;
    }

    public ControllerResult Register(IReadOnlyDictionary<string, string> data)
    {
        return Run(201, () => _register.Execute(new RegisterPackageRequest(
            Get(data, "tracking_number"),
            Get(data, "carrier"),
            Get(data, "recipient"))));
    }

    public ControllerResult AddEvent(string trackingNumber, IReadOnlyDictionary<string, string> data)
    {
        var occurredAt = Get(data, "occurred_at");
        var viewModel = _addEvent.RecordEvent(
            trackingNumber,
            Get(data, "status"),
            Get(data, "location"),
            Get(data, "note"),
            occurredAt.Length > 0 ? occurredAt : null);

        var body = viewModel.Ok ? ToBody(viewModel) : _presenter.PresentError(viewModel.Headline);
        return new ControllerResult(StatusFor(viewModel.ErrorKind), body);
    }

    public ControllerResult GetStatus(string trackingNumber)
    {
        return Run(200, () => _getStatus.Execute(trackingNumber));
    }

    private static int StatusFor(string? errorKind)
    {
        switch (errorKind)
        {
            case null: return 200;
            case "not_found": return 404;
            default: return 422;
        }
    }

    private static IReadOnlyDictionary<string, object?> ToBody(TrackingEventViewModel viewModel)
    {
        return new Dictionary<string, object?>
        {
            ["ok"] = viewModel.Ok,
            ["headline"] = viewModel.Headline,
            ["tracking_number"] = viewModel.TrackingNumber,
            ["status"] = viewModel.Status,
            ["delivered"] = viewModel.Delivered,
            ["timeline"] = viewModel.Timeline,
            ["error_kind"] = viewModel.ErrorKind
        };
    }

    private static string Get(IReadOnlyDictionary<string, string> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value : "";
    }

    private ControllerResult Run(int okCode, Func<PackageResponse> action)
    {
        try
        {
            return new ControllerResult(okCode, _presenter.Present(action()));
        }
        catch (PackageNotFoundException e)
        {
            return Error(404, $"Package not found: {e.Message}");
        }
        catch (DuplicatePackageException e)
        {
            return Error(409, $"Package already registered: {e.Message}");
        }
        catch (InvalidRequestException e)
        {
            return Error(422, e.Message);
        }
        catch (DomainException e)
        {
            return Error(422, e.Message);
        }
    }

    private ControllerResult Error(int code, string message)
    {
        return new ControllerResult(code, _presenter.PresentError(message));
    }
}
