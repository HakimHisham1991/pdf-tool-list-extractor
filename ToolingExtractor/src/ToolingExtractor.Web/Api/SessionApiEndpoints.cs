using System.Text.Json;
using ToolingExtractor.Application.Services;
using ToolingExtractor.Web.Services;

namespace ToolingExtractor.Web.Api;

public static class SessionApiEndpoints
{
    public static void MapSessionApi(this WebApplication app)
    {
        app.MapPost("/api/session/reload", async (
            HttpContext ctx,
            BrowserSessionService sessions,
            DataResetService reset) =>
        {
            await sessions.ResetOnReloadAsync(ctx, reset);
            return Results.Ok(new { ok = true });
        }).DisableAntiforgery();

        app.MapGet("/api/session/workspace", (HttpContext ctx, BrowserSessionService sessions) =>
        {
            var workspace = sessions.GetWorkspace(ctx);
            return workspace == null ? Results.NoContent() : Results.Json(workspace.Value);
        });

        app.MapMethods("/api/session/workspace", ["PUT", "POST"], async (
            HttpContext ctx,
            BrowserSessionService sessions) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(json))
                return Results.BadRequest();

            try
            {
                JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON." });
            }

            sessions.SaveWorkspace(ctx, json);
            return Results.Ok(new { saved = true });
        }).DisableAntiforgery();
    }
}
