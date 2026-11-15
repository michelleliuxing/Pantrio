"use client";

import Link from "next/link";
import { useState } from "react";
import { Check, Plus, Search, Star } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { useDebounced } from "@/hooks/use-debounced";
import { useRecipes, type RecipeSummary } from "@/lib/queries/recipes";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  target: string;
  onPick: (recipe: RecipeSummary) => Promise<unknown>;
};

/** Bottom sheet for picking dishes. Stays open so several dishes can be added in a row. */
export function RecipePicker({ open, onOpenChange, target, onPick }: Props) {
  const [search, setSearch] = useState("");
  const [added, setAdded] = useState<Record<string, number>>({});
  const debounced = useDebounced(search);
  const recipes = useRecipes({ search: debounced.trim() });

  return (
    <Sheet
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        if (!o) {
          setAdded({});
          setSearch("");
        }
      }}
    >
      <SheetContent side="bottom" className="max-h-[85dvh] rounded-t-2xl pb-[env(safe-area-inset-bottom)]">
        <SheetHeader className="pb-0">
          <SheetTitle>Add dishes</SheetTitle>
          <SheetDescription>To {target}. Tap as many as you like.</SheetDescription>
        </SheetHeader>
        <div className="relative px-4">
          <Search className="pointer-events-none absolute top-1/2 left-7 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search recipes"
            className="h-11 pl-9"
            aria-label="Search recipes"
          />
        </div>
        <ul className="min-h-40 flex-1 overflow-y-auto px-2 pb-4">
          {recipes.data?.map((r) => (
            <li key={r.id}>
              <button
                type="button"
                className="flex min-h-14 w-full items-center gap-3 rounded-lg px-3 text-left hover:bg-muted active:bg-muted"
                onClick={async () => {
                  await onPick(r);
                  setAdded((a) => ({ ...a, [r.id]: (a[r.id] ?? 0) + 1 }));
                }}
              >
                {r.isFavourite ? (
                  <Star className="size-4 shrink-0 fill-amber-400 text-amber-400" />
                ) : (
                  <span className="size-4 shrink-0" />
                )}
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium">{r.name}</span>
                  <span className="text-xs text-muted-foreground">Serves {r.servings}</span>
                </span>
                {added[r.id] ? (
                  <span className="inline-flex items-center gap-1 text-sm font-medium text-primary">
                    <Check className="size-4" /> {added[r.id] > 1 ? `×${added[r.id]}` : "Added"}
                  </span>
                ) : (
                  <Plus className="size-5 text-muted-foreground" />
                )}
              </button>
            </li>
          ))}
          {recipes.data?.length === 0 && (
            <li className="p-6 text-center text-sm text-muted-foreground">
              {debounced ? "No recipes match." : "No recipes yet."}{" "}
              <Link href="/recipes/new" className="font-medium text-foreground underline">
                Add a recipe
              </Link>
            </li>
          )}
        </ul>
      </SheetContent>
    </Sheet>
  );
}
