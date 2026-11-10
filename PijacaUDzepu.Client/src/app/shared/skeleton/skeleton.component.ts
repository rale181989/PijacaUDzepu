import { Component, Input } from '@angular/core';

// Sive "kostur" kartice dok se spisak učitava, umesto spinera:
// 'grid' liči na karticu proizvoda na početnoj, 'list' na karticu porudžbine ili proizvoda u spisku
@Component({
  selector: 'app-skeleton',
  templateUrl: './skeleton.component.html',
  styleUrls: ['./skeleton.component.scss'],
  standalone: false,
})
export class SkeletonComponent {
  @Input() variant: 'grid' | 'list' = 'list';
  @Input() set count(value: number) {
    this.items = Array.from({ length: value });
  }
  items: unknown[] = Array.from({ length: 4 });
}
