import { AfterViewInit, Component, ElementRef, EventEmitter, Input, NgZone, OnChanges, OnDestroy, Output, ViewChild } from '@angular/core';
import type * as Leaflet from 'leaflet';
import { GeoPoint } from '../maps';

// Mapa grada i pijace sa tačkom tezge (Leaflet + OpenStreetMap, besplatno). Leaflet se učitava tek kada se mapa
// prikaže, pa ne opterećuje ostale stranice. editable: dodir na mapu pomera tačku (prodavac u profilu).
const NOVI_SAD: GeoPoint = { lat: 45.2551, lng: 19.8452 };

@Component({
  selector: 'app-stall-map',
  template: `<div #el class="stall-map" [style.height.px]="height"></div>`,
  styles: [`
    :host { display: block; }
    .stall-map { width: 100%; border-radius: 10px; overflow: hidden; background: #e8efe4; z-index: 0; }
    :host ::ng-deep .stall-label { font-weight: 600; font-size: 12px; }
  `],
  standalone: false,
})
export class StallMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() market: GeoPoint | null = null;
  @Input() stall: GeoPoint | null = null;
  @Input() stallLabel = 'Tezga';
  @Input() marketLabel = '';
  @Input() editable = false;
  @Input() height = 220;
  @Output() pointChange = new EventEmitter<GeoPoint>();

  @ViewChild('el', { static: true }) el!: ElementRef<HTMLDivElement>;

  private L?: typeof Leaflet;
  private map?: Leaflet.Map;
  private marketLayer?: Leaflet.Layer;
  private stallLayer?: Leaflet.CircleMarker;
  private resize?: ResizeObserver;
  private centeredOn = '';
  private drawnKey = '';

  constructor(private zone: NgZone) {}

  async ngAfterViewInit() {
    const mod: any = await import('leaflet');
    const L: typeof Leaflet = mod.default ?? mod;
    this.L = L;
    // Leaflet radi van Angular zone, da pomeranje mape ne pokreće proveru izmena na celoj stranici
    this.zone.runOutsideAngular(() => {
      this.map = L.map(this.el.nativeElement, { zoomControl: true, attributionControl: true });
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap'
      }).addTo(this.map);
      if (this.editable)
        this.map.on('click', (e: Leaflet.LeafletMouseEvent) =>
          this.zone.run(() => this.pointChange.emit({ lat: e.latlng.lat, lng: e.latlng.lng })));
      // Ionic prikazuje stranicu sa animacijom: mapa mora ponovo da izmeri svoju veličinu
      this.resize = new ResizeObserver(() => this.refit());
      this.resize.observe(this.el.nativeElement);
    });
    this.draw();
  }

  // Posle promene veličine (animacija stranice, kartica): ponovo izmeri, centriraj i postavi natpis iznad tačke
  private refit() {
    const map = this.map;
    if (!map) return;
    map.invalidateSize({ animate: false });
    const p = this.stall ?? this.market;
    if (p) map.setView([p.lat, p.lng], map.getZoom(), { animate: false });
    const tip = this.stallLayer?.getTooltip();
    if (tip && this.stallLayer) { this.stallLayer.closeTooltip(); this.stallLayer.openTooltip(); }
  }

  ngOnChanges() {
    this.draw();
  }

  ngOnDestroy() {
    this.resize?.disconnect();
    this.map?.remove();
  }

  private draw() {
    const L = this.L, map = this.map;
    if (!L || !map) return;
    // Roditelj može da pošalje nov objekat sa istim koordinatama: crta se samo kada se vrednosti promene
    const key = JSON.stringify([this.market, this.stall, this.stallLabel, this.marketLabel]);
    if (key === this.drawnKey) return;
    this.drawnKey = key;

    this.marketLayer?.remove();
    this.marketLayer = undefined;
    if (this.market) {
      this.marketLayer = L.circle([this.market.lat, this.market.lng], {
        radius: 70, color: '#6b7a65', weight: 1, dashArray: '4 4', fillOpacity: 0.08
      }).addTo(map);
      if (this.marketLabel) this.marketLayer.bindTooltip(this.marketLabel, { direction: 'top' });
    }

    this.stallLayer?.remove();
    this.stallLayer = undefined;
    if (this.stall) {
      this.stallLayer = L.circleMarker([this.stall.lat, this.stall.lng], {
        radius: 11, color: '#ffffff', weight: 3, fillColor: '#2e7d32', fillOpacity: 1
      }).addTo(map);
      this.stallLayer.bindTooltip(this.stallLabel, { permanent: true, direction: 'top', offset: [0, -10], className: 'stall-label' });
    }

    // Pogled se postavlja kada se promeni ono na šta se gleda (ne posle svakog dodira prodavca na mapi)
    const focus = this.stall ? 's' : this.market ? `m${this.market.lat},${this.market.lng}` : 'c';
    if (focus === this.centeredOn) return;
    this.centeredOn = focus;
    if (this.stall) map.setView([this.stall.lat, this.stall.lng], 18);
    else if (this.market) map.setView([this.market.lat, this.market.lng], 17);
    else map.setView([NOVI_SAD.lat, NOVI_SAD.lng], 13);
  }
}
