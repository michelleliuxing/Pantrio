using Pantrio.Domain.Quantities;

namespace Pantrio.Domain.Tests;

public class UnitsTests
{
    [Theory]
    [InlineData("tsp", 1, 5)]
    [InlineData("tbsp", 1, 20)]
    [InlineData("cup", 1, 250)]
    [InlineData("l", 1.5, 1500)]
    [InlineData("ml", 140, 140)]
    [InlineData("tbsp", 2, 40)]
    public void Volume_converts_to_millilitres_with_australian_measures(string unit, decimal qty, decimal expectedMl)
    {
        var u = Units.Parse(unit);
        Assert.Equal(Dimension.Volume, u.Dimension);
        Assert.Equal(expectedMl, Units.ToBase(qty, u));
    }

    [Theory]
    [InlineData("g", 200, 200)]
    [InlineData("kg", 1.25, 1250)]
    public void Mass_converts_to_grams(string unit, decimal qty, decimal expectedG)
    {
        var u = Units.Parse(unit);
        Assert.Equal(Dimension.Mass, u.Dimension);
        Assert.Equal(expectedG, Units.ToBase(qty, u));
    }

    [Fact]
    public void Convert_within_a_dimension()
    {
        Assert.Equal(4m, Units.Convert(1m, Units.Tablespoon, Units.Teaspoon));
        Assert.Equal(0.5m, Units.Convert(500m, Units.Gram, Units.Kilogram));
    }

    [Fact]
    public void Never_converts_between_mass_and_volume()
    {
        Assert.Throws<InvalidOperationException>(() => Units.Convert(1m, Units.Cup, Units.Gram));
        Assert.Throws<InvalidOperationException>(() => Units.Convert(1m, Units.Kilogram, Units.Litre));
    }

    [Fact]
    public void Never_converts_between_count_labels()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Units.Convert(1m, Units.Count("clove"), Units.Count("bunch")));
    }

    [Theory]
    [InlineData("grams", "g")]
    [InlineData("Kilograms", "kg")]
    [InlineData("mL", "ml")]
    [InlineData("Litres", "l")]
    [InlineData("L", "l")]
    [InlineData("teaspoons", "tsp")]
    [InlineData("Tbsp.", "tbsp")]
    [InlineData("tablespoon", "tbsp")]
    [InlineData("cups", "cup")]
    [InlineData("cloves", "clove")]
    [InlineData("bunches", "bunch")]
    [InlineData("tins", "can")]
    [InlineData("packets", "pack")]
    [InlineData("slices", "slice")]
    [InlineData("whole", "each")]
    [InlineData("pcs", "each")]
    [InlineData("", "each")]
    [InlineData(null, "each")]
    [InlineData("  ", "each")]
    public void Normalises_common_spellings(string? raw, string expected)
    {
        Assert.True(Units.TryNormalise(raw, out var unit));
        Assert.Equal(expected, unit.Code);
    }

    [Fact]
    public void Unknown_single_word_is_a_count_label()
    {
        Assert.True(Units.TryNormalise("knob", out var unit));
        Assert.Equal(Dimension.Count, unit.Dimension);
        Assert.Equal("knob", unit.BaseLabel);
    }

    [Theory]
    [InlineData("2 cups")]
    [InlineData("a pinch of")]
    [InlineData("g/ml")]
    [InlineData("123")]
    public void Rejects_input_that_is_not_a_unit(string raw)
    {
        Assert.False(Units.TryNormalise(raw, out _));
        Assert.Throws<FormatException>(() => Units.Parse(raw));
    }

    [Fact]
    public void Base_label_for_measured_units_is_the_base_unit()
    {
        Assert.Equal("g", Units.Kilogram.BaseLabel);
        Assert.Equal("ml", Units.Cup.BaseLabel);
        Assert.Equal("clove", Units.Count("clove").BaseLabel);
    }
}

public class AmountAndScalingTests
{
    [Fact]
    public void Null_quantity_is_a_text_amount()
    {
        var a = Amount.FromRecipe(null, null, " to taste ");
        Assert.Equal(Dimension.Text, a.Dimension);
        Assert.Null(a.BaseValue);
        Assert.Equal("to taste", a.Text);
        Assert.Equal(string.Empty, a.UnitLabel);
    }

