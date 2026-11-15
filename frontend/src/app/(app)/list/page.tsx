"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Check, ChevronDown, ExternalLink, Plus, Trash2, TrendingUp } from "lucide-react";
import { cn } from "cn";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { errorMessage } from "@/lib/api/client";
import { dayOfMonth, parseDate } from "@/lib/dates";
import { CATEGORIES, CATEGORY_LABELS } from "@/lib/labels";
import { useIngredientSearch } from "@/lib/queries/recipes";
import { useMe } from "@/lib/queries/me";
import {
  useAddExtra,
  useClearCompleted,
  useDeleteListItem,
  useSetPurchaseUrl,
  useShoppingList,
  useUpdateListItem,
  type ListItem,
} from "@/lib/queries/list";
import { isHttpUrl, linkHost } from "@/components/recipes/editor-model";
import { useDebounced } from "@/hooks/use-debounced";

export default function ListPage() {
  const list = useShoppingList();
  const me = useMe().data;
  const canEdit = me ? me.role !== "viewer" : false;
  const clear = useClearCompleted();

  const groups = useMemo(() => {
    const items = list.data?.items ?? [];
    const toBuy = items.filter((i) => i.state === "needed" && !i.inCheckGroup);
    return {
      aisles: CATEGORIES.map((c) => ({ category: c, items: toBuy.filter((i) => i.category === c) })).filter((g) => g.items.length),
      check: items.filter((i) => i.inCheckGroup && i.state === "needed"),
      done: items.filter((i) => i.state !== "needed"),
      remaining: toBuy.length,
    };
  }, [list.data]);

  return (
    <div className="space-y-5 pb-8">
      <div className="flex items-baseline justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">List</h1>
        {list.data && list.data.items.length > 0 && (
          <p className="text-sm text-muted-foreground">
            {groups.remaining === 0 ? "All done" : `${groups.remaining} to buy`}
          </p>
        )}
      </div>

      {canEdit && <AddExtraForm />}

      {list.isPending ? (
        <div className="space-y-2">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-14 rounded-xl" />
          ))}
        </div>
      ) : list.data?.items.length === 0 ? (
        <div className="rounded-xl border border-dashed p-8 text-center">
          <p className="font-medium">Your list is empty</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Plan some dishes, then tap <span className="font-medium text-foreground">Build shopping list</span>.
          </p>
          <Button render={<Link href="/plan" />} nativeButton={false} variant="outline" className="mt-4 h-10">
            Go to the plan
          </Button>
        </div>
      ) : (
        <>
          {groups.aisles.map((g) => (
            <Group key={g.category} title={CATEGORY_LABELS[g.category]} items={g.items} canEdit={canEdit} />
          ))}

          {groups.check.length > 0 && (
            <Group
              title="Check you have"
              hint="Pantry basics. Tap “Need it” if you’re out."
              items={groups.check}
              canEdit={canEdit}
              check
            />
          )}

          {groups.done.length > 0 && (
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-medium tracking-wide text-muted-foreground uppercase">
                  Done <span className="normal-case">({groups.done.length})</span>
                </h2>
                {canEdit && (
                  <Button
                    variant="ghost"
                    className="h-9 text-muted-foreground"
                    disabled={clear.isPending}
                    onClick={() =>
                      clear.mutate(undefined, {
                        onSuccess: () => toast("Cleared bought and have-it items"),
                        onError: (e) => toast.error(errorMessage(e)),
                      })
                    }
                  >
                    <Trash2 /> Clear
                  </Button>
                )}
              </div>
              <ul className="divide-y rounded-xl border opacity-70">
                {groups.done.map((i) => (
                  <Row key={i.id} item={i} canEdit={canEdit} />
                ))}
              </ul>
            </section>
          )}
        </>
      )}
    </div>
  );
}

