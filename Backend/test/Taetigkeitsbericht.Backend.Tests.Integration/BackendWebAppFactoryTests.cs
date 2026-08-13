using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Taetigkeitsbericht.Backend.Tests.Integration;

/// <summary>Smoke-Test: ASP.NET-Host startet ohne echten DB-Zugriff auf „/“.</summary>
public class BackendWebAppFactoryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BackendWebAppFactoryTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    "Host=127.0.0.1;Port=1;Database=unused;Username=u;Password=p");
                builder.UseSetting("Jwt:Key", "integration-test-secret-key-32chars!!");
                builder.UseSetting("Jwt:Issuer", "Test");
                builder.UseSetting("Jwt:Audience", "Test");
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
    }

    [Fact]
    public async Task Root_liefert_html_statusseite()
    {
        var response = await _client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Taetigkeitsbericht.Backend");
        body.Should().Contain("<!DOCTYPE html>");
        body.Should().Contain("Datenbank nicht erreichbar");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }
}
