import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AuthContext, type AuthContextValue } from './useAuth';
import { LoginPage } from './LoginPage';

/**
 * Voir AUDIT.md F-1 : tant que le refresh silencieux est en vol (isInitializing), le
 * formulaire de connexion ne doit pas s'afficher — sinon il flashe avant la redirection
 * vers /dashboard pour un utilisateur déjà connecté qui recharge la page.
 */
function renderWithAuth(overrides: Partial<AuthContextValue>) {
  const value: AuthContextValue = {
    auth: null,
    isAuthenticated: false,
    isInitializing: false,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    hasAtLeastRole: () => false,
    ...overrides,
  };

  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthContext.Provider value={value}>
        <LoginPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  it('shows a loader instead of the form while isInitializing is true', () => {
    renderWithAuth({ isInitializing: true, isAuthenticated: false });

    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
    expect(screen.getByText(/chargement/i)).toBeInTheDocument();
  });

  it('shows the login form once initialization is done and the user is not authenticated', () => {
    renderWithAuth({ isInitializing: false, isAuthenticated: false });

    expect(screen.getByRole('button', { name: /se connecter/i })).toBeInTheDocument();
  });
});
