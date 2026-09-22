import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorBoundary } from './ErrorBoundary';

function Boom(): never {
  throw new Error('boom');
}

// React et ErrorBoundary.componentDidCatch logguent tous deux l'erreur attendue sur
// console.error — bruit normal ici, pas une régression à signaler.
beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  vi.restoreAllMocks();
});

/**
 * Voir AUDIT.md F-2 : App.tsx passe désormais key={location.pathname} à l'ErrorBoundary de
 * route pour qu'une erreur déclenchée sur /products/A ne reste pas affichée en arrivant sur
 * /products/B. Ce test vérifie le mécanisme générique dont ce fix dépend : changer la `key`
 * d'un ErrorBoundary React force un vrai remount qui réinitialise son état d'erreur.
 */
describe('ErrorBoundary', () => {
  it('clears its error state when its key changes (route change)', () => {
    const { rerender } = render(
      <ErrorBoundary key="/products/A">
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByText(/erreur inattendue/i)).toBeInTheDocument();

    rerender(
      <ErrorBoundary key="/products/B">
        <p>Produit B</p>
      </ErrorBoundary>,
    );

    expect(screen.queryByText(/erreur inattendue/i)).not.toBeInTheDocument();
    expect(screen.getByText('Produit B')).toBeInTheDocument();
  });

  it('keeps showing the error across a rerender with the same key', () => {
    const { rerender } = render(
      <ErrorBoundary key="/products/A">
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByText(/erreur inattendue/i)).toBeInTheDocument();

    rerender(
      <ErrorBoundary key="/products/A">
        <p>Ce contenu ne devrait pas apparaître</p>
      </ErrorBoundary>,
    );

    // Même clé : pas de remount, l'ErrorBoundary reste "figée" sur son erreur jusqu'au clic
    // sur "Réessayer" — c'est exactement le problème que la clé sur la route corrige.
    expect(screen.getByText(/erreur inattendue/i)).toBeInTheDocument();
  });
});
