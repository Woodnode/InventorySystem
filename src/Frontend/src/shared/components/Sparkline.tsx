import { useId } from 'react';

/**
 * Courbe compacte d'une série de valeurs.
 *
 * Tracée à la main plutôt qu'avec une bibliothèque de graphiques : il s'agit d'une
 * polyligne et d'une aire, quand le plus léger des paquets du marché pèse plus de
 * cent kilo-octets pour ce seul usage.
 *
 * La série est aussi restituée en texte (`resume`) : une courbe seule n'est pas
 * lisible au lecteur d'écran, et la valeur doit exister ailleurs que dans la forme.
 */
export function Sparkline({
  valeurs,
  resume,
  couleur = 'var(--color-brand-700)',
  hauteur = 40,
  className = '',
}: {
  valeurs: number[];
  /** Description restituée aux technologies d'assistance. */
  resume: string;
  couleur?: string;
  hauteur?: number;
  className?: string;
}) {
  const id = useId();

  if (valeurs.length < 2) return null;

  const LARGEUR = 100;
  const min = Math.min(...valeurs);
  const max = Math.max(...valeurs);
  // Série plate : sans cette garde, l'amplitude nulle diviserait par zéro et la
  // courbe disparaîtrait au lieu de s'afficher comme une ligne médiane.
  const amplitude = max - min || 1;

  const points = valeurs.map((v, i) => {
    const x = (i / (valeurs.length - 1)) * LARGEUR;
    const y = hauteur - ((v - min) / amplitude) * (hauteur - 4) - 2;
    return [x, y] as const;
  });

  const ligne = points.map(([x, y]) => `${x.toFixed(2)},${y.toFixed(2)}`).join(' ');
  const aire = `M0,${hauteur} L${ligne.split(' ').join(' L')} L${LARGEUR},${hauteur} Z`;

  // Repère du zéro, quand la série passe de part et d'autre.
  const zeroVisible = min < 0 && max > 0;
  const yZero = hauteur - ((0 - min) / amplitude) * (hauteur - 4) - 2;

  return (
    <svg
      className={className}
      viewBox={`0 0 ${LARGEUR} ${hauteur}`}
      /* La courbe s'étire en largeur sans déformer l'épaisseur du trait, que
         `vector-effect` maintient constante. */
      preserveAspectRatio="none"
      role="img"
      aria-label={resume}
    >
      <defs>
        <linearGradient id={`${id}-remplissage`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={couleur} stopOpacity="0.22" />
          <stop offset="100%" stopColor={couleur} stopOpacity="0" />
        </linearGradient>
      </defs>

      {zeroVisible && (
        <line
          x1="0"
          y1={yZero}
          x2={LARGEUR}
          y2={yZero}
          stroke="currentColor"
          strokeOpacity="0.25"
          strokeWidth="1"
          strokeDasharray="2 2"
          vectorEffect="non-scaling-stroke"
        />
      )}

      <path d={aire} fill={`url(#${id}-remplissage)`} />
      <polyline
        points={ligne}
        fill="none"
        stroke={couleur}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
