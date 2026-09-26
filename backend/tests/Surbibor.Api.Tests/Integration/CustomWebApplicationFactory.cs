using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Surbibor.Api.Tests.Unit;
using Surbibor.Infrastructure;
using Surbibor.Infrastructure.Email;

namespace Surbibor.Api.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs reads these eagerly (before ConfigureAppConfiguration below is applied) to
        // set up token validation and the Redis backplane, so they must be host settings to be
        // seen there. An empty Redis connection string disables the backplane.
        builder.UseSetting("ConnectionStrings:Redis", "");
        builder.UseSetting("Jwt:Secret", "test-only-secret-do-not-use-in-production-min-32-chars");
        builder.UseSetting("Jwt:Issuer", "Surbibor");
        builder.UseSetting("Jwt:Audience", "Surbibor");

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
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}
