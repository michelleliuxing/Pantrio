using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Pantrio.Domain.Quantities;

public enum Dimension
{
    Mass,
    Volume,
    Count,
    Text,
}

/// <summary>A unit the app accepts. Mass and volume convert to a base unit (g, ml); counts are their own base.</summary>
public sealed record UnitDef(string Code, Dimension Dimension, decimal ToBase)
{
    /// <summary>The label a merged list line is keyed by: "g", "ml", or the count label itself.</summary>
    public string BaseLabel => Dimension switch
    {
        Dimension.Mass => "g",
        Dimension.Volume => "ml",
        _ => Code,
    };
}

/// <summary>
/// The accepted units, Australian measures (1 tsp = 5 ml, 1 tbsp = 20 ml, 1 cup = 250 ml).
/// There is deliberately no conversion between mass and volume.
/// </summary>
public static partial class Units
{
    public const string Each = "each";

    public static readonly UnitDef Gram = new("g", Dimension.Mass, 1m);
    public static readonly UnitDef Kilogram = new("kg", Dimension.Mass, 1000m);
    public static readonly UnitDef Millilitre = new("ml", Dimension.Volume, 1m);
    public static readonly UnitDef Litre = new("l", Dimension.Volume, 1000m);
    public static readonly UnitDef Teaspoon = new("tsp", Dimension.Volume, 5m);
    public static readonly UnitDef Tablespoon = new("tbsp", Dimension.Volume, 20m);
    public static readonly UnitDef Cup = new("cup", Dimension.Volume, 250m);

    /// <summary>Count labels offered in the UI. Other single-word labels are accepted as counts too.</summary>
    public static readonly IReadOnlyList<string> KnownCountLabels =
    [
        Each, "bunch", "clove", "can", "pack", "slice", "jar", "bottle", "sprig", "stalk",
        "head", "sheet", "fillet", "rasher", "leaf", "punnet", "bag", "piece", "handful", "pinch",
    ];

    private static readonly UnitDef[] Measured = [Gram, Kilogram, Millilitre, Litre, Teaspoon, Tablespoon, Cup];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = "g", ["gm"] = "g", ["gms"] = "g", ["gram"] = "g", ["grams"] = "g", ["gramme"] = "g", ["grammes"] = "g",
        ["kg"] = "kg", ["kgs"] = "kg", ["kilo"] = "kg", ["kilos"] = "kg", ["kilogram"] = "kg", ["kilograms"] = "kg",
        ["ml"] = "ml", ["mls"] = "ml", ["millilitre"] = "ml", ["millilitres"] = "ml", ["milliliter"] = "ml", ["milliliters"] = "ml",
        ["l"] = "l", ["lt"] = "l", ["ltr"] = "l", ["litre"] = "l", ["litres"] = "l", ["liter"] = "l", ["liters"] = "l",
        ["tsp"] = "tsp", ["tsps"] = "tsp", ["teaspoon"] = "tsp", ["teaspoons"] = "tsp",
        ["tbsp"] = "tbsp", ["tbsps"] = "tbsp", ["tbs"] = "tbsp", ["tbl"] = "tbsp", ["tablespoon"] = "tbsp", ["tablespoons"] = "tbsp",
        ["cup"] = "cup", ["cups"] = "cup", ["c"] = "cup",
        ["each"] = Each, ["ea"] = Each, ["whole"] = Each, ["x"] = Each, ["pc"] = Each, ["pcs"] = Each,
        ["piece"] = Each, ["pieces"] = Each, ["item"] = Each, ["items"] = Each,
        ["bunch"] = "bunch", ["bunches"] = "bunch",
        ["clove"] = "clove", ["cloves"] = "clove",
        ["can"] = "can", ["cans"] = "can", ["tin"] = "can", ["tins"] = "can",
        ["pack"] = "pack", ["packs"] = "pack", ["packet"] = "pack", ["packets"] = "pack", ["pkt"] = "pack", ["pkts"] = "pack",
        ["slice"] = "slice", ["slices"] = "slice",
        ["jar"] = "jar", ["jars"] = "jar",
        ["bottle"] = "bottle", ["bottles"] = "bottle",
        ["sprig"] = "sprig", ["sprigs"] = "sprig",
        ["stalk"] = "stalk", ["stalks"] = "stalk", ["stick"] = "stalk", ["sticks"] = "stalk",
        ["head"] = "head", ["heads"] = "head",
        ["sheet"] = "sheet", ["sheets"] = "sheet",
        ["fillet"] = "fillet", ["fillets"] = "fillet",
        ["rasher"] = "rasher", ["rashers"] = "rasher",
        ["leaf"] = "leaf", ["leaves"] = "leaf",
        ["punnet"] = "punnet", ["punnets"] = "punnet",
        ["bag"] = "bag", ["bags"] = "bag",
        ["handful"] = "handful", ["handfuls"] = "handful",
        ["pinch"] = "pinch", ["pinches"] = "pinch",
    };

    [GeneratedRegex(@"^[a-z][a-z\-]{0,23}$")]
    private static partial Regex CountLabelPattern();

    /// <summary>
    /// Normalises a unit as typed by a person or the AI. Null or blank means "each".
    /// Known aliases map to their code; any other short single word is accepted as a count label.
    /// Returns false for input that cannot be a unit (digits, several words, punctuation).
    /// </summary>
    public static bool TryNormalise(string? raw, [NotNullWhen(true)] out UnitDef? unit)
    {
        var text = raw?.Trim().TrimEnd('.').ToLowerInvariant() ?? string.Empty;
        if (text.Length == 0)
        {
            unit = Count(Each);
            return true;
        }

        if (Aliases.TryGetValue(text, out var code))
        {
            unit = Measured.FirstOrDefault(u => u.Code == code) ?? Count(code);
            return true;
        }

        if (CountLabelPattern().IsMatch(text))
        {
            unit = Count(text);
            return true;
        }

        unit = null;
        return false;
    }

    public static UnitDef Parse(string? raw) =>
        TryNormalise(raw, out var unit) ? unit : throw new FormatException($"'{raw}' is not a recognised unit.");

    public static UnitDef Count(string label) => new(label, Dimension.Count, 1m);

    /// <summary>Converts a quantity to its dimension's base unit (g, ml, or the count label).</summary>
    public static decimal ToBase(decimal quantity, UnitDef unit) => quantity * unit.ToBase;

    /// <summary>Converts between two units of the same dimension. Mass and volume never convert into each other.</summary>
    public static decimal Convert(decimal quantity, UnitDef from, UnitDef to)
    {
        if (from.Dimension != to.Dimension || from.Dimension == Dimension.Text)
        {
            throw new InvalidOperationException($"Cannot convert {from.Code} ({from.Dimension}) to {to.Code} ({to.Dimension}).");
        }

        if (from.Dimension == Dimension.Count && from.Code != to.Code)
        {
            throw new InvalidOperationException($"Cannot convert count '{from.Code}' to '{to.Code}'.");
        }

        return quantity * from.ToBase / to.ToBase;
    }

}
