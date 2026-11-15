"use client";

import Link from "next/link";
import { useState } from "react";
import { Plus, Search, Star, Clock, Users } from "lucide-react";
import { cn } from "cn";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { useDebounced } from "@/hooks/use-debounced";
import { useRecipes, useRecipeTags, useToggleFavourite } from "@/lib/queries/recipes";

export default function RecipesPage() {
  const [search, setSearch] = useState("");
  const [tag, setTag] = useState<string | undefined>();
  const [favourites, setFavourites] = useState(false);
  const debounced = useDebounced(search);
  const recipes = useRecipes({ search: debounced.trim(), tag, favourites });
  const tags = useRecipeTags();
  const toggleFavourite = useToggleFavourite();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Recipes</h1>
        <Button render={<Link href="/recipes/new" />} nativeButton={false} className="h-10 px-4">
          <Plus />
          New
        </Button>
      </div>

      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder="Search dishes or tags"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="h-11 pl-9"
          aria-label="Search recipes"
        />
      </div>

      <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
        <Chip active={favourites} onClick={() => setFavourites((f) => !f)}>
          <Star className={cn("size-3.5", favourites && "fill-current")} />
          Favourites
        </Chip>
        {tags.data?.map((t) => (
          <Chip key={t.tag} active={tag === t.tag} onClick={() => setTag((cur) => (cur === t.tag ? undefined : t.tag))}>
            {t.tag}
          </Chip>
        ))}
      </div>

      {recipes.isPending ? (
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-[72px] rounded-xl" />
          ))}
        </div>
      ) : recipes.data?.length === 0 ? (
        <EmptyState filtered={!!(debounced || tag || favourites)} />
      ) : (
        <ul className="divide-y rounded-xl border">
          {recipes.data?.map((r) => (
            <li key={r.id} className="flex items-center">
              <Link href={`/recipes/${r.id}`} className="min-w-0 flex-1 px-4 py-3">
                <p className="truncate font-medium">{r.name}</p>
                <p className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                  <span className="inline-flex items-center gap-1">
                    <Users className="size-3" /> {r.servings}
                  </span>
                  {r.prepMins != null && (
                    <span className="inline-flex items-center gap-1">
                      <Clock className="size-3" /> {r.prepMins} min
                    </span>
                  )}
                  <span>{r.ingredientCount} ingredients</span>
                  {r.tags.length > 0 && <span className="truncate">{r.tags.join(" · ")}</span>}
                </p>
              </Link>
              <button
                type="button"
                className="flex size-12 shrink-0 items-center justify-center text-muted-foreground"
                aria-label={r.isFavourite ? `Unfavourite ${r.name}` : `Favourite ${r.name}`}
                aria-pressed={r.isFavourite}
                onClick={() => toggleFavourite.mutate({ id: r.id, isFavourite: !r.isFavourite })}
              >
                <Star className={cn("size-5", r.isFavourite && "fill-amber-400 text-amber-400")} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={cn(
        "inline-flex h-8 shrink-0 items-center gap-1.5 rounded-full border px-3 text-sm",
        active ? "border-primary bg-primary text-primary-foreground" : "bg-background text-foreground",
      )}
    >
      {children}
    </button>
  );
}

function EmptyState({ filtered }: { filtered: boolean }) {
  return (
    <div className="rounded-xl border border-dashed p-8 text-center">
      {filtered ? (
        <p className="text-muted-foreground">No recipes match.</p>
      ) : (
        <div className="space-y-3">
          <p className="font-medium">No recipes yet</p>
          <p className="text-sm text-muted-foreground">Add a dish by name; Pantrio can draft its ingredients for you.</p>
          <Button render={<Link href="/recipes/new" />} nativeButton={false} className="h-10">
            <Plus />
            Add your first recipe
          </Button>
        </div>
      )}
    </div>
  );
}
