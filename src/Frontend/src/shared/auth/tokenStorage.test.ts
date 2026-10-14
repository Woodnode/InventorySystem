import { beforeEach, describe, expect, it } from 'vitest';
import {
  clearAuth,
  getAuth,
  hasAtLeastRole,
  hasSessionHint,
  setAuth,
  subscribe,
  type StoredAuth,
} from './tokenStorage';

const SESSION_HINT_KEY = 'inventory.session';

function sampleAuth(overrides: Partial<StoredAuth> = {}): StoredAuth {
  return {
    accessToken: 'access-123',
    accessTokenExpiresAtUtc: '2026-07-15T12:00:00Z',
    email: 'test@example.com',
    displayName: 'Test User',
    roles: ['Employe'],
    ...overrides,
  };
}

function everythingInLocalStorage(): string {
  return Object.keys(localStorage)
    .map((key) => `${key}=${localStorage.getItem(key)}`)
    .join('\n');
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

  it('never writes a token or profile data to localStorage', () => {
    setAuth(sampleAuth());

    const stored = everythingInLocalStorage();
    expect(stored).not.toContain('access-123');
    expect(stored).not.toContain('test@example.com');
    expect(localStorage.getItem(SESSION_HINT_KEY)).toBe('1');
  });

  it('drops a refresh token that a response might still carry', () => {
    setAuth({ ...sampleAuth(), refreshToken: 'refresh-abc' } as StoredAuth);

    expect(getAuth()).not.toHaveProperty('refreshToken');
    expect(everythingInLocalStorage()).not.toContain('refresh-abc');
  });

  it('records a session hint so a reload knows to refresh silently', () => {
    expect(hasSessionHint()).toBe(false);

    setAuth(sampleAuth());

    expect(hasSessionHint()).toBe(true);
  });
});

describe('clearAuth', () => {
  it('resets getAuth to null and removes the session hint', () => {
    setAuth(sampleAuth());

    clearAuth();

    expect(getAuth()).toBeNull();
    expect(hasSessionHint()).toBe(false);
  });
});

describe('cross-tab sync via the storage event', () => {
  it('logs out when another tab clears the session', () => {
    setAuth(sampleAuth());

    window.dispatchEvent(new StorageEvent('storage', { key: SESSION_HINT_KEY, newValue: null }));

    expect(getAuth()).toBeNull();
  });

  it('notifies listeners when another tab logs in, so AuthProvider can refresh', () => {
    const received: Array<StoredAuth | null> = [];
    const unsubscribe = subscribe((auth) => received.push(auth));

    localStorage.setItem(SESSION_HINT_KEY, '1');
    window.dispatchEvent(new StorageEvent('storage', { key: SESSION_HINT_KEY, newValue: '1' }));
    unsubscribe();

    expect(received).toEqual([null]);
    expect(hasSessionHint()).toBe(true);
  });

  it('ignores storage events for unrelated keys', () => {
    setAuth(sampleAuth());

    window.dispatchEvent(new StorageEvent('storage', { key: 'some.other.key', newValue: null }));

    expect(getAuth()?.accessToken).toBe('access-123');
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
