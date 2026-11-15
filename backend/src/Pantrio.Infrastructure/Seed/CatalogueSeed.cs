using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Infrastructure.Seed;

/// <summary>Starter ingredient catalogue copied into each new household. Australian names, metric defaults.</summary>
public static class CatalogueSeed
{
    // name | category | staple (s) | default unit | aliases (comma separated)
    private const string Data = """
        brown onion|produce||each|onion,yellow onion
        red onion|produce||each|spanish onion
        spring onion|produce||bunch|scallion,green onion,shallots
        garlic|produce||clove|garlic clove
        ginger|produce||g|fresh ginger
        carrot|produce||each|carrots
        potato|produce||g|potatoes,white potato
        sweet potato|produce||g|kumara
        capsicum|produce||each|bell pepper,red capsicum,pepper
        zucchini|produce||each|courgette
        eggplant|produce||each|aubergine
        broccoli|produce||head|
        cauliflower|produce||head|
        baby spinach|produce||g|spinach
        lettuce|produce||head|iceberg lettuce,cos lettuce
        tomato|produce||each|tomatoes
        cherry tomatoes|produce||punnet|
        cucumber|produce||each|lebanese cucumber,continental cucumber
        mushrooms|produce||g|button mushrooms,cup mushrooms
        celery|produce||stalk|
        corn|produce||each|corn cob,sweetcorn
        avocado|produce||each|
        lemon|produce||each|lemons
        lime|produce||each|limes
        coriander|produce||bunch|cilantro
        parsley|produce||bunch|flat-leaf parsley,italian parsley
        basil|produce||bunch|thai basil
        mint|produce||bunch|
        chilli|produce||each|red chilli,chili,fresh chilli
        bok choy|produce||bunch|pak choy,baby bok choy
        cabbage|produce||head|wombok,chinese cabbage
        green beans|produce||g|beans
        snow peas|produce||g|
        pumpkin|produce||g|butternut pumpkin,kent pumpkin
        banana|produce||each|bananas
        apple|produce||each|apples
        chicken thigh fillets|meat_seafood||g|chicken thighs,chicken thigh
        chicken breast|meat_seafood||g|chicken breasts,chicken breast fillets
        chicken drumsticks|meat_seafood||g|
        beef mince|meat_seafood||g|minced beef,ground beef
        pork mince|meat_seafood||g|minced pork,ground pork
        beef steak|meat_seafood||g|scotch fillet,rump steak,sirloin
        pork belly|meat_seafood||g|
        lamb chops|meat_seafood||g|lamb cutlets
        bacon|meat_seafood||rasher|bacon rashers
        sausages|meat_seafood||each|snags
        salmon fillets|meat_seafood||g|salmon
        prawns|meat_seafood||g|shrimp,green prawns
        white fish fillets|meat_seafood||g|barramundi,basa,fish fillets
        eggs|dairy_eggs||each|egg
        milk|dairy_eggs||ml|full cream milk
        butter|dairy_eggs||g|unsalted butter,salted butter
        cheddar cheese|dairy_eggs||g|tasty cheese,grated cheese,cheese
        parmesan|dairy_eggs||g|parmesan cheese,parmigiano
        mozzarella|dairy_eggs||g|mozzarella cheese
        feta|dairy_eggs||g|feta cheese
        greek yoghurt|dairy_eggs||g|yoghurt,yogurt,natural yoghurt
        thickened cream|dairy_eggs||ml|cream,heavy cream
        sour cream|dairy_eggs||g|
        bread|bakery||slice|sliced bread,loaf
        tortillas|bakery||each|wraps,flour tortillas
        burger buns|bakery||each|buns,bread rolls
        panko breadcrumbs|pantry||g|panko,breadcrumbs
        jasmine rice|pantry||g|rice,white rice,long grain rice
        basmati rice|pantry||g|
        spaghetti|pantry||g|pasta
        penne|pantry||g|
        egg noodles|pantry||g|noodles,hokkien noodles
        rice noodles|pantry||g|vermicelli
        plain flour|pantry||g|flour,all-purpose flour
        cornflour|pantry||g|cornstarch,corn flour
        white sugar|pantry||g|sugar,caster sugar
        brown sugar|pantry||g|
        honey|pantry||ml|
        rolled oats|pantry||g|oats
        canned diced tomatoes|pantry||can|diced tomatoes,tinned tomatoes,crushed tomatoes
        tomato paste|pantry||g|tomato puree
        coconut milk|pantry||can|coconut cream
        chicken stock|pantry||ml|chicken broth
        vegetable stock|pantry||ml|vegetable broth
        beef stock|pantry||ml|beef broth
        chickpeas|pantry||can|canned chickpeas
        black beans|pantry||can|
        red lentils|pantry||g|lentils
        peanut butter|pantry||g|
        olive oil|pantry|s|ml|extra virgin olive oil,evoo
        vegetable oil|pantry|s|ml|cooking oil,canola oil,oil
        sesame oil|sauces_condiments||ml|
        soy sauce|sauces_condiments||ml|light soy sauce,soya sauce
        dark soy sauce|sauces_condiments||ml|
        oyster sauce|sauces_condiments||ml|
        fish sauce|sauces_condiments||ml|
        sweet chilli sauce|sauces_condiments||ml|
        tomato sauce|sauces_condiments||ml|ketchup
        bbq sauce|sauces_condiments||ml|barbecue sauce
        mayonnaise|sauces_condiments||g|mayo,kewpie mayonnaise
        dijon mustard|sauces_condiments||g|mustard
        rice vinegar|sauces_condiments||ml|
        white vinegar|sauces_condiments||ml|vinegar
        balsamic vinegar|sauces_condiments||ml|
        mirin|sauces_condiments||ml|
        tonkatsu sauce|sauces_condiments||ml|katsu sauce
        japanese curry roux|international||g|curry roux,golden curry,curry blocks
        curry paste|international||g|red curry paste,green curry paste
        gochujang|international||g|
        miso paste|international||g|miso
        salt|spices|s||sea salt,table salt
        black pepper|spices|s||pepper,ground black pepper
        paprika|spices||tsp|smoked paprika,sweet paprika
        ground cumin|spices||tsp|cumin
        ground coriander|spices||tsp|
        curry powder|spices||tsp|
        garam masala|spices||tsp|
        chilli flakes|spices||tsp|red pepper flakes
        dried oregano|spices||tsp|oregano
        ground cinnamon|spices||tsp|cinnamon
        turmeric|spices||tsp|ground turmeric
        frozen peas|frozen||g|peas
        frozen mixed vegetables|frozen||g|mixed veg
        frozen berries|frozen||g|
        orange juice|drinks||ml|
        coffee|drinks||g|ground coffee,coffee beans
        tea bags|drinks||each|tea
        potato chips|snacks||pack|chips,crisps
        crackers|snacks||pack|rice crackers
        dishwashing liquid|household||bottle|dish soap,detergent
        paper towel|household||pack|paper towels
        toilet paper|household||pack|toilet roll
        cling wrap|household||each|plastic wrap
        """;

    public static IReadOnlyList<Ingredient> Create(Guid householdId)
    {
        var result = new List<Ingredient>();
        foreach (var line in Data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split('|');
            result.Add(new Ingredient
            {
                HouseholdId = householdId,
                Name = p[0],
                NormalizedName = Ingredient.Normalize(p[0]),
                Category = Categories.Parse(p[1]),
                IsStaple = p[2] == "s",
                DefaultUnit = p[3].Length == 0 ? null : p[3],
                Aliases = p[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Ingredient.Normalize).ToList(),
            });
        }

        return result;
    }
}

public static class Categories
{
    /// <summary>Parses the snake_case wire name ("meat_seafood") or the enum name.</summary>
    public static AisleCategory Parse(string value) =>
        TryParse(value, out var c) ? c : throw new FormatException($"Unknown aisle category '{value}'.");

    public static bool TryParse(string? value, out AisleCategory category)
    {
        category = AisleCategory.Other;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out category);
    }

    public static string ToWire(AisleCategory category) =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(category.ToString());
}
