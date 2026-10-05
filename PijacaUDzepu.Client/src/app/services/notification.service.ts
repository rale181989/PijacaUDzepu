import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private hubConnection: signalR.HubConnection | null = null;
  private hubUrl = environment.apiUrl.replace('/api', '/hubs/orders');
  private apiUrl = environment.apiUrl + '/orders';
  private connecting = false;

  private pendingCountSubject = new BehaviorSubject<number>(0);
  pendingCount$ = this.pendingCountSubject.asObservable();

  private audioContext: AudioContext | null = null;

  get pendingCount(): number {
    return this.pendingCountSubject.value;
  }

  constructor(
    private authService: AuthService,
    private http: HttpClient,
    private ngZone: NgZone
  ) {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.startConnection();
        if (this.authService.hasRole('VendorAdmin')) {
          this.loadPendingCount();
        }
      } else {
        this.stopConnection();
      }
    });
  }

  loadPendingCount() {
    this.http.get<{ count: number }>(`${this.apiUrl}/vendor-pending-count`).subscribe({
      next: res => this.pendingCountSubject.next(res.count),
      error: () => {}
    });
  }

  private async startConnection() {
    if (this.connecting || this.hubConnection?.state === signalR.HubConnectionState.Connected) return;

    const token = this.authService.token;
    if (!token) return;

    this.connecting = true;

    if (this.hubConnection) {
      await this.hubConnection.stop().catch(() => {});
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('NewOrder', (_data: any) => {
      this.ngZone.run(() => {
        this.pendingCountSubject.next(this.pendingCount + 1);
        this.playSound();
      });
    });

    this.hubConnection.on('OrderStatusChanged', (_data: any) => {
      this.ngZone.run(() => {
        this.playSound();
      });
    });

    try {
      await this.hubConnection.start();
    } catch {}
    finally {
      this.connecting = false;
    }
  }

  private stopConnection() {
    this.hubConnection?.stop().catch(() => {});
    this.hubConnection = null;
    this.pendingCountSubject.next(0);
  }

  private playSound() {
    try {
      if (!this.audioContext) {
        this.audioContext = new AudioContext();
      }
      const ctx = this.audioContext;
      const oscillator = ctx.createOscillator();
      const gain = ctx.createGain();
      oscillator.connect(gain);
      gain.connect(ctx.destination);
      oscillator.frequency.setValueAtTime(880, ctx.currentTime);
      oscillator.frequency.setValueAtTime(1100, ctx.currentTime + 0.1);
      gain.gain.setValueAtTime(0.3, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.3);
      oscillator.start(ctx.currentTime);
      oscillator.stop(ctx.currentTime + 0.3);
    } catch {}
  }
}