function Group({ title, hint, items, canEdit, check }: { title: string; hint?: string; items: ListItem[]; canEdit: boolean; check?: boolean }) {
  return (
    <section className="space-y-2">
      <div>
        <h2 className="text-sm font-medium tracking-wide text-muted-foreground uppercase">{title}</h2>
        {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      </div>
      <ul className={cn("divide-y rounded-xl border", check && "border-dashed")}>
        {items.map((i) => (
          <Row key={i.id} item={i} canEdit={canEdit} check={check} />
        ))}
      </ul>
    </section>
  );
}

function Row({ item, canEdit, check }: { item: ListItem; canEdit: boolean; check?: boolean }) {
  const update = useUpdateListItem();
  const remove = useDeleteListItem();
  const [open, setOpen] = useState(false);
  const done = item.state !== "needed";
  const fail = (e: unknown) => toast.error(errorMessage(e));
  const set = (state: ListItem["state"]) => update.mutate({ id: item.id, state }, { onError: fail });

  return (
    <li>
      <div className="flex items-stretch">
        <button
          type="button"
          disabled={!canEdit}
          onClick={() => set(done ? "needed" : check ? "have_it" : "bought")}
          className="flex min-h-14 min-w-0 flex-1 items-center gap-3 px-3 text-left active:bg-muted"
          aria-pressed={done}
          aria-label={`${item.name}${item.amount ? `, ${item.amount}` : ""}: ${done ? "done, tap to undo" : check ? "tap if you have it" : "tap when bought"}`}
        >
          <span
            className={cn(
              "flex size-7 shrink-0 items-center justify-center rounded-full border-2 transition-colors",
              done ? "border-primary bg-primary text-primary-foreground" : "border-muted-foreground/40",
            )}
          >
            {done && <Check className="size-4" strokeWidth={3} />}
          </span>
          <span className="min-w-0 flex-1">
            <span className={cn("block truncate text-base", done && "text-muted-foreground line-through")}>{item.name}</span>
            {item.amountIncreased && (
              <span className="mt-0.5 inline-flex items-center gap-1 rounded bg-amber-100 px-1.5 py-0.5 text-xs font-medium text-amber-900 dark:bg-amber-500/15 dark:text-amber-200">
                <TrendingUp className="size-3" /> More needed{item.boughtAmount ? ` (bought ${item.boughtAmount})` : ""}
              </span>
            )}
            {item.state === "have_it" && <span className="text-xs text-muted-foreground">Have it</span>}
          </span>
          {item.amount && <span className="shrink-0 text-right text-sm font-medium tabular-nums">{item.amount}</span>}
        </button>
        {item.purchaseUrl && (
          <a
            href={item.purchaseUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="flex w-10 shrink-0 items-center justify-center text-muted-foreground hover:text-foreground"
            aria-label={`Buy ${item.name} at ${linkHost(item.purchaseUrl)}`}
            title={linkHost(item.purchaseUrl)}
          >
            <ExternalLink className="size-4" />
          </a>
        )}
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className="flex w-11 shrink-0 items-center justify-center text-muted-foreground"
          aria-expanded={open}
          aria-label={`Details for ${item.name}`}
        >
          <ChevronDown className={cn("size-4 transition-transform", open && "rotate-180")} />
        </button>
      </div>

      {open && (
        <div className="space-y-3 border-t bg-muted/30 px-3 py-3 text-sm">
          {item.sources.length > 0 ? (
            <ul className="space-y-1">
              {item.sources.map((s) => (
                <li key={s.entryId} className="flex justify-between gap-3">
                  <span className="truncate">
                    {s.recipeName || "Removed dish"}
                    <span className="ml-1.5 text-xs text-muted-foreground">wk {dayOfMonth(s.weekStart)} {monthShort(s.weekStart)}</span>
                  </span>
                  <span className="shrink-0 text-muted-foreground tabular-nums">{s.amount}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted-foreground">Added by hand.</p>
          )}
          {canEdit && item.ingredientId && <BuyLinkEditor key={item.purchaseUrl ?? ""} item={item} />}
          {!item.ingredientId && item.purchaseUrl && (
            <p className="text-xs text-muted-foreground">Buy link from the catalogue entry “{item.name}”.</p>
          )}
          {canEdit && (
            <div className="flex flex-wrap gap-2">
              {item.state === "needed" && !check && (
                <Button variant="outline" className="h-10" onClick={() => set("have_it")}>
                  Have it already
                </Button>
              )}
              {check && item.state === "needed" && (
                <Button variant="outline" className="h-10" onClick={() => update.mutate({ id: item.id, inCheckGroup: false }, { onError: fail })}>
                  Need it
                </Button>
              )}
              {item.isExtra && (
                <Button variant="destructive" className="h-10" onClick={() => remove.mutate(item.id, { onError: fail })}>
                  <Trash2 /> Remove
                </Button>
              )}
            </div>
          )}
        </div>
      )}
    </li>
  );
}

/** Add, change or remove the buy link of the catalogue ingredient behind a list line. */
function BuyLinkEditor({ item }: { item: ListItem }) {
  const setUrl = useSetPurchaseUrl();
  const [value, setValue] = useState(item.purchaseUrl ?? "");
  const changed = value.trim() !== (item.purchaseUrl ?? "");
  const invalid = value.trim() !== "" && !isHttpUrl(value);

  return (
    <form
      className="space-y-1"
      onSubmit={(e) => {
        e.preventDefault();
        if (invalid || !item.ingredientId) return;
        setUrl.mutate(
          { ingredientId: item.ingredientId, purchaseUrl: value.trim() },
          {
            onSuccess: () => toast.success(value.trim() ? "Buy link saved" : "Buy link removed"),
            onError: (err) => toast.error(errorMessage(err)),
          },
        );
      }}
    >
      <label className="text-xs text-muted-foreground" htmlFor={`buy-${item.id}`}>
        Where to buy
      </label>
      <div className="flex gap-2">
        <input
          id={`buy-${item.id}`}
          type="url"
          inputMode="url"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder="Paste a Woolworths or Coles product link"
          aria-invalid={invalid || undefined}
          className="h-10 min-w-0 flex-1 rounded-lg border border-input bg-background px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive md:text-sm"
        />
        <Button type="submit" variant="outline" className="h-10" disabled={!changed || invalid || setUrl.isPending}>
          Save
        </Button>
      </div>
      {invalid && <p className="text-xs text-destructive">Paste the full link, starting with https://</p>}
    </form>
  );
}

function monthShort(iso: string) {
  return new Intl.DateTimeFormat("en-AU", { month: "short", timeZone: "UTC" }).format(parseDate(iso));
}

/** Free-typed extras. Known catalogue names are suggested and land in the right aisle. */
function AddExtraForm() {
  const add = useAddExtra();
  const [name, setName] = useState("");
  const [qty, setQty] = useState("");
  const [focused, setFocused] = useState(false);
  const search = useDebounced(name, 200);
  const suggestions = useIngredientSearch(search, focused);
  const options = (suggestions.data ?? []).slice(0, 5);

  function submit(value = name) {
    const trimmed = value.trim();
    if (!trimmed) return;
    add.mutate(
      { name: trimmed, quantityText: qty.trim() || null },
      {
        onSuccess: (item) => toast.success(`Added ${item.name}`),
        onError: (e) => toast.error(errorMessage(e)),
      },
    );
    setName("");
    setQty("");
  }

  return (
    <form
      className="relative flex gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
    >
      <input
        value={name}
        onChange={(e) => setName(e.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setTimeout(() => setFocused(false), 150)}
        placeholder="Add an extra, e.g. milk"
        aria-label="Extra item"
        autoComplete="off"
        className="h-12 min-w-0 flex-1 rounded-lg border border-input bg-transparent px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
      />
      <input
        value={qty}
        onChange={(e) => setQty(e.target.value)}
        placeholder="Qty"
        aria-label="Quantity (optional)"
        className="h-12 w-20 rounded-lg border border-input bg-transparent px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
      />
      <Button type="submit" className="size-12" disabled={!name.trim()} aria-label="Add extra">
        <Plus className="size-5" />
      </Button>
      {focused && name.trim() && options.length > 0 && (
        <ul className="absolute inset-x-0 top-full z-20 mt-1 rounded-lg border bg-popover p-1 shadow-md" role="listbox">
          {options.map((o) => (
            <li
              key={o.id}
              role="option"
              aria-selected={false}
              className="flex cursor-pointer justify-between rounded-md px-3 py-2.5 text-sm hover:bg-muted"
              onMouseDown={(e) => {
                e.preventDefault();
                submit(o.name);
              }}
            >
              <span>{o.name}</span>
              <span className="text-xs text-muted-foreground">{CATEGORY_LABELS[o.category]}</span>
            </li>
          ))}
        </ul>
      )}
    </form>
  );
}
