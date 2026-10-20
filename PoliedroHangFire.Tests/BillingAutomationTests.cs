using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PoliedroHangFire.Infrastructure.External.Billing;

namespace PoliedroHangFire.Tests;

public class BillingAutomationTests
{
    private static BillingAutomation Service(FakeHttp handler, bool enabled = true)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:BaseUrl"] = "http://localhost:15068", ["Billing:AdminKey"] = "test-only-unused-secret",
            ["Billing:EmissionEnabled"] = enabled.ToString()
        }).Build();
        return new BillingAutomation(new HttpClient(handler), config, null!, NullLogger<BillingAutomation>.Instance);
    }

    [Fact]
    public async Task DisabledInstallationNeverQueriesOrEmits()
    {
        var http = new FakeHttp();
        await Service(http, false).ProcessResolutionAsync(1);
        Assert.Empty(http.Paths);
    }

    [Theory]
    [InlineData("{\"allowed\":false}")]
    [InlineData("{}")]
    public async Task IneligibleClientNeverReadsInvoicesOrEmits(string eligibility)
    {
        var http = new FakeHttp(eligibility);
        await Service(http).ProcessResolutionAsync(12);
        Assert.Equal(new[] { "/api/v1/admin/automation/resolutions/12/eligibility" }, http.Paths);
    }

    [Fact]
    public async Task EmptyPendingDoesNotEmit()
    {
        var http = new FakeHttp("{\"allowed\":true}", "{\"success\":true,\"data\":[]}");
        await Service(http).ProcessResolutionAsync(12);
        Assert.Equal(2, http.Paths.Count);
    }

    [Fact]
    public async Task EligibleClientUsesNewBillingContract()
    {
        var http = new FakeHttp("{\"allowed\":true}", "{\"success\":true,\"data\":[{\"number\":\"TEST\"}]}", "[{\"status\":true}]");
        await Service(http).ProcessResolutionAsync(12);
        Assert.Equal("/api/v1/billing/12", http.Paths.Last());
        Assert.Contains("\"data\":[", http.LastBody);
        Assert.Equal(3, http.Paths.Count);
    }

    [Fact]
    public async Task PartialBillingFailureIsNotReportedAsSuccess()
    {
        var http = new FakeHttp("{\"allowed\":true}", "{\"success\":true,\"data\":[{}]}", "[{\"status\":false}]");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(http).ProcessResolutionAsync(12));
        Assert.Equal(3, http.Paths.Count);
    }

    private class FakeHttp(params string[] replies) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public string? LastBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.True(request.Headers.Contains("X-Billing-Admin-Key"));
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (request.Content is not null) LastBody = await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(replies[Paths.Count - 1], System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
