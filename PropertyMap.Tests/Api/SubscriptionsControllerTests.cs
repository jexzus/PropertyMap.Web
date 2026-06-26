using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;
using Xunit;

namespace PropertyMap.Tests.Api;

public class SubscriptionsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SubscriptionsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_WithoutSubscription_Returns404()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await client.GetAsync("/api/subscriptions/mine");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Subscribe_ThenGetMine_ReturnsSubscription()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var plans = await (await client.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");

        var subscribeResp = await client.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));
        Assert.Equal(HttpStatusCode.OK, subscribeResp.StatusCode);

        var mineResp = await client.GetAsync("/api/subscriptions/mine");
        var sub = await mineResp.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(sub);
        Assert.Equal(profesional.Id, sub!.PlanId);
        Assert.Equal("Profesional", sub.PlanNombre);
    }

    [Fact]
    public async Task Subscribe_Twice_ReplacesExistingSubscription()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var plans = await (await client.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var gratuito = plans!.First(p => p.Slug == "gratuito");
        var premium = plans!.First(p => p.Slug == "premium");

        await client.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(gratuito.Id));
        await client.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(premium.Id));

        var sub = await (await client.GetAsync("/api/subscriptions/mine")).Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.Equal(premium.Id, sub!.PlanId);
    }
}
