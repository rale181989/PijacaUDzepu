import { Pipe, PipeTransform } from '@angular/core';
import { TranslateService } from '../services/translate.service';

@Pipe({ name: 't', pure: false, standalone: false })
export class TranslatePipe implements PipeTransform {
  constructor(private ts: TranslateService) {}
  transform(key: string): string {
    return this.ts.t(key);
  }
}
