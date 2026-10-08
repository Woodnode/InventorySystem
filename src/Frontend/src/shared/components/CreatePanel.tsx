import { useId, useState, type ReactNode } from 'react';

/**
 * Formulaire de création repliable.
 *
 * Les formulaires de création étaient dépliés en permanence, en tête de page : sur
 * un écran de 375px il fallait faire défiler 1050px — plus d'un écran et demi —
 * avant d'atteindre le premier élément de la liste. Or ces pages servent d'abord à
 * consulter. Le formulaire est donc fermé par défaut et ne s'ouvre qu'à la demande.
 */
export function CreatePanel({
  label,
  children,
  defaultOpen = false,
}: {
  /** Libellé du bouton d'ouverture, ex. « Nouveau produit ». */
  label: string;
  children: ReactNode;
  defaultOpen?: boolean;
}) {
  const [ouvert, setOuvert] = useState(defaultOpen);
  const id = useId();

  return (
    <section className="mt-6">
      <button
        type="button"
        className="btn-secondary w-full sm:w-auto"
        onClick={() => setOuvert((o) => !o)}
        aria-expanded={ouvert}
        aria-controls={id}
      >
        <span aria-hidden="true">{ouvert ? '−' : '+'}</span>
        {label}
      </button>

      {/* Le contenu est retiré du DOM plutôt que masqué : les champs d'un formulaire
          caché resteraient sinon accessibles au clavier et annoncés par un lecteur. */}
      {ouvert && (
        <div id={id} className="mt-3">
          {children}
        </div>
      )}
    </section>
  );
}
