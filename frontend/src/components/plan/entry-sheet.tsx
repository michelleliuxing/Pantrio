"use client";

import Link from "next/link";
import { ArrowRightLeft, BookOpen, Minus, Plus, ShoppingBasket, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { dayName, dayOfMonth } from "@/lib/dates";
import { slotLabel } from "@/lib/meals";
import type { PlanEntry } from "@/lib/queries/plan";

type Props = {
  entry: PlanEntry | null;
  onClose: () => void;
  onServings: (entry: PlanEntry, servings: number) => void;
  onPlace: (entry: PlanEntry) => void;
  onToCart: (entry: PlanEntry) => void;
  onRemove: (entry: PlanEntry) => void;
};

export function EntrySheet({ entry, onClose, onServings, onPlace, onToCart, onRemove }: Props) {
  const servings = entry ? (entry.servingsOverride ?? entry.recipeServings) : 0;

  return (
    <Sheet open={!!entry} onOpenChange={(o) => !o && onClose()}>
      <SheetContent side="bottom" className="rounded-t-2xl pb-[max(1rem,env(safe-area-inset-bottom))]">
        {entry && (
          <>
            <SheetHeader className="pb-0">
              <SheetTitle className="pr-8 text-lg">{entry.recipeName}</SheetTitle>
              <SheetDescription>
                {entry.day && entry.mealSlot
                  ? `${dayName(entry.day, "long")} ${dayOfMonth(entry.day)} · ${slotLabel(entry.mealSlot)}`
                  : "In the meal cart"}
              </SheetDescription>
            </SheetHeader>
            <div className="space-y-4 px-4">
              <div className="flex items-center justify-between rounded-xl border p-3">
                <div>
                  <p className="font-medium">Servings</p>
                  <p className="text-xs text-muted-foreground">
                    Recipe makes {entry.recipeServings}
                    {entry.servingsOverride != null && " · scaled for this meal"}
                  </p>
                </div>
                <div className="flex items-center gap-1">
                  <Button
                    variant="outline"
                    size="icon-lg"
                    className="size-11"
                    disabled={servings <= 1}
                    onClick={() => onServings(entry, servings - 1)}
                    aria-label="Fewer servings"
                  >
                    <Minus />
                  </Button>
                  <span className="w-8 text-center text-lg font-semibold tabular-nums" aria-live="polite">
                    {servings}
                  </span>
                  <Button
                    variant="outline"
                    size="icon-lg"
                    className="size-11"
                    disabled={servings >= 50}
                    onClick={() => onServings(entry, servings + 1)}
                    aria-label="More servings"
                  >
                    <Plus />
                  </Button>
                </div>
              </div>

              <div className="grid gap-2">
                <Button className="h-12 justify-start" variant="secondary" onClick={() => onPlace(entry)}>
                  <ArrowRightLeft /> {entry.day ? "Move to another meal" : "Place on a day"}
                </Button>
                {entry.day && (
                  <Button className="h-12 justify-start" variant="secondary" onClick={() => onToCart(entry)}>
                    <ShoppingBasket /> Move to meal cart
                  </Button>
                )}
                <Button
                  className="h-12 justify-start"
                  variant="secondary"
                  render={<Link href={`/recipes/${entry.recipeId}`} />}
                  nativeButton={false}
                >
                  <BookOpen /> Open recipe
                </Button>
                <Button className="h-12 justify-start" variant="destructive" onClick={() => onRemove(entry)}>
                  <Trash2 /> Remove from plan
                </Button>
              </div>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}
