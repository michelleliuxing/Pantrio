"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { ChevronLeft, ChevronRight, Copy, Loader2, MapPin, Plus, ShoppingCart, X } from "lucide-react";
import { cn } from "cn";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EntrySheet } from "@/components/plan/entry-sheet";
import { RecipePicker } from "@/components/plan/recipe-picker";
import { errorMessage } from "@/lib/api/client";
import { addDays, dayName, dayOfMonth, formatWeek, mondayOf, todayIn, weekDays } from "@/lib/dates";
import { MEAL_SLOTS, slotLabel, type MealSlot } from "@/lib/meals";
import { useMe } from "@/lib/queries/me";
import {
  useAddEntry,
  useBuildList,
  useCopyPreviousWeek,
  useDeleteEntry,
  useMealPlan,
  useUpdateEntry,
  type PlanEntry,
} from "@/lib/queries/plan";

type Target = { day: string; slot: MealSlot } | null; // null = meal cart

export default function PlanPage() {
  const me = useMe().data;
  if (!me) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-9 w-40" />
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }
  return <Planner timeZone={me.household.timeZone} hidden={me.household.hiddenMealSlots} canEdit={me.role !== "viewer"} />;
}

function Planner({ timeZone, hidden, canEdit }: { timeZone: string; hidden: MealSlot[]; canEdit: boolean }) {
  const router = useRouter();
  const today = todayIn(timeZone);
  const [week, setWeek] = useState(() => mondayOf(today));
  const plan = useMealPlan(week);
  const add = useAddEntry(week);
  const update = useUpdateEntry(week);
  const remove = useDeleteEntry(week);
  const copy = useCopyPreviousWeek(week);
  const build = useBuildList();

  const [pickerTarget, setPickerTarget] = useState<Target | undefined>(undefined);
  const [selected, setSelected] = useState<PlanEntry | null>(null);
  const [placing, setPlacing] = useState<PlanEntry | null>(null);

  const slots = MEAL_SLOTS.filter((s) => !hidden.includes(s));
  const entries = plan.data?.entries ?? [];
  const cart = entries.filter((e) => !e.day);
  const at = (day: string, slot: MealSlot) => entries.filter((e) => e.day === day && e.mealSlot === slot);
  const fail = (err: unknown) => toast.error(errorMessage(err));

  function place(entry: PlanEntry, target: Target) {
    setPlacing(null);
    update.mutate(
      { id: entry.id, day: target?.day ?? null, mealSlot: target?.slot ?? null, servingsOverride: entry.servingsOverride },
      { onError: fail },
    );
  }

  function changeWeek(next: string) {
    setPlacing(null);
    setWeek(next);
  }

  const targetLabel =
    pickerTarget === undefined
      ? ""
      : pickerTarget === null
        ? "the meal cart"
        : `${dayName(pickerTarget.day, "long")} ${slotLabel(pickerTarget.slot).toLowerCase()}`;

  return (
    <div className="space-y-4 pb-20">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-2xl font-semibold tracking-tight">Plan</h1>
        <div className="flex items-center gap-1">
          <Button variant="ghost" size="icon-lg" onClick={() => changeWeek(addDays(week, -7))} aria-label="Previous week">
            <ChevronLeft />
          </Button>
          <button
            type="button"
            className="min-w-28 rounded-md px-2 py-1 text-center text-sm font-medium"
            onClick={() => changeWeek(mondayOf(today))}
            title="Go to this week"
          >
            {formatWeek(week)}
            {week === mondayOf(today) && <span className="block text-xs font-normal text-muted-foreground">This week</span>}
          </button>
          <Button variant="ghost" size="icon-lg" onClick={() => changeWeek(addDays(week, 7))} aria-label="Next week">
            <ChevronRight />
          </Button>
        </div>
      </div>

      {placing && (
        <div className="sticky top-16 z-20 flex items-center gap-2 rounded-xl bg-primary px-4 py-3 text-primary-foreground shadow-lg">
          <MapPin className="size-4 shrink-0" />
          <p className="min-w-0 flex-1 text-sm">
            Tap a meal for <span className="font-semibold">{placing.recipeName}</span>
          </p>
          {placing.day && (
            <button type="button" className="rounded-md px-2 py-1 text-sm underline" onClick={() => place(placing, null)}>
              To cart
            </button>
          )}
          <button type="button" onClick={() => setPlacing(null)} aria-label="Cancel placing" className="flex size-8 items-center justify-center">
            <X className="size-4" />
          </button>
        </div>
      )}

      <section className="rounded-xl border bg-muted/40 p-3">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="font-medium">
            Meal cart <span className="text-sm font-normal text-muted-foreground">{cart.length || ""}</span>
          </h2>
          {canEdit && (
            <Button variant="outline" className="h-9" onClick={() => setPickerTarget(null)}>
              <Plus /> Add dishes
            </Button>
          )}
        </div>
        {plan.isPending ? (
          <Skeleton className="h-9 w-full" />
        ) : cart.length === 0 ? (
          <p className="text-sm text-muted-foreground">Dishes for this week that aren’t on a day yet. They still go on the list.</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {cart.map((e) => (
              <DishChip
                key={e.id}
                entry={e}
                active={placing?.id === e.id}
                onTap={() => (canEdit ? setPlacing(placing?.id === e.id ? null : e) : setSelected(e))}
                onMore={() => setSelected(e)}
              />
            ))}
          </div>
        )}
        {cart.length > 0 && !placing && canEdit && (
          <p className="mt-2 text-xs text-muted-foreground">Tap a dish, then tap a meal to place it.</p>
        )}
      </section>

      {plan.isPending ? (
        <Skeleton className="h-96 rounded-xl" />
      ) : (
        <div className="grid gap-3 lg:grid-cols-7">
          {weekDays(week).map((day) => (
            <section
              key={day}
              className={cn("rounded-xl border p-3 lg:p-2", day === today && "border-primary/50 ring-1 ring-primary/30")}
              aria-label={`${dayName(day, "long")} ${dayOfMonth(day)}`}
            >
              <h3 className="mb-2 flex items-baseline gap-1.5 text-sm font-semibold">
                {dayName(day)} <span className="font-normal text-muted-foreground">{dayOfMonth(day)}</span>
                {day === today && <span className="ml-auto text-xs font-medium text-primary">Today</span>}
              </h3>
              <div className="space-y-1.5">
                {slots.map((slot) => {
                  const here = at(day, slot);
                  return (
                    <div
                      key={slot}
                      className={cn(
                        "flex min-h-12 items-center gap-2 rounded-lg px-2 py-1 lg:flex-col lg:items-stretch",
                        placing ? "cursor-pointer bg-primary/5 ring-1 ring-primary/40 ring-dashed hover:bg-primary/10" : "bg-muted/40",
                      )}
                      onClick={placing ? () => place(placing, { day, slot }) : undefined}
                      role={placing ? "button" : undefined}
                      aria-label={placing ? `Place ${placing.recipeName} on ${dayName(day, "long")} ${slot}` : undefined}
                    >
                      <span className="w-16 shrink-0 text-xs font-medium text-muted-foreground lg:w-auto">{slotLabel(slot)}</span>
                      <div className="flex min-w-0 flex-1 flex-wrap gap-1.5">
                        {here.map((e) => (
                          <DishChip
                            key={e.id}
                            entry={e}
                            compact
                            active={placing?.id === e.id}
                            onTap={placing ? undefined : () => setSelected(e)}
                          />
                        ))}
                      </div>
                      {!placing && canEdit && (
                        <button
                          type="button"
                          className="flex size-9 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-background lg:hidden"
                          onClick={() => setPickerTarget({ day, slot })}
                          aria-label={`Add a dish to ${dayName(day, "long")} ${slot}`}
                        >
                          <Plus className="size-4" />
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            </section>
          ))}
        </div>
      )}

      {canEdit && plan.data && entries.length === 0 && (
        <Button
          variant="outline"
          className="h-11 w-full"
          disabled={copy.isPending}
          onClick={() =>
            copy.mutate(undefined, {
              onSuccess: (p) => toast(p.entries.length ? `Copied ${p.entries.length} dishes from last week` : "Last week was empty"),
              onError: fail,
            })
          }
        >
          <Copy /> Copy last week’s plan
        </Button>
      )}

      {canEdit && (
        <div className="fixed inset-x-0 bottom-16 z-20 border-t bg-background/95 p-3 backdrop-blur md:bottom-0">
          <div className="mx-auto flex max-w-5xl items-center gap-3">
            <p className="min-w-0 flex-1 truncate text-sm text-muted-foreground">
              {entries.length} {entries.length === 1 ? "dish" : "dishes"} this week
            </p>
            <Button
              className="h-11 px-5"
              disabled={build.isPending || entries.length === 0}
              onClick={() =>
                build.mutate(week, {
                  onSuccess: (list) => {
                    toast.success(`Shopping list updated: ${list.items.length} items`);
                    router.push("/list");
                  },
                  onError: fail,
                })
              }
            >
              {build.isPending ? <Loader2 className="animate-spin" /> : <ShoppingCart />}
              Build shopping list
            </Button>
          </div>
        </div>
      )}

      <RecipePicker
        open={pickerTarget !== undefined}
        onOpenChange={(o) => !o && setPickerTarget(undefined)}
        target={targetLabel}
        onPick={(recipe) =>
          add.mutateAsync({ recipeId: recipe.id, day: pickerTarget?.day ?? null, mealSlot: pickerTarget?.slot ?? null }).catch(fail)
        }
      />

      <EntrySheet
        entry={selected}
        onClose={() => setSelected(null)}
        onServings={(e, servings) => {
          const updated = { ...e, servingsOverride: servings === e.recipeServings ? null : servings };
          setSelected(updated);
          update.mutate({ id: e.id, day: e.day, mealSlot: e.mealSlot, servingsOverride: updated.servingsOverride }, { onError: fail });
        }}
        onPlace={(e) => {
          setSelected(null);
          setPlacing(e);
        }}
        onToCart={(e) => {
          setSelected(null);
          place(e, null);
        }}
        onRemove={(e) => {
          setSelected(null);
          remove.mutate(e.id, {
            onSuccess: () => toast(`Removed ${e.recipeName}`),
            onError: fail,
          });
        }}
      />
    </div>
  );
}

function DishChip({
  entry,
  active,
  compact,
  onTap,
  onMore,
}: {
  entry: PlanEntry;
  active?: boolean;
  compact?: boolean;
  onTap?: () => void;
  onMore?: () => void;
}) {
  const scaled = entry.servingsOverride != null;
  return (
    <span
      className={cn(
        "inline-flex max-w-full items-center rounded-full border bg-background text-sm shadow-xs",
        active && "border-primary bg-primary text-primary-foreground",
      )}
    >
      <button
        type="button"
        onClick={
          onTap
            ? (ev) => {
                ev.stopPropagation();
                onTap();
              }
            : undefined
        }
        className={cn("min-w-0 truncate px-3 text-left", compact ? "h-8" : "h-9")}
      >
        {entry.recipeName}
        {scaled && <span className={cn("ml-1 text-xs", active ? "opacity-80" : "text-muted-foreground")}>×{entry.servingsOverride}</span>}
      </button>
      {onMore && (
        <button
          type="button"
          onClick={(ev) => {
            ev.stopPropagation();
            onMore();
          }}
          className={cn("flex h-9 w-8 items-center justify-center border-l text-xs", active ? "border-primary-foreground/30" : "text-muted-foreground")}
          aria-label={`Options for ${entry.recipeName}`}
        >
          •••
        </button>
      )}
    </span>
  );
}
