using System;

namespace PackageTracker.UseCases;

public class UseCaseException : Exception
{
    public UseCaseException(string message) : base(message)
    {
    }
}

public sealed class PackageNotFoundException : UseCaseException
{
    public PackageNotFoundException(string trackingNumber) : base(trackingNumber)
    {
    }
}

public sealed class DuplicatePackageException : UseCaseException
{
    public DuplicatePackageException(string trackingNumber) : base(trackingNumber)
    {
    }
}

public sealed class InvalidRequestException : UseCaseException
{
    public InvalidRequestException(string message) : base(message)
    {
    }
}
