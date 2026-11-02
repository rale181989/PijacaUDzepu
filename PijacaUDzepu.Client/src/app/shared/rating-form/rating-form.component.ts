import { Component, Input } from '@angular/core';
import { ModalController } from '@ionic/angular/lazy';

export interface RatingResult {
  vendorScore: number;
  courierScore?: number;
  comment?: string;
}

// Ocena završene porudžbine: zvezdice za prodavca i (kod dostave) kurira + kratak komentar.
// Otvara se preko ModalController-a; rezultat je u data posle dismiss-a (role 'confirm').
@Component({
  selector: 'app-rating-form',
  templateUrl: './rating-form.component.html',
  styleUrls: ['./rating-form.component.scss'],
  standalone: false,
})
export class RatingFormComponent {
  @Input() vendorName = '';
  @Input() withCourier = false;

  readonly scores = [1, 2, 3, 4, 5];
  vendorScore = 0;
  courierScore = 0;
  comment = '';

  constructor(private modalController: ModalController) {}

  get canSubmit(): boolean {
    return this.vendorScore > 0 && (!this.withCourier || this.courierScore > 0);
  }

  cancel() {
    this.modalController.dismiss(null, 'cancel');
  }

  submit() {
    if (!this.canSubmit) return;
    const result: RatingResult = {
      vendorScore: this.vendorScore,
      courierScore: this.withCourier ? this.courierScore : undefined,
      comment: this.comment.trim() || undefined
    };
    this.modalController.dismiss(result, 'confirm');
  }
}
