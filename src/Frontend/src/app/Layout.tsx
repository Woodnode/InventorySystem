import { useState, useEffect } from 'react';
import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../features/auth/useAuth';
import { RequireRole } from '../routes/RequireRole';
import { LowStockToasts } from '../shared/components/LowStockToasts';
import './Layout.css';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `nav-link-custom block md:inline-block ${isActive ? 'active' : ''}`;

/** Shell de l'app : nav + zone de contenu (voir plan §7). */
export function Layout() {
  const { auth, logout } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [pathname]);

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  function closeMenu() {
    setIsMenuOpen(false);
  }

  return (
    <div className="app-wrapper flex flex-col relative">
      <a
        href="#contenu-principal"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-white focus:px-3 focus:py-2 focus:shadow"
      >
        Aller au contenu
      </a>
      <header className="app-header">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 md:px-6 py-4">
          <div className="flex items-center justify-between w-full md:w-auto">
            <span className="nav-brand md:mr-6">Inventaire</span>
            
            {/* Hamburger Button */}
            <button 
              className="md:hidden p-2 -mr-2 text-slate-600 hover:text-slate-900 rounded-lg focus:outline-none focus:ring-2 focus:ring-slate-200" 
              onClick={() => setIsMenuOpen(!isMenuOpen)}
              aria-label="Toggle navigation"
            >
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                {isMenuOpen ? (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                ) : (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                )}
              </svg>
            </button>
          </div>

          {/* Desktop Navigation */}
          <nav aria-label="Navigation principale" className="hidden md:flex items-center gap-2">
            <NavLink to="/dashboard" className={navLinkClass}>Tableau de bord</NavLink>
            <NavLink to="/products" className={navLinkClass}>Produits</NavLink>
            <NavLink to="/warehouses" className={navLinkClass}>Entrepôts</NavLink>
            <RequireRole role="Gestionnaire">
              <NavLink to="/suppliers" className={navLinkClass}>Fournisseurs</NavLink>
            </RequireRole>
            <NavLink to="/movements" className={navLinkClass}>Mouvements</NavLink>
          </nav>

          {/* Desktop User Menu */}
          <div className="hidden md:flex items-center gap-4 text-sm">
            <span className="text-slate-600 font-medium bg-white/50 px-3 py-1.5 rounded-full border border-slate-200">
              {auth?.displayName} <span className="text-slate-400 mx-1">·</span> <span className="text-[#34a0a4] font-semibold">{auth?.roles.join(', ')}</span>
            </span>
            <button 
              type="button" 
              className="px-4 py-2 bg-white hover:bg-slate-50 border border-slate-200 text-slate-700 rounded-xl font-medium transition-colors shadow-sm"
              onClick={handleLogout}
            >
              Déconnexion
            </button>
          </div>
        </div>

        {/* Mobile Navigation Dropdown */}
        {isMenuOpen && (
          <div className="md:hidden absolute top-full left-0 right-0 bg-white border-b border-slate-200 shadow-lg z-50">
            <nav className="flex flex-col p-4 gap-2">
              <NavLink to="/dashboard" onClick={closeMenu} className={navLinkClass}>Tableau de bord</NavLink>
              <NavLink to="/products" onClick={closeMenu} className={navLinkClass}>Produits</NavLink>
              <NavLink to="/warehouses" onClick={closeMenu} className={navLinkClass}>Entrepôts</NavLink>
              <RequireRole role="Gestionnaire">
                <NavLink to="/suppliers" onClick={closeMenu} className={navLinkClass}>Fournisseurs</NavLink>
              </RequireRole>
              <NavLink to="/movements" onClick={closeMenu} className={navLinkClass}>Mouvements</NavLink>
              
              <div className="border-t border-slate-100 mt-2 pt-4 flex flex-col gap-4">
                <span className="text-slate-600 font-medium px-2">
                  {auth?.displayName} <span className="text-slate-400 mx-1">·</span> <span className="text-[#34a0a4] font-semibold">{auth?.roles.join(', ')}</span>
                </span>
                <button 
                  type="button" 
                  className="w-full px-4 py-2 bg-slate-50 border border-slate-200 text-slate-700 rounded-lg font-medium"
                  onClick={() => { closeMenu(); handleLogout(); }}
                >
                  Déconnexion
                </button>
              </div>
            </nav>
          </div>
        )}
      </header>

      <LowStockToasts />

      <div id="contenu-principal" className="flex-1">
        <Outlet />
      </div>
    </div>
  );
}
