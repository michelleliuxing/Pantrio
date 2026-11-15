using System.Globalization;
using System.Text.Json;
using Pantrio.Domain.Quantities;
using Pantrio.Infrastructure.Seed;

namespace Pantrio.Infrastructure.Ai;

/// <summary>
/// The JSON contract the model must follow, and the server-side validation of what comes back.
/// Strict-mode JSON schema: every property required, nulls expressed as type unions, no extra properties.
/// </summary>
public static class SuggestionParser
{
    public const int MaxIngredients = 40;

    public static readonly string[] CategoryWireNames =
        Enum.GetValues<Pantrio.Domain.AisleCategory>().Select(Categories.ToWire).ToArray();

    public static readonly string[] AcceptedUnits =
        ["g", "kg", "ml", "l", "tsp", "tbsp", "cup", .. Units.KnownCountLabels];

    public static string JsonSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "servings", "ingredients" },
        properties = new
        {
            servings = new { type = "integer" },
            ingredients = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "name", "quantity", "unit", "quantity_text", "category", "optional", "is_staple", "note" },
                    properties = new
                    {
                        name = new { type = "string" },
                        quantity = new { type = new[] { "number", "null" } },
                        unit = new { type = new[] { "string", "null" }, @enum = AcceptedUnits.Cast<string?>().Append(null).ToArray() },
                        quantity_text = new { type = new[] { "string", "null" } },
                        category = new { type = "string", @enum = CategoryWireNames },
                        optional = new { type = "boolean" },
                        is_staple = new { type = "boolean" },
                        note = new { type = new[] { "string", "null" } },
                    },
                },
            },
        },
    });

    /// <summary>Validates and normalises the model's JSON. Throws <see cref="SuggestionFailedException"/> when it is unusable.</summary>
    public static (int Servings, List<SuggestedIngredient> Ingredients) Parse(string json, int requestedServings)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new SuggestionFailedException("The draft was not valid JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ingredients", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                throw new SuggestionFailedException("The draft had no ingredient list.");
            }

            var servings = root.TryGetProperty("servings", out var s) && s.TryGetInt32(out var sv) && sv is > 0 and <= 50 ? sv : requestedServings;
            var result = new List<SuggestedIngredient>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in list.EnumerateArray().Take(MaxIngredients))
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Text(item, "name")?.Trim();
                if (string.IsNullOrEmpty(name) || name.Length > 120 || !seen.Add(name))
                {
                    continue;
                }

                var quantity = item.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number && q.TryGetDecimal(out var qv) && qv > 0 && qv <= 100000
                    ? Math.Round(qv, 3)
                    : (decimal?)null;
                var rawUnit = Text(item, "unit");
                var quantityText = Trim(Text(item, "quantity_text"), 100);
                string? unit = null;

                if (quantity is not null)
                {
                    if (Units.TryNormalise(rawUnit, out var def))
                    {
                        unit = def.Code;
                    }
                    else
                    {
                        // An amount in a unit we cannot merge stays readable as text rather than being dropped.
                        quantityText ??= $"{quantity.Value.ToString(CultureInfo.InvariantCulture)} {rawUnit}".Trim();
                        quantity = null;
                    }
                }

                var category = Categories.TryParse(Text(item, "category"), out var c) ? Categories.ToWire(c) : "other";
                result.Add(new SuggestedIngredient(
                    name,
                    quantity,
                    unit,
                    quantity is null ? quantityText : null,
                    category,
                    Bool(item, "optional"),
                    Bool(item, "is_staple"),
                    Trim(Text(item, "note"), 200)));
            }

            if (result.Count == 0)
            {
                throw new SuggestionFailedException("The draft had no usable ingredients.");
            }

            return (servings, result);
        }
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var t && t.Length > max ? t[..max] : value.Trim();
}
