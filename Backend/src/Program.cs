using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMitarbeiterRepository, MitarbeiterRepository>();
builder.Services.AddScoped<IZeiteintragRepository, ZeiteintragRepository>();
builder.Services.AddScoped<IPasswordHasher<Mitarbeiter>, PasswordHasher<Mitarbeiter>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailConfirmationTokenService, EmailConfirmationTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var smtpEnabled = builder.Configuration.GetValue<bool>($"{SmtpEmailOptions.SectionName}:Enabled");
if (smtpEnabled)
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}

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
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGraphQL("/graphql").WithOptions(o =>
{
    o.Tool.Enable = false; // Banana Cake Pop deaktiviert
});

app.MapGet("/", () => "Taetigkeitsbericht.Backend");

app.Run();
