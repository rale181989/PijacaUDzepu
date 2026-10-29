import { Injectable } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { Haptics, NotificationType } from '@capacitor/haptics';

export type SoundKind = 'newOrder' | 'update';

// Zvuk se generiše kroz Web Audio, bez audio fajlova.
// Browseri (i Android WebView) dozvoljavaju zvuk tek posle prve interakcije korisnika,
// pa se AudioContext "otključava" na prvi dodir/klik/taster.
@Injectable({ providedIn: 'root' })
export class SoundService {
  private ctx: AudioContext | null = null;

  constructor() {
    const unlock = () => {
      this.ensureContext();
      if (this.ctx?.state === 'running') {
        ['pointerdown', 'touchstart', 'keydown'].forEach(e => document.removeEventListener(e, unlock, true));
      }
    };
    ['pointerdown', 'touchstart', 'keydown'].forEach(e => document.addEventListener(e, unlock, true));
  }

  play(kind: SoundKind) {
    this.vibrate(kind);
    const ctx = this.ensureContext();
    if (!ctx) return;

    // Nova porudžbina: tri rastuća tona, dva puta (da se čuje i kad telefon nije u ruci)
    const notes = kind === 'newOrder'
      ? [659, 880, 1175, 0, 659, 880, 1175]
      : [880, 1175];
    const step = kind === 'newOrder' ? 0.16 : 0.12;

    notes.forEach((freq, i) => {
      if (freq) this.tone(ctx, freq, ctx.currentTime + i * step, step * 1.6);
    });
  }

  private tone(ctx: AudioContext, freq: number, start: number, duration: number) {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(freq, start);
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(0.5, start + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(start);
    osc.stop(start + duration + 0.05);
  }

  private ensureContext(): AudioContext | null {
    try {
      if (!this.ctx) {
        const Ctor = window.AudioContext || (window as any).webkitAudioContext;
        if (!Ctor) return null;
        this.ctx = new Ctor();
      }
      if (this.ctx.state === 'suspended') this.ctx.resume().catch(() => {});
      return this.ctx;
    } catch {
      return null;
    }
  }

  private vibrate(kind: SoundKind) {
    if (!Capacitor.isNativePlatform()) return;
    Haptics.notification({ type: kind === 'newOrder' ? NotificationType.Warning : NotificationType.Success }).catch(() => {});
  }
}
