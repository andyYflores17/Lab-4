using System;

namespace PackageTracker.FrameworksDrivers;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "demo")
        {
            PackageTrackingConsole.RunDemo();
            return 0;
        }

        if (args[0] == "serve")
        {
            PackageTrackingServer.Run(ApplicationComposition.Build().PackageController);
            return 0;
        }

        Console.Error.WriteLine("Usage: dotnet run --project src/PackageTracker.FrameworksDrivers -- [demo|serve]");
        return 1;
    }
}
