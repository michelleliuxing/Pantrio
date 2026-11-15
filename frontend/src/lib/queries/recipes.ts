"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "@/lib/api/client";

export type RecipeSummary = Schemas["RecipeSummaryDto"];
export type Recipe = Schemas["RecipeDto"];
export type RecipeUpsert = Schemas["RecipeUpsertRequest"];

export const recipeKeys = {
  all: ["recipes"] as const,
  list: (filters: { search?: string; tag?: string; favourites?: boolean }) => ["recipes", "list", filters] as const,
  tags: ["recipes", "tags"] as const,
  detail: (id: string) => ["recipes", "detail", id] as const,
};

export function useRecipes(filters: { search?: string; tag?: string; favourites?: boolean }) {
  return useQuery({
    queryKey: recipeKeys.list(filters),
    queryFn: () =>
      unwrap(
        api.GET("/api/recipes", {
          params: {
            query: {
              search: filters.search || undefined,
              tag: filters.tag || undefined,
              favourites: filters.favourites || undefined,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useRecipeTags() {
  return useQuery({ queryKey: recipeKeys.tags, queryFn: () => unwrap(api.GET("/api/recipes/tags")) });
}

export function useRecipe(id: string | undefined) {
  return useQuery({
    queryKey: recipeKeys.detail(id ?? ""),
    queryFn: () => unwrap(api.GET("/api/recipes/{id}", { params: { path: { id: id! } } })),
    enabled: !!id,
  });
}

export function useReferenceData() {
  return useQuery({ queryKey: ["reference"], queryFn: () => unwrap(api.GET("/api/reference")), staleTime: Infinity });
}

export function useSaveRecipe(id?: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RecipeUpsert) =>
      id
        ? unwrap(api.PUT("/api/recipes/{id}", { params: { path: { id } }, body }))
        : unwrap(api.POST("/api/recipes", { body })),
    onSuccess: (recipe) => {
      qc.setQueryData(recipeKeys.detail(recipe.id), recipe);
      qc.invalidateQueries({ queryKey: ["recipes", "list"] });
      qc.invalidateQueries({ queryKey: recipeKeys.tags });
      qc.invalidateQueries({ queryKey: ["ingredients"] });
    },
  });
}

export function useToggleFavourite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isFavourite }: { id: string; isFavourite: boolean }) =>
      unwrap(api.PATCH("/api/recipes/{id}", { params: { path: { id } }, body: { isFavourite } })),
    onMutate: async ({ id, isFavourite }) => {
      await qc.cancelQueries({ queryKey: ["recipes", "list"] });
      const previous = qc.getQueriesData<RecipeSummary[]>({ queryKey: ["recipes", "list"] });
      qc.setQueriesData<RecipeSummary[]>({ queryKey: ["recipes", "list"] }, (list) =>
        list?.map((r) => (r.id === id ? { ...r, isFavourite } : r)),
      );
      qc.setQueryData<Recipe>(recipeKeys.detail(id), (r) => (r ? { ...r, isFavourite } : r));
      return { previous };
    },
    onError: (_err, _vars, ctx) => ctx?.previous.forEach(([key, data]) => qc.setQueryData(key, data)),
    onSettled: () => qc.invalidateQueries({ queryKey: ["recipes", "list"] }),
  });
}

export function useDuplicateRecipe() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.POST("/api/recipes/{id}/duplicate", { params: { path: { id } } })),
    onSuccess: (recipe) => {
      qc.setQueryData(recipeKeys.detail(recipe.id), recipe);
      qc.invalidateQueries({ queryKey: ["recipes", "list"] });
    },
  });
}

export function useDeleteRecipe() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/api/recipes/{id}", { params: { path: { id } } })),
    onSuccess: (_data, id) => {
      qc.removeQueries({ queryKey: recipeKeys.detail(id) });
      qc.invalidateQueries({ queryKey: recipeKeys.all });
      qc.invalidateQueries({ queryKey: ["meal-plans"] });
    },
  });
}

export function useIngredientSearch(search: string, enabled: boolean) {
  return useQuery({
    queryKey: ["ingredients", "search", search],
    queryFn: () => unwrap(api.GET("/api/ingredients", { params: { query: { search, limit: 8 } } })),
    enabled: enabled && search.trim().length > 0,
    staleTime: 60_000,
    placeholderData: keepPreviousData,
  });
}
