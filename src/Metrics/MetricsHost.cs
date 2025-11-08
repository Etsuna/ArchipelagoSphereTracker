using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

public static class MetricsHost
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        var port = Environment.GetEnvironmentVariable("METRICS_PORT") ?? "8080";
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        var app = builder.Build();

        app.MapGet("/metrics", async () =>
        {
            var payload = await MetricsCollector.BuildAsync();
            return Results.Text(payload, "text/plain; version=0.0.4");
        });

        await app.RunAsync();
    }
}
