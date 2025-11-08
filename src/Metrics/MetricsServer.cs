using System.Net;
using System.Text;

public static class MetricsServer
{
    public static async Task RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("METRICS_PORT") ?? "8080";
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://0.0.0.0:{port}/metrics/");
        listener.Start();
        Console.WriteLine($"[metrics] listening on {port}");

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            if (ctx.Request.Url!.AbsolutePath != "/metrics")
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                continue;
            }

            var payload = await MetricsCollector.BuildAsync();
            var bytes = Encoding.UTF8.GetBytes(payload);

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain; version=0.0.4";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }
    }
}
