using System;
using PackageTracker.Entities;

namespace PackageTracker.UseCases;

/// <summary>The wire names used in requests and responses ("in_transit", ...).</summary>
public static class TrackingStatusNames
{
    public static string ToWireName(TrackingStatus status)
    {
        switch (status)
        {
            case TrackingStatus.Registered: return "registered";
            case TrackingStatus.InTransit: return "in_transit";
            case TrackingStatus.OutForDelivery: return "out_for_delivery";
            case TrackingStatus.Delivered: return "delivered";
            case TrackingStatus.DeliveryException: return "exception";
            default: throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    public static bool TryParse(string? wireName, out TrackingStatus status)
    {
        switch (wireName)
        {
            case "registered": status = TrackingStatus.Registered; return true;
            case "in_transit": status = TrackingStatus.InTransit; return true;
            case "out_for_delivery": status = TrackingStatus.OutForDelivery; return true;
            case "delivered": status = TrackingStatus.Delivered; return true;
            case "exception": status = TrackingStatus.DeliveryException; return true;
            default: status = default; return false;
        }
    }
}
