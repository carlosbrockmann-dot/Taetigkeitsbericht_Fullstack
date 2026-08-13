using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Amazon.AuroraDsql.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Taetigkeitsbericht.Backend;
using Taetigkeitsbericht.Backend.Data;
using Taetigkeitsbericht.Backend.GraphQL;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;
using Taetigkeitsbericht.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EmailConfirmationOptions>(
    builder.Configuration.GetSection(EmailConfirmationOptions.SectionName));
builder.Services.Configure<SmtpEmailOptions>(
    builder.Configuration.GetSection(SmtpEmailOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

if (databaseOptions.UseDsql)
{
    if (string.IsNullOrWhiteSpace(databaseOptions.Host))
    {
        throw new InvalidOperationException(
            "Database:UseDsql=true erfordert Database:Host (DSQL-Endpoint).");
    }

    builder.Services.AddDsqlDataSource(
        databaseOptions.Host,
        cfg =>
        {
            cfg.User = databaseOptions.User;
            cfg.Database = databaseOptions.Database;
            cfg.Port = databaseOptions.Port;
            cfg.OrmPrefix = "efcore";
        });
    builder.Services.AddDbContext<AppDbContext>((sp, options) => options.UseDsql(sp));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMitarbeiterRepository, MitarbeiterRepository>();
builder.Services.AddScoped<IZeiteintragRepository, ZeiteintragRepository>();
builder.Services.AddScoped<ILoginTokenRepository, LoginTokenRepository>();
builder.Services.AddScoped<IPasswordHasher<Mitarbeiter>, PasswordHasher<Mitarbeiter>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailConfirmationTokenService, EmailConfirmationTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<LoggingEmailSender>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<IEmailSender>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpEmailOptions>>().Value;
    return options.Enabled
        ? sp.GetRequiredService<SmtpEmailSender>()
        : sp.GetRequiredService<LoggingEmailSender>();
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key fehlt in appsettings.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = signingKey,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                    ?? context.Principal?.FindFirst("jti")?.Value;
                var raw = context.HttpContext.Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(jti)
                    || string.IsNullOrWhiteSpace(raw)
                    || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("Token ohne gültige Session.");
                    return;
                }

                var token = raw["Bearer ".Length..].Trim();
                var hash = JwtTokenService.ComputeTokenHash(token);
                var repo = context.HttpContext.RequestServices.GetRequiredService<ILoginTokenRepository>();
                if (!await repo.IsActiveAsync(jti, hash, context.HttpContext.RequestAborted))
                {
                    context.Fail("Token ist ungültig, abgelaufen oder widerrufen.");
                }
            },
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy => policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .DisableIntrospection(!builder.Environment.IsDevelopment())
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment())
    .ModifyServerOptions(o => o.EnableSchemaRequests = true)
    .ModifyCostOptions(o =>
    {
        // Introspection (GraphiQL Docs/Explorer) überschreitet sonst oft die Defaults.
        if (builder.Environment.IsDevelopment())
        {
            o.EnforceCostLimits = false;
        }
    });

var app = builder.Build();

{
    var smtp = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpEmailOptions>>().Value;
    app.Logger.LogInformation(
        "E-Mail-Versand: Smtp.Enabled={SmtpEnabled}, Host={SmtpHost}, From gesetzt={FromSet}, UserName gesetzt={UserSet}, Password gesetzt={PasswordSet}",
        smtp.Enabled,
        smtp.Host,
        !string.IsNullOrWhiteSpace(smtp.From),
        !string.IsNullOrWhiteSpace(smtp.UserName),
        !string.IsNullOrWhiteSpace(smtp.Password));
    app.Logger.LogInformation(
        "Datenbank: UseDsql={UseDsql}, Host={Host}, MigrateOnStartup={Migrate}",
        databaseOptions.UseDsql,
        databaseOptions.UseDsql ? databaseOptions.Host : "(ConnectionStrings:DefaultConnection)",
        databaseOptions.MigrateOnStartup);
}

if (databaseOptions.MigrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    app.Logger.LogInformation("Führe EF-Core-Migrationen aus…");
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Migrationen abgeschlossen.");
}

var enableHttpsRedirection = app.Configuration.GetValue(
    "HttpsRedirection:Enabled",
    app.Environment.IsDevelopment());
if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapGet("/graphiql", () => Results.Redirect("/graphiql/index.html"));
}

// Einziger REST-Endpunkt: Bestätigungslink aus der E-Mail (Browser-GET).
app.MapGet("/api/auth/confirm-email", async (
    string token,
    IAuthService authService,
    CancellationToken cancellationToken) =>
{
    var (ok, error) = await authService.ConfirmEmailAsync(token, cancellationToken);
    if (!ok)
    {
        return Results.BadRequest(new { error });
    }

    return Results.Ok(new { message = "E-Mail-Adresse erfolgreich bestätigt. Sie können sich jetzt anmelden." });
});

app.MapGraphQL("/graphql").WithOptions(o =>
{
    o.Tool.Enable = false; // Banana Cake Pop deaktiviert
    o.EnableSchemaRequests = true; // Schema per /graphql?sdl
});

app.MapGet("/", () => app.Environment.IsDevelopment()
    ? Results.Redirect("/graphiql")
    : Results.Text("Taetigkeitsbericht.Backend"));

app.Run();

public partial class Program;
