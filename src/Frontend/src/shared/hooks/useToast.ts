import { useContext } from 'react';
import { ToastContext } from '../components/toastContext';

/** Notifications éphémères. Doit être appelé sous `ToastProvider` (monté dans AppProviders). */
export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast doit être utilisé à l’intérieur de ToastProvider.');
  return ctx;
}
