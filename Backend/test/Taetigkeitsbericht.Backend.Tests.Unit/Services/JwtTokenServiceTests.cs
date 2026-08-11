using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.Tests.Unit.Services;

public class JwtTokenServiceTests
{
    [Fact]
    public void ComputeTokenHash_ist_deterministisch_und_hex()
    {
        var a = JwtTokenService.ComputeTokenHash("token-xyz");
        var b = JwtTokenService.ComputeTokenHash("token-xyz");

        a.Should().Be(b);
        a.Should().MatchRegex("^[0-9A-F]{64}$");
        a.Should().NotBe(JwtTokenService.ComputeTokenHash("anderes-token"));
    }

    [Fact]
    public void CreateToken_liefert_jwt_jti_ablauf_und_hash()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-secret-key-mindestens-32-zeichen!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiresMinutes"] = "30",
            })
            .Build();

        var sut = new JwtTokenService(config);
        var mitarbeiter = new Mitarbeiter
        {
            Id = 11,
            Benutzername = "alice",
            Email = "alice@example.com",
            PasswortHash = "hash",
        };

        var before = DateTimeOffset.UtcNow;
        var result = sut.CreateToken(mitarbeiter);

        result.Token.Should().NotBeNullOrWhiteSpace();
        result.Jti.Should().HaveLength(32);
        result.ExpiresAt.Should().BeAfter(before.AddMinutes(25));
        result.ExpiresAt.Should().BeBefore(before.AddMinutes(35));
        result.TokenHash.Should().Be(JwtTokenService.ComputeTokenHash(result.Token));
    }

    [Fact]
    public void CreateToken_ohne_Jwt_Key_wirft()
    {
        var config = new ConfigurationBuilder().Build();
        var sut = new JwtTokenService(config);
        var mitarbeiter = new Mitarbeiter
        {
            Id = 1,
            Benutzername = "bob",
            Email = "bob@example.com",
            PasswortHash = "hash",
        };

        var act = () => sut.CreateToken(mitarbeiter);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Key*");
    }
}
