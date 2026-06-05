using System.Text.Json;
using ToolingExtractor.Application;
using ToolingExtractor.Application.Services;

namespace ToolingExtractor.Web.Services;

public class BrowserSessionService
{
    public const string SessionCookieName = "te_browser_session";
    public const string TabIdHeaderName = "X-Te-Tab-Id";

    private static readonly SemaphoreSlim NewSessionLock = new(1, 1);

    private readonly string _workspaceDir = ToolingPaths.ResolveContentPath("./data/session-workspaces");

    public static bool IsStaticRequest(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)) return true;
        return value.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".map", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsApiRequest(PathString path) =>
        path.StartsWithSegments("/api");

    /// <summary>HTML page requests only — may reset DB when session cookie is missing (new tab).</summary>
    public async Task EnsurePageSessionAsync(HttpContext ctx, DataResetService reset, CancellationToken ct = default)
    {
        var existing = ctx.Request.Cookies[SessionCookieName];
        if (!string.IsNullOrEmpty(existing))
        {
            ctx.Items[SessionCookieName] = existing;
            return;
        }

        await NewSessionLock.WaitAsync(ct);
        try
        {
            existing = ctx.Request.Cookies[SessionCookieName];
            if (!string.IsNullOrEmpty(existing))
            {
                ctx.Items[SessionCookieName] = existing;
                return;
            }

            await reset.ResetAllAsync(ct);
            var sessionId = Guid.NewGuid().ToString("N");
            AppendSessionCookie(ctx, sessionId);
            ctx.Items[SessionCookieName] = sessionId;
        }
        finally
        {
            NewSessionLock.Release();
        }
    }

    /// <summary>API requests — attach cookie if present; never reset data (avoids sendBeacon/fetch races).</summary>
    public void AttachApiSession(HttpContext ctx)
    {
        var existing = ctx.Request.Cookies[SessionCookieName];
        if (!string.IsNullOrEmpty(existing))
            ctx.Items[SessionCookieName] = existing;
    }

    public async Task ResetOnReloadAsync(HttpContext ctx, DataResetService reset, CancellationToken ct = default)
    {
        var previousId = ResolveWorkspaceKey(ctx);
        await reset.ResetAllAsync(ct);

        if (!string.IsNullOrEmpty(previousId))
            DeleteWorkspaceFile(previousId);

        var sessionId = Guid.NewGuid().ToString("N");
        AppendSessionCookie(ctx, sessionId);
        ctx.Items[SessionCookieName] = sessionId;
    }

    public JsonElement? GetWorkspace(HttpContext ctx)
    {
        var key = ResolveWorkspaceKey(ctx);
        if (string.IsNullOrEmpty(key))
            return null;

        var path = WorkspacePath(key);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SaveWorkspace(HttpContext ctx, string jsonBody)
    {
        var key = ResolveWorkspaceKey(ctx);
        if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(jsonBody))
            return;

        try
        {
            File.WriteAllText(WorkspacePath(key), jsonBody);
        }
        catch
        {
            // best effort
        }
    }

    private string ResolveWorkspaceKey(HttpContext ctx)
    {
        var cookie = GetSessionId(ctx);
        if (!string.IsNullOrEmpty(cookie))
            return cookie;

        var tabId = ctx.Request.Headers[TabIdHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tabId))
            return "tab_" + SanitizeKey(tabId);

        return string.Empty;
    }

    private string GetSessionId(HttpContext ctx) =>
        ctx.Items[SessionCookieName] as string
        ?? ctx.Request.Cookies[SessionCookieName]
        ?? string.Empty;

    private static void AppendSessionCookie(HttpContext ctx, string sessionId)
    {
        ctx.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true
        });
    }

    private static string SanitizeKey(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Length > 120 ? value[..120] : value;
    }

    private string WorkspacePath(string key) =>
        Path.Combine(_workspaceDir, SanitizeKey(key) + ".json");

    private void DeleteWorkspaceFile(string key)
    {
        try
        {
            var path = WorkspacePath(key);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
