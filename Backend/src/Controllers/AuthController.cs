using Microsoft.AspNetCore.Mvc;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, error, mitarbeiter) = await _authService.RegisterAsync(request, cancellationToken);
        if (!ok || mitarbeiter is null)
        {
            return BadRequest(new { error });
        }

        return Created($"/api/mitarbeiter/{mitarbeiter.Id}", new
        {
            mitarbeiter.Id,
            mitarbeiter.Benutzername,
            mitarbeiter.Email,
            mitarbeiter.EmailBestaetigt,
            hinweis = "Bitte E-Mail-Adresse über den Bestätigungslink bestätigen.",
        });
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var (ok, error) = await _authService.ConfirmEmailAsync(token, cancellationToken);
        if (!ok)
        {
            return BadRequest(new { error });
        }

        return Ok(new { message = "E-Mail-Adresse erfolgreich bestätigt. Sie können sich jetzt anmelden." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var (ok, error, response) = await _authService.LoginAsync(request, cancellationToken);
        if (!ok || response is null)
        {
            return Unauthorized(new { error });
        }

        return Ok(response);
    }
}
