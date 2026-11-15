import type { Schemas } from "@/lib/api/client";

export type AisleCategory = Schemas["AisleCategory"];

export const CATEGORY_LABELS: Record<AisleCategory, string> = {
  produce: "Produce",
  meat_seafood: "Meat & seafood",
  dairy_eggs: "Dairy & eggs",
  bakery: "Bakery",
  pantry: "Pantry",
  sauces_condiments: "Sauces & condiments",
  spices: "Spices",
  international: "International",
  frozen: "Frozen",
  drinks: "Drinks",
  snacks: "Snacks",
  household: "Household",
  other: "Other",
};

export const CATEGORIES = Object.keys(CATEGORY_LABELS) as AisleCategory[];

/** How a recipe amount reads in the editor and on dish details, e.g. "400 g", "2 cloves", "to taste". */
export function formatAmount(quantity: number | null | undefined, unit: string | null | undefined, text?: string | null) {
  if (quantity == null) return text ?? "";
  const n = Number.isInteger(quantity) ? String(quantity) : String(Number(quantity.toFixed(3)));
  if (!unit || unit === "each") return n;
  const plural = quantity !== 1 && !["g", "kg", "ml", "l", "tsp", "tbsp"].includes(unit);
  const label = unit === "l" ? "L" : plural ? pluralise(unit) : unit;
  return `${n} ${label}`;
}

function pluralise(label: string) {
  if (label === "leaf") return "leaves";
  return /(ch|sh|s|x)$/.test(label) ? `${label}es` : `${label}s`;
}
