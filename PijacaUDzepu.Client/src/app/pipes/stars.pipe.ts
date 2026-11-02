import { Pipe, PipeTransform } from '@angular/core';

// Ocena 1–5 kao zvezdice: 4 -> "★★★★☆"
@Pipe({ name: 'stars', standalone: false })
export class StarsPipe implements PipeTransform {
  transform(score?: number | null): string {
    const n = Math.max(0, Math.min(5, Math.round(score ?? 0)));
    return '★'.repeat(n) + '☆'.repeat(5 - n);
  }
}
