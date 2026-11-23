import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { sr } from '../i18n/sr';
import { en } from '../i18n/en';
import { ru } from '../i18n/ru';

export type Lang = 'sr' | 'en' | 'ru';

// Rečnici su u src/app/i18n/<jezik>.ts (jedan fajl po jeziku; kompajler proverava da en i ru imaju sve ključeve)

const TRANSLATIONS: Record<Lang, Record<string, string | undefined>> = { sr, en, ru };

@Injectable({ providedIn: 'root' })
export class TranslateService {
  private langSource = new BehaviorSubject<Lang>(this.loadLang());
  lang$ = this.langSource.asObservable();

  constructor() {
    // Jezik stranice prati izabrani jezik (pregledač, čitači ekrana, prevodilac)
    document.documentElement.lang = this.lang;
  }

  get lang(): Lang { return this.langSource.value; }

  t(key: string): string {
    return TRANSLATIONS[this.lang]?.[key] ?? TRANSLATIONS['sr']?.[key] ?? key;
  }

  // Jedinica uz količinu: "2 veze", "5 veza", "2 bunches"; skraćenice (kg, kom) se ne menjaju.
  // Množina samo ako je jezik ima za tu jedinicu (bez prelaska na srpski oblik u drugom jeziku).
  unit(unit: string, qty?: number | null): string {
    const base = 'unit.' + unit;
    if (qty != null && Number.isInteger(qty) && TRANSLATIONS[this.lang]?.[base + '.one']) return this.plural(qty, base);
    return this.t(base) || unit;
  }

  // Broj u formatu jezika: "1,5" (sr, ru), "1.5" (en); "1.200" za hiljade u srpskom
  num(n: number, maxDecimals = 2): string {
    const locale = this.lang === 'en' ? 'en-US' : this.lang === 'ru' ? 'ru-RU' : 'sr-Latn';
    return n.toLocaleString(locale, { maximumFractionDigits: maxDecimals });
  }

  // Množina po jeziku: ključevi "<base>.one", "<base>.few", "<base>.many" (engleski: one/many)
  plural(n: number, base: string): string {
    const lastTwo = Math.abs(n) % 100, last = Math.abs(n) % 10;
    const form = last === 1 && lastTwo !== 11 ? 'one'
      : last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? 'few' : 'many';
    const key = this.lang === 'en' ? (n === 1 ? 'one' : 'many') : form;
    return this.t(`${base}.${key}`);
  }

  setLang(lang: Lang) {
    this.langSource.next(lang);
    localStorage.setItem('lang', lang);
    document.documentElement.lang = lang;
  }

  private loadLang(): Lang {
    try {
      const saved = localStorage.getItem('lang') as Lang;
      if (saved && TRANSLATIONS[saved]) return saved;
    } catch {}
    return 'sr';
  }
}
