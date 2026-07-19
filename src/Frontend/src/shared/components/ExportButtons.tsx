import { useState } from 'react';

/**
 * Paire de boutons "Exporter CSV" / "Exporter Excel". `onExport` reçoit le format choisi
 * et doit déclencher le téléchargement (voir `exportProducts`/`exportMovements`).
 */
export function ExportButtons({
  onExport,
}: {
  onExport: (format: 'Csv' | 'Excel') => Promise<void>;
}) {
  const [pending, setPending] = useState<'Csv' | 'Excel' | null>(null);

  async function handleClick(format: 'Csv' | 'Excel') {
    setPending(format);
    try {
      await onExport(format);
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex gap-2">
      <button
        type="button"
        className="btn-secondary"
        disabled={pending !== null}
        onClick={() => void handleClick('Csv')}
      >
        {pending === 'Csv' ? 'Export…' : 'Exporter CSV'}
      </button>
      <button
        type="button"
        className="btn-secondary"
        disabled={pending !== null}
        onClick={() => void handleClick('Excel')}
      >
        {pending === 'Excel' ? 'Export…' : 'Exporter Excel'}
      </button>
    </div>
  );
}
