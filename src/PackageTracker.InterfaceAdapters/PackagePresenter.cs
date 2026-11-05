using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PackageTracker.UseCases;

namespace PackageTracker.InterfaceAdapters;

public sealed class PackagePresenter
{
    public IReadOnlyDictionary<string, object?> Present(PackageResponse response)
    {
        var events = response.Events
            .Select(e => (object)new Dictionary<string, object?>
            {
                ["status"] = e.Status,
                ["location"] = e.Location,
                ["occurred_at"] = e.OccurredAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                ["note"] = e.Note
            })
            .ToList();

        return new Dictionary<string, object?>
        {
            ["tracking_number"] = response.TrackingNumber,
            ["carrier"] = response.Carrier,
            ["recipient"] = response.Recipient,
            ["status"] = response.Status,
            ["delivered"] = response.Delivered,
            ["events"] = events
        };
    }

    public IReadOnlyDictionary<string, object?> PresentError(string message)
    {
        return new Dictionary<string, object?> { ["error"] = message };
    }
}
