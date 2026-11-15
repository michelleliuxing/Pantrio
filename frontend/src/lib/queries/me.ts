"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, unwrap, type Schemas } from "@/lib/api/client";

export const meKey = ["me"] as const;

export function useMe() {
  return useQuery({
    queryKey: meKey,
    queryFn: () => unwrap(api.GET("/api/me")),
    retry: (count, err) => !(err instanceof ApiError && err.status === 401) && count < 1,
    staleTime: 5 * 60_000,
  });
}

export function useAuthProviders() {
  return useQuery({
    queryKey: ["auth-providers"],
    queryFn: () => unwrap(api.GET("/api/auth/providers")),
    staleTime: Infinity,
  });
}

export function useLogout() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/api/auth/logout")),
    onSuccess: () => {
      qc.clear();
      window.location.assign("/sign-in");
    },
  });
}

export function useUpdateHousehold() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Schemas["UpdateHouseholdRequest"]) => unwrap(api.PATCH("/api/household", { body })),
    onSuccess: (household) => {
      qc.setQueryData<Schemas["MeResponse"]>(meKey, (me) => (me ? { ...me, household } : me));
    },
  });
}
