"use client";

import { useId, useState } from "react";
import { cn } from "cn";
import type { Schemas } from "@/lib/api/client";
import { useDebounced } from "@/hooks/use-debounced";
import { CATEGORY_LABELS } from "@/lib/labels";
import { useIngredientSearch } from "@/lib/queries/recipes";

type Props = {
  value: string;
  linked: boolean;
  invalid?: boolean;
  onChange: (name: string) => void;
  onPick: (ingredient: Schemas["IngredientDto"]) => void;
  autoFocus?: boolean;
};

/** Free-text ingredient name with suggestions from the household catalogue, so the same ingredient merges across recipes. */
export function IngredientNameInput({ value, linked, invalid, onChange, onPick, autoFocus }: Props) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const search = useDebounced(value, 200);
  const results = useIngredientSearch(search, open);
  const listId = useId();
  const options = (results.data ?? []).filter((i) => !(linked && i.name.toLowerCase() === value.trim().toLowerCase()));
  const showList = open && value.trim().length > 0 && options.length > 0;

  function pick(i: Schemas["IngredientDto"]) {
    onPick(i);
    setOpen(false);
  }

  return (
    <div className="relative min-w-0 flex-1">
      <input
        value={value}
        autoFocus={autoFocus}
        placeholder="Ingredient"
        autoComplete="off"
        autoCapitalize="none"
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-invalid={invalid || undefined}
        className={cn(
          "h-11 w-full rounded-lg border border-input bg-transparent px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive md:text-sm",
        )}
        onChange={(e) => {
          onChange(e.target.value);
          setOpen(true);
          setActive(0);
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={(e) => {
          if (!showList) return;
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setActive((a) => Math.min(a + 1, options.length - 1));
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((a) => Math.max(a - 1, 0));
          } else if (e.key === "Enter") {
            e.preventDefault();
            pick(options[active]);
          } else if (e.key === "Escape") {
            setOpen(false);
          }
        }}
      />
      {showList && (
        <ul
          id={listId}
          role="listbox"
          className="absolute inset-x-0 top-full z-20 mt-1 max-h-64 overflow-auto rounded-lg border bg-popover p-1 shadow-md"
        >
          {options.map((i, idx) => (
            <li
              key={i.id}
              role="option"
              aria-selected={idx === active}
              className={cn(
                "flex cursor-pointer items-center justify-between gap-2 rounded-md px-3 py-2.5 text-sm",
                idx === active && "bg-muted",
              )}
              onMouseDown={(e) => {
                e.preventDefault();
                pick(i);
              }}
            >
              <span className="truncate">{i.name}</span>
              <span className="shrink-0 text-xs text-muted-foreground">{CATEGORY_LABELS[i.category]}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
