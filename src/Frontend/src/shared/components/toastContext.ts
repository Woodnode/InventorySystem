import { createContext } from 'react';

export type ToastTone = 'success' | 'error' | 'info';

export interface Toast {
  id: string;
  tone: ToastTone;
  message: string;
}

/* Isolé de ToastProvider : un fichier qui exporte à la fois un composant et autre
   chose casse le rafraîchissement à chaud de Vite. */
export const ToastContext = createContext<{
  /** Affiche une notification éphémère. Retourne son identifiant. */
  notify: (message: string, tone?: ToastTone) => string;
} | null>(null);
