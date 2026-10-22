import { describe, expect, it } from 'vitest';
import { formatPoids } from './formatPoids';

/** Espaces insécables (fr-CA) ramenés à des espaces simples pour lire les attentes. */
const lisible = (texte: string) => texte.replace(/[  ]/g, ' ');

describe('formatPoids', () => {
  it.each([null, undefined, 0])('étant donné %s, quand formater, alors un tiret plutôt que « 0 g »', (valeur) => {
    expect(formatPoids(valeur)).toBe('—');
  });

  it('étant donné moins d’un kilo, quand formater, alors grammes entiers', () => {
    expect(lisible(formatPoids(219.6))).toBe('220 g');
  });

  it('étant donné entre 1 et 100 kg, quand formater, alors une décimale', () => {
    expect(lisible(formatPoids(12_340))).toBe('12,3 kg');
  });

  it('étant donné au moins 100 kg, quand formater, alors kilos entiers avec séparateur de milliers', () => {
    expect(lisible(formatPoids(412_000_000))).toBe('412 000 kg');
  });

  it('étant donné un poids négatif, quand formater, alors même unité que sa valeur absolue', () => {
    expect(lisible(formatPoids(-1500))).toBe('-1,5 kg');
  });
});
