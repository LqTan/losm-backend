using Hangfire.Dashboard;

namespace LocationSearch.Api;

public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public const string InternalProxyHeader = "X-Internal-Proxy";

    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var http = context.GetHttpContext();

        if (http.Request.Headers.TryGetValue(InternalProxyHeader, out var internalMarker)
            && internalMarker.ToString() == "1")
        {
            return true;
        }

        return http.User?.Identity?.IsAuthenticated == true
            && http.User.IsInRole("Admin");
    }
}