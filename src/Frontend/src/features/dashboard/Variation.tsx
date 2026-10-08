import { TrendDown, TrendUp, Minus } from '@phosphor-icons/react';

/**
 * Variation entre deux périodes.
 *
 * Volontairement neutre sur le jugement : pour un inventaire, un stock qui baisse
 * peut signifier des ventes plutôt qu'un problème. La couleur indique le sens du
 * mouvement, pas s'il est bon, et le libellé nomme ce qui est comparé.
 */
export function Variation({
  pourcentage,
  periodeJours,
}: {
  /** `null` quand la période précédente est à zéro : rien à comparer. */
  pourcentage: number | null;
  periodeJours: number;
}) {
  if (pourcentage === null) {
    return (
      <span className="inline-flex items-center gap-1 text-xs font-medium text-slate-600">
        <Minus size={14} aria-hidden="true" />
        Pas de comparaison possible
      </span>
    );
  }

  const hausse = pourcentage > 0;
  const stable = pourcentage === 0;
  const Icone = stable ? Minus : hausse ? TrendUp : TrendDown;

  /* La hausse prend la couleur de marque. L'ambre signale une baisse sans la
     traiter comme une erreur : le signe et l'icône nomment le sens. */
  const ton = stable
    ? 'bg-slate-100 text-slate-700'
    : hausse
      ? 'bg-brand-900/10 text-brand-900'
      : 'bg-amber-100 text-amber-900';

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-semibold ${ton}`}
    >
      <Icone size={14} weight="bold" aria-hidden="true" />
      {hausse && '+'}
      {pourcentage.toLocaleString('fr-CA')} %
      <span className="font-normal">sur {periodeJours} j</span>
    </span>
  );
}
