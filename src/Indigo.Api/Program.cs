using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Indigo.Api.Errors;
using Indigo.Api.Security;
using Indigo.Api.Swagger;
using Indigo.Application.Abstractions.Security;
using Indigo.Infrastructure;
using Indigo.Infrastructure.Logging;
using Indigo.Infrastructure.Persistence;
using Indigo.Infrastructure.Security;
using Indigo.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


var cadenaDeConexion = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(cadenaDeConexion))
{
    var rutaDeAppDb = Path.Combine(builder.Environment.ContentRootPath, "app.db");
    cadenaDeConexion = new SqliteConnectionStringBuilder { DataSource = rutaDeAppDb }.ToString();
}

var rutaDeAlmacenamiento = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");

Directory.CreateDirectory(rutaDeAlmacenamiento);

var opcionesJwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();


builder.Services.AddInfrastructure(cadenaDeConexion, opcionesJwt, rutaDeAlmacenamiento);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.MapInboundClaims = false;

        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opcionesJwt.Issuer,
            ValidateAudience = true,
            ValidAudience = opcionesJwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcionesJwt.Key)),
            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero,

            RoleClaimType = ClaimsDelToken.Rol,
            NameClaimType = ClaimsDelToken.Email,
        };
    });

builder.Services.AddAuthorization(opciones =>
{
    opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services
    .AddControllers()
    .AddJsonOptions(opciones =>
    {
        opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddMvcOptions(opciones =>
    {
        opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorDeExcepciones>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo { Title = "Indigo API", Version = "v1" });

    opciones.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá el token que devuelve POST /api/auth/login (sin la palabra Bearer).",
    });

    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });

    opciones.SchemaFilter<FiltroEsquemaEnums>();
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Host.UseSerilog((contexto, servicios, configuracion) =>
{
    var accesoHttp = servicios.GetRequiredService<IHttpContextAccessor>();

    configuracion
        .ReadFrom.Configuration(contexto.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With(new UserIdEnricher(() => accesoHttp.HttpContext?.User.FindFirstValue(ClaimsDelToken.UsuarioId)))
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine("logs", "indigo-api-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7);
});

var app = builder.Build();


app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opciones => opciones.SwaggerEndpoint("/swagger/v1/swagger.json", "Indigo API v1"));
}


app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rutaDeAlmacenamiento),
    RequestPath = "/uploads",
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { servicio = "Indigo API", version = "v1", swagger = "/swagger" }))
    .AllowAnonymous();

if (app.Configuration.GetValue("Seed:Enabled", true))
{
    using var alcance = app.Services.CreateScope();

    await alcance.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await alcance.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.Run();

public partial class Program { }
