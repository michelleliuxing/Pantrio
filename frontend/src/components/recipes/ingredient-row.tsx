"use client";

import { useState } from "react";
import { Controller, useWatch, type Control, type FieldErrors, type UseFormRegister, type UseFormSetValue } from "react-hook-form";
import { ArrowDown, ArrowUp, ChevronDown, ExternalLink, Link2, Sparkles, Trash2 } from "lucide-react";
import { cn } from "cn";
import { Switch } from "@/components/ui/switch";
import { CATEGORIES, CATEGORY_LABELS } from "@/lib/labels";
import { IngredientNameInput } from "./ingredient-name-input";
import { isHttpUrl, linkHost, TEXT_UNIT, type RecipeForm } from "./editor-model";

type Props = {
  index: number;
  count: number;
  control: Control<RecipeForm>;
  register: UseFormRegister<RecipeForm>;
  setValue: UseFormSetValue<RecipeForm>;
  errors?: FieldErrors<RecipeForm["ingredients"][number]>;
  units: string[];
  onRemove: () => void;
  onMove: (to: number) => void;
  autoFocus?: boolean;
};

const fieldClass =
  "h-11 rounded-lg border border-input bg-transparent px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive md:text-sm";

export function IngredientRow({ index, count, control, register, setValue, errors, units, onRemove, onMove, autoFocus }: Props) {
  const [open, setOpen] = useState(false);
  const row = useWatch({ control, name: `ingredients.${index}` });
  const isText = row.unit === TEXT_UNIT;

  return (
    <li
      className={cn(
        "space-y-2 rounded-xl border bg-card p-3",
        row.draft && "border-amber-300 bg-amber-50/60 dark:border-amber-500/40 dark:bg-amber-500/5",
      )}
    >
      <div className="flex items-start gap-2">
        <Controller
          control={control}
          name={`ingredients.${index}.name`}
          render={({ field }) => (
            <IngredientNameInput
              value={field.value}
              linked={!!row.ingredientId}
              invalid={!!errors?.name}
              autoFocus={autoFocus}
              onChange={(name) => {
                field.onChange(name);
                setValue(`ingredients.${index}.ingredientId`, null, { shouldDirty: true });
                // A known-empty link belonged to the previous name; don't let it clear another entry's link.
                if (row.purchaseUrl === "") setValue(`ingredients.${index}.purchaseUrl`, null);
              }}
              onPick={(i) => {
                field.onChange(i.name);
                setValue(`ingredients.${index}.ingredientId`, i.id, { shouldDirty: true });
                setValue(`ingredients.${index}.category`, i.category, { shouldDirty: true });
                setValue(`ingredients.${index}.isStaple`, i.isStaple, { shouldDirty: true });
                setValue(`ingredients.${index}.purchaseUrl`, i.purchaseUrl ?? "", { shouldDirty: true });
                if (!row.quantity && i.defaultUnit) setValue(`ingredients.${index}.unit`, i.defaultUnit, { shouldDirty: true });
              }}
            />
          )}
        />
        <button
          type="button"
          onClick={onRemove}
          className="flex size-11 shrink-0 items-center justify-center rounded-lg text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
          aria-label={`Delete ${row.name || "ingredient"}`}
        >
          <Trash2 className="size-4" />
        </button>
      </div>

      <div className="flex gap-2">
        {isText ? (
          <input
            {...register(`ingredients.${index}.quantityText`)}
            placeholder="e.g. to taste, a pinch"
            className={cn(fieldClass, "min-w-0 flex-1")}
            aria-label="Amount as text"
          />
        ) : (
          <input
            {...register(`ingredients.${index}.quantity`)}
            inputMode="decimal"
            placeholder="Qty"
            aria-label="Quantity"
            aria-invalid={!!errors?.quantity || undefined}
            className={cn(fieldClass, "w-24 min-w-0 flex-1 sm:flex-none")}
          />
        )}
        <select
          {...register(`ingredients.${index}.unit`)}
          aria-label="Unit"
          className={cn(fieldClass, "w-32 shrink-0 px-2")}
        >
          {units.map((u) => (
            <option key={u} value={u}>
              {u === "l" ? "L" : u}
            </option>
          ))}
          {!units.includes(row.unit) && row.unit !== TEXT_UNIT && <option value={row.unit}>{row.unit}</option>}
          <option value={TEXT_UNIT}>as text…</option>
        </select>
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className="flex size-11 shrink-0 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
          aria-expanded={open}
          aria-label="More options"
        >
          <ChevronDown className={cn("size-4 transition-transform", open && "rotate-180")} />
        </button>
      </div>

      {row.possibleMatchId && !row.ingredientId && (
        <button
          type="button"
          className="w-full rounded-lg bg-muted px-3 py-2 text-left text-xs"
          onClick={() => {
            setValue(`ingredients.${index}.ingredientId`, row.possibleMatchId, { shouldDirty: true });
            setValue(`ingredients.${index}.name`, row.possibleMatchName ?? row.name, { shouldDirty: true });
            setValue(`ingredients.${index}.possibleMatchId`, null);
          }}
        >
          Same as <span className="font-medium">{row.possibleMatchName}</span> in your catalogue? Tap to use it, so the
          list merges them.
        </button>
      )}

      {errors?.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
      {errors?.quantity && <p className="text-xs text-destructive">{errors.quantity.message}</p>}
      {errors?.purchaseUrl && !open && <p className="text-xs text-destructive">{errors.purchaseUrl.message}</p>}

      {(row.optional || row.isStaple || row.draft || row.note || row.purchaseUrl) && !open && (
        <div className="flex flex-wrap gap-1.5 text-xs">
          {row.draft && (
            <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 font-medium text-amber-800 dark:bg-amber-500/15 dark:text-amber-300">
              <Sparkles className="size-3" /> AI draft
            </span>
          )}
          {row.optional && <span className="rounded-full bg-muted px-2 py-0.5">optional</span>}
          {row.isStaple && <span className="rounded-full bg-muted px-2 py-0.5">staple</span>}
          {row.note && <span className="truncate rounded-full bg-muted px-2 py-0.5">{row.note}</span>}
          {row.purchaseUrl && isHttpUrl(row.purchaseUrl) && (
            <a
              href={row.purchaseUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex max-w-full items-center gap-1 truncate rounded-full bg-muted px-2 py-0.5 hover:underline"
            >
              <Link2 className="size-3 shrink-0" /> {linkHost(row.purchaseUrl)}
            </a>
          )}
        </div>
      )}

      {open && (
        <div className="space-y-3 border-t pt-3">
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-muted-foreground">Aisle</span>
            <select {...register(`ingredients.${index}.category`)} className={cn(fieldClass, "w-full")}>
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {CATEGORY_LABELS[c]}
                </option>
              ))}
            </select>
          </label>
          <input {...register(`ingredients.${index}.note`)} placeholder="Note, e.g. or breast" className={cn(fieldClass, "w-full")} />
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-muted-foreground">Where to buy</span>
            <div className="flex gap-2">
              <Controller
                control={control}
                name={`ingredients.${index}.purchaseUrl`}
                render={({ field }) => (
                  <input
                    type="url"
                    inputMode="url"
                    value={field.value ?? ""}
                    onChange={(e) => field.onChange(e.target.value)}
                    placeholder="Paste a Woolworths or Coles product link"
                    aria-invalid={!!errors?.purchaseUrl || undefined}
                    className={cn(fieldClass, "min-w-0 flex-1")}
                  />
                )}
              />
              {row.purchaseUrl && isHttpUrl(row.purchaseUrl) && (
                <a
                  href={row.purchaseUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex size-11 shrink-0 items-center justify-center rounded-lg border text-muted-foreground hover:bg-muted"
                  aria-label={`Open the buy link for ${row.name}`}
                >
                  <ExternalLink className="size-4" />
                </a>
              )}
            </div>
            {errors?.purchaseUrl && <span className="text-xs text-destructive">{errors.purchaseUrl.message}</span>}
          </label>
          <div className="grid grid-cols-2 gap-2">
            <Controller
              control={control}
              name={`ingredients.${index}.optional`}
              render={({ field }) => (
                <label className="flex h-11 items-center justify-between rounded-lg border px-3 text-sm">
                  Optional
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </label>
              )}
            />
            <Controller
              control={control}
              name={`ingredients.${index}.isStaple`}
              render={({ field }) => (
                <label className="flex h-11 items-center justify-between rounded-lg border px-3 text-sm">
                  Staple
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </label>
              )}
            />
          </div>
          <p className="text-xs text-muted-foreground">
            Optional items stay off the shopping list. Staples (salt, oil) go under “Check you have”. Aisle, staple and buy
            link apply to this ingredient in every recipe and on the shopping list.
          </p>
          <div className="flex gap-2">
            <button type="button" disabled={index === 0} onClick={() => onMove(index - 1)} className="flex h-10 flex-1 items-center justify-center gap-1 rounded-lg border text-sm disabled:opacity-40">
              <ArrowUp className="size-4" /> Up
            </button>
            <button type="button" disabled={index === count - 1} onClick={() => onMove(index + 1)} className="flex h-10 flex-1 items-center justify-center gap-1 rounded-lg border text-sm disabled:opacity-40">
              <ArrowDown className="size-4" /> Down
            </button>
          </div>
        </div>
      )}
    </li>
  );
}
