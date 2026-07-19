import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { getAuth } from '../auth/tokenStorage';

/** Reflète LowStockNotification côté backend (System.Text.Json sérialise en camelCase). */
export interface LowStockAlert {
  id: string; // clé React locale (le hub n'a pas d'id de message, on en génère un)
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  lowStockThreshold: number;
}

const AUTO_DISMISS_MS = 10_000;

/** Dérive l'origine de l'API (sans le suffixe /api/v1) pour joindre le hub SignalR. */
function hubUrl(): string {
  const apiUrl = import.meta.env.VITE_API_URL ?? 'https://localhost:7296/api/v1';
  return `${apiUrl.replace(/\/api\/v1\/?$/, '')}/hubs/stock`;
}

/**
 * Se connecte au hub SignalR "stock" et maintient la liste des alertes de stock bas
 * reçues en temps réel (auto-expirées après quelques secondes). Une seule connexion
 * pour toute l'app authentifiée — voir montage dans Layout (plan §13).
 */
export function useLowStockAlerts() {
  const [alerts, setAlerts] = useState<LowStockAlert[]>([]);
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());

  function dismiss(id: string) {
    setAlerts((prev) => prev.filter((a) => a.id !== id));
    const timer = timers.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timers.current.delete(id);
    }
  }

  useEffect(() => {
    const auth = getAuth();
    if (!auth?.accessToken) return;

    // Capturé ici (plutôt que relu via `timers.current` dans le cleanup) : la valeur du
    // ref pourrait en théorie changer d'ici le démontage, cette closure fige la bonne carte.
    const timerMap = timers.current;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => getAuth()?.accessToken ?? '',
        // Le client SignalR active withCredentials par défaut (pensé pour l'auth par
        // cookie). On authentifie par Bearer JWT, pas par cookie, et le CORS backend
        // n'autorise pas les requêtes "credentialed" (AllowCredentials n'est pas activé,
        // volontairement — voir Program.cs) : sans ce false explicite, la négociation
        // échoue silencieusement côté navigateur (CORS bloque la vraie réponse).
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    connection.on('LowStock', (payload: Omit<LowStockAlert, 'id'>) => {
      const alert: LowStockAlert = { ...payload, id: crypto.randomUUID() };
      setAlerts((prev) => [...prev, alert]);
      timers.current.set(
        alert.id,
        setTimeout(() => dismiss(alert.id), AUTO_DISMISS_MS),
      );
    });

    void connection.start().catch((err: unknown) => {
      // Une alerte temps réel manquée n'est pas bloquante pour l'usage de l'app
      // (le stock bas reste visible sur /products) — on se contente de logger.
      console.error('Connexion au hub de stock impossible :', err);
    });

    return () => {
      void connection.stop();
      for (const timer of timerMap.values()) clearTimeout(timer);
      timerMap.clear();
    };
    // Connexion établie une fois par montage (session authentifiée) — volontairement pas
    // de dépendance sur `getAuth()`, qui n'est pas réactif (voir tokenStorage.ts).
  }, []);

  return { alerts, dismiss };
}
