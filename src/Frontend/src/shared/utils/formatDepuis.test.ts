import { describe, expect, it } from 'vitest';
import { estRelatif, formatDepuis } from './formatDepuis';

const maintenant = new Date('2026-10-21T15:00:00Z');
const avant = (secondes: number) => new Date(maintenant.getTime() - secondes * 1000);

describe('formatDepuis', () => {
  it("affiche « à l'instant » sous une minute", () => {
    expect(formatDepuis(avant(30), maintenant)).toBe("à l'instant");
  });

  it("ramène une date future à « à l'instant »", () => {
    expect(formatDepuis(avant(-17 * 60), maintenant)).toBe("à l'instant");
  });

  it('compte en minutes sous une heure', () => {
    expect(formatDepuis(avant(18 * 60), maintenant)).toBe('il y a 18 minutes');
  });

  it('compte en heures sous une journée', () => {
    expect(formatDepuis(avant(3 * 3600), maintenant)).toBe('il y a 3 heures');
  });

  it('dit « hier » pour la veille', () => {
    expect(formatDepuis(avant(26 * 3600), maintenant)).toBe('hier');
  });

  it('accepte une chaîne ISO', () => {
    expect(formatDepuis(avant(5 * 60).toISOString(), maintenant)).toBe('il y a 5 minutes');
  });

  it('revient à une date courte au-delà de sept jours', () => {
    const resultat = formatDepuis(avant(20 * 86400), maintenant);
    expect(resultat).not.toMatch(/il y a/);
    expect(resultat).toMatch(/oct/);
  });
});

describe('estRelatif', () => {
  it('est vrai sous sept jours', () => {
    expect(estRelatif(avant(6 * 86400), maintenant)).toBe(true);
  });

  it('est faux au-delà, quand formatDepuis rend une date', () => {
    expect(estRelatif(avant(8 * 86400), maintenant)).toBe(false);
  });
});
