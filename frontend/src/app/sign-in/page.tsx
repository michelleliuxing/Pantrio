"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, errorMessage, unwrap } from "@/lib/api/client";
import { useAuthProviders } from "@/lib/queries/me";

export default function SignInPage() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-sm flex-col justify-center gap-8 px-6 py-12">
      <div className="space-y-2 text-center">
        <h1 className="text-3xl font-semibold tracking-tight">Pantrio</h1>
        <p className="text-muted-foreground">Plan the week’s meals together. The shopping list builds itself.</p>
      </div>
      <Suspense fallback={null}>
        <SignInOptions />
      </Suspense>
    </main>
  );
}

function SignInOptions() {
  const params = useSearchParams();
  const returnUrl = params.get("returnUrl") ?? "/plan";
  const error = params.get("error");
  const providers = useAuthProviders();

  return (
    <div className="space-y-6">
      {error && (
        <p role="alert" className="rounded-lg bg-destructive/10 p-3 text-sm text-destructive">
          Google sign-in didn’t complete. Please try again.
        </p>
      )}

      <a
        href={`/api/auth/google/login?returnUrl=${encodeURIComponent(returnUrl)}`}
        aria-disabled={!providers.data?.google}
        className="flex h-12 w-full items-center justify-center gap-3 rounded-lg border bg-background text-base font-medium shadow-xs hover:bg-muted aria-disabled:pointer-events-none aria-disabled:opacity-50"
      >
        <GoogleMark />
        Continue with Google
      </a>
      {providers.data && !providers.data.google && (
        <p className="text-center text-xs text-muted-foreground">
          Google sign-in isn’t configured on this server yet.
        </p>
      )}

      {providers.data?.devLogin && <DevLogin returnUrl={returnUrl} />}
    </div>
  );
}

/** Development only: the API refuses this endpoint outside Development/Testing. */
function DevLogin({ returnUrl }: { returnUrl: string }) {
  const qc = useQueryClient();
  const [email, setEmail] = useState("dev@pantrio.local");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await unwrap(api.POST("/api/auth/dev-login", { body: { email } }));
      qc.clear();
      window.location.assign(returnUrl.startsWith("/") ? returnUrl : "/plan");
    } catch (err) {
      setError(errorMessage(err));
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-3 rounded-xl border border-dashed p-4">
      <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Development sign-in</p>
      <div className="space-y-1.5">
        <Label htmlFor="dev-email">Email</Label>
        <Input id="dev-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} className="h-11" />
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <Button type="submit" variant="secondary" className="h-11 w-full" disabled={busy}>
        {busy && <Loader2 className="animate-spin" />}
        Sign in without Google
      </Button>
    </form>
  );
}

function GoogleMark() {
  return (
    <svg viewBox="0 0 48 48" className="size-5" aria-hidden>
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z" />
      <path fill="#FF3D00" d="m6.3 14.7 6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-7.9l-6.5 5C9.5 39.6 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
    </svg>
  );
}
