/**
 * Source de vérité unique pour les tokens, en dehors de React : l'intercepteur axios
 * (module chargé une fois, hors de l'arbre de composants) et AuthContext lisent/écrivent
 * tous les deux ici, avec persistance localStorage pour survivre à un refresh de page.
 */
export interface StoredAuth {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  email: string;
  displayName: string;
  roles: string[];
}

/**
 * Ce qui survit à un refresh de page. L'access token n'y figure volontairement PAS :
 * il ne vit qu'en mémoire (variable `current` ci-dessous), pour qu'un XSS lisant
 * localStorage ne récupère jamais un bearer token directement utilisable — seulement
 * un refresh token, qui doit repasser par /auth/refresh (et est rotatif côté backend).
 * Après un rechargement, `current.accessToken` vaut '' jusqu'à ce qu'AuthProvider
 * déclenche un refresh silencieux au montage (voir AuthContext.tsx).
 */
type PersistedAuth = Omit<StoredAuth, 'accessToken'>;

const STORAGE_KEY = 'inventory.auth';

function readFromStorage(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const persisted = JSON.parse(raw) as PersistedAuth;
    return { ...persisted, accessToken: '' };
  } catch {
    return null;
  }
}

let current: StoredAuth | null = readFromStorage();

type Listener = (auth: StoredAuth | null) => void;
const listeners = new Set<Listener>();

function notify(): void {
  for (const listener of listeners) listener(current);
}

export function getAuth(): StoredAuth | null {
  return current;
}

export function setAuth(auth: StoredAuth): void {
  current = auth;
  const persisted: PersistedAuth = {
    accessTokenExpiresAtUtc: auth.accessTokenExpiresAtUtc,
    refreshToken: auth.refreshToken,
    email: auth.email,
    displayName: auth.displayName,
    roles: auth.roles,
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(persisted));
  notify();
}

export function clearAuth(): void {
  current = null;
  localStorage.removeItem(STORAGE_KEY);
  notify();
}

// Synchronisation multi-onglets : le refresh token est rotatif côté backend (chaque
// /auth/refresh révoque l'ancien), donc si deux onglets sont ouverts sur la même session,
// celui qui n'a pas déclenché le refresh doit apprendre le nouveau token — sinon son
// prochain refresh tente d'utiliser un token déjà consommé et se déconnecte à tort.
// L'event 'storage' ne se déclenche que dans les AUTRES onglets, jamais dans celui qui
// écrit — pas de boucle avec setAuth/clearAuth ci-dessus.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key !== STORAGE_KEY) return;
    current = event.newValue
      ? { ...(JSON.parse(event.newValue) as PersistedAuth), accessToken: '' }
      : null;
    notify();
  });
}

/** Pour AuthContext : s'abonner aux changements (login/logout/refresh) et re-render. */
export function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function hasRole(role: string): boolean {
  return current?.roles.includes(role) ?? false;
}

/** Un Gestionnaire hérite des droits Employé, un Admin hérite de tout (voir policies backend). */
const ROLE_HIERARCHY: Record<string, string[]> = {
  Employe: ['Employe', 'Gestionnaire', 'Admin'],
  Gestionnaire: ['Gestionnaire', 'Admin'],
  Admin: ['Admin'],
};

export function hasAtLeastRole(minimumRole: 'Employe' | 'Gestionnaire' | 'Admin'): boolean {
  if (!current) return false;
  const accepted = ROLE_HIERARCHY[minimumRole] ?? [minimumRole];
  return current.roles.some((r) => accepted.includes(r));
}
