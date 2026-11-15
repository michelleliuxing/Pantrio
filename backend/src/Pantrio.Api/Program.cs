using System.Text.Json;
using System.Text.Json.Serialization;
using Pantrio.Api;
using Pantrio.Api.Auth;
using Pantrio.Api.Features.Ai;
using Pantrio.Api.Features.Households;
using Pantrio.Api.Features.Ingredients;
using Pantrio.Api.Features.Recipes;
using Pantrio.Api.Infrastructure;
using Pantrio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var publicOrigin = new Uri(config["App:PublicOrigin"] ?? "http://localhost:3000");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict; // numbers are numbers, in JSON and in the OpenAPI types
});
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddPantrioOpenApi();

builder.Services.AddPantrioInfrastructure(config);
builder.Services.AddPantrioAuth(config, builder.Environment);
builder.Services.AddPantrioRateLimits(config);
builder.Services.AddScoped<IngredientMatcher>();
builder.Services.AddScoped<RecipeService>();

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
api.MapIngredientEndpoints();
api.MapRecipeEndpoints();
api.MapAiEndpoints();

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
