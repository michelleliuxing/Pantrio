import { z } from "zod";
import type { Schemas } from "@/lib/api/client";
import { CATEGORIES, type AisleCategory } from "@/lib/labels";

export const TEXT_UNIT = "__text";

const rowSchema = z
  .object({
    key: z.string(),
    ingredientId: z.string().nullable(),
    name: z.string().trim().min(1, "Name the ingredient"),
    quantity: z.string(),
    unit: z.string(),
    quantityText: z.string(),
    optional: z.boolean(),
    isStaple: z.boolean(),
    category: z.enum(CATEGORIES as [AisleCategory, ...AisleCategory[]]),
    note: z.string(),
    /** Came from an AI suggestion and has not been saved yet. */
    draft: z.boolean(),
    /** A close catalogue entry the AI draft might mean; offered, never applied automatically. */
    possibleMatchId: z.string().nullable(),
    possibleMatchName: z.string().nullable(),
    /**
     * The catalogue buy link. null = not known on this row (a new or typed row), so saving leaves the
     * catalogue's link alone; "" = known to be empty, or cleared by the person.
     */
    purchaseUrl: z.string().nullable(),
  })
  .superRefine((row, ctx) => {
    if (row.unit !== TEXT_UNIT && row.quantity.trim() !== "") {
      const n = Number(row.quantity);
      if (!Number.isFinite(n) || n <= 0) {
        ctx.addIssue({ code: "custom", path: ["quantity"], message: "Use a number above 0" });
      }
    }
    if (row.purchaseUrl && row.purchaseUrl.trim() !== "" && !isHttpUrl(row.purchaseUrl)) {
      ctx.addIssue({ code: "custom", path: ["purchaseUrl"], message: "Paste the full link, starting with https://" });
    }
  });

export function isHttpUrl(value: string): boolean {
  try {
    const url = new URL(value.trim());
    return url.protocol === "https:" || url.protocol === "http:";
  } catch {
    return false;
  }
}

/** "woolworths.com.au" from a product URL, for compact labels. */
export function linkHost(value: string): string {
  try {
    return new URL(value).hostname.replace(/^www\./, "");
  } catch {
    return value;
  }
}

export const recipeFormSchema = z.object({
  name: z.string().trim().min(1, "Give the dish a name").max(200),
  servings: z.number().int().min(1, "At least 1").max(50, "At most 50"),
  prepMins: z.string().refine((v) => v.trim() === "" || (/^\d+$/.test(v.trim()) && Number(v) <= 1440), "Minutes, 0 to 1440"),
  tags: z.array(z.string()),
  notes: z.string().max(4000),
  methodText: z.string().max(20000),
  sourceUrl: z.string().refine((v) => v.trim() === "" || /^https?:\/\/\S+$/i.test(v.trim()), "Use a full http(s) link"),
  ingredients: z.array(rowSchema).max(100),
});

export type RecipeForm = z.infer<typeof recipeFormSchema>;
export type IngredientRowValue = RecipeForm["ingredients"][number];

let counter = 0;
export const newKey = () => `row-${Date.now().toString(36)}-${(counter++).toString(36)}`;

export function emptyRow(partial: Partial<IngredientRowValue> = {}): IngredientRowValue {
  return {
    key: newKey(),
    ingredientId: null,
    name: "",
    quantity: "",
    unit: "each",
    quantityText: "",
    optional: false,
    isStaple: false,
    category: "other",
    note: "",
    draft: false,
    possibleMatchId: null,
    possibleMatchName: null,
    purchaseUrl: null,
    ...partial,
  };
}

export function toForm(recipe?: Schemas["RecipeDto"], defaults?: { name?: string }): RecipeForm {
  if (!recipe) {
    return {
      name: defaults?.name ?? "",
      servings: 2,
      prepMins: "",
      tags: [],
      notes: "",
      methodText: "",
      sourceUrl: "",
      ingredients: [],
    };
  }

  return {
    name: recipe.name,
    servings: recipe.servings,
    prepMins: recipe.prepMins?.toString() ?? "",
    tags: recipe.tags,
    notes: recipe.notes ?? "",
    methodText: recipe.methodText ?? "",
    sourceUrl: recipe.sourceUrl ?? "",
    ingredients: recipe.ingredients.map((i) =>
      emptyRow({
        ingredientId: i.ingredientId,
        name: i.name,
        quantity: i.quantity?.toString() ?? "",
        unit: i.quantity == null ? (i.quantityText ? TEXT_UNIT : "each") : (i.unit ?? "each"),
        quantityText: i.quantityText ?? "",
        optional: i.optional,
        isStaple: i.isStaple,
        category: i.category,
        note: i.note ?? "",
        purchaseUrl: i.purchaseUrl ?? "",
      }),
    ),
  };
}

export function toRequest(form: RecipeForm, isFavourite?: boolean): Schemas["RecipeUpsertRequest"] {
  return {
    name: form.name.trim(),
    servings: form.servings,
    prepMins: form.prepMins.trim() === "" ? null : Number(form.prepMins),
    tags: form.tags,
    notes: form.notes,
    methodText: form.methodText,
    sourceUrl: form.sourceUrl.trim(),
    isFavourite,
    ingredients: form.ingredients.map((r) => {
      const isText = r.unit === TEXT_UNIT || r.quantity.trim() === "";
      return {
        ingredientId: r.ingredientId,
        name: r.name.trim(),
        quantity: isText ? null : Number(r.quantity),
        unit: isText ? null : r.unit,
        quantityText: r.quantityText.trim() || null,
        optional: r.optional,
        isStaple: r.isStaple,
        category: r.category,
        note: r.note.trim() || null,
        purchaseUrl: r.purchaseUrl === null ? null : r.purchaseUrl.trim(),
      };
    }),
  };
}

export function fromSuggestion(s: Schemas["SuggestedIngredientDto"]): IngredientRowValue {
  return emptyRow({
    ingredientId: s.ingredientId,
    name: s.name,
    quantity: s.quantity?.toString() ?? "",
    unit: s.quantity == null ? TEXT_UNIT : (s.unit ?? "each"),
    quantityText: s.quantityText ?? "",
    optional: s.optional,
    isStaple: s.isStaple,
    category: s.category,
    note: s.note ?? "",
    draft: true,
    possibleMatchId: s.possibleMatch?.ingredientId ?? null,
    possibleMatchName: s.possibleMatch?.name ?? null,
    purchaseUrl: s.ingredientId ? (s.purchaseUrl ?? "") : null,
  });
}