    [Fact]
    public void Quantity_without_unit_is_each()
    {
        var a = Amount.FromRecipe(2m, null, null);
        Assert.Equal(Dimension.Count, a.Dimension);
        Assert.Equal("each", a.UnitLabel);
        Assert.Equal(2m, a.BaseValue);
    }

    [Fact]
    public void Measured_amount_is_stored_in_base_units()
    {
        var a = Amount.FromRecipe(2m, "tbsp", null);
        Assert.Equal(40m, a.BaseValue);
        Assert.Equal("ml", a.UnitLabel);
    }

    [Fact]
    public void Factor_uses_override_over_recipe_servings()
    {
        Assert.Equal(2m, Scaling.ForServings(2, 4).AsDecimal);
        Assert.Equal(1m, Scaling.ForServings(2, null).AsDecimal);
        Assert.Equal(0.5m, Scaling.ForServings(4, 2).AsDecimal);
    }

    [Fact]
    public void Scaling_multiplies_before_dividing_so_values_stay_exact()
    {
        var a = Amount.FromRecipe(400m, "g", null).Scale(Scaling.ForServings(2, 4));
        Assert.Equal(800m, a.BaseValue);

        var third = Amount.FromRecipe(100m, "g", null).Scale(Scaling.ForServings(3, 1));
        Assert.Equal(100m, Math.Round(third.BaseValue!.Value * 3, 20));
    }

    [Fact]
    public void Text_amounts_are_not_scaled()
    {
        var a = Amount.OfText("a pinch").Scale(Scaling.ForServings(2, 6));
        Assert.Equal("a pinch", a.Text);
        Assert.Null(a.BaseValue);
    }

    [Fact]
    public void Count_amounts_scale()
    {
        var a = Amount.FromRecipe(3m, "cloves", null).Scale(Scaling.ForServings(2, 3));
        Assert.Equal(4.5m, a.BaseValue);
        Assert.Equal("clove", a.UnitLabel);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(2, 0)]
    public void Invalid_servings_are_rejected(int servings, int? overrideServings)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scaling.ForServings(servings, overrideServings));
    }
}

public class QuantityFormatterTests
{
    [Theory]
    [InlineData(800, "800 g")]
    [InlineData(999, "999 g")]
    [InlineData(1000, "1 kg")]
    [InlineData(1250, "1.25 kg")]
    [InlineData(2.5, "2.5 g")]
    [InlineData(12.4, "12 g")]
    public void Mass(decimal grams, string expected) =>
        Assert.Equal(expected, QuantityFormatter.Format(Dimension.Mass, grams, "g"));

    [Theory]
    [InlineData(140, "140 ml")]
    [InlineData(1000, "1 L")]
    [InlineData(1500, "1.5 L")]
    [InlineData(5, "5 ml")]
    public void Volume(decimal ml, string expected) =>
        Assert.Equal(expected, QuantityFormatter.Format(Dimension.Volume, ml, "ml"));

    [Theory]
    [InlineData(3, "3")]
    [InlineData(2.2, "3")]
    [InlineData(0.5, "1")]
    public void Each_rounds_up_on_the_list(decimal count, string expected) =>
        Assert.Equal(expected, QuantityFormatter.Format(Dimension.Count, count, "each"));

    [Fact]
    public void Each_does_not_round_up_repeating_fraction_noise()
    {
        var third = 1m / 3m;
        Assert.Equal("1", QuantityFormatter.Format(Dimension.Count, third + third + third, "each"));
    }

    [Fact]
    public void Each_can_show_exact_value_off_the_list() =>
        Assert.Equal("2.5", QuantityFormatter.Format(Dimension.Count, 2.5m, "each", roundUpEach: false));

    [Theory]
    [InlineData(5, "clove", "5 cloves")]
    [InlineData(1, "bunch", "1 bunch")]
    [InlineData(2, "bunch", "2 bunches")]
    [InlineData(1.5, "can", "1.5 cans")]
    [InlineData(2, "leaf", "2 leaves")]
    public void Labelled_counts(decimal count, string label, string expected) =>
        Assert.Equal(expected, QuantityFormatter.Format(Dimension.Count, count, label));
}
