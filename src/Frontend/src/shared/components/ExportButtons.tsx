import { useState } from 'react';
import { getErrorMessage } from '../api-client/errorMessage';

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
  const [error, setError] = useState<string | null>(null);

  async function handleClick(format: 'Csv' | 'Excel') {
    setPending(format);
    setError(null);
    try {
      await onExport(format);
    } catch (err) {
      setError(getErrorMessage(err, "Échec de l'export."));
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex flex-col items-end gap-1">
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
      {error && (
        <p role="alert" className="text-xs text-rose-600">
          {error}
        </p>
      )}
    </div>
  );
}
