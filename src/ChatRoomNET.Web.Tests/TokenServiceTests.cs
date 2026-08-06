using System.IdentityModel.Tokens.Jwt;
using ChatRoomNET.Web.Domain;
using ChatRoomNET.Web.Services;
using Microsoft.Extensions.Configuration;

namespace ChatRoomNET.Web.Tests;

public class TokenServiceTests
{
    private static TokenService CreateService(string expiryMinutes = "60")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = expiryMinutes,
                ["Jwt:Key"] = "unit-test-signing-key-at-least-32-bytes-long!!"
            })
            .Build();

        return new TokenService(configuration);
    }

    [Fact]
    public void CreateToken_IncludesUserClaims()
    {
        var service = CreateService();
        var user = new ApplicationUser
        {
            Id = "user-123",
            UserName = "alice",
            Email = "alice@example.com"
        };

        var token = service.CreateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("user-123", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("alice", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal("alice@example.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Equal("test-audience", jwt.Audiences.Single());
    }

    [Fact]
    public void CreateToken_SetsExpiryAccordingToConfig()
    {
        var service = CreateService(expiryMinutes: "30");
        var user = new ApplicationUser { Id = "user-123", UserName = "alice", Email = "alice@example.com" };

        var token = service.CreateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expectedExpiry = DateTime.UtcNow.AddMinutes(30);
        Assert.True(Math.Abs((jwt.ValidTo - expectedExpiry).TotalSeconds) < 30);
    }

    [Fact]
    public void CreateToken_ProducesDistinctJtiPerCall()
    {
        var service = CreateService();
        var user = new ApplicationUser { Id = "user-123", UserName = "alice", Email = "alice@example.com" };

        var jwt1 = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));
        var jwt2 = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        var jti1 = jwt1.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwt2.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        Assert.NotEqual(jti1, jti2);
    }
}
