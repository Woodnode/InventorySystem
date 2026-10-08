import { useState, useEffect, useRef } from 'react';
import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  ArrowsLeftRight,
  ChartLine,
  List,
  Moon,
  Package,
  Sun,
  Truck,
  Warehouse,
  X,
  type Icon,
} from '@phosphor-icons/react';
import { useAuth } from '../features/auth/useAuth';
import { useTheme } from './ThemeProvider';
import { RequireRole } from '../routes/RequireRole';
import { LowStockToasts } from '../shared/components/LowStockToasts';
import { RechercheGlobale } from '../shared/components/RechercheGlobale';
import './Layout.css';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `nav-link-custom ${isActive ? 'active' : ''}`;

/*
 * Source unique des entrées de navigation : la liste était auparavant recopiée
 * intégralement pour le bureau et pour le mobile, donc modifiable d'un côté sans
 * l'autre.
 */
const NAV_ITEMS: { to: string; label: string; icon: Icon; role?: 'Gestionnaire' }[] = [
  { to: '/dashboard', label: 'Tableau de bord', icon: ChartLine },
  { to: '/products', label: 'Produits', icon: Package },
  { to: '/warehouses', label: 'Entrepôts', icon: Warehouse },
  { to: '/suppliers', label: 'Fournisseurs', icon: Truck, role: 'Gestionnaire' },
  { to: '/movements', label: 'Mouvements', icon: ArrowsLeftRight },
];

function NavItems({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <>
      {NAV_ITEMS.map(({ to, label, icon: Icone, role }) => {
        const link = (
          <NavLink key={to} to={to} onClick={onNavigate} className={navLinkClass}>
            <Icone size={16} aria-hidden="true" />
            {label}
          </NavLink>
        );
        return role ? (
          <RequireRole key={to} role={role}>
            {link}
          </RequireRole>
        ) : (
          link
        );
      })}
    </>
  );
}

/** Shell de l'app : nav + zone de contenu (voir plan §7). */
export function Layout() {
  const { auth, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const burgerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [pathname]);

  // Le menu restait ouvert après une navigation au clavier ou un retour arrière.
  useEffect(() => {
    setIsMenuOpen(false);
  }, [pathname]);

  /*
   * Un menu ouvert se ferme par Échap et par un clic à l'extérieur : sans cela, la
   * seule sortie était de réappuyer exactement sur le bouton, et le focus restait
   * piégé derrière le panneau.
   */
  useEffect(() => {
    if (!isMenuOpen) return;

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        setIsMenuOpen(false);
        burgerRef.current?.focus();
      }
    }
    function onPointerDown(e: PointerEvent) {
      const cible = e.target as Node;
      if (!menuRef.current?.contains(cible) && !burgerRef.current?.contains(cible)) {
        setIsMenuOpen(false);
      }
    }

    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('pointerdown', onPointerDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('pointerdown', onPointerDown);
    };
  }, [isMenuOpen]);

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  const identite = auth && (
    /* max-w + truncate : sans borne, un nom d affichage long repousse la navigation
       au-dela de la largeur disponible et ramene le debordement horizontal. */
    <span className="flex min-w-0 items-baseline gap-1 text-slate-700">
      <span className="max-w-[10rem] truncate font-medium" title={auth.displayName}>
        {auth.displayName}
      </span>{' '}
      {/* #0f6b6f : variante assombrie du sarcelle de marque, 6,4:1 sur blanc
          (la teinte d'origine #34a0a4 plafonnait à 3,2:1). */}
      <span className="role-label shrink-0 font-semibold">{auth.roles.join(', ')}</span>
    </span>
  );

  return (
    <div className="app-wrapper flex flex-col relative">
      <a
        href="#contenu-principal"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-white focus:px-3 focus:py-2 focus:shadow"
      >
        Aller au contenu
      </a>
      <header className="app-header">
        {/*
          Bascule à `lg` et non `md` : la navigation complète réclame 943px de large.
          Entre 768 et 943px, elle débordait horizontalement et passait sur deux lignes.
        */}
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-3 sm:px-6">
          <span className="nav-brand shrink-0">Inventaire</span>

          <nav aria-label="Navigation principale" className="hidden lg:flex items-center gap-1">
            <NavItems />
          </nav>

          <div className="flex min-w-0 items-center gap-2 text-sm">
            <RechercheGlobale />
            <button
              type="button"
              className="btn-secondary h-11 w-11 shrink-0 px-0"
              aria-label={theme === 'dark' ? 'Activer le thème clair' : 'Activer le thème sombre'}
              onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
            >
              {theme === 'dark' ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
            </button>
            <div className="hidden min-w-0 items-center gap-2 lg:flex">
              {identite}
              <button type="button" className="btn-secondary shrink-0" onClick={handleLogout}>
                Déconnexion
              </button>
            </div>
            <button
              ref={burgerRef}
              className="lg:hidden flex h-11 w-11 shrink-0 items-center justify-center rounded-field text-slate-600 hover:bg-slate-100 hover:text-slate-900"
              onClick={() => setIsMenuOpen((ouvert) => !ouvert)}
              aria-label={isMenuOpen ? 'Fermer le menu' : 'Ouvrir le menu'}
              aria-expanded={isMenuOpen}
              aria-controls="menu-mobile"
            >
              {isMenuOpen ? (
                <X size={24} aria-hidden="true" />
              ) : (
                <List size={24} aria-hidden="true" />
              )}
            </button>
          </div>
        </div>

        {isMenuOpen && (
          <div
            id="menu-mobile"
            ref={menuRef}
            className="menu-mobile lg:hidden absolute top-full left-0 right-0 border-b border-slate-200 bg-white z-50"
          >
            <nav aria-label="Menu" className="flex flex-col p-4 gap-1">
              <NavItems onNavigate={() => setIsMenuOpen(false)} />

              <div className="border-t border-slate-200 mt-3 pt-4 flex flex-col gap-3">
                <span className="px-2 text-sm">{identite}</span>
                <button type="button" className="btn-secondary w-full" onClick={handleLogout}>
                  Déconnexion
                </button>
              </div>
            </nav>
          </div>
        )}
      </header>

      <LowStockToasts />

      {/* tabIndex : sans lui, le lien d'évitement change l'URL mais ne déplace pas
          réellement le focus dans plusieurs navigateurs. */}
      <div id="contenu-principal" tabIndex={-1} className="flex-1 focus:outline-none">
        <Outlet />
      </div>
    </div>
  );
}
