namespace Pantrio.Domain.Quantities;

/// <summary>
/// A recipe amount resolved to its merge dimension. Measured amounts hold an exact base-unit value
/// (g, ml, or a count of <see cref="UnitLabel"/>); text amounts ("to taste") hold only the phrase.
/// </summary>
public sealed record Amount
{
    private Amount(Dimension dimension, decimal? baseValue, string unitLabel, string? text)
    {
        Dimension = dimension;
        BaseValue = baseValue;
        UnitLabel = unitLabel;
        Text = text;
    }

    public Dimension Dimension { get; }

    /// <summary>Exact value in base units. Null only for text amounts.</summary>
    public decimal? BaseValue { get; }

    /// <summary>"g", "ml", a count label such as "clove", or empty for text.</summary>
    public string UnitLabel { get; }

    /// <summary>The phrase for text amounts, e.g. "to taste". Null for measured amounts.</summary>
    public string? Text { get; }

    public static Amount Measured(decimal quantity, UnitDef unit) =>
        unit.Dimension == Dimension.Text
            ? throw new ArgumentException("Use Amount.OfText for text amounts.", nameof(unit))
            : new Amount(unit.Dimension, Units.ToBase(quantity, unit), unit.BaseLabel, null);

    public static Amount OfText(string? phrase) =>
        new(Dimension.Text, null, string.Empty, string.IsNullOrWhiteSpace(phrase) ? null : phrase.Trim());

    /// <summary>
    /// Resolves a recipe ingredient's stored fields. A null quantity means the amount is vague and
    /// <paramref name="quantityText"/> holds the phrase. A null or blank unit with a quantity means "each".
    /// </summary>
    public static Amount FromRecipe(decimal? quantity, string? unit, string? quantityText)
    {
        if (quantity is null)
        {
            return OfText(quantityText);
        }

        return Measured(quantity.Value, Units.Parse(unit));
    }

    /// <summary>Scales measured amounts exactly; text amounts are left alone.</summary>
    public Amount Scale(Scaling.Factor factor) =>
        BaseValue is null ? this : new Amount(Dimension, factor.Apply(BaseValue.Value), UnitLabel, Text);
}
