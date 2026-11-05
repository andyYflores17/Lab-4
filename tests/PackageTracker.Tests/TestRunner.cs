using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PackageTracker.FrameworksDrivers;
using PackageTracker.UseCases;

namespace PackageTracker.Tests;

internal static class Check
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
        }
    }
}

public static class TestRunner
{
    private sealed record Test(string Name, Action Body);

    private static readonly Test[] Tests = new[]
    {
        new Test("entities reference nothing and use only the base library", EntitiesStayPure),
        new Test("use cases use only entities and the base library", UseCasesStayInner),
        new Test("register, track and deliver through the controller", RegisterTrackDeliver),
        new Test("an event flows through boundary, presenter and view model", ViewModelReachesTheView)
    };

    private static readonly string[] OuterPlatformPrefixes = { "System.Net", "System.Data", "System.IO", "System.Web", "System.Windows" };

    private static readonly Regex UsingDirective =
        new Regex(@"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:\w+\s*=\s*)?([A-Za-z_][\w\.]*)\s*;");

    private static readonly Regex ProjectReference = new Regex(@"<ProjectReference\s+Include=""([^""]+)""");

    public static int Main()
    {
        var failures = 0;
        foreach (var test in Tests)
        {
            try
            {
                test.Body();
                Console.WriteLine($"PASS  {test.Name}");
            }
            catch (Exception error)
            {
                failures++;
                Console.WriteLine($"FAIL  {test.Name}: {error.Message}");
            }
        }

        Console.WriteLine(failures == 0
            ? $"All {Tests.Length} tests passed."
            : $"{failures} of {Tests.Length} tests failed.");
        return failures == 0 ? 0 : 1;
    }

    // ---------- architecture tests: the Dependency Rule, checked from source ----------

    private static void EntitiesStayPure()
    {
        foreach (var (file, ns) in UsingsIn("PackageTracker.Entities"))
        {
            Check.True(IsPlatform(ns), $"{file} uses {ns}");
        }

        var csproj = ProjectFileText("PackageTracker.Entities");
        Check.True(!csproj.Contains("<ProjectReference") && !csproj.Contains("<PackageReference"),
            "PackageTracker.Entities.csproj must reference nothing");
    }

    private static void UseCasesStayInner()
    {
        foreach (var (file, ns) in UsingsIn("PackageTracker.UseCases"))
        {
            var allowed = IsPlatform(ns)
                || ns == "PackageTracker.Entities"
                || ns == "PackageTracker.UseCases";
            Check.True(allowed, $"{file} uses {ns}");
        }

        var csproj = ProjectFileText("PackageTracker.UseCases");
        Check.True(!csproj.Contains("<PackageReference"), "UseCases must not reference packages");
        foreach (Match match in ProjectReference.Matches(csproj))
        {
            Check.True(match.Groups[1].Value.Contains("PackageTracker.Entities"),
                $"UseCases may only reference Entities, found {match.Groups[1].Value}");
        }
    }

    private static bool IsPlatform(string ns)
    {
        var isSystem = ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal);
        var isOuter = OuterPlatformPrefixes.Any(p => ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal));
        return isSystem && !isOuter;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (a folder containing src/).");
    }

    private static string ProjectFileText(string project)
    {
        return File.ReadAllText(Path.Combine(RepoRoot(), "src", project, project + ".csproj"));
    }

    private static List<(string File, string Namespace)> UsingsIn(string project)
    {
        var dir = Path.Combine(RepoRoot(), "src", project);
        var found = new List<(string File, string Namespace)>();
        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(file))
            {
                var match = UsingDirective.Match(line);
                if (match.Success)
                {
                    found.Add((Path.GetFileName(file), match.Groups[1].Value));
                }
            }
        }

        return found;
    }

    // ---------- behaviour tests ----------

    private static Dictionary<string, string> Data(params (string Key, string Value)[] pairs)
    {
        return pairs.ToDictionary(p => p.Key, p => p.Value);
    }

    private static void RegisterTrackDeliver()
    {
        var c = ApplicationComposition.Build().PackageController;

        Check.Equal(201, c.Register(Data(("tracking_number", "T1"), ("carrier", "UPS"), ("recipient", "Ada"))).StatusCode, "register");
        Check.Equal(409, c.Register(Data(("tracking_number", "T1"), ("carrier", "UPS"), ("recipient", "Ada"))).StatusCode, "duplicate");

        var moved = c.AddEvent("T1", Data(("status", "in_transit"), ("location", "Atlanta")));
        Check.Equal(200, moved.StatusCode, "in_transit event");
        Check.Equal("in_transit", Convert.ToString(moved.Body["status"]), "status after event");

        Check.Equal(422, c.AddEvent("T1", Data(("status", "bogus"), ("location", "x"))).StatusCode, "unknown status");

        Check.Equal(200, c.AddEvent("T1", Data(("status", "delivered"), ("location", "Jacksonville"))).StatusCode, "delivered");
        Check.Equal(422, c.AddEvent("T1", Data(("status", "in_transit"), ("location", "x"))).StatusCode, "event after delivery");

        var status = c.GetStatus("T1");
        Check.Equal(200, status.StatusCode, "get status");
        Check.True((bool)status.Body["delivered"]!, "package should be delivered");
        Check.Equal(404, c.GetStatus("nope").StatusCode, "unknown package");
    }

    private static void ViewModelReachesTheView()
    {
        var app = ApplicationComposition.Build();
        app.RegisterPackage.Execute(new RegisterPackageRequest("T9", "DHL", "Sam"));

        var ok = app.TrackingEventController.RecordEvent("T9", "in_transit", "Atlanta, GA");
        Check.True(ok.Ok, "event should be recorded");
        Check.Equal("in_transit", ok.Status, "view model status");
        Check.Equal(1, ok.Timeline.Count, "timeline length");

        var bad = app.TrackingEventController.RecordEvent("T9", "nonsense", "x");
        Check.Equal("rejected", bad.ErrorKind, "unknown status is rejected");

        var missing = app.TrackingEventController.RecordEvent("nope", "in_transit", "x");
        Check.Equal("not_found", missing.ErrorKind, "unknown package");
    }
}
