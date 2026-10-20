using System.Text.Json;
using System.Text.Json.Serialization;
using BovinoVision.Api.Errores;
using BovinoVision.Api.Modulos;
using BovinoVision.Infraestructura;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opciones =>
{
    opciones.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
builder.Services.AddProblemDetails();
// Bad requests (missing or malformed parameters) reach the handler in every environment.
builder.Services.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<ManejadorSolicitudesInvalidas>();
builder.Services.AddExceptionHandler<ManejadorErroresDeDominio>();
builder.Services.AddOpenApi();

var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
    politica.WithOrigins(origenesPermitidos).AllowAnyMethod().AllowAnyHeader()));

builder.Services.AgregarInfraestructuraBase(builder.Configuration);
builder.Services.AgregarModulos(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Configuration.GetValue<bool>("BaseDeDatos:AplicarMigracionesAlIniciar"))
{
    await app.Services.AplicarMigracionesAsync();
}

app.MapGet("/health", () => new { status = "ok" }).WithTags("salud");
app.MapearModulos();

await app.RunAsync();
