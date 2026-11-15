"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "@/lib/api/client";
import { listKey, type ListItem, type ShoppingList } from "./plan";

export function useShoppingList() {
  return useQuery({
    queryKey: listKey,
    queryFn: () => unwrap(api.GET("/api/shopping-list")),
    refetchOnWindowFocus: true,
  });
}

/**
 * Ticking must feel instant in a supermarket: the cache changes first, the request follows,
 * and a failure rolls back. Requests for different items run in parallel.
 */
export function useUpdateListItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string } & Schemas["UpdateListItemRequest"]) =>
      unwrap(api.PATCH("/api/shopping-list/items/{id}", { params: { path: { id } }, body })),
    onMutate: async ({ id, state, inCheckGroup }) => {
      await qc.cancelQueries({ queryKey: listKey });
      const previous = qc.getQueryData<ShoppingList>(listKey);
      qc.setQueryData<ShoppingList>(listKey, (list) =>
        list
          ? {
              items: list.items.map((i) =>
                i.id === id
                  ? {
                      ...i,
                      state: state ?? i.state,
                      inCheckGroup: inCheckGroup ?? i.inCheckGroup,
                      amountIncreased: state ? false : i.amountIncreased,
                    }
                  : i,
              ),
            }
          : list,
      );
      return { previous };
    },
    onError: (_e, _v, ctx) => qc.setQueryData(listKey, ctx?.previous),
    onSuccess: (item) =>
      qc.setQueryData<ShoppingList>(listKey, (list) => (list ? { items: list.items.map((i) => (i.id === item.id ? item : i)) } : list)),
  });
}

export function useAddExtra() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Schemas["AddExtraRequest"]) => unwrap(api.POST("/api/shopping-list/items", { body })),
    onSuccess: (item) => qc.setQueryData<ShoppingList>(listKey, (list) => (list ? { items: [...list.items, item] } : { items: [item] })),
  });
}

export function useDeleteListItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/api/shopping-list/items/{id}", { params: { path: { id } } })),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: listKey });
      const previous = qc.getQueryData<ShoppingList>(listKey);
      qc.setQueryData<ShoppingList>(listKey, (list) => (list ? { items: list.items.filter((i) => i.id !== id) } : list));
      return { previous };
    },
    onError: (_e, _v, ctx) => qc.setQueryData(listKey, ctx?.previous),
  });
}

export function useClearCompleted() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/api/shopping-list/clear-bought")),
    onSuccess: (list) => qc.setQueryData(listKey, list),
  });
}

/** Sets or clears a catalogue ingredient's buy link from the list. The list reads links live, so a refetch shows it. */
export function useSetPurchaseUrl() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ ingredientId, purchaseUrl }: { ingredientId: string; purchaseUrl: string }) =>
      unwrap(api.PATCH("/api/ingredients/{id}", { params: { path: { id: ingredientId } }, body: { purchaseUrl } })),
    onSuccess: (ingredient) => {
      qc.setQueryData<ShoppingList>(listKey, (list) =>
        list
          ? { items: list.items.map((i) => (i.ingredientId === ingredient.id ? { ...i, purchaseUrl: ingredient.purchaseUrl } : i)) }
          : list,
      );
      qc.invalidateQueries({ queryKey: listKey }); // extras matching this name pick it up too
      qc.invalidateQueries({ queryKey: ["recipes", "detail"] });
      qc.invalidateQueries({ queryKey: ["ingredients"] });
    },
  });
}

export type { ListItem };
