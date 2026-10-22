import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthContextValue } from '../features/auth/useAuth';
import { ProtectedRoute } from './ProtectedRoute';

function PageDeConnexion() {
  const location = useLocation();
  const origine = (location.state as { from?: string } | null)?.from;
  return <p>Connexion (retour vers {origine ?? 'aucune page'})</p>;
}

function afficher(etat: Pick<AuthContextValue, 'isAuthenticated' | 'isInitializing'>) {
  const valeur: AuthContextValue = {
    auth: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    hasAtLeastRole: () => true,
    ...etat,
  };
  return render(
    <AuthContext.Provider value={valeur}>
      <MemoryRouter initialEntries={['/produits/42']}>
        <Routes>
          <Route path="/login" element={<PageDeConnexion />} />
          <Route
            path="/produits/:id"
            element={
              <ProtectedRoute>
                <p>Fiche produit</p>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}

describe('ProtectedRoute', () => {
  it('étant donné un visiteur non connecté, quand ouvrir une page protégée, alors renvoyé à la connexion avec la page visée', () => {
    afficher({ isAuthenticated: false, isInitializing: false });

    expect(screen.getByText('Connexion (retour vers /produits/42)')).toBeInTheDocument();
    expect(screen.queryByText('Fiche produit')).not.toBeInTheDocument();
  });

  it("étant donné une session en cours de restauration, quand ouvrir une page protégée, alors un chargement accessible s'affiche sans monter la page", () => {
    afficher({ isAuthenticated: true, isInitializing: true });

    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByText('Fiche produit')).not.toBeInTheDocument();
  });

  it('étant donné une session prête, quand ouvrir une page protégée, alors la page est affichée', () => {
    afficher({ isAuthenticated: true, isInitializing: false });

    expect(screen.getByText('Fiche produit')).toBeInTheDocument();
  });
});
