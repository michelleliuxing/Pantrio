"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "@/lib/api/client";

export type MealPlan = Schemas["MealPlanDto"];
export type PlanEntry = Schemas["MealPlanEntryDto"];
export type ShoppingList = Schemas["ShoppingListDto"];
export type ListItem = Schemas["ShoppingListItemDto"];

export const planKey = (week: string) => ["meal-plans", week] as const;
export const listKey = ["shopping-list"] as const;

export function useMealPlan(week: string) {
  return useQuery({
    queryKey: planKey(week),
    queryFn: () => unwrap(api.GET("/api/meal-plans/{weekStart}", { params: { path: { weekStart: week } } })),
  });
}

export function useAddEntry(week: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Schemas["AddEntryRequest"]) =>
      unwrap(api.POST("/api/meal-plans/{weekStart}/entries", { params: { path: { weekStart: week } }, body })),
    onSuccess: (entry) => {
      qc.setQueryData<MealPlan>(planKey(week), (plan) =>
        plan ? { ...plan, entries: [...plan.entries, entry] } : plan,
      );
      qc.invalidateQueries({ queryKey: planKey(week) });
    },
  });
}

/** Moves or re-serves an entry. Optimistic: the grid updates at once and rolls back if the server refuses. */
export function useUpdateEntry(week: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string } & Schemas["UpdateEntryRequest"]) =>
      unwrap(api.PUT("/api/meal-plan-entries/{id}", { params: { path: { id } }, body })),
    onMutate: async ({ id, day, mealSlot, servingsOverride }) => {
      await qc.cancelQueries({ queryKey: planKey(week) });
      const previous = qc.getQueryData<MealPlan>(planKey(week));
      qc.setQueryData<MealPlan>(planKey(week), (plan) =>
        plan
          ? {
              ...plan,
              entries: plan.entries.map((e) =>
                e.id === id ? { ...e, day: day ?? null, mealSlot: mealSlot ?? null, servingsOverride: servingsOverride ?? null } : e,
              ),
            }
          : plan,
      );
      return { previous };
    },
    onError: (_e, _v, ctx) => qc.setQueryData(planKey(week), ctx?.previous),
    onSettled: () => qc.invalidateQueries({ queryKey: planKey(week) }),
  });
}

export function useDeleteEntry(week: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/api/meal-plan-entries/{id}", { params: { path: { id } } })),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: planKey(week) });
      const previous = qc.getQueryData<MealPlan>(planKey(week));
      qc.setQueryData<MealPlan>(planKey(week), (plan) => (plan ? { ...plan, entries: plan.entries.filter((e) => e.id !== id) } : plan));
      return { previous };
    },
    onError: (_e, _v, ctx) => qc.setQueryData(planKey(week), ctx?.previous),
    onSettled: () => qc.invalidateQueries({ queryKey: planKey(week) }),
  });
}

export function useCopyPreviousWeek(week: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/api/meal-plans/{weekStart}/copy-previous", { params: { path: { weekStart: week } } })),
    onSuccess: (plan) => qc.setQueryData(planKey(week), plan),
  });
}

export function useBuildList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (weekStart: string) => unwrap(api.POST("/api/shopping-list/build", { body: { weekStart } })),
    onSuccess: (list) => qc.setQueryData(listKey, list),
  });
}

export function useAddRecipeToList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Schemas["AddRecipeToListRequest"]) => unwrap(api.POST("/api/shopping-list/add-recipe", { body })),
    onSuccess: (list) => {
      qc.setQueryData(listKey, list);
      qc.invalidateQueries({ queryKey: ["meal-plans"] });
    },
  });
}
