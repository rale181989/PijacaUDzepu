import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '../services/translate.service';

@Pipe({ name: 't', pure: false, standalone: false })
export class TranslatePipe implements PipeTransform {
  private ts = inject(TranslateService);

  transform(key: string): string {
    return this.ts.t(key);
  }
}
