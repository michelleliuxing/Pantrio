"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api/client";

export default function Home() {
  const health = useQuery({
    queryKey: ["health"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/health");
      if (error) throw new Error("API unavailable");
      return data;
    },
  });

  return (
    <main className="mx-auto flex max-w-md flex-col gap-2 p-6">
      <h1 className="text-2xl font-semibold">Pantrio</h1>
      <p className="text-muted-foreground">
        API: {health.isPending ? "checking…" : health.isError ? "unreachable" : health.data?.status}
      </p>
    </main>
  );
}
