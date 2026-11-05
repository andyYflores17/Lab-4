namespace PackageTracker.Entities;

public enum TrackingStatus
{
    Registered,
    InTransit,
    OutForDelivery,
    Delivered,
    DeliveryException
}
