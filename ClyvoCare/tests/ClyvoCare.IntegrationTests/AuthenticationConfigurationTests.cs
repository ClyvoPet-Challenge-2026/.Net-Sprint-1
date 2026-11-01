using System.Security.Cryptography;
using ClyvoCare.API.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClyvoCare.IntegrationTests;

public class AuthenticationConfigurationTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("private")]
    [InlineData("weak")]
    [InlineData("malformed")]
    public void BearerOptions_UnsupportedPublicKey_FailsConfiguration(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pem");
        try
        {
            if (scenario == "private") File.WriteAllText(path, "-----BEGIN PRIVATE KEY-----");
            if (scenario == "malformed") File.WriteAllText(path, "invalid PEM");
            if (scenario == "weak")
            {
                using var key = RSA.Create(1024);
                File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem());
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Jwt:PublicKeyPath"] = path }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddClyvoCareAuthentication(configuration);
            using var provider = services.BuildServiceProvider();
            void LoadOptions() => provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);

            if (scenario == "missing")
                Assert.Throws<FileNotFoundException>(LoadOptions);
            else if (scenario == "malformed")
                Assert.Throws<ArgumentException>(LoadOptions);
            else
                Assert.Throws<InvalidOperationException>(LoadOptions);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
