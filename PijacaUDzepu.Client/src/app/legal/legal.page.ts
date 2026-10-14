import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateService } from '../services/translate.service';
import { LEGAL_CONTENT, LEGAL_DOCS, LEGAL_INFO, LegalDoc, LegalDocument } from './legal-content';

@Component({
  selector: 'app-legal',
  templateUrl: './legal.page.html',
  styleUrls: ['./legal.page.scss'],
  standalone: false,
})
export class LegalPage implements OnInit {
  doc: LegalDoc = 'terms';
  updated = LEGAL_INFO.updated;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const doc = params.get('doc') as LegalDoc;
      if (LEGAL_DOCS.includes(doc)) {
        this.doc = doc;
      } else {
        this.router.navigate(['/legal', 'terms'], { replaceUrl: true });
      }
    });
  }

  get content(): LegalDocument {
    return LEGAL_CONTENT[this.ts.lang][this.doc];
  }
}
