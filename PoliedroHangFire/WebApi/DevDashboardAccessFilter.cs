using Hangfire.Dashboard;

namespace PoliedroHangFire.WebApi;

public class DevDashboardAccessFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
