using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.GraphQL;

public class RegisterPayload
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public int? MitarbeiterId { get; init; }
    public string? Benutzername { get; init; }
    public string? Email { get; init; }
    public bool? EmailBestaetigt { get; init; }
    public string? Hinweis { get; init; }
}

public class LoginPayload
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public LoginResponse? Login { get; init; }
}

public class ConfirmEmailPayload
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? Message { get; init; }
}

public class ZeiteintragInput
{
    public Guid? Id { get; set; }
    public int? MandantId { get; set; }
    public DateOnly Datum { get; set; }
    public TimeOnly UhrzeitVon { get; set; }
    public TimeOnly UhrzeitBis { get; set; }
    public TimeOnly? PauseBeginn { get; set; }
    public TimeOnly? PauseEnde { get; set; }
    public TimeOnly? Pause2Beginn { get; set; }
    public TimeOnly? Pause2Ende { get; set; }
    public string? Anmerkung { get; set; }

    public Zeiteintrag ToEntity(int mitarbeiterId) => new()
    {
        Id = Id ?? Guid.Empty,
        MitarbeiterId = mitarbeiterId,
        MandantId = MandantId,
        Datum = Datum,
        UhrzeitVon = UhrzeitVon,
        UhrzeitBis = UhrzeitBis,
        PauseBeginn = PauseBeginn,
        PauseEnde = PauseEnde,
        Pause2Beginn = Pause2Beginn,
        Pause2Ende = Pause2Ende,
        Anmerkung = Anmerkung,
    };
}

public class SpeichereZeiteintraegePayload
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<Zeiteintrag>? Eintraege { get; init; }
}
