using ToolingExtractor.Application.Services;
using ToolingExtractor.Web.Services;

namespace ToolingExtractor.Web.Middleware;

public class BrowserSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, BrowserSessionService sessions, DataResetService reset)
    {
        if (!BrowserSessionService.IsStaticRequest(ctx.Request.Path))
        {
            if (BrowserSessionService.IsApiRequest(ctx.Request.Path))
                sessions.AttachApiSession(ctx);
            else
                await sessions.EnsurePageSessionAsync(ctx, reset);
        }

        await next(ctx);
    }
}
