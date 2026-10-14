import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  templateUrl: './app-footer.component.html',
  styleUrls: ['./app-footer.component.scss'],
  standalone: false,
})
export class AppFooterComponent {
  year = new Date().getFullYear();
}
