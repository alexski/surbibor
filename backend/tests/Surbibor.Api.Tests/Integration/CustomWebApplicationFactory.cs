using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Surbibor.Infrastructure;

namespace Surbibor.Api.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Disable the Redis SignalR backplane for tests; not needed for a single test host.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = null,
                ["Jwt:Secret"] = "test-only-secret-do-not-use-in-production-min-32-chars",
                ["Jwt:Issuer"] = "Surbibor",
                ["Jwt:Audience"] = "Surbibor",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SurbiborDbContext>(options => options.UseInMemoryDatabase(_dbName));
        });
    }
}
