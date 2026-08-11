using System.Security.Claims;
using FluentAssertions;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.Tests.Unit.Services;

public class CurrentUserServiceTests
{
    private readonly CurrentUserService _sut = new();

    [Fact]
    public void GetMitarbeiterId_liest_NameIdentifier()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
        ]));

        _sut.GetMitarbeiterId(user).Should().Be(42);
    }

    [Fact]
    public void GetMitarbeiterId_liest_Sub_wenn_NameIdentifier_fehlt()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "7"),
        ]));

        _sut.GetMitarbeiterId(user).Should().Be(7);
    }

    [Fact]
    public void GetMitarbeiterId_gibt_null_bei_ungueltigem_Claim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "abc"),
        ]));

        _sut.GetMitarbeiterId(user).Should().BeNull();
    }

    [Fact]
    public void GetMitarbeiterId_gibt_null_ohne_Claims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        _sut.GetMitarbeiterId(user).Should().BeNull();
    }
}
