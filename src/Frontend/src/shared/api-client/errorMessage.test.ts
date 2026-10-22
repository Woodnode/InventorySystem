import { AxiosError, AxiosHeaders } from 'axios';
import { describe, expect, it } from 'vitest';
import { getErrorMessage } from './errorMessage';

function erreurHttp(status: number, data?: unknown) {
  return new AxiosError('échec', 'ERR_BAD_RESPONSE', undefined, undefined, {
    status,
    statusText: '',
    headers: {},
    config: { headers: new AxiosHeaders() },
    data,
  });
}

describe('getErrorMessage', () => {
  it("étant donné un ProblemDetails avec détail, quand traduire, alors le message métier de l'API est repris", () => {
    expect(getErrorMessage(erreurHttp(409, { title: 'Conflit', detail: 'Ce SKU est déjà utilisé par un autre produit.' })))
      .toBe('Ce SKU est déjà utilisé par un autre produit.');
  });

  it('étant donné un ProblemDetails sans détail, quand traduire, alors le titre est repris', () => {
    expect(getErrorMessage(erreurHttp(400, { title: 'Erreur de validation' }))).toBe('Erreur de validation');
  });

  it.each([
    [401, 'Session expirée — reconnecte-toi.'],
    [403, "Tu n'as pas la permission d'effectuer cette action."],
    [404, 'Ressource introuvable.'],
  ])('étant donné un statut %i sans corps, quand traduire, alors message dédié', (status, message) => {
    expect(getErrorMessage(erreurHttp(status))).toBe(message);
  });

  it('étant donné une erreur serveur sans corps, quand traduire, alors le message de repli fourni', () => {
    expect(getErrorMessage(erreurHttp(500), 'Export impossible.')).toBe('Export impossible.');
  });

  it("étant donné l'absence de réponse, quand traduire, alors l'utilisateur est invité à vérifier sa connexion", () => {
    const erreur = new AxiosError('Network Error', 'ERR_NETWORK');

    expect(getErrorMessage(erreur)).toContain('vérifie ta connexion');
  });

  it("étant donné une erreur qui n'est pas HTTP, quand traduire, alors rien de son message interne ne fuit", () => {
    expect(getErrorMessage(new TypeError("Cannot read properties of undefined (reading 'id')"))).toBe(
      'Une erreur est survenue. Réessaie.',
    );
  });
});
