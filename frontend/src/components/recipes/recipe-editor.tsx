"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowLeft, Copy, Loader2, Minus, MoreVertical, Plus, ShoppingCart, Sparkles, Star, Trash2, WandSparkles } from "lucide-react";
import { cn } from "cn";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api, errorMessage, unwrap } from "@/lib/api/client";
import { useMe } from "@/lib/queries/me";
import { useAddRecipeToList } from "@/lib/queries/plan";
import {
  useDeleteRecipe,
  useDuplicateRecipe,
  useReferenceData,
  useSaveRecipe,
  useToggleFavourite,
  type Recipe,
} from "@/lib/queries/recipes";
import { emptyRow, fromSuggestion, recipeFormSchema, toForm, toRequest, type RecipeForm } from "./editor-model";
import { IngredientRow } from "./ingredient-row";
import { TagsInput } from "./tags-input";

const FALLBACK_UNITS = ["g", "kg", "ml", "l", "tsp", "tbsp", "cup", "each", "bunch", "clove", "can", "pack", "slice"];

export function RecipeEditor({ recipe }: { recipe?: Recipe }) {
  const router = useRouter();
  const me = useMe().data;
  const canEdit = me ? me.role !== "viewer" : true;
  const reference = useReferenceData();
  const units = reference.data?.units.map((u) => u.code) ?? FALLBACK_UNITS;
  const save = useSaveRecipe(recipe?.id);
  const duplicate = useDuplicateRecipe();
  const remove = useDeleteRecipe();
  const favourite = useToggleFavourite();
  const addToList = useAddRecipeToList();
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [confirmReplace, setConfirmReplace] = useState(false);
  const [focusKey, setFocusKey] = useState<string | null>(null);

  const form = useForm<RecipeForm>({ resolver: zodResolver(recipeFormSchema), defaultValues: toForm(recipe) });
  const { control, register, handleSubmit, setValue, reset, formState } = form;
  const rows = useFieldArray({ control, name: "ingredients" });
  const values = useWatch({ control });
  const hasDrafts = values.ingredients?.some((r) => r.draft) ?? false;

  const suggest = useMutation({
    mutationFn: (mode: "all" | "more") =>
      unwrap(
        api.POST("/api/ai/ingredient-suggestions", {
          body: {
            dishName: form.getValues("name"),
            servings: form.getValues("servings"),
            notes: form.getValues("notes") || null,
            mode,
            alreadyListed: mode === "more" ? form.getValues("ingredients").map((r) => r.name) : [],
          },
        }),
      ),
    onSuccess: (draft, mode) => {
      const drafted = draft.ingredients.map(fromSuggestion);
      if (mode === "all") {
        rows.replace(drafted);
      } else {
        rows.append(drafted, { shouldFocus: false });
      }
      toast.success(
        drafted.length === 0
          ? "Nothing more to suggest."
          : `${drafted.length} ${mode === "more" ? "more " : ""}ingredients drafted. Review, then save.`,
      );
    },
    onError: (err) => toast.error(errorMessage(err)),
  });

  function startSuggest(mode: "all" | "more") {
    if (!form.getValues("name").trim()) {
      form.setError("name", { message: "Name the dish first" });
      return;
    }
    if (mode === "all" && form.getValues("ingredients").length > 0) {
      setConfirmReplace(true);
      return;
    }
    suggest.mutate(mode);
  }

  const onSubmit = handleSubmit((data) =>
    save.mutate(toRequest(data, recipe?.isFavourite), {
      onSuccess: (saved) => {
        reset(toForm(saved));
        toast.success("Recipe saved");
        if (!recipe) router.replace(`/recipes/${saved.id}`);
      },
      onError: (err) => toast.error(errorMessage(err)),
    }),
  );

  const errors = formState.errors;

  return (
    <form onSubmit={onSubmit} className="space-y-6 pb-24" noValidate>
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon-lg"
          render={<Link href="/recipes" />}
          nativeButton={false}
          aria-label="Back to recipes"
        >
          <ArrowLeft />
        </Button>
        <h1 className="min-w-0 flex-1 truncate text-xl font-semibold tracking-tight">
          {recipe ? "Edit recipe" : "New recipe"}
        </h1>
        {recipe && (
          <>
            <Button
              type="button"
              variant="ghost"
              size="icon-lg"
              aria-pressed={recipe.isFavourite}
              aria-label={recipe.isFavourite ? "Remove from favourites" : "Add to favourites"}
              onClick={() => favourite.mutate({ id: recipe.id, isFavourite: !recipe.isFavourite })}
            >
              <Star className={cn(recipe.isFavourite && "fill-amber-400 text-amber-400")} />
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button type="button" variant="ghost" size="icon-lg" aria-label="More actions" />}>
                <MoreVertical />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem
                  onClick={() =>
                    addToList.mutate(
                      { recipeId: recipe.id },
                      {
                        onSuccess: () =>
                          toast.success("Added to this week’s cart and the shopping list", {
                            action: { label: "View list", onClick: () => router.push("/list") },
                          }),
                        onError: (err) => toast.error(errorMessage(err)),
                      },
                    )
                  }
                >
                  <ShoppingCart /> Add to shopping list
                </DropdownMenuItem>
                <DropdownMenuItem
                  onClick={() =>
                    duplicate.mutate(recipe.id, {
                      onSuccess: (copy) => {
                        toast.success("Duplicated");
                        router.push(`/recipes/${copy.id}`);
                      },
                      onError: (err) => toast.error(errorMessage(err)),
                    })
                  }
                >
                  <Copy /> Duplicate
                </DropdownMenuItem>
                {me?.canDelete && (
                  <DropdownMenuItem variant="destructive" onClick={() => setConfirmDelete(true)}>
                    <Trash2 /> Delete
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </>
        )}
      </div>

      <fieldset disabled={!canEdit} className="space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="recipe-name">Dish</Label>
          <Input
            id="recipe-name"
            {...register("name")}
            placeholder="e.g. Chicken katsu curry"
            className="h-12 text-lg"
            aria-invalid={!!errors.name || undefined}
            autoFocus={!recipe}
          />
          {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label>Servings</Label>
            <div className="flex h-11 items-center rounded-lg border">
              <button
                type="button"
                className="flex h-full w-11 items-center justify-center text-muted-foreground disabled:opacity-40"
                onClick={() => setValue("servings", Math.max(1, (values.servings ?? 2) - 1), { shouldDirty: true })}
                disabled={(values.servings ?? 2) <= 1}
                aria-label="Fewer servings"
              >
                <Minus className="size-4" />
              </button>
              <span className="flex-1 text-center font-medium tabular-nums" aria-live="polite">
                {values.servings}
              </span>
              <button
                type="button"
                className="flex h-full w-11 items-center justify-center text-muted-foreground disabled:opacity-40"
                onClick={() => setValue("servings", Math.min(50, (values.servings ?? 2) + 1), { shouldDirty: true })}
                disabled={(values.servings ?? 2) >= 50}
                aria-label="More servings"
              >
                <Plus className="size-4" />
              </button>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="prep">Prep (min)</Label>
            <Input id="prep" inputMode="numeric" {...register("prepMins")} placeholder="—" className="h-11" aria-invalid={!!errors.prepMins || undefined} />
          </div>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="tags">Tags</Label>
          <TagsInput id="tags" value={values.tags ?? []} onChange={(t) => setValue("tags", t, { shouldDirty: true })} />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="notes">Notes</Label>
          <Textarea id="notes" {...register("notes")} rows={2} placeholder="e.g. no coriander, vegetarian, kids version" />
          <p className="text-xs text-muted-foreground">Suggestions take these notes into account.</p>
        </div>
      </fieldset>

      <section className="space-y-3">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-lg font-semibold">
            Ingredients <span className="text-sm font-normal text-muted-foreground">{rows.fields.length || ""}</span>
          </h2>
          {canEdit && (
            <div className="flex gap-1.5">
              {rows.fields.length > 0 && (
                <Button type="button" variant="outline" className="h-10" disabled={suggest.isPending} onClick={() => startSuggest("more")}>
                  <Plus /> More
                </Button>
              )}
              <Button type="button" variant="secondary" className="h-10" disabled={suggest.isPending} onClick={() => startSuggest("all")}>
                {suggest.isPending ? <Loader2 className="animate-spin" /> : <WandSparkles />}
                {rows.fields.length > 0 ? "Regenerate" : "Suggest ingredients"}
              </Button>
            </div>
          )}
        </div>

        {hasDrafts && (
          <p className="flex items-start gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-500/10 dark:text-amber-200">
            <Sparkles className="mt-0.5 size-4 shrink-0" />
            AI drafts are not saved yet. Check amounts, edit or delete rows, then save the recipe.
          </p>
        )}

        {rows.fields.length === 0 && !suggest.isPending && (
          <p className="rounded-xl border border-dashed p-6 text-center text-sm text-muted-foreground">
            Tap <span className="font-medium text-foreground">Suggest ingredients</span> to draft them from the dish name, or add
            them yourself.
          </p>
        )}

        <ul className="space-y-2">
          {rows.fields.map((field, index) => (
            <IngredientRow
              key={field.id}
              index={index}
              count={rows.fields.length}
              control={control}
              register={register}
              setValue={setValue}
              errors={errors.ingredients?.[index]}
              units={units}
              autoFocus={field.key === focusKey}
              onRemove={() => rows.remove(index)}
              onMove={(to) => rows.move(index, to)}
            />
          ))}
        </ul>

        {canEdit && (
          <Button
            type="button"
            variant="outline"
            className="h-11 w-full border-dashed"
            onClick={() => {
              const row = emptyRow();
              setFocusKey(row.key);
              rows.append(row, { shouldFocus: false });
            }}
          >
            <Plus /> Add ingredient
          </Button>
        )}
      </section>

      <details className="group rounded-xl border p-4" open={!!recipe?.methodText || !!recipe?.sourceUrl}>
        <summary className="cursor-pointer list-none font-medium">
          Method and source <span className="text-sm font-normal text-muted-foreground">(optional)</span>
        </summary>
        <fieldset disabled={!canEdit} className="mt-4 space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="method">Cooking method</Label>
            <Textarea id="method" {...register("methodText")} rows={6} placeholder="Your own steps, if you want them here." />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="source">Source link</Label>
            <Input id="source" type="url" {...register("sourceUrl")} placeholder="https://" className="h-11" aria-invalid={!!errors.sourceUrl || undefined} />
            {errors.sourceUrl && <p className="text-xs text-destructive">{errors.sourceUrl.message}</p>}
          </div>
        </fieldset>
      </details>

      {canEdit && (
        <div className="fixed inset-x-0 bottom-16 z-20 border-t bg-background/95 p-3 backdrop-blur md:bottom-0">
          <div className="mx-auto flex max-w-5xl items-center gap-3">
            <p className="min-w-0 flex-1 truncate text-sm text-muted-foreground">
              {formState.isDirty || hasDrafts ? "Unsaved changes" : recipe ? "All changes saved" : ""}
            </p>
            <Button type="submit" className="h-11 px-6" disabled={save.isPending}>
              {save.isPending && <Loader2 className="animate-spin" />}
              Save recipe
            </Button>
          </div>
        </div>
      )}

      <Dialog open={confirmReplace} onOpenChange={setConfirmReplace}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Replace the ingredient list?</DialogTitle>
            <DialogDescription>Regenerating drafts a fresh list and replaces the rows below. Use “More” to add to them instead.</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" variant="outline" className="h-11" onClick={() => setConfirmReplace(false)}>
              Cancel
            </Button>
            <Button
              type="button"
              className="h-11"
              onClick={() => {
                setConfirmReplace(false);
                suggest.mutate("all");
              }}
            >
              Replace
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {recipe && (
        <Dialog open={confirmDelete} onOpenChange={setConfirmDelete}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete “{recipe.name}”?</DialogTitle>
              <DialogDescription>It will also be removed from any meal plans. This can’t be undone.</DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button type="button" variant="outline" className="h-11" onClick={() => setConfirmDelete(false)}>
                Cancel
              </Button>
              <Button
                type="button"
                variant="destructive"
                className="h-11"
                disabled={remove.isPending}
                onClick={() =>
                  remove.mutate(recipe.id, {
                    onSuccess: () => {
                      toast.success("Recipe deleted");
                      router.replace("/recipes");
                    },
                    onError: (err) => toast.error(errorMessage(err)),
                  })
                }
              >
                Delete
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </form>
  );
}
