/**
 * Poids en grammes rendu dans l'unité qui se lit le mieux.
 *
 * Un livre de poche pèse 220 g, une palette plusieurs centaines de kilos : afficher
 * les deux dans la même unité donne soit « 0,22 kg », soit « 412 000 g ».
 */
export function formatPoids(grammes: number | null | undefined): string {
  // Zero gramme n'est jamais une mesure : c'est une donnee absente ou un stock vide.
  if (grammes === null || grammes === undefined || grammes === 0) return '—';

  if (Math.abs(grammes) < 1000) {
    // Sous le kilo, le gramme entier suffit : la décimale n'apporte rien.
    return `${Math.round(grammes).toLocaleString('fr-CA')} g`;
  }

  const kg = grammes / 1000;
  // Une décimale jusqu'à 100 kg, aucune au-delà : au-dessus, le gramme près est illusoire.
  const decimales = Math.abs(kg) < 100 ? 1 : 0;
  return `${kg.toLocaleString('fr-CA', {
    minimumFractionDigits: decimales,
    maximumFractionDigits: decimales,
  })} kg`;
}
