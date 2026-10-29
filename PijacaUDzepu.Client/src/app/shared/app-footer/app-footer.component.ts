import { Component } from '@angular/core';
import { LEGAL_INFO } from '../../legal/legal-content';

@Component({
  selector: 'app-footer',
  templateUrl: './app-footer.component.html',
  styleUrls: ['./app-footer.component.scss'],
  standalone: false,
})
export class AppFooterComponent {
  year = new Date().getFullYear();
  supportEmail = LEGAL_INFO.supportEmail;
}
