import { TranslateService } from './translate.service';
import { sr } from '../i18n/sr';
import { en } from '../i18n/en';
import { ru } from '../i18n/ru';

describe('TranslateService', () => {
  let ts: TranslateService;

  beforeEach(() => {
    localStorage.clear();
    ts = new TranslateService();
  });

  it('podrazumevani jezik je srpski i prati <html lang>', () => {
    expect(ts.lang).toBe('sr');
    ts.setLang('en');
    expect(document.documentElement.lang).toBe('en');
    expect(localStorage.getItem('lang')).toBe('en');
    expect(new TranslateService().lang).toBe('en');
  });

  it('nepoznat ključ vraća sam ključ, a ključ koji jezik nema vraća srpski tekst', () => {
    expect(ts.t('nema.takvog.kljuca')).toBe('nema.takvog.kljuca');
    ts.setLang('ru');
    // ruski nema množinu za "Veza", pa se koristi osnovni naziv jedinice
    expect(ts.unit('Veza', 5)).toBe(ru['unit.Veza']);
  });

  it('množina: srpski one/few/many, engleski one/many', () => {
    expect(ts.unit('Veza', 1)).toBe(sr['unit.Veza.one']);
    expect(ts.unit('Veza', 2)).toBe(sr['unit.Veza.few']);
    expect(ts.unit('Veza', 5)).toBe(sr['unit.Veza.many']);
    ts.setLang('en');
    expect(ts.unit('Veza', 1)).toBe(en['unit.Veza.one']);
    expect(ts.unit('Veza', 3)).toBe(en['unit.Veza.many']);
  });

  it('brojevi po jeziku: 1,5 na srpskom, 1.5 na engleskom', () => {
    expect(ts.num(1.5)).toBe('1,5');
    ts.setLang('en');
    expect(ts.num(1.5)).toBe('1.5');
  });

  it('svaki jezik ima isti broj prevoda za tekstove kupca (bez množine jedinica)', () => {
    const base = (o: object) => Object.keys(o).filter(k => !/^unit\.[^.]+\.(one|few|many)$/.test(k)).sort();
    expect(base(en)).toEqual(base(sr));
    expect(base(ru)).toEqual(base(sr));
  });
});
