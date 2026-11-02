import { Component, EventEmitter, Input, Output } from '@angular/core';

// Paginacija ispod liste: "‹ Prethodna · Strana 2 od 5 · Sledeća ›" i izbor veličine strane.
// Roditelj čuva stranu i veličinu i učitava podatke na (pageChange) / (pageSizeChange).
// Sa storageKey se izabrana veličina pamti na uređaju (PagerComponent.savedSize je čita).
@Component({
  selector: 'app-pager',
  templateUrl: './pager.component.html',
  styleUrls: ['./pager.component.scss'],
  standalone: false,
})
export class PagerComponent {
  @Input() page = 0;
  @Input() pageSize = 10;
  @Input() totalCount = 0;
  @Input() pageSizes = [10, 20];
  @Input() disabled = false;
  @Input() storageKey?: string;
  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  // Lista koja staje na najmanju stranu nema potrebe za paginacijom
  get visible(): boolean {
    return this.totalCount > Math.min(...this.pageSizes);
  }

  go(page: number) {
    if (this.disabled || page < 0 || page >= this.pageCount || page === this.page) return;
    this.pageChange.emit(page);
  }

  setSize(size: number) {
    if (!size || size === this.pageSize) return;
    if (this.storageKey) {
      try { localStorage.setItem(this.storageKey, String(size)); } catch {}
    }
    this.pageSizeChange.emit(size);
  }

  static savedSize(key: string, sizes = [10, 20]): number {
    try {
      const v = Number(localStorage.getItem(key));
      return sizes.includes(v) ? v : sizes[0];
    } catch {
      return sizes[0];
    }
  }

  // Strana na kojoj ostaje prva prikazana stavka kada se promeni veličina strane
  static pageAfterResize(page: number, oldSize: number, newSize: number): number {
    return Math.floor(page * oldSize / newSize);
  }
}

// Isečak liste za jednu stranu, kada su svi podaci već učitani (npr. izveštaji)
export function pageOf<T>(list: T[], page: number, size: number): T[] {
  return list.slice(page * size, page * size + size);
}
