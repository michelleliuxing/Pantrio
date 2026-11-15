using System.Text.Json;
using System.Text.Json.Serialization;
using Pantrio.Api;
using Pantrio.Api.Auth;
using Pantrio.Api.Features.Households;
using Pantrio.Api.Infrastructure;
using Pantrio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var publicOrigin = new Uri(config["App:PublicOrigin"] ?? "http://localhost:3000");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddPantrioOpenApi();

builder.Services.AddPantrioInfrastructure(config);
builder.Services.AddPantrioAuth(config, builder.Environment);
builder.Services.AddPantrioRateLimits(config);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UsePublicOrigin(publicOrigin);
app.UseAuthentication();
app.UseHouseholdContext();
app.UseCsrfProtection(publicOrigin);
app.UseRateLimiter();
app.UseAuthorization();

app.MapOpenApi("/api/openapi/{documentName}.json");

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithTags("System");

api.MapAuthEndpoints();
api.MapHouseholdEndpoints();

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
