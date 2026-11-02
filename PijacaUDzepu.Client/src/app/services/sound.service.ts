import { Injectable } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { Haptics, NotificationType } from '@capacitor/haptics';

// alarm = zvono za nove porudžbine koje se ponavlja (NotificationService) dok se ne prihvate/odbiju
export type SoundKind = 'newOrder' | 'alarm' | 'update';

interface Melody {
  notes: number[];
  step: number;
}

// Nova porudžbina: tri rastuća tona, dva puta (da se čuje i kad telefon nije u ruci)
const URGENT: Melody = { notes: [659, 880, 1175, 0, 659, 880, 1175], step: 0.16 };
const UPDATE: Melody = { notes: [880, 1175], step: 0.12 };

// Zvuk se generiše u kodu, bez audio fajlova, i pušta kao WAV kroz <audio> element:
// Capacitor WebView ga pušta i bez dodira korisnika (setMediaPlaybackRequiresUserGesture(false)),
// pa zvono zazvoni čim stigne porudžbina. Web Audio (AudioContext) je rezerva - njega i WebView
// i browseri drže utišanim do prvog dodira/klika/tastera, pa se tada "otključava".
@Injectable({ providedIn: 'root' })
export class SoundService {
  private ctx: AudioContext | null = null;
  private wavUrls = new Map<Melody, string>();

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
    const melody = kind === 'update' ? UPDATE : URGENT;

    try {
      const audio = new Audio(this.wavUrl(melody));
      audio.play().catch(() => this.playWebAudio(melody));
    } catch {
      this.playWebAudio(melody);
    }
  }

  private playWebAudio(melody: Melody) {
    const ctx = this.ensureContext();
    if (!ctx || ctx.state !== 'running') return;
    melody.notes.forEach((freq, i) => {
      if (freq) this.tone(ctx, freq, ctx.currentTime + i * melody.step, melody.step * 1.6);
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

  // Ista melodija kao tone(), izračunata u 16-bitni mono WAV (pravi se jednom i čuva kao blob URL)
  private wavUrl(melody: Melody): string {
    const cached = this.wavUrls.get(melody);
    if (cached) return cached;

    const rate = 22050;
    const noteLength = melody.step * 1.6;
    const total = Math.ceil((melody.step * (melody.notes.length - 1) + noteLength + 0.05) * rate);
    const samples = new Float32Array(total);

    melody.notes.forEach((freq, i) => {
      if (!freq) return;
      const start = Math.floor(i * melody.step * rate);
      const length = Math.floor(noteLength * rate);
      for (let n = 0; n < length && start + n < total; n++) {
        const t = n / rate;
        // Brz uspon do 0.5 za 20 ms, pa eksponencijalno utišavanje do kraja tona
        const gain = t < 0.02 ? 0.5 * (t / 0.02) : 0.5 * Math.pow(0.0002, (t - 0.02) / (noteLength - 0.02));
        const x = t * freq;
        const triangle = 4 * Math.abs(x - Math.floor(x + 0.5)) - 1;
        samples[start + n] += triangle * gain;
      }
    });

    const buffer = new ArrayBuffer(44 + total * 2);
    const view = new DataView(buffer);
    const text = (offset: number, s: string) => [...s].forEach((c, i) => view.setUint8(offset + i, c.charCodeAt(0)));
    text(0, 'RIFF');
    view.setUint32(4, 36 + total * 2, true);
    text(8, 'WAVE');
    text(12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, 1, true);
    view.setUint32(24, rate, true);
    view.setUint32(28, rate * 2, true);
    view.setUint16(32, 2, true);
    view.setUint16(34, 16, true);
    text(36, 'data');
    view.setUint32(40, total * 2, true);
    samples.forEach((v, i) => view.setInt16(44 + i * 2, Math.max(-1, Math.min(1, v)) * 0x7fff, true));

    const url = URL.createObjectURL(new Blob([buffer], { type: 'audio/wav' }));
    this.wavUrls.set(melody, url);
    return url;
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
    // Zvono vibrira duže; Haptics u browseru koristi navigator.vibrate (Android Chrome)
    if (kind === 'alarm') {
      Haptics.vibrate({ duration: 1000 }).catch(() => {});
      return;
    }
    if (!Capacitor.isNativePlatform()) return;
    Haptics.notification({ type: kind === 'newOrder' ? NotificationType.Warning : NotificationType.Success }).catch(() => {});
  }
}
