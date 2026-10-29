import { Component, OnDestroy, OnInit } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { Subscription } from 'rxjs';
import { SupportRequest, SupportService } from '../services/support.service';
import { NotificationService } from '../services/notification.service';

@Component({
  selector: 'app-admin-support',
  templateUrl: './admin-support.page.html',
  styleUrls: ['./admin-support.page.scss'],
  standalone: false,
})
export class AdminSupportPage implements OnInit, OnDestroy {
  tab: 'open' | 'resolved' = 'open';
  requests: SupportRequest[] = [];
  loading = false;
  private sub?: Subscription;
  private loadSub?: Subscription;

  categoryLabels: Record<string, string> = {
    'Problem': 'Problem sa aplikacijom', 'Question': 'Pitanje', 'Account': 'Nalog i prijava', 'Other': 'Ostalo'
  };

  constructor(
    private supportService: SupportService,
    public notificationService: NotificationService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.sub = this.notificationService.supportRequest$.subscribe(() => {
      if (this.tab === 'open') this.load();
    });
  }

  ionViewWillEnter() {
    this.load();
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
    this.loadSub?.unsubscribe();
  }

  setTab(tab: 'open' | 'resolved') {
    this.tab = tab;
    this.load();
  }

  load() {
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadSub = this.supportService.getAll(this.tab === 'resolved').subscribe({
      next: r => { this.requests = r; this.loading = false; },
      error: () => this.loading = false
    });
    this.notificationService.loadSupportOpenCount();
  }

  doRefresh(event: any) {
    this.load();
    setTimeout(() => event.target.complete(), 500);
  }

  setResolved(request: SupportRequest, value: boolean) {
    this.supportService.setResolved(request.id, value).subscribe({
      next: async () => {
        this.requests = this.requests.filter(r => r.id !== request.id);
        this.notificationService.loadSupportOpenCount();
        const toast = await this.toastController.create({
          message: value ? `Pitanje #${request.id} je rešeno` : `Pitanje #${request.id} je ponovo otvoreno`,
          duration: 2000, color: 'success'
        });
        await toast.present();
      }
    });
  }

  replyLink(request: SupportRequest): string {
    const subject = encodeURIComponent(`Re: Pijaca u Džepu – pitanje #${request.id}`);
    return `mailto:${request.email}?subject=${subject}`;
  }
}
