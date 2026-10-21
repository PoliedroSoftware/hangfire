namespace PoliedroHangFire.WebApi;

public static class HangfireStorageConfiguration
{
    public static string TablesPrefix(IConfiguration configuration)
    {
        var prefix = configuration["Hangfire:TablesPrefix"] ?? "Hangfire";
        if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, @"\A[A-Za-z][A-Za-z0-9_]{0,39}\z"))
            throw new InvalidOperationException("Hangfire:TablesPrefix must contain only letters, digits and underscores.");
        return prefix;
    }
}
