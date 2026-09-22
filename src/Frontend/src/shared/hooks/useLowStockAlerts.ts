import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { getAuth, subscribe } from '../auth/tokenStorage';

/** Reflète LowStockNotification côté backend (System.Text.Json sérialise en camelCase). */
export interface LowStockAlert {
  id: string;
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  lowStockThreshold: number;
}

const AUTO_DISMISS_MS = 10_000;

function hubUrl(): string {
  let apiUrl = import.meta.env.VITE_API_URL ?? 'https://localhost:7296/api/v1';
  if (window.location.hostname === '10.0.2.2') {
    apiUrl = 'http://10.0.2.2:5244/api/v1';
  }
  return `${apiUrl.replace(/\/api\/v1\/?$/, '')}/hubs/stock`;
}

/**
 * Se connecte au hub SignalR "stock" et maintient la liste des alertes de stock bas.
 * Se reconnecte quand un access token devient disponible (login / refresh multi-onglets).
 */
export function useLowStockAlerts() {
  const [alerts, setAlerts] = useState<LowStockAlert[]>([]);
  const [accessToken, setAccessToken] = useState(() => getAuth()?.accessToken ?? '');
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());

  const dismiss = useCallback((id: string) => {
    setAlerts((prev) => prev.filter((a) => a.id !== id));
    const timer = timers.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timers.current.delete(id);
    }
  }, []);

  useEffect(() => {
    return subscribe((auth) => {
      setAccessToken(auth?.accessToken ?? '');
    });
  }, []);

  useEffect(() => {
    if (!accessToken) return;

    const timerMap = timers.current;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => getAuth()?.accessToken ?? '',
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
      console.error('Connexion au hub de stock impossible :', err);
    });

    return () => {
      void connection.stop();
      for (const timer of timerMap.values()) clearTimeout(timer);
      timerMap.clear();
    };
  }, [accessToken, dismiss]);

  return { alerts, dismiss };
}
