"use client";

import { useState } from "react";
import { toast } from "sonner";
import { LogOut } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { errorMessage, type Schemas } from "@/lib/api/client";
import { useLogout, useMe, useUpdateHousehold } from "@/lib/queries/me";
import { MEAL_SLOTS, slotLabel } from "@/lib/meals";

const TIME_ZONES = [
  "Australia/Sydney",
  "Australia/Melbourne",
  "Australia/Brisbane",
  "Australia/Adelaide",
  "Australia/Darwin",
  "Australia/Perth",
  "Australia/Hobart",
  "Pacific/Auckland",
];

export default function SettingsPage() {
  const me = useMe().data;
  if (!me) {
    return (
      <div className="mx-auto max-w-lg space-y-4">
        <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
        <Skeleton className="h-40 w-full rounded-xl" />
        <Skeleton className="h-44 w-full rounded-xl" />
      </div>
    );
  }
  return <SettingsForm key={me.household.id} me={me} />;
}

function SettingsForm({ me }: { me: Schemas["MeResponse"] }) {
  const update = useUpdateHousehold();
  const logout = useLogout();
  const [name, setName] = useState(me.household.name);
  const [timeZone, setTimeZone] = useState(me.household.timeZone);
  const [prefs, setPrefs] = useState(me.household.dietaryPrefs.join(", "));
  const isOwner = me.role === "owner";
  const hidden = new Set(me.household.hiddenMealSlots);

  function save(body: Schemas["UpdateHouseholdRequest"], message = "Saved") {
    update.mutate(body, {
      onSuccess: () => toast.success(message),
      onError: (err) => toast.error(errorMessage(err)),
    });
  }

  return (
    <div className="mx-auto max-w-lg space-y-8">
      <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>

      <section className="space-y-4">
        <h2 className="text-sm font-medium tracking-wide text-muted-foreground uppercase">Household</h2>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            save({
              name,
              timeZone,
              dietaryPrefs: prefs
                .split(",")
                .map((p) => p.trim())
                .filter(Boolean),
            });
          }}
        >
          <div className="space-y-1.5">
            <Label htmlFor="household-name">Name</Label>
            <Input id="household-name" value={name} onChange={(e) => setName(e.target.value)} disabled={!isOwner} className="h-11" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="time-zone">Time zone</Label>
            <select
              id="time-zone"
              value={timeZone}
              onChange={(e) => setTimeZone(e.target.value)}
              disabled={!isOwner}
              className="h-11 w-full rounded-lg border border-input bg-transparent px-3 text-sm"
            >
              {[...new Set([timeZone, ...TIME_ZONES])].map((tz) => (
                <option key={tz} value={tz}>
                  {tz.replace("_", " ")}
                </option>
              ))}
            </select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="dietary">Dietary preferences</Label>
            <Input
              id="dietary"
              placeholder="e.g. no pork, less spicy"
              value={prefs}
              onChange={(e) => setPrefs(e.target.value)}
              disabled={!isOwner}
              className="h-11"
            />
            <p className="text-xs text-muted-foreground">Comma separated. Used when suggesting ingredients.</p>
          </div>
          {isOwner && (
            <Button type="submit" className="h-11" disabled={update.isPending}>
              Save household
            </Button>
          )}
        </form>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-medium tracking-wide text-muted-foreground uppercase">Meal rows</h2>
        <p className="text-sm text-muted-foreground">Hide a row you never plan, such as breakfast.</p>
        <div className="divide-y rounded-xl border">
          {MEAL_SLOTS.map((slot) => (
            <label key={slot} className="flex h-14 items-center justify-between px-4">
              <span className="font-medium">{slotLabel(slot)}</span>
              <Switch
                checked={!hidden.has(slot)}
                disabled={!isOwner || update.isPending}
                onCheckedChange={(shown) => {
                  const next = new Set(hidden);
                  if (shown) next.delete(slot);
                  else next.add(slot);
                  save({ hiddenMealSlots: [...next] }, shown ? `${slotLabel(slot)} shown` : `${slotLabel(slot)} hidden`);
                }}
              />
            </label>
          ))}
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-medium tracking-wide text-muted-foreground uppercase">Account</h2>
        <div className="rounded-xl border p-4">
          <p className="font-medium">{me.user.displayName}</p>
          <p className="text-sm text-muted-foreground">{me.user.email}</p>
        </div>
        <Button variant="outline" className="h-11 w-full" onClick={() => logout.mutate()} disabled={logout.isPending}>
          <LogOut />
          Sign out
        </Button>
      </section>
    </div>
  );
}
