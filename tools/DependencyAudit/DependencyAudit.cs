// Dependency audit for the two inner layers (Entities, UseCases).
// For every using directive it records which circle the namespace names and whether
// that points inward; it also audits the project files (the compiler-enforced half of
// the rule) and runs the class-failure-catalog checks that can be automated.
// Exit code 1 if anything is flagged.
//
//   dotnet run --project tools/DependencyAudit
//   dotnet run --project tools/DependencyAudit -- --only-problems
//   dotnet run --project tools/DependencyAudit -- <path-to-repo-root>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PackageTracker.Tools;

public static class DependencyAudit
{
    private sealed record UsingRow(string Rel, int Line, string Import, string Circle, string Verdict);

    private sealed record RefRow(string Rel, int Line, string Name, string Circle, string Verdict, bool IsPackage);

    private const string Ok = "inward — OK";
    private const string Bad = "VIOLATION — fix required";

    private static readonly Regex UsingDirective =
        new Regex(@"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:\w+\s*=\s*)?([A-Za-z_][\w\.]*)\s*;");

    private static readonly Regex TypeDeclaration = new Regex(@"\b(?:class|interface|record|struct|enum)\s+(\w+)");

    private static readonly Regex BaseList = new Regex(
        @"^\s*(?:(?:public|internal|private|protected|sealed|abstract|static|partial)\s+)*(?:class|record|struct|interface)\s+(\w+)(?:<[^>]*>)?(?:\([^)]*\))?\s*:\s*([^{;\r\n]+)",
        RegexOptions.Multiline);

    private static readonly Regex ProjectReference = new Regex(@"<ProjectReference\s+Include=""([^""]+)""");

    private static readonly Regex PackageReference = new Regex(@"<PackageReference\s+Include=""([^""]+)""");

    private static readonly Regex LineComment = new Regex("//.*$");

    private static readonly string[] OuterPlatformPrefixes = { "System.Net", "System.Data", "System.IO", "System.Web", "System.Windows" };

    private static readonly HashSet<string> BclBases = new HashSet<string>
    {
        "Exception", "ApplicationException", "ArgumentException", "InvalidOperationException",
        "object", "IEquatable", "IComparable", "IDisposable", "IEnumerable"
    };

    private static readonly Dictionary<string, HashSet<string>> Allowed = new Dictionary<string, HashSet<string>>
    {
        ["entities"] = new HashSet<string> { "entities", "platform" },
        ["use_cases"] = new HashSet<string> { "entities", "use cases", "platform" }
    };

    private static readonly Dictionary<string, string> Label = new Dictionary<string, string>
    {
        ["entities"] = "inner (entities)",
        ["use cases"] = "inner (use cases)",
        ["interface adapters"] = "outer (interface adapters)",
        ["frameworks & drivers"] = "outer (frameworks)",
        ["platform"] = "stable platform (BCL)",
        ["third-party"] = "outer (frameworks)"
    };

    private static readonly Dictionary<string, string> ProjectCircle = new Dictionary<string, string>
    {
        ["PackageTracker.Entities"] = "entities",
        ["PackageTracker.UseCases"] = "use cases",
        ["PackageTracker.InterfaceAdapters"] = "interface adapters",
        ["PackageTracker.FrameworksDrivers"] = "frameworks & drivers"
    };

