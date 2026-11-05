using System;

namespace PackageTracker.Entities;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

public sealed class InvalidTrackingEventException : DomainException
{
    public InvalidTrackingEventException(string message) : base(message)
    {
    }
}
