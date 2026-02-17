namespace BookingPlatform.Web.Middleware;

public sealed class CorrelationIdMiddleware : IMiddleware
{
    private const string CorrelationHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var incoming = context.Request.Headers[CorrelationHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            context.TraceIdentifier = incoming;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
