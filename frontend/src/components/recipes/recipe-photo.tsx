"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { Camera, ImagePlus, Loader2, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/api/client";
import { resizeImage } from "@/lib/image";
import { useDeleteRecipeImage, useUploadRecipeImage } from "@/lib/queries/recipes";

type Props = {
  /** Saved recipe: changes upload straight away. New recipe: the photo is held until the recipe is saved. */
  recipeId?: string;
  imageUrl: string | null;
  canEdit: boolean;
  pending?: Blob | null;
  onPendingChange?: (blob: Blob | null) => void;
};

export function RecipePhoto({ recipeId, imageUrl, canEdit, pending, onPendingChange }: Props) {
  const input = useRef<HTMLInputElement>(null);
  const upload = useUploadRecipeImage();
  const remove = useDeleteRecipeImage();
  const [preparing, setPreparing] = useState(false);
  const busy = preparing || upload.isPending || remove.isPending;

  // Object URL for a photo chosen before the recipe exists; released when it changes or the editor closes.
  const preview = useMemo(() => (pending ? URL.createObjectURL(pending) : null), [pending]);
  useEffect(() => () => {
    if (preview) URL.revokeObjectURL(preview);
  }, [preview]);

  async function choose(file: File | undefined) {
    if (!file) return;
    setPreparing(true);
    try {
      const blob = await resizeImage(file);
      if (recipeId) {
        await upload.mutateAsync({ id: recipeId, blob });
        toast.success("Photo saved");
      } else {
        onPendingChange?.(blob);
      }
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setPreparing(false);
      if (input.current) input.current.value = "";
    }
  }

  function clear() {
    if (recipeId && imageUrl) {
      remove.mutate(recipeId, { onSuccess: () => toast("Photo removed"), onError: (err) => toast.error(errorMessage(err)) });
    } else {
      onPendingChange?.(null);
    }
  }

  const src = preview ?? imageUrl;
  const fileInput = (
    <input
      ref={input}
      type="file"
      accept="image/*"
      className="sr-only"
      tabIndex={-1}
      aria-hidden
      onChange={(e) => choose(e.target.files?.[0])}
    />
  );

  if (!src) {
    if (!canEdit) return null;
    return (
      <>
        {fileInput}
        <button
          type="button"
          onClick={() => input.current?.click()}
          disabled={busy}
          className="flex h-28 w-full flex-col items-center justify-center gap-1.5 rounded-xl border border-dashed text-sm text-muted-foreground hover:bg-muted/50 disabled:opacity-60"
        >
          {busy ? <Loader2 className="size-5 animate-spin" /> : <ImagePlus className="size-5" />}
          {busy ? "Preparing photo…" : "Add a photo"}
        </button>
      </>
    );
  }

  return (
    <div className="relative overflow-hidden rounded-xl border bg-muted">
      {fileInput}
      {/* A plain img: the photo is behind the session cookie, which Next's image optimiser would not send. */}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt="" className="aspect-[16/9] w-full object-cover" />
      {busy && (
        <div className="absolute inset-0 flex items-center justify-center bg-background/60">
          <Loader2 className="size-6 animate-spin" aria-label="Saving photo" />
        </div>
      )}
      {canEdit && (
        <div className="absolute right-2 bottom-2 flex gap-1.5">
          <Button type="button" variant="secondary" className="h-9 shadow-sm" disabled={busy} onClick={() => input.current?.click()}>
            <Camera /> Change
          </Button>
          <Button type="button" variant="secondary" size="icon-lg" className="size-9 shadow-sm" disabled={busy} onClick={clear} aria-label="Remove photo">
            <Trash2 />
          </Button>
        </div>
      )}
      {!recipeId && pending && (
        <span className="absolute top-2 left-2 rounded-full bg-background/90 px-2 py-0.5 text-xs font-medium">Saved with the recipe</span>
      )}
    </div>
  );
}
