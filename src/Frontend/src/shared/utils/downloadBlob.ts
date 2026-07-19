/**
 * Déclenche le téléchargement d'un blob dans le navigateur (pattern standard : ancre
 * temporaire + URL objet). Réutilisé par tous les exports (produits, mouvements...).
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

/**
 * Extrait le nom de fichier d'un en-tête `Content-Disposition: attachment; filename=...`.
 * Retourne `fallback` si l'en-tête est absent ou mal formé.
 */
export function extractFilename(contentDisposition: string | undefined, fallback: string): string {
  if (!contentDisposition) return fallback;
  const match = /filename="?([^";]+)"?/i.exec(contentDisposition);
  return match?.[1] ?? fallback;
}
