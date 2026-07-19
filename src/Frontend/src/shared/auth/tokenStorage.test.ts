import { beforeEach, describe, expect, it } from 'vitest';
import { clearAuth, getAuth, hasAtLeastRole, setAuth, type StoredAuth } from './tokenStorage';

const STORAGE_KEY = 'inventory.auth';

function sampleAuth(overrides: Partial<StoredAuth> = {}): StoredAuth {
  return {
    accessToken: 'access-123',
    accessTokenExpiresAtUtc: '2026-07-15T12:00:00Z',
    refreshToken: 'refresh-abc',
    email: 'test@example.com',
    displayName: 'Test User',
    roles: ['Employe'],
    ...overrides,
  };
}

beforeEach(() => {
  clearAuth();
  localStorage.clear();
});

describe('setAuth / getAuth', () => {
  it('keeps the access token in memory', () => {
    setAuth(sampleAuth());

    expect(getAuth()?.accessToken).toBe('access-123');
  });

  it('never persists the access token to localStorage', () => {
    setAuth(sampleAuth());

    const raw = localStorage.getItem(STORAGE_KEY);
    expect(raw).not.toBeNull();
    const persisted = JSON.parse(raw!) as Record<string, unknown>;
    expect(persisted).not.toHaveProperty('accessToken');
  });

  it('persists the refresh token and profile fields to localStorage', () => {
    setAuth(sampleAuth());

    const persisted = JSON.parse(localStorage.getItem(STORAGE_KEY)!) as Record<string, unknown>;
    expect(persisted).toMatchObject({
      refreshToken: 'refresh-abc',
      email: 'test@example.com',
      displayName: 'Test User',
      roles: ['Employe'],
    });
  });
});

describe('clearAuth', () => {
  it('resets getAuth to null and removes the persisted entry', () => {
    setAuth(sampleAuth());

    clearAuth();

    expect(getAuth()).toBeNull();
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});

describe('cross-tab sync via the storage event', () => {
  it('adopts a rotated refresh token written by another tab, without exposing an access token', () => {
    setAuth(sampleAuth());

    // Un autre onglet a rafraîchi la session : localStorage change sous nos pieds,
    // le navigateur relaie ça via un event 'storage' (ne se déclenche jamais dans
    // l'onglet qui a lui-même écrit — on le simule ici pour tester le handler).
    const rotated = { ...sampleAuth(), refreshToken: 'refresh-rotated' };
    const { accessToken: _unused, ...persisted } = rotated;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(persisted));

    window.dispatchEvent(
      new StorageEvent('storage', {
        key: STORAGE_KEY,
        newValue: JSON.stringify(persisted),
      }),
    );

    expect(getAuth()?.refreshToken).toBe('refresh-rotated');
    expect(getAuth()?.accessToken).toBe('');
  });

  it('logs out when another tab clears the session', () => {
    setAuth(sampleAuth());

    window.dispatchEvent(new StorageEvent('storage', { key: STORAGE_KEY, newValue: null }));

    expect(getAuth()).toBeNull();
  });

  it('ignores storage events for unrelated keys', () => {
    setAuth(sampleAuth());

    window.dispatchEvent(new StorageEvent('storage', { key: 'some.other.key', newValue: null }));

    expect(getAuth()?.refreshToken).toBe('refresh-abc');
  });
});

describe('hasAtLeastRole', () => {
  it('returns false when no session is active', () => {
    expect(hasAtLeastRole('Employe')).toBe(false);
  });

  it('lets a Gestionnaire pass an Employe-minimum check (role hierarchy)', () => {
    setAuth(sampleAuth({ roles: ['Gestionnaire'] }));

    expect(hasAtLeastRole('Employe')).toBe(true);
  });

  it('does not let an Employe pass a Gestionnaire-minimum check', () => {
    setAuth(sampleAuth({ roles: ['Employe'] }));

    expect(hasAtLeastRole('Gestionnaire')).toBe(false);
  });

  it('lets an Admin pass every minimum-role check', () => {
    setAuth(sampleAuth({ roles: ['Admin'] }));

    expect(hasAtLeastRole('Employe')).toBe(true);
    expect(hasAtLeastRole('Gestionnaire')).toBe(true);
    expect(hasAtLeastRole('Admin')).toBe(true);
  });
});
