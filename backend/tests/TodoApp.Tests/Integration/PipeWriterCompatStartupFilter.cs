using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;

namespace TodoApp.Tests.Integration;

/// <summary>
/// Test-only shim. The net8.0 TestHost package ships a response PipeWriter without <c>UnflushedBytes</c>, which
/// System.Text.Json from a newer shared runtime (e.g. only .NET 10 installed, with RollForward) requires.
/// Swapping the response body feature for a stream-backed one avoids that. It is harmless on a real .NET 8 runtime
/// and is never used outside the test project (Kestrel is not affected).
/// </summary>
public class PipeWriterCompatStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            var original = context.Features.Get<IHttpResponseBodyFeature>()!;
            var wrapper = new StreamResponseBodyFeature(original.Stream);
            context.Features.Set<IHttpResponseBodyFeature>(wrapper);
            try
            {
                await nextMiddleware();
                await wrapper.CompleteAsync();
            }
            finally
            {
                context.Features.Set(original);
            }
        });

        next(app);
    };
}
