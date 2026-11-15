namespace Pantrio.Domain.Quantities;

public static class Scaling
{
    /// <summary>
    /// A servings ratio kept as numerator and denominator so that values are multiplied before they
    /// are divided. 400 g × 4 / 2 stays exactly 800 g; only genuinely repeating fractions round, at
    /// decimal's 28-digit precision.
    /// </summary>
    public readonly record struct Factor(int Numerator, int Denominator)
    {
        public static Factor One => new(1, 1);

        public decimal Apply(decimal value) => Numerator == Denominator ? value : value * Numerator / Denominator;

        public decimal AsDecimal => (decimal)Numerator / Denominator;
    }

    /// <summary>Factor = (entry's servings override, else the recipe's servings) / recipe's servings.</summary>
    public static Factor ForServings(int recipeServings, int? servingsOverride)
    {
        if (recipeServings <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeServings), "A recipe needs at least one serving.");
        }

        if (servingsOverride is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(servingsOverride), "A servings override must be at least one.");
        }

        return new Factor(servingsOverride ?? recipeServings, recipeServings);
    }
}
