using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PlansControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlansControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetActive_ReturnsSeededPlans()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var plans = await resp.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.Contains(plans!, p => p.Slug == "gratuito");
        Assert.Contains(plans!, p => p.Slug == "profesional");
        Assert.Contains(plans!, p => p.Slug == "premium");
    }
}
