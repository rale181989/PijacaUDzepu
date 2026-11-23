import { AfterViewInit, Component, ElementRef, EventEmitter, Input, NgZone, OnChanges, OnDestroy, Output, ViewChild } from '@angular/core';
import type * as Leaflet from 'leaflet';
import { GeoPoint } from '../maps';
import { FeaturesService } from '../../services/features.service';

// Mapa grada i pijace sa tačkom tezge (Leaflet; podloga sa servera, podrazumevano OSM.de sa latiničnim nazivima).
// Leaflet se učitava tek kada se mapa prikaže, pa ne opterećuje ostale stranice.
// - Na telefonu jedan prst pomera STRANICU, a mapa se pomera i zumira sa dva prsta (inače palac "zapne" u mapi).
// - Dugme "Na tezgu" vraća pogled na tezgu (ili pijacu) posle pomeranja.
// - editable: dodir na mapu pomera tačku (prodavac u profilu); pogled tada ne skače na dodirnuto mesto.
const NOVI_SAD: GeoPoint = { lat: 45.2551, lng: 19.8452 };
const MARKET_RADIUS_M = 70;
const DEFAULT_ZOOM = 17;

@Component({
  selector: 'app-stall-map',
  template: `
    <div class="wrap" [style.height.px]="height">
      <div #el class="stall-map"></div>
      <button type="button" class="recenter" (click)="recenter()" [attr.aria-label]="recenterLabel">
        <ion-icon name="locate-outline" aria-hidden="true"></ion-icon><span>{{ recenterLabel }}</span>
      </button>
      <span *ngIf="touch" class="gesture-hint">{{ gestureHint }}</span>
    </div>`,
  styles: [`
    :host { display: block; }
    .wrap { position: relative; width: 100%; }
    .stall-map { position: absolute; inset: 0; border-radius: 10px; overflow: hidden; background: #e8efe4; z-index: 0; }
    .recenter {
      position: absolute; right: 8px; bottom: 24px; z-index: 500;
      display: inline-flex; align-items: center; gap: 4px; min-height: 40px; padding: 0 10px;
      border: 0; border-radius: 8px; background: #fff; color: #1b5e20; font: 600 13px/1 inherit;
      box-shadow: 0 1px 4px rgba(0, 0, 0, .3);
    }
    .recenter ion-icon { font-size: 18px; }
    .gesture-hint {
      position: absolute; left: 8px; bottom: 22px; z-index: 500; pointer-events: none;
      padding: 3px 7px; border-radius: 6px; background: rgba(255, 255, 255, .9); color: #333; font-size: 11px;
    }
    :host ::ng-deep .stall-label { font-weight: 600; font-size: 12px; }
    /* Chrome ostavlja tanku svetlu liniju između pločica pri razlomljenom pomeranju; ovako se pločice stapaju */
    :host ::ng-deep .leaflet-tile { mix-blend-mode: plus-lighter; }
    :host ::ng-deep .market-label { font-size: 11px; color: #4a5446; background: rgba(255, 255, 255, .85); border: 0; box-shadow: none; padding: 1px 5px; }
    :host ::ng-deep .market-label::before { display: none; }
  `],
  standalone: false,
})
export class StallMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() market: GeoPoint | null = null;
  @Input() stall: GeoPoint | null = null;
  @Input() stallLabel = 'Tezga';
  @Input() marketLabel = '';
  // Preciznost GPS-a u metrima: svetloplavi krug oko tačke (prodavac posle "Ovde sam")
  @Input() accuracy: number | null = null;
  @Input() editable = false;
  @Input() height = 220;
  @Input() recenterLabel = 'Na tezgu';
  @Input() gestureHint = 'Dva prsta: pomeranje i zum';
  @Output() pointChange = new EventEmitter<GeoPoint>();

  @ViewChild('el', { static: true }) el!: ElementRef<HTMLDivElement>;

  // Dodirni ekran (telefon): mapa se ne pomera jednim prstom
  readonly touch = typeof window !== 'undefined' && window.matchMedia?.('(pointer: coarse)').matches;

  private L?: typeof Leaflet;
  private map?: Leaflet.Map;
  private layers: Leaflet.Layer[] = [];
  private stallLayer?: Leaflet.CircleMarker;
  private resize?: ResizeObserver;
  private centeredOn = '';
  private drawnKey = '';
  private pickedKey = '';

  constructor(private zone: NgZone, private features: FeaturesService) {}

  async ngAfterViewInit() {
    const [mod, f]: [any, { mapTileUrl: string; mapAttribution: string }] = await Promise.all([import('leaflet'), this.features.load()]);
    const L: typeof Leaflet = mod.default ?? mod;
    this.L = L;
    // Leaflet radi van Angular zone, da pomeranje mape ne pokreće proveru izmena na celoj stranici
    this.zone.runOutsideAngular(() => {
      this.map = L.map(this.el.nativeElement, {
        zoomControl: true,
        attributionControl: true,
        dragging: !this.touch,
        touchZoom: true
      });
      L.tileLayer(f.mapTileUrl, { maxZoom: 19, attribution: f.mapAttribution }).addTo(this.map);
      if (this.editable)
        this.map.on('click', (e: Leaflet.LeafletMouseEvent) => this.zone.run(() => {
          const p = { lat: e.latlng.lat, lng: e.latlng.lng };
          this.pickedKey = this.pointKey(p);
          this.pointChange.emit(p);
        }));
      // Ionic prikazuje stranicu sa animacijom: mapa mora ponovo da izmeri svoju veličinu
      this.resize = new ResizeObserver(() => this.refit());
      this.resize.observe(this.el.nativeElement);
    });
    this.draw();
  }

  ngOnChanges() {
    this.draw();
  }

  ngOnDestroy() {
    this.resize?.disconnect();
    this.map?.remove();
  }

  // Pogled nazad na tezgu (ili pijacu), na podrazumevanom uvećanju
  recenter() {
    const p = this.stall ?? this.market ?? NOVI_SAD;
    this.map?.setView([p.lat, p.lng], this.stall || this.market ? DEFAULT_ZOOM : 13);
  }

  private pointKey(p: GeoPoint | null): string {
    return p ? `${p.lat.toFixed(6)},${p.lng.toFixed(6)}` : '';
  }

  // Posle promene veličine (animacija stranice, kartica): ponovo izmeri, centriraj i postavi natpis iznad tačke
  private refit() {
    const map = this.map;
    if (!map) return;
    map.invalidateSize({ animate: false });
    const p = this.stall ?? this.market;
    if (p) map.setView([p.lat, p.lng], map.getZoom(), { animate: false });
    if (this.stallLayer?.getTooltip()) { this.stallLayer.closeTooltip(); this.stallLayer.openTooltip(); }
  }

  private draw() {
    const L = this.L, map = this.map;
    if (!L || !map) return;
    // Roditelj može da pošalje nov objekat sa istim koordinatama: crta se samo kada se vrednosti promene
    const key = JSON.stringify([this.market, this.stall, this.stallLabel, this.marketLabel, this.accuracy]);
    if (key === this.drawnKey) return;
    this.drawnKey = key;

    this.layers.forEach(l => l.remove());
    this.layers = [];
    this.stallLayer = undefined;

    if (this.market) {
      this.layers.push(L.circle([this.market.lat, this.market.lng], {
        radius: MARKET_RADIUS_M, color: '#6b7a65', weight: 1, dashArray: '4 4', fillOpacity: 0.08, interactive: false
      }).addTo(map));
      // Natpis pijace stalno na vrhu kruga (tooltip na prelaz mišem se na telefonu ne vidi)
      if (this.marketLabel) {
        const top = this.market.lat + MARKET_RADIUS_M / 111_320;
        this.layers.push(L.tooltip({ permanent: true, direction: 'top', className: 'market-label', interactive: false })
          .setLatLng([top, this.market.lng]).setContent(this.marketLabel).addTo(map));
      }
    }

    let accuracyCircle: Leaflet.Circle | undefined;
    if (this.stall && this.accuracy && this.accuracy > 0) {
      accuracyCircle = L.circle([this.stall.lat, this.stall.lng], {
        radius: this.accuracy, color: '#1e88e5', weight: 1, fillColor: '#64b5f6', fillOpacity: 0.15, interactive: false
      }).addTo(map);
      this.layers.push(accuracyCircle);
    }

    if (this.stall) {
      this.stallLayer = L.circleMarker([this.stall.lat, this.stall.lng], {
        radius: 11, color: '#ffffff', weight: 3, fillColor: '#2e7d32', fillOpacity: 1
      }).addTo(map);
      this.stallLayer.bindTooltip(this.stallLabel, { permanent: true, direction: 'top', offset: [0, -10], className: 'stall-label' });
      this.layers.push(this.stallLayer);
    }

    // Pogled ide na tačku kada je postavljena spolja (GPS, server); dodir prodavca na mapu ne pomera pogled
    const target = this.stall ?? this.market;
    const focus = this.stall ? `s${this.pointKey(this.stall)}` : this.market ? `m${this.pointKey(this.market)}` : 'c';
    if (focus === this.centeredOn) return;
    const first = !this.centeredOn;
    this.centeredOn = focus;
    if (this.stall && this.pointKey(this.stall) === this.pickedKey) return;
    // Neprecizan GPS: ceo krug preciznosti u pogledu, da prodavac vidi gde može da bude
    if (accuracyCircle && this.accuracy! > 60) map.fitBounds(accuracyCircle.getBounds(), { padding: [12, 12], maxZoom: DEFAULT_ZOOM });
    else if (target) map.setView([target.lat, target.lng], first ? DEFAULT_ZOOM : map.getZoom());
    else map.setView([NOVI_SAD.lat, NOVI_SAD.lng], 13);
  }
}
