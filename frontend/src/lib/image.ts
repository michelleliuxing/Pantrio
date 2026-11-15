/**
 * Shrinks a photo in the browser before upload: longest side at most `maxSide`, JPEG at `quality`.
 * A 4 MB phone photo usually becomes 150–400 KB, which keeps uploads quick on mobile data and the
 * database small. EXIF orientation is applied, so portrait photos stay upright.
 */
export async function resizeImage(file: File, maxSide = 1600, quality = 0.85): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
  try {
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    const width = Math.round(bitmap.width * scale);
    const height = Math.round(bitmap.height * scale);
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("This browser can't prepare the photo.");
    ctx.fillStyle = "#ffffff"; // transparent PNGs get a white background instead of black
    ctx.fillRect(0, 0, width, height);
    ctx.drawImage(bitmap, 0, 0, width, height);
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", quality));
    if (!blob) throw new Error("This browser can't prepare the photo.");
    return blob;
  } finally {
    bitmap.close();
  }
}
