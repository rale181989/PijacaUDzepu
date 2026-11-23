import { PRODUCT_CATEGORIES, quantityStep, roundQty } from './product.model';

describe('količine proizvoda', () => {
  it('korak je 0,5 za kilogram i litar, a 1 za komade, veze, pakovanja…', () => {
    expect(quantityStep('Kg')).toBe(0.5);
    expect(quantityStep('Litar')).toBe(0.5);
    for (const unit of ['Komad', 'Veza', 'Gram', 'Pakovanje', 'Kutija', 'Flasa'] as const)
      expect(quantityStep(unit)).toBe(1);
  });

  it('zaokružuje na dve decimale, pa sabiranje pola kila ne pravi 1,4999999', () => {
    let qty = 0;
    for (let i = 0; i < 3; i++) qty = roundQty(qty + 0.5);
    expect(qty).toBe(1.5);
    expect(roundQty(0.1 + 0.2)).toBe(0.3);
  });

  it('svaka grupa proizvoda je u spisku tačno jednom (filter, forma prodavca, izveštaji)', () => {
    expect(new Set(PRODUCT_CATEGORIES).size).toBe(PRODUCT_CATEGORIES.length);
    expect(PRODUCT_CATEGORIES.slice(0, 3)).toEqual(['Voce', 'Povrce', 'ZacinskoBilje']);
    expect(PRODUCT_CATEGORIES).toContain('Cvece');
    expect(PRODUCT_CATEGORIES).toContain('Riba');
    expect(PRODUCT_CATEGORIES[PRODUCT_CATEGORIES.length - 1]).toBe('Ostalo');
  });
});
