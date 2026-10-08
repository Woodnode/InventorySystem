/** Précédent/Suivant + indicateur de page — masqué s'il n'y a qu'une seule page. */
export function PaginationControls({
  page,
  pageSize,
  totalCount,
  onPageChange,
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalPages <= 1) return null;

  return (
    <nav
      aria-label="Pagination"
      className="mt-4 flex items-center justify-between gap-3 text-sm text-slate-600"
    >
      <button
        type="button"
        className="btn-secondary"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        <span aria-hidden="true">←</span>
        <span className="sr-only sm:not-sr-only">Précédent</span>
      </button>
      {/* aria-live : le numéro changeait sans que rien ne l'annonce. */}
      <span aria-live="polite" className="text-center">
        Page {page} / {totalPages}
        <span className="sr-only"> — {totalCount} éléments au total</span>
      </span>
      <button
        type="button"
        className="btn-secondary"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
      >
        <span className="sr-only sm:not-sr-only">Suivant</span>
        <span aria-hidden="true">→</span>
      </button>
    </nav>
  );
}
