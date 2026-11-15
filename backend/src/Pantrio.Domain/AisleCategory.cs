namespace Pantrio.Domain;

/// <summary>Fixed aisle list. Serialised as snake_case strings (e.g. "meat_seafood").</summary>
public enum AisleCategory
{
    Produce,
    MeatSeafood,
    DairyEggs,
    Bakery,
    Pantry,
    SaucesCondiments,
    Spices,
    International,
    Frozen,
    Drinks,
    Snacks,
    Household,
    Other,
}

public enum MealSlot
{
    Breakfast,
    Lunch,
    Dinner,
}

public enum HouseholdRole
{
    Owner,
    Editor,
    Viewer,
}
