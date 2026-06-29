using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace PropertyMap.Tests.Api;

public class RateLimitTestWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true"
            });
        });
    }
}
