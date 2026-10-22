import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Variation } from './Variation';

const texte = (el: HTMLElement) => (el.textContent ?? '').replace(/[  ]/g, ' ');

describe('Variation', () => {
  it("étant donné aucune période de référence, quand afficher, alors pas de pourcentage inventé", () => {
    const { container } = render(<Variation pourcentage={null} periodeJours={30} />);

    expect(texte(container)).toBe('Pas de comparaison possible');
  });

  it('étant donné une hausse, quand afficher, alors signe plus et période nommée', () => {
    const { container } = render(<Variation pourcentage={12.5} periodeJours={30} />);

    expect(texte(container)).toBe('+12,5 %sur 30 j');
  });

  it('étant donné une baisse, quand afficher, alors signe moins sans plus', () => {
    const { container } = render(<Variation pourcentage={-8} periodeJours={7} />);

    expect(texte(container)).toBe('-8 %sur 7 j');
    expect(container.firstElementChild).toHaveClass('bg-amber-100');
  });

  it('étant donné une stabilité, quand afficher, alors ni plus ni couleur de hausse', () => {
    const { container } = render(<Variation pourcentage={0} periodeJours={30} />);

    expect(texte(container)).toBe('0 %sur 30 j');
    expect(container.firstElementChild).toHaveClass('bg-slate-100');
  });
});
