import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import axios, { type InternalAxiosRequestConfig } from 'axios';
import { apiClient } from './client';
import { clearAuth, getAuth, setAuth } from '../auth/tokenStorage';

/**
 * Vérifie le comportement de l'intercepteur 401 (voir AUDIT.md B-R1) : un 401 renvoyé par
 * /auth/login|register|refresh ne doit jamais déclencher clearAuth()/redirection — c'est un
 * échec d'authentification normal que l'appelant (le formulaire) doit pouvoir afficher
 * lui-même, pas une session expirée.
 */
describe('apiClient 401 interceptor', () => {
  const originalAdapter = apiClient.defaults.adapter;
  const originalLocation = window.location;
  let assignSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    clearAuth();
    localStorage.clear();

    // jsdom expose `location.assign` en lecture seule (non reconfigurable) : vi.spyOn direct
    // échoue avec "Cannot redefine property". On remplace toute la propriété `window.location`
    // par un objet contrôlable, comme le recommande la doc Vitest pour ce cas précis.
    assignSpy = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...originalLocation, assign: assignSpy },
    });
  });

  afterEach(() => {
    apiClient.defaults.adapter = originalAdapter;
    Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
    vi.restoreAllMocks();
  });

  function mockAdapterRejecting401(url: string) {
    apiClient.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      if (config.url === url) {
        return Promise.reject({
          config,
          response: { status: 401, data: {}, statusText: 'Unauthorized', headers: {}, config },
          isAxiosError: true,
        });
      }
      throw new Error(`Unexpected request to ${config.url} in test`);
    };
  }

  it('does not clear the session or redirect on 401 from /auth/login', async () => {
    setAuth({
      accessToken: 'stale-access',
      accessTokenExpiresAtUtc: '2026-01-01T00:00:00Z',
      refreshToken: 'stale-refresh',
      email: 'user@test.local',
      displayName: 'User',
      roles: ['Employe'],
    });
    mockAdapterRejecting401('/auth/login');

    await expect(apiClient.post('/auth/login', { email: 'a', password: 'b' })).rejects.toBeTruthy();

    // La session existante (refresh token) ne doit pas avoir été effacée par un simple
    // mauvais mot de passe sur /auth/login.
    expect(getAuth()?.refreshToken).toBe('stale-refresh');
    expect(assignSpy).not.toHaveBeenCalled();
  });

  it('does not clear the session or redirect on 401 from /auth/refresh', async () => {
    setAuth({
      accessToken: 'stale-access',
      accessTokenExpiresAtUtc: '2026-01-01T00:00:00Z',
      refreshToken: 'stale-refresh',
      email: 'user@test.local',
      displayName: 'User',
      roles: ['Employe'],
    });
    mockAdapterRejecting401('/auth/refresh');

    await expect(
      apiClient.post('/auth/refresh', { refreshToken: 'stale-refresh' }),
    ).rejects.toBeTruthy();

    expect(getAuth()?.refreshToken).toBe('stale-refresh');
    expect(assignSpy).not.toHaveBeenCalled();
  });

  it('clears the session and redirects on 401 from a protected, already-retried endpoint', async () => {
    setAuth({
      accessToken: 'stale-access',
      accessTokenExpiresAtUtc: '2026-01-01T00:00:00Z',
      refreshToken: 'stale-refresh',
      email: 'user@test.local',
      displayName: 'User',
      roles: ['Employe'],
    });

    // _retried: true simule un refresh déjà tenté sans succès (voir client.ts) — un
    // deuxième 401 sur un endpoint protégé signifie une session vraiment expirée.
    apiClient.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      (config as InternalAxiosRequestConfig & { _retried?: boolean })._retried = true;
      return Promise.reject({
        config,
        response: { status: 401, data: {}, statusText: 'Unauthorized', headers: {}, config },
        isAxiosError: true,
      });
    };

    await expect(apiClient.get('/products')).rejects.toBeTruthy();

    expect(getAuth()).toBeNull();
    expect(assignSpy).toHaveBeenCalledWith('/login');
  });

  it('on a first 401, silently refreshes and retries the original request with the new token', async () => {
    // Chemin le plus risqué du fichier (single-flight refresh + relecture de la requête
    // d'origine) et jusqu'ici jamais exercé par les tests (voir ré-audit) — seules les
    // branches sans retry l'étaient.
    setAuth({
      accessToken: 'stale-access',
      accessTokenExpiresAtUtc: '2026-01-01T00:00:00Z',
      refreshToken: 'stale-refresh',
      email: 'user@test.local',
      displayName: 'User',
      roles: ['Employe'],
    });

    // requestNewAccessToken() appelle axios.post (l'instance globale, pas apiClient) —
    // il faut le mocker séparément de l'adapter d'apiClient.
    const refreshResponse = {
      accessToken: 'fresh-access',
      accessTokenExpiresAtUtc: '2026-01-01T01:00:00Z',
      refreshToken: 'fresh-refresh',
      email: 'user@test.local',
      displayName: 'User',
      roles: ['Employe'],
    };
    const postSpy = vi.spyOn(axios, 'post').mockResolvedValue({ data: refreshResponse });

    let callCount = 0;
    apiClient.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      callCount += 1;
      if (callCount === 1) {
        return Promise.reject({
          config,
          response: { status: 401, data: {}, statusText: 'Unauthorized', headers: {}, config },
          isAxiosError: true,
        });
      }
      // Requête rejouée : doit porter le nouveau token, pas l'ancien.
      return {
        data: { ok: true },
        status: 200,
        statusText: 'OK',
        headers: {},
        config,
      };
    };

    const response = await apiClient.get('/products');

    expect(response.status).toBe(200);
    expect(response.data).toEqual({ ok: true });
    expect(callCount).toBe(2);
    expect(postSpy).toHaveBeenCalledOnce();
    expect(assignSpy).not.toHaveBeenCalled();
    // requestNewAccessToken() appelle setAuth(data) : la session en mémoire doit refléter
    // le nouveau couple de jetons après le refresh silencieux.
    expect(getAuth()?.accessToken).toBe('fresh-access');
    expect(getAuth()?.refreshToken).toBe('fresh-refresh');
  });
});
