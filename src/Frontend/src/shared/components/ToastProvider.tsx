import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { X } from '@phosphor-icons/react';
import { ToastContext, type ToastTone, type Toast } from './toastContext';

const DUREE_MS = 6_000;

/*
 * Les retours d'action passaient par `alert()` : dialogue natif bloquant, non
 * traduisible, impossible à tester, et incohérent avec les alertes de stock bas déjà
 * affichées en surimpression. Les créations réussies, elles, ne disaient rien du tout.
 *
 * Position basse, à l'opposé des alertes de stock bas (en haut) : les deux piles
 * peuvent coexister sans se recouvrir.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const minuteurs = useRef(new Map<string, ReturnType<typeof setTimeout>>());

  const retirer = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const minuteur = minuteurs.current.get(id);
    if (minuteur) {
      clearTimeout(minuteur);
      minuteurs.current.delete(id);
    }
  }, []);

  const notify = useCallback(
    (message: string, tone: ToastTone = 'success') => {
      const id = crypto.randomUUID();
      setToasts((prev) => [...prev.slice(-2), { id, tone, message }]);
      minuteurs.current.set(
        id,
        setTimeout(() => retirer(id), DUREE_MS),
      );
      return id;
    },
    [retirer],
  );

  // Les minuteurs en cours doivent mourir avec le provider, sinon ils écrivent
  // dans un état démonté lors d'un rechargement à chaud ou d'une déconnexion.
  useEffect(() => {
    const encours = minuteurs.current;
    return () => {
      encours.forEach(clearTimeout);
      encours.clear();
    };
  }, []);

  const styles: Record<ToastTone, string> = {
    success: 'border-brand-300 bg-brand-900/10 text-brand-900',
    error: 'border-rose-300 bg-rose-50 text-rose-900',
    info: 'border-slate-300 bg-white text-slate-800',
  };

  return (
    <ToastContext.Provider value={{ notify }}>
      {children}
      <div className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col gap-2 sm:left-auto sm:right-4 sm:w-96">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            role={toast.tone === 'error' ? 'alert' : 'status'}
            className={`toast-entre pointer-events-auto flex items-start justify-between gap-2 rounded-card border p-4 ${styles[toast.tone]}`}
          >
            {/* whitespace-pre-line : les bilans d'import tiennent sur plusieurs lignes. */}
            <p className="min-w-0 whitespace-pre-line text-sm font-medium">{toast.message}</p>
            <button
              type="button"
              className="-mr-2 -mt-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-field hover:bg-black/5"
              aria-label="Fermer la notification"
              onClick={() => retirer(toast.id)}
            >
              <X size={16} aria-hidden="true" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}
