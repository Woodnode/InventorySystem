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
    /*
     * `top-20` et non `top-4` : en `z-50` contre un header collant en `z-40`, la pile
     * recouvrait le bouton d'ouverture du menu.
     * Largeur fluide : `w-80` fixe débordait sur un écran de 320px.
     */
    <div className="pointer-events-none fixed inset-x-4 top-20 z-50 flex flex-col gap-2 sm:left-auto sm:right-4 sm:w-80">
      {alerts.map((alert) => (
        <div
          key={alert.id}
          role="alert"
          className="pointer-events-auto rounded-card border border-amber-300 bg-amber-50 p-4"
        >
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <p className="text-sm font-semibold text-amber-900">Stock bas</p>
              <p className="mt-1 text-sm text-amber-900">
                <Link to={`/products/${alert.productId}`} className="font-medium underline underline-offset-2">
                  {alert.name}
                </Link>{' '}
                {/* text-amber-800 : 6,4:1. L'ancien text-amber-600 tombait à 3,0:1. */}
                <span className="text-amber-800">· {alert.sku}</span>
              </p>
              <p className="mt-1 text-xs text-amber-800">
                {alert.quantity} en stock (seuil {alert.lowStockThreshold})
              </p>
            </div>
            <button
              type="button"
              className="-mr-2 -mt-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-field text-amber-900 hover:bg-amber-100"
              aria-label={`Fermer l'alerte pour ${alert.name}`}
              onClick={() => dismiss(alert.id)}
            >
              <span aria-hidden="true">✕</span>
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
