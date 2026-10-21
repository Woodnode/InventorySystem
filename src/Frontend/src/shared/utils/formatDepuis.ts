const relatif = new Intl.RelativeTimeFormat('fr-CA', { numeric: 'auto' });

const SEPT_JOURS = 7 * 86400;

/**
 * Vrai quand `formatDepuis` rend un temps relatif plutôt qu'une date : une phrase
 * dit « il y a 3 heures » mais « le 25 sept. », et l'appelant doit le savoir.
 */
export function estRelatif(date: string | Date, maintenant: Date = new Date()): boolean {
  const instant = typeof date === 'string' ? new Date(date) : date;
  return Math.abs(instant.getTime() - maintenant.getTime()) / 1000 < SEPT_JOURS;
}

/**
 * Temps écoulé depuis une date, en français : « il y a 18 minutes », « hier ».
 *
 * Une date complète (« 2026-10-21 14:03:12 ») oblige à faire le calcul de tête
 * pour savoir si un mouvement est récent. Au-delà d'une semaine, la date courte
 * redevient plus utile qu'un « il y a 23 jours ».
 */
export function formatDepuis(date: string | Date, maintenant: Date = new Date()): string {
  const instant = typeof date === 'string' ? new Date(date) : date;
  const secondes = Math.round((instant.getTime() - maintenant.getTime()) / 1000);
  const ecart = Math.abs(secondes);

  /* Un mouvement n'est jamais dans le futur : une horloge client en retard sur le
     serveur, ou une date d'amorçage, affichait « dans 17 minutes ». */
  if (ecart < 60 || secondes > 0) return "à l'instant";
  if (ecart < 3600) return relatif.format(Math.round(secondes / 60), 'minute');
  if (ecart < 86400) return relatif.format(Math.round(secondes / 3600), 'hour');
  if (ecart < SEPT_JOURS) return relatif.format(Math.round(secondes / 86400), 'day');

  // Espace insécable : « 25 » et « sept. » se retrouvaient sur deux lignes sur mobile.
  return instant.toLocaleDateString('fr-CA', { day: 'numeric', month: 'short' }).replace(' ', ' ');
}
