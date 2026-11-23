import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

// Funkcije koje server može da isključi (env Features__<Ime>=false, GET /api/features).
// Učitava se jednom; dok ne stigne odgovor (ili ako ne stigne), funkcija se ne prikazuje.
export interface Features {
  stallLocation: boolean;
}

@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private current: Features = { stallLocation: false };
  private loading?: Promise<Features>;

  constructor(private http: HttpClient) {}

  get stallLocation(): boolean {
    return this.current.stallLocation;
  }

  load(): Promise<Features> {
    this.loading ??= firstValueFrom(this.http.get<Features>(`${environment.apiUrl}/features`))
      .then(f => this.current = { stallLocation: !!f?.stallLocation })
      .catch(() => {
        this.loading = undefined; // sledeći put pokušaj ponovo
        return this.current;
      });
    return this.loading;
  }
}