    public static int Main(string[] args)
    {
        var onlyProblems = args.Contains("--only-problems");
        var rootArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        var src = Path.Combine(rootArg ?? FindRoot(), "src");
        var layers = new[] { ("PackageTracker.Entities", "entities"), ("PackageTracker.UseCases", "use_cases") };

        // pass 1: every type declared in the inner layers (to recognise legitimate base types)
        var declared = new HashSet<string>();
        foreach (var layer in layers)
        {
            foreach (var file in CsFiles(src, layer.Item1))
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    foreach (Match match in TypeDeclaration.Matches(StripComment(line)))
                    {
                        declared.Add(match.Groups[1].Value);
                    }
                }
            }
        }

        var rows = new List<UsingRow>();
        var refs = new List<RefRow>();
        var noImportFiles = new List<string>();
        var noRefProjects = new List<string>();
        var baseHits = new List<string>();
        var fileCount = 0;

        foreach (var layer in layers)
        {
            var key = layer.Item2;
            foreach (var file in CsFiles(src, layer.Item1))
            {
                fileCount++;
                var rel = Rel(src, file);
                var lines = File.ReadAllLines(file).Select(StripComment).ToArray();
                var count = 0;
                for (var i = 0; i < lines.Length; i++)
                {
                    var match = UsingDirective.Match(lines[i]);
                    if (!match.Success)
                    {
                        continue;
                    }

                    count++;
                    var ns = match.Groups[1].Value;
                    var circle = CircleOf(ns);
                    rows.Add(new UsingRow(rel, i + 1, ns, circle, Allowed[key].Contains(circle) ? Ok : Bad));
                }

                if (count == 0)
                {
                    noImportFiles.Add(rel);
                }

                var text = string.Join("\n", lines);
                foreach (Match match in BaseList.Matches(text))
                {
                    var lineNumber = text.Take(match.Groups[1].Index).Count(c => c == '\n') + 1;
                    foreach (var part in SplitTopLevel(match.Groups[2].Value))
                    {
                        var name = BaseName(part);
                        if (!declared.Contains(name) && !BclBases.Contains(name))
                        {
                            baseHits.Add($"{rel}:{lineNumber} class `{match.Groups[1].Value}` inherits `{name}`, which is not declared in the inner layers or the BCL");
                        }
                    }
                }
            }

            var projectDir = Path.Combine(src, layer.Item1);
            var projectFiles = Directory.GetFiles(projectDir, "*.csproj").OrderBy(f => f, StringComparer.Ordinal).ToList();
            var foundRef = false;
            foreach (var projectFile in projectFiles)
            {
                var rel = Rel(src, projectFile);
                var lines = File.ReadAllLines(projectFile);
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in ProjectReference.Matches(lines[i]))
                    {
                        foundRef = true;
                        var name = Path.GetFileNameWithoutExtension(match.Groups[1].Value.Replace('\\', '/'));
                        var circle = ProjectCircle.TryGetValue(name, out var known) ? known : "third-party";
                        refs.Add(new RefRow(rel, i + 1, name, circle, Allowed[key].Contains(circle) ? Ok : Bad, false));
                    }

                    foreach (Match match in PackageReference.Matches(lines[i]))
                    {
                        foundRef = true;
                        refs.Add(new RefRow(rel, i + 1, match.Groups[1].Value, "third-party", Bad, true));
                    }
                }
            }

            if (!foundRef)
            {
                noRefProjects.AddRange(projectFiles.Select(f => Path.GetFileName(f)));
            }
        }

        var badRows = rows.Where(x => x.Verdict == Bad).ToList();
        var badRefs = refs.Where(x => x.Verdict == Bad).ToList();

        Console.WriteLine("| File | Import | Which circle does it name? | Verdict (inward / VIOLATION) |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var row in rows.Where(x => !onlyProblems || x.Verdict == Bad))
        {
            Console.WriteLine($"| {Path.GetFileName(row.Rel)} | {row.Import} | {Label[row.Circle]} | {row.Verdict} |");
        }

        if (noImportFiles.Count > 0 && !onlyProblems)
        {
            Console.WriteLine();
            Console.WriteLine($"Files with no imports: {string.Join(", ", noImportFiles)}");
        }

        Console.WriteLine();
        Console.WriteLine("Project references (the compiler-enforced half of the rule)");
        Console.WriteLine("| Project file | Reference | Which circle does it name? | Verdict (inward / VIOLATION) |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var reference in refs.Where(x => !onlyProblems || x.Verdict == Bad))
        {
            Console.WriteLine($"| {Path.GetFileName(reference.Rel)} | {reference.Name} | {Label[reference.Circle]} | {reference.Verdict} |");
        }

        if (noRefProjects.Count > 0 && !onlyProblems)
        {
            Console.WriteLine();
            Console.WriteLine($"Projects with no references: {string.Join(", ", noRefProjects)}");
        }

        Console.WriteLine();
        Console.WriteLine("Violations found (file + line): " + (badRows.Count + badRefs.Count == 0 ? "none" : ""));
        foreach (var row in badRows)
        {
            Console.WriteLine($"- {row.Rel} line {row.Line}: uses `{row.Import}` ({Label[row.Circle]})");
        }

        foreach (var reference in badRefs)
        {
            Console.WriteLine($"- {reference.Rel} line {reference.Line}: references `{reference.Name}` ({Label[reference.Circle]})");
        }

        var signatureHits = badRows
            .Select(x => $"{x.Rel}:{x.Line} namespace `{x.Import}` is in scope, so its types can reach signatures")
            .ToList();
        var sdkHits = rows.Where(x => x.Circle == "third-party")
            .Select(x => $"{x.Rel}:{x.Line} uses third-party namespace `{x.Import}`")
            .Concat(refs.Where(x => x.IsPackage).Select(x => $"{x.Rel}:{x.Line} references package `{x.Name}`"))
            .ToList();

        Console.WriteLine();
        Console.WriteLine("Class failure catalog (automated checks)");
        PrintCatalog("Framework types in use-case signatures / fields", signatureHits);
        PrintCatalog("Entities or use cases inheriting framework/ORM base classes", baseHits);
        PrintCatalog("Third-party / model-SDK references inside the inner layers", sdkHits);

        var byCircle = rows.GroupBy(x => x.Circle).ToDictionary(g => g.Key, g => g.Count());
        int Count(string circle) => byCircle.TryGetValue(circle, out var n) ? n : 0;
        var outer = rows.Count - Count("platform") - Count("entities") - Count("use cases");
        var violations = badRows.Count + badRefs.Count;
        var catalog = signatureHits.Count + baseHits.Count + sdkHits.Count;

        Console.WriteLine();
        Console.WriteLine($"Breakdown: platform {Count("platform")}, entities {Count("entities")}, use cases {Count("use cases")}, outer circles {outer}.");
        Console.WriteLine($"Summary: {rows.Count} using directives audited across {fileCount} files; {violations} violations; {catalog} catalog hits.");
        return violations > 0 || catalog > 0 ? 1 : 0;
    }

    private static void PrintCatalog(string label, List<string> hits)
    {
        Console.WriteLine($"- {label}: " + (hits.Count == 0 ? "none" : ""));
        foreach (var hit in hits)
        {
            Console.WriteLine($"    * {hit}");
        }
    }

    private static string CircleOf(string ns)
    {
        if (ns == "PackageTracker.Entities" || ns.StartsWith("PackageTracker.Entities.", StringComparison.Ordinal)) return "entities";
        if (ns == "PackageTracker.UseCases" || ns.StartsWith("PackageTracker.UseCases.", StringComparison.Ordinal)) return "use cases";
        if (ns == "PackageTracker.InterfaceAdapters" || ns.StartsWith("PackageTracker.InterfaceAdapters.", StringComparison.Ordinal)) return "interface adapters";
        if (ns == "PackageTracker.FrameworksDrivers" || ns.StartsWith("PackageTracker.FrameworksDrivers.", StringComparison.Ordinal)) return "frameworks & drivers";
        if (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal))
        {
            var outerPlatform = OuterPlatformPrefixes.Any(p => ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal));
            return outerPlatform ? "third-party" : "platform";
        }

        return "third-party";
    }

    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = "";
        foreach (var ch in text)
        {
            if (ch == '<') depth++;
            else if (ch == '>') depth--;

            if (ch == ',' && depth == 0)
            {
                parts.Add(current);
                current = "";
            }
            else
            {
                current += ch;
            }
        }

        parts.Add(current);
        return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    private static string BaseName(string part)
    {
        var withoutGenerics = Regex.Replace(part, "<.*>", "");
        var lastDot = withoutGenerics.LastIndexOf('.');
        return (lastDot >= 0 ? withoutGenerics.Substring(lastDot + 1) : withoutGenerics).Trim();
    }

    private static string StripComment(string line) => LineComment.Replace(line, "");

    private static string Rel(string src, string file) => Path.GetRelativePath(src, file).Replace('\\', '/');

    private static bool IsBuildOutput(string relative)
    {
        var first = relative.Replace('\\', '/').Split('/')[0];
        return first == "obj" || first == "bin";
    }

    private static List<string> CsFiles(string src, string project)
    {
        var dir = Path.Combine(src, project);
        return Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(Path.GetRelativePath(dir, f)))
            .OrderBy(f => Rel(src, f), StringComparer.Ordinal)
            .ToList();
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (a folder containing src/).");
    }
}
