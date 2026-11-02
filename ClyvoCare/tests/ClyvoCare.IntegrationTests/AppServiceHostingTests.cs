using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ClyvoCare.IntegrationTests;

public class AppServiceHostingTests
{
    [Theory]
    [InlineData("10.1.2.3", true, true)]
    [InlineData("172.16.1.2", true, true)]
    [InlineData("192.168.1.2", true, true)]
    [InlineData("::ffff:10.1.2.3", true, true)]
    [InlineData("::ffff:172.16.1.2", true, true)]
    [InlineData("::ffff:192.168.1.2", true, true)]
    [InlineData("203.0.113.10", true, false)]
    [InlineData("::ffff:203.0.113.10", true, false)]
    [InlineData("2001:db8::1", true, false)]
    [InlineData("10.1.2.3", false, false)]
    public async Task ForwardedHttps_OnlyTrustedAppServiceProxyAvoidsRedirect(string proxy,
        bool enabled, bool trusted)
    {
        using var factory = new CustomWebApplicationFactory();
        using var application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AppService:Enabled"] = enabled.ToString(),
                    ["https_port"] = "443"
                }));
        });
        var context = await application.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
            context.Request.Scheme = "http";
            context.Request.Host = new("clyvocare-dotnet.azurewebsites.net");
            context.Request.Path = "/api/cidades";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
        });

        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
        Assert.Equal(trusted ? 200 : 307, context.Response.StatusCode);
        if (!trusted)
            Assert.Equal("https://clyvocare-dotnet.azurewebsites.net/api/cidades",
                context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Production_DoesNotExposeDevelopmentSwagger()
    {
        using var factory = new CustomWebApplicationFactory();
        using var application = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = application.CreateClient(new() { BaseAddress = new("https://localhost") });

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
