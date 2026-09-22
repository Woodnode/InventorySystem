import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { AuthContext, type AuthContextValue } from '../auth/useAuth';
import { apiClient } from '../../shared/api-client/client';
import { WarehousesPage } from './WarehousesPage';

// Voir AUDIT.md F-4 / ré-audit : le CRUD Warehouses (édition inline + activer/désactiver)
// n'avait aucun test malgré des bugs réels trouvés à l'audit (entrepôts désactivés restés
// sélectionnables ailleurs, defaultValues périmées). Ces tests couvrent le comportement de
// la page elle-même : liste, édition, et le toggle actif/inactif.
vi.mock('../../shared/api-client/client', () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn() },
}));

const mockedApiClient = vi.mocked(apiClient, true);

const activeWarehouseId = '11111111-1111-4111-8111-111111111111';
const inactiveWarehouseId = '22222222-2222-4222-8222-222222222222';
const activeWarehouse = { id: activeWarehouseId, name: 'Entrepôt A', address: '1 rue Test', isActive: true };
const inactiveWarehouse = { id: inactiveWarehouseId, name: 'Entrepôt B', address: null, isActive: false };

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const authValue: AuthContextValue = {
    auth: null,
    isAuthenticated: true,
    isInitializing: false,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    hasAtLeastRole: () => true, // Gestionnaire : édition/activation visibles
  };

  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={authValue}>
        <WarehousesPage />
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

describe('WarehousesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedApiClient.get.mockResolvedValue({ data: [activeWarehouse, inactiveWarehouse] });
  });

  it('renders warehouses from the API and marks inactive ones', async () => {
    renderPage();

    expect(await screen.findByText('Entrepôt A')).toBeInTheDocument();
    expect(screen.getByText('Entrepôt B')).toBeInTheDocument();
    expect(screen.getByText('Inactif')).toBeInTheDocument();
  });

  it('submits the edited name/address via PUT', async () => {
    mockedApiClient.put.mockResolvedValue({ data: undefined });
    renderPage();

    await screen.findByText('Entrepôt A');
    const row = screen.getByText('Entrepôt A').closest('li')!;
    fireEvent.click(within(row).getByRole('button', { name: 'Éditer' }));

    const nameInput = within(row).getByRole('textbox', { name: 'Nom' });
    fireEvent.change(nameInput, { target: { value: 'Entrepôt A renommé' } });
    fireEvent.click(within(row).getByRole('button', { name: 'Enregistrer' }));

    await waitFor(() =>
      expect(mockedApiClient.put).toHaveBeenCalledWith(
        '/warehouses/11111111-1111-4111-8111-111111111111',
        expect.objectContaining({ name: 'Entrepôt A renommé' }),
      ),
    );
  });

  it('toggles active state via PATCH with the flipped boolean', async () => {
    mockedApiClient.patch.mockResolvedValue({ data: undefined });
    renderPage();

    await screen.findByText('Entrepôt A');
    const row = screen.getByText('Entrepôt A').closest('li')!;
    fireEvent.click(within(row).getByRole('button', { name: 'Désactiver' }));

    await waitFor(() =>
      expect(mockedApiClient.patch).toHaveBeenCalledWith('/warehouses/11111111-1111-4111-8111-111111111111/active', { isActive: false }),
    );
  });

  it('pre-fills the edit form with the current values, not stale ones, on each open', async () => {
    // Régression testée : useForm({ defaultValues }) n'est lu qu'au premier montage — sans
    // reset() explicite à l'ouverture, ré-ouvrir "Éditer" pouvait montrer des valeurs
    // périmées (voir ré-audit).
    renderPage();

    await screen.findByText('Entrepôt A');
    const row = screen.getByText('Entrepôt A').closest('li')!;

    fireEvent.click(within(row).getByRole('button', { name: 'Éditer' }));
    const nameInput = within(row).getByRole('textbox', { name: 'Nom' }) as HTMLInputElement;
    expect(nameInput.value).toBe('Entrepôt A');

    fireEvent.click(within(row).getByRole('button', { name: 'Annuler' }));
    fireEvent.click(within(row).getByRole('button', { name: 'Éditer' }));

    const reopenedInput = within(row).getByRole('textbox', { name: 'Nom' }) as HTMLInputElement;
    expect(reopenedInput.value).toBe('Entrepôt A');
  });
});
