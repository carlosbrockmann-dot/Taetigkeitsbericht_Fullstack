using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/zeiteintraege")]
public class ZeiteintraegeController : ControllerBase
{
    private readonly IZeiteintragRepository _zeiteintragRepository;
    private readonly ICurrentUserService _currentUserService;

    public ZeiteintraegeController(
        IZeiteintragRepository zeiteintragRepository,
        ICurrentUserService currentUserService)
    {
        _zeiteintragRepository = zeiteintragRepository;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<IActionResult> Speichern(
        [FromBody] List<Zeiteintrag> eintraege,
        CancellationToken cancellationToken)
    {
        var mitarbeiterId = _currentUserService.GetMitarbeiterId(User);
        if (mitarbeiterId is null)
        {
            return Unauthorized();
        }

        if (eintraege.Count == 0)
        {
            return BadRequest(new { error = "Liste der Zeiteinträge ist leer." });
        }

        foreach (var eintrag in eintraege)
        {
            eintrag.MitarbeiterId = mitarbeiterId.Value;
        }

        var gespeichert = await _zeiteintragRepository.AddRangeAsync(eintraege, cancellationToken);
        return Ok(gespeichert);
    }

    [HttpGet]
    public async Task<IActionResult> Abfragen(
        [FromQuery] DateOnly? von,
        [FromQuery] DateOnly? bis,
        CancellationToken cancellationToken)
    {
        var mitarbeiterId = _currentUserService.GetMitarbeiterId(User);
        if (mitarbeiterId is null)
        {
            return Unauthorized();
        }

        var liste = await _zeiteintragRepository.GetByMitarbeiterUndZeitraumAsync(
            mitarbeiterId.Value,
            von,
            bis,
            cancellationToken);

        return Ok(liste);
    }
}
