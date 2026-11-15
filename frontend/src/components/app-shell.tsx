"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import { CalendarDays, BookOpen, ShoppingCart, Settings } from "lucide-react";
import { cn } from "cn";
import { ApiError } from "@/lib/api/client";
import { useMe } from "@/lib/queries/me";

const tabs = [
  { href: "/plan", label: "Plan", icon: CalendarDays },
  { href: "/recipes", label: "Recipes", icon: BookOpen },
  { href: "/list", label: "List", icon: ShoppingCart },
] as const;

/**
 * Signed-in frame: a bottom bar on phones and a top bar on desktop. The frame and the page always render
 * (pages show their own loading states), and anonymous visitors are sent to sign in once /api/me says 401.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const me = useMe();
  const unauthorised = me.error instanceof ApiError && me.error.status === 401;

  useEffect(() => {
    if (unauthorised) {
      router.replace(`/sign-in?returnUrl=${encodeURIComponent(pathname)}`);
    }
  }, [unauthorised, pathname, router]);

  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-30 border-b bg-background/90 backdrop-blur supports-[backdrop-filter]:bg-background/70">
        <div className="mx-auto flex h-14 max-w-5xl items-center gap-4 px-4">
          <Link href="/plan" className="text-lg font-semibold tracking-tight">
            Pantrio
          </Link>
          <nav className="hidden flex-1 items-center gap-1 md:flex" aria-label="Main">
            {tabs.map((t) => (
              <Link
                key={t.href}
                href={t.href}
                className={cn(
                  "rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground hover:text-foreground",
                  isActive(t.href) && "bg-muted text-foreground",
                )}
              >
                {t.label}
              </Link>
            ))}
          </nav>
          <Link
            href="/settings"
            className={cn(
              "ml-auto flex size-10 items-center justify-center rounded-full text-muted-foreground hover:text-foreground",
              isActive("/settings") && "text-foreground",
            )}
            aria-label="Settings"
          >
            <Settings className="size-5" />
          </Link>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 pt-4 pb-28 md:pb-10">
        {me.isError && !unauthorised ? (
          <div className="flex flex-col items-center gap-3 py-16 text-center">
            <p className="font-medium">Pantrio can’t reach its server right now.</p>
            <button className="text-sm underline" onClick={() => me.refetch()}>
              Try again
            </button>
          </div>
        ) : (
          children
        )}
      </main>

      <nav
        className="fixed inset-x-0 bottom-0 z-30 border-t bg-background/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden"
        aria-label="Main"
      >
        <div className="mx-auto grid max-w-md grid-cols-3">
          {tabs.map((t) => (
            <Link
              key={t.href}
              href={t.href}
              className={cn(
                "flex h-16 flex-col items-center justify-center gap-1 text-xs font-medium text-muted-foreground",
                isActive(t.href) && "text-primary",
              )}
            >
              <t.icon className="size-6" aria-hidden />
              {t.label}
            </Link>
          ))}
        </div>
      </nav>
    </div>
  );
}
