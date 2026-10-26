using System.Net;
using Microsoft.Extensions.Configuration;
using PoliedroHangFire.Infrastructure.External.Billing.Adapters.DianResolutionBilling;

namespace PoliedroHangFire.Tests;

public class DianResolutionServiceTests
{
    private static DianResolutionService Service(FakeHttp handler) =>
        new(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["External:DianresolutionUrl"] = "http://localhost:5001/api/v1/dianresolution"
        }).Build());

    [Fact]
    public async Task ReadsCatalogAndKeepsOnlyActiveResolutions()
    {
        var http = new FakeHttp("""
            [{"resolutionId":1,"description":"PUNTO FRIO","active":true},
             {"resolutionId":2,"description":"INACTIVA","active":false}]
            """);

        var resolutions = await Service(http).GetActiveResolutionsAsync();

        Assert.Equal(new[] { 1 }, resolutions.Select(r => r.ResolutionId));
        Assert.Equal("PUNTO FRIO", resolutions[0].Description);
        Assert.Equal("/api/v1/dianresolution", http.Paths.Single());
    }

    [Fact]
    public async Task EmptyCatalogIsNotAnError()
    {
        Assert.Empty(await Service(new FakeHttp("[]")).GetActiveResolutionsAsync());
        Assert.Empty(await Service(new FakeHttp("", HttpStatusCode.NoContent)).GetActiveResolutionsAsync());
    }

    private class FakeHttp(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
