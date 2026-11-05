using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PackageTracker.InterfaceAdapters;

namespace PackageTracker.FrameworksDrivers;

public static class PackageTrackingServer
{
    private static readonly Regex EventsRoute = new Regex("^/packages/([^/]+)/events$");
    private static readonly Regex PackageRoute = new Regex("^/packages/([^/]+)$");

    public static void Run(PackageTrackingController controller, string prefix = "http://localhost:8000/")
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Console.WriteLine($"Package tracker listening on {prefix}");

        while (true)
        {
            var context = listener.GetContext();
            Handle(context, controller);
        }
    }

    private static void Handle(HttpListenerContext context, PackageTrackingController controller)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var method = context.Request.HttpMethod;
        var eventsMatch = EventsRoute.Match(path);
        var packageMatch = PackageRoute.Match(path);

        ControllerResult result;
        if (method == "POST" && path == "/packages")
        {
            result = controller.Register(ReadJsonBody(context.Request));
        }
        else if (method == "POST" && eventsMatch.Success)
        {
            result = controller.AddEvent(
                Uri.UnescapeDataString(eventsMatch.Groups[1].Value),
                ReadJsonBody(context.Request));
        }
        else if (method == "GET" && packageMatch.Success)
        {
            result = controller.GetStatus(Uri.UnescapeDataString(packageMatch.Groups[1].Value));
        }
        else
        {
            result = new ControllerResult(404, new Dictionary<string, object?> { ["error"] = "Not found" });
        }

        Write(context.Response, result);
    }

    private static Dictionary<string, string> ReadJsonBody(HttpListenerRequest request)
    {
        var values = new Dictionary<string, string>();
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var text = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(text))
            {
                return values;
            }

            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? ""
                    : property.Value.ToString();
            }
        }
        catch (JsonException)
        {
            // an unreadable body is treated as an empty one; the use case reports what is missing
        }

        return values;
    }

    private static void Write(HttpListenerResponse response, ControllerResult result)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize((object)result.Body));
        response.StatusCode = result.StatusCode;
        response.ContentType = "application/json";
        response.ContentLength64 = payload.Length;
        response.OutputStream.Write(payload, 0, payload.Length);
        response.OutputStream.Close();
    }
}
