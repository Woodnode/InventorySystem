import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AxiosError, AxiosHeaders } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient } from '../../shared/api-client/client';
import { ToastProvider } from '../../shared/components/ToastProvider';
import { MovementForm } from './MovementForm';

vi.mock('../../shared/api-client/client', () => ({
  apiClient: { get: vi.fn(), post: vi.fn() },
}));

const mockedApiClient = vi.mocked(apiClient, true);

const produitId = '33333333-3333-4333-8333-333333333333';
const boutique = { id: '11111111-1111-4111-8111-111111111111', name: 'Boutique', address: null, isActive: true };
const reserve = { id: '22222222-2222-4222-8222-222222222222', name: 'Réserve', address: null, isActive: true };
const ferme = { id: '44444444-4444-4444-8444-444444444444', name: 'Ancien dépôt', address: null, isActive: false };

function afficher() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <MovementForm productId={produitId} />
      </ToastProvider>
    </QueryClientProvider>,
  );
}

function selectionner(libelle: string, valeur: string) {
  const champ = screen.getByText(libelle).closest('label')!;
  fireEvent.change(within(champ).getByRole('combobox'), { target: { value: valeur } });
}

describe('MovementForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedApiClient.get.mockResolvedValue({ data: [boutique, reserve, ferme] });
  });

  it('étant donné un entrepôt désactivé, quand ouvrir le formulaire, alors il ne figure pas dans la liste', async () => {
    afficher();

    expect(await screen.findByRole('option', { name: 'Boutique' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Ancien dépôt' })).not.toBeInTheDocument();
  });

  it("étant donné une entrée, quand l'enregistrer, alors la destination est nulle et un identifiant client est joint", async () => {
    mockedApiClient.post.mockResolvedValue({ data: { id: 'nouveau' } });
    afficher();
    await screen.findByRole('option', { name: 'Boutique' });

    selectionner('Entrepôt', boutique.id);
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '4' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));

    await waitFor(() => expect(mockedApiClient.post).toHaveBeenCalledTimes(1));
    const [url, corps] = mockedApiClient.post.mock.calls[0];
    expect(url).toBe('/movements');
    expect(corps).toMatchObject({ productId: produitId, warehouseId: boutique.id, type: 'In', quantity: 4, toWarehouseId: null });
    expect((corps as { clientGuid: string }).clientGuid).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("étant donné un transfert, quand choisir le type, alors le champ de destination apparaît", async () => {
    afficher();
    await screen.findByRole('option', { name: 'Boutique' });

    expect(screen.queryByText('Entrepôt destination')).not.toBeInTheDocument();
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'Transfer' } });

    expect(await screen.findByText('Entrepôt destination')).toBeInTheDocument();
    expect(screen.getByText('Entrepôt source')).toBeInTheDocument();
  });

  it('étant donné un transfert vers la source, quand enregistrer, alors refusé sans appel réseau', async () => {
    afficher();
    await screen.findByRole('option', { name: 'Boutique' });
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'Transfer' } });
    await screen.findByText('Entrepôt destination');

    selectionner('Entrepôt source', boutique.id);
    selectionner('Entrepôt destination', boutique.id);
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));

    expect(await screen.findByText("L'entrepôt de destination doit être différent de l'entrepôt source")).toBeInTheDocument();
    expect(mockedApiClient.post).not.toHaveBeenCalled();
  });

  it("étant donné une quantité nulle, quand enregistrer, alors l'envoi est bloqué sans appel réseau", async () => {
    afficher();
    await screen.findByRole('option', { name: 'Boutique' });

    selectionner('Entrepôt', boutique.id);
    const quantite = screen.getByRole('spinbutton') as HTMLInputElement;
    fireEvent.change(quantite, { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));

    // Le champ porte min=1 : le navigateur refuse la saisie avant même la validation zod.
    expect(quantite.validity.rangeUnderflow).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(mockedApiClient.post).not.toHaveBeenCalled();
  });

  it('étant donné un transfert sans destination, quand enregistrer, alors destination exigée', async () => {
    afficher();
    await screen.findByRole('option', { name: 'Boutique' });
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'Transfer' } });
    await screen.findByText('Entrepôt destination');

    selectionner('Entrepôt source', boutique.id);
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));

    expect(await screen.findByText("L'entrepôt de destination est requis pour un transfert")).toBeInTheDocument();
    expect(mockedApiClient.post).not.toHaveBeenCalled();
  });

  it("étant donné un refus de l'API, quand enregistrer, alors le motif métier est affiché", async () => {
    const reponse = {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { title: 'Règle métier violée', detail: 'Stock insuffisant : 2 disponible(s), 5 demandé(s).' },
    };
    mockedApiClient.post.mockRejectedValue(new AxiosError('400', 'ERR_BAD_REQUEST', undefined, undefined, reponse));
    afficher();
    await screen.findByRole('option', { name: 'Boutique' });

    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'Out' } });
    selectionner('Entrepôt', boutique.id);
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Stock insuffisant : 2 disponible(s), 5 demandé(s).');
  });
});
