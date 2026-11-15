"use client";

import { Suspense } from "react";
import { useParams } from "next/navigation";
import { RecipeEditor } from "@/components/recipes/recipe-editor";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/client";
import { useRecipe } from "@/lib/queries/recipes";

export default function RecipePage() {
  return (
    <Suspense fallback={<EditorSkeleton />}>
      <LoadedRecipe />
    </Suspense>
  );
}

function LoadedRecipe() {
  const { id } = useParams<{ id: string }>();
  const recipe = useRecipe(id);

  if (recipe.isPending) return <EditorSkeleton />;
  if (recipe.isError) {
    const missing = recipe.error instanceof ApiError && recipe.error.status === 404;
    return <p className="py-16 text-center text-muted-foreground">{missing ? "This recipe doesn’t exist." : recipe.error.message}</p>;
  }
  // Keyed so a different recipe (e.g. after duplicating) starts from its own saved values.
  return <RecipeEditor key={recipe.data.id + recipe.data.updatedAt} recipe={recipe.data} />;
}

function EditorSkeleton() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-10 w-40" />
      <Skeleton className="h-12 w-full" />
      <Skeleton className="h-11 w-full" />
      <Skeleton className="h-40 w-full rounded-xl" />
    </div>
  );
}
