import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { AuthContext, type AuthContextValue } from '../auth/useAuth';
import { apiClient } from '../../shared/api-client/client';
import { SuppliersPage } from './SuppliersPage';

// Voir ré-audit : Suppliers n'avait pas reçu l'activer/désactiver ajouté à Warehouses (parité
// manquante), et aucun des deux n'avait de test. Couvre ici liste, édition et toggle actif.
vi.mock('../../shared/api-client/client', () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn() },
}));

const mockedApiClient = vi.mocked(apiClient, true);

const activeSupplierId = '33333333-3333-4333-8333-333333333333';
const inactiveSupplierId = '44444444-4444-4444-8444-444444444444';
const activeSupplier = {
  id: activeSupplierId, name: 'Fournisseur A', contactEmail: 'a@test.local', phone: null, isActive: true,
};
const inactiveSupplier = {
  id: inactiveSupplierId, name: 'Fournisseur B', contactEmail: null, phone: null, isActive: false,
};

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const authValue: AuthContextValue = {
    auth: null,
    isAuthenticated: true,
    isInitializing: false,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    hasAtLeastRole: () => true,
  };

  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={authValue}>
        <SuppliersPage />
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

describe('SuppliersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedApiClient.get.mockResolvedValue({ data: [activeSupplier, inactiveSupplier] });
  });

  it('renders suppliers from the API and marks inactive ones', async () => {
    renderPage();

    expect(await screen.findByText('Fournisseur A')).toBeInTheDocument();
    expect(screen.getByText('Fournisseur B')).toBeInTheDocument();
    expect(screen.getByText('Inactif')).toBeInTheDocument();
  });

  it('submits the edited name via PUT', async () => {
    mockedApiClient.put.mockResolvedValue({ data: undefined });
    renderPage();

    await screen.findByText('Fournisseur A');
    const row = screen.getByText('Fournisseur A').closest('li')!;
    fireEvent.click(within(row).getByRole('button', { name: 'Éditer' }));

    const nameInput = within(row).getByRole('textbox', { name: 'Nom' });
    fireEvent.change(nameInput, { target: { value: 'Fournisseur A renommé' } });
    fireEvent.click(within(row).getByRole('button', { name: 'Enregistrer' }));

    await waitFor(() =>
      expect(mockedApiClient.put).toHaveBeenCalledWith(
        `/suppliers/${activeSupplierId}`,
        expect.objectContaining({ name: 'Fournisseur A renommé' }),
      ),
    );
  });

  it('toggles active state via PATCH with the flipped boolean (parity with Warehouses)', async () => {
    mockedApiClient.patch.mockResolvedValue({ data: undefined });
    renderPage();

    await screen.findByText('Fournisseur A');
    const row = screen.getByText('Fournisseur A').closest('li')!;
    fireEvent.click(within(row).getByRole('button', { name: 'Désactiver' }));

    await waitFor(() =>
      expect(mockedApiClient.patch).toHaveBeenCalledWith(`/suppliers/${activeSupplierId}/active`, { isActive: false }),
    );
  });

  it('activates an inactive supplier via PATCH with isActive: true', async () => {
    mockedApiClient.patch.mockResolvedValue({ data: undefined });
    renderPage();

    await screen.findByText('Fournisseur B');
    const row = screen.getByText('Fournisseur B').closest('li')!;
    fireEvent.click(within(row).getByRole('button', { name: 'Activer' }));

    await waitFor(() =>
      expect(mockedApiClient.patch).toHaveBeenCalledWith(`/suppliers/${inactiveSupplierId}/active`, { isActive: true }),
    );
  });

  it('pre-fills the edit form with the current values on each open', async () => {
    renderPage();

    await screen.findByText('Fournisseur A');
    const row = screen.getByText('Fournisseur A').closest('li')!;

    fireEvent.click(within(row).getByRole('button', { name: 'Éditer' }));
    const nameInput = within(row).getByRole('textbox', { name: 'Nom' }) as HTMLInputElement;
    expect(nameInput.value).toBe('Fournisseur A');

    fireEvent.click(within(row).getByRole('button', { name: 'Annuler' }));
    fireEvent.click(within(row).getByRole('button', { name: 'Éditer' }));

    const reopenedInput = within(row).getByRole('textbox', { name: 'Nom' }) as HTMLInputElement;
    expect(reopenedInput.value).toBe('Fournisseur A');
  });
});
