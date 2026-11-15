import type { Schemas } from "@/lib/api/client";

export type MealSlot = Schemas["MealSlot"];

export const MEAL_SLOTS: readonly MealSlot[] = ["breakfast", "lunch", "dinner"];

export function slotLabel(slot: MealSlot): string {
  return slot.charAt(0).toUpperCase() + slot.slice(1);
}
