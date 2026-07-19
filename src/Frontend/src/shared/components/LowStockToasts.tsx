import { Link } from 'react-router-dom';
import { useLowStockAlerts } from '../hooks/useLowStockAlerts';

/**
 * Empile les alertes de stock bas reçues en temps réel via SignalR (voir StockHub côté
 * backend). Montée une fois dans Layout — active sur toutes les pages authentifiées.
 */
export function LowStockToasts() {
  const { alerts, dismiss } = useLowStockAlerts();

  if (alerts.length === 0) return null;

  return (
    <div className="pointer-events-none fixed right-4 top-4 z-50 flex flex-col gap-2">
      {alerts.map((alert) => (
        <div
          key={alert.id}
          role="alert"
          className="pointer-events-auto w-80 rounded-xl border border-amber-200 bg-amber-50 p-4 shadow-lg"
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-amber-800">Stock bas</p>
              <p className="mt-1 text-sm text-amber-700">
                <Link to={`/products/${alert.productId}`} className="underline underline-offset-2">
                  {alert.name}
                </Link>{' '}
                <span className="text-amber-600">· {alert.sku}</span>
              </p>
              <p className="mt-1 text-xs text-amber-600">
                {alert.quantity} en stock (seuil {alert.lowStockThreshold})
              </p>
            </div>
            <button
              type="button"
              className="text-amber-500 hover:text-amber-700"
              aria-label="Fermer l'alerte"
              onClick={() => dismiss(alert.id)}
            >
              ✕
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
