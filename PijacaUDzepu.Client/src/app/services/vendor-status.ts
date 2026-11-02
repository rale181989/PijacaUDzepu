import { TranslateService } from './translate.service';

// Tekst za zatvorenu tezgu, npr. "Zatvoreno · otvara se sutra u 07:00" ili "Trenutno ne prima porudžbine".
// Server šalje razlog (Paused/ClosedDay/Hours/Inactive) i lokalno vreme otvaranja ("2026-11-03T07:00:00").
export function vendorClosedText(ts: TranslateService, reason?: string | null, opensAt?: string | null): string {
  if (reason === 'Paused') return ts.t('open.paused');
  if (!opensAt) return ts.t('open.closed');

  const at = new Date(opensAt);
  const today = new Date();
  const tomorrow = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1);
  const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();
  const day = sameDay(at, today) ? ts.t('open.today')
    : sameDay(at, tomorrow) ? ts.t('open.tomorrow')
    : ts.t('day.' + (at.getDay() === 0 ? 7 : at.getDay()));
  const hasTime = at.getHours() !== 0 || at.getMinutes() !== 0;
  const time = `${String(at.getHours()).padStart(2, '0')}:${String(at.getMinutes()).padStart(2, '0')}`;

  return `${ts.t('open.closed')} · ${ts.t('open.opens')} ${day}${hasTime ? ` ${ts.t('open.at')} ${time}` : ''}`;
}
