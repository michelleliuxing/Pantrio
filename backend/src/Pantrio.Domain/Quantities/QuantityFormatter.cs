using System.Globalization;

namespace Pantrio.Domain.Quantities;

/// <summary>Display rounding. Stored values stay exact; only text shown to people is rounded.</summary>
public static class QuantityFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats a base-unit total for display, e.g. "800 g", "1.25 kg", "140 ml", "1.5 L", "3", "5 cloves".
    /// On the shopping list, "each" counts round up to a whole number (<paramref name="roundUpEach"/>).
    /// </summary>
    public static string Format(Dimension dimension, decimal baseValue, string unitLabel, bool roundUpEach = true) =>
        dimension switch
        {
            Dimension.Mass => baseValue >= 1000m ? $"{Number(baseValue / 1000m, 2)} kg" : $"{Number(baseValue, SmallPlaces(baseValue))} g",
            Dimension.Volume => baseValue >= 1000m ? $"{Number(baseValue / 1000m, 2)} L" : $"{Number(baseValue, SmallPlaces(baseValue))} ml",
            Dimension.Count when unitLabel == Units.Each =>
                roundUpEach ? Number(Math.Ceiling(Round(baseValue, 6)), 0) : Number(baseValue, 2),
            Dimension.Count => $"{Number(baseValue, 2)} {Pluralise(unitLabel, baseValue)}",
            _ => string.Empty,
        };

    /// <summary>Simple English plural for count labels: clove → cloves, bunch → bunches, leaf → leaves.</summary>
    public static string Pluralise(string label, decimal count)
    {
        if (count == 1m || label.Length == 0)
        {
            return label;
        }

        if (label == "leaf")
        {
            return "leaves";
        }

        return label.EndsWith("ch", StringComparison.Ordinal) || label.EndsWith("sh", StringComparison.Ordinal)
               || label.EndsWith('s') || label.EndsWith('x')
            ? label + "es"
            : label + "s";
    }

    private static int SmallPlaces(decimal value) => value >= 10m ? 0 : 1;

    // Strip noise left by repeating fractions (0.99999… → 1) before rounding up.
    private static decimal Round(decimal value, int places) => Math.Round(value, places, MidpointRounding.AwayFromZero);

    private static string Number(decimal value, int places) =>
        Round(value, places).ToString(places == 0 ? "0" : "0." + new string('#', places), Culture);
}
