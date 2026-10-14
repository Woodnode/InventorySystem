/**
 * Source de vérité unique pour la session, en dehors de React : l'intercepteur axios
 * (module chargé une fois, hors de l'arbre de composants) et AuthContext lisent/écrivent
 * tous les deux ici.
 *
 * Aucun jeton n'est persisté côté JavaScript. L'access token ne vit qu'en mémoire
 * (variable `current` ci-dessous) ; le refresh token voyage dans un cookie httpOnly posé
 * par l'API, que le script de la page ne peut ni lire ni exfiltrer. Après un rechargement,
 * `current` vaut null jusqu'à ce qu'AuthProvider déclenche un refresh silencieux au
 * montage : le navigateur joint le cookie de lui-même (voir AuthContext.tsx).
 */
export interface StoredAuth {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  email: string;
  displayName: string;
  roles: string[];
}

/**
 * Seul ce drapeau survit au rechargement : il ne contient aucun jeton ni donnée de profil,
 * il indique seulement qu'une session a été ouverte dans ce navigateur. Il évite un appel
 * à /auth/refresh voué à l'échec pour un visiteur jamais connecté, et sert de signal entre
 * onglets (voir l'écouteur 'storage' plus bas).
 */
const SESSION_HINT_KEY = 'inventory.session';

function writeSessionHint(present: boolean): void {
  try {
    if (present) localStorage.setItem(SESSION_HINT_KEY, '1');
    else localStorage.removeItem(SESSION_HINT_KEY);
  } catch {
    // Stockage indisponible (navigation privée stricte) : la session reste valable dans cet onglet.
  }
}

export function hasSessionHint(): boolean {
  try {
    return localStorage.getItem(SESSION_HINT_KEY) !== null;
  } catch {
    return false;
  }
}

let current: StoredAuth | null = null;

type Listener = (auth: StoredAuth | null) => void;
const listeners = new Set<Listener>();

function notify(): void {
  for (const listener of listeners) listener(current);
}

export function getAuth(): StoredAuth | null {
  return current;
}

export function setAuth(auth: StoredAuth): void {
  current = {
    accessToken: auth.accessToken,
    accessTokenExpiresAtUtc: auth.accessTokenExpiresAtUtc,
    email: auth.email,
    displayName: auth.displayName,
    roles: auth.roles,
  };
  writeSessionHint(true);
  notify();
}

export function clearAuth(): void {
  current = null;
  writeSessionHint(false);
  notify();
}

// Synchronisation multi-onglets. Le cookie est partagé par tous les onglets, donc la
// rotation du refresh token ne demande plus rien : chaque onglet renouvelle avec le
// dernier cookie posé. Reste à propager la connexion et la déconnexion : l'event
// 'storage' ne se déclenche que dans les AUTRES onglets, jamais dans celui qui écrit,
// donc pas de boucle avec setAuth/clearAuth ci-dessus.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key !== SESSION_HINT_KEY) return;
    if (event.newValue === null) {
      // Déconnexion dans un autre onglet : le cookie est déjà effacé, on oublie l'access token.
      current = null;
      notify();
    } else if (!current) {
      // Connexion dans un autre onglet : AuthProvider voit l'indicateur et relance un
      // refresh silencieux pour obtenir son propre access token.
      notify();
    }
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
