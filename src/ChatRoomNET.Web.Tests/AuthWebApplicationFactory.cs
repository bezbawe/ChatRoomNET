using ChatRoomNET.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChatRoomNET.Web.Tests;

public class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"chatroomnet-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "ChatRoomNET.Tests",
                ["Jwt:Audience"] = "ChatRoomNET.Tests.Client",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Jwt:Key"] = "test-signing-key-not-for-production-use-32bytes!!"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ChatDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ChatDbContext>>();

            services.AddDbContext<ChatDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
