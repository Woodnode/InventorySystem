import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../features/auth/useAuth';
import { RequireRole } from '../routes/RequireRole';
import { LowStockToasts } from '../shared/components/LowStockToasts';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
    isActive ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
  }`;

/** Shell de l'app : nav + zone de contenu (voir plan §7). */
export function Layout() {
  const { auth, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-3">
          <div className="flex items-center gap-1">
            <span className="mr-4 font-semibold text-slate-900">Inventaire</span>
            <NavLink to="/dashboard" className={navLinkClass}>
              Tableau de bord
            </NavLink>
            <NavLink to="/products" className={navLinkClass}>
              Produits
            </NavLink>
            <NavLink to="/warehouses" className={navLinkClass}>
              Entrepôts
            </NavLink>
            <RequireRole role="Gestionnaire">
              <NavLink to="/suppliers" className={navLinkClass}>
                Fournisseurs
              </NavLink>
            </RequireRole>
            <NavLink to="/movements" className={navLinkClass}>
              Mouvements
            </NavLink>
          </div>

          <div className="flex items-center gap-3 text-sm">
            <span className="text-slate-500">
              {auth?.displayName} · <span className="font-medium">{auth?.roles.join(', ')}</span>
            </span>
            <button type="button" className="btn-secondary" onClick={handleLogout}>
              Déconnexion
            </button>
          </div>
        </div>
      </header>

      <LowStockToasts />

      <Outlet />
    </div>
  );
}
