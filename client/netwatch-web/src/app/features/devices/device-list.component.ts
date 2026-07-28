import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DEVICE_CATEGORIES, Device, DeviceCategory } from '../../core/models/monitoring.models';
import { AuthService } from '../../core/services/auth.service';
import { NetWatchApiService } from '../../core/services/netwatch-api.service';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'nw-device-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, StatusBadgeComponent],
  template: `
    <header class="page-head">
      <h1>{{ 'devices.title' | translate }}</h1>
      @if (auth.canEdit()) {
        <a class="btn btn-primary" routerLink="/devices/new">{{ 'devices.add' | translate }}</a>
      }
    </header>

    <div class="filters card">
      <input
        type="search"
        [placeholder]="'devices.search' | translate"
        [ngModel]="search()"
        (ngModelChange)="onSearchChange($event)"
      />
      <select [ngModel]="category()" (ngModelChange)="onCategoryChange($event)">
        <option value="">{{ 'devices.allCategories' | translate }}</option>
        @for (option of categories; track option) {
          <option [value]="option">{{ option }}</option>
        }
      </select>
    </div>

    @if (loading()) {
      <p class="empty-state">{{ 'common.loading' | translate }}</p>
    } @else if (devices().length === 0) {
      <p class="empty-state">{{ 'devices.empty' | translate }}</p>
    } @else {
      <div class="card table-wrap">
        <table>
          <thead>
            <tr>
              <th>{{ 'devices.status' | translate }}</th>
              <th>{{ 'devices.name' | translate }}</th>
              <th>{{ 'devices.host' | translate }}</th>
              <th>{{ 'devices.category' | translate }}</th>
              <th>{{ 'devices.site' | translate }}</th>
              <th>{{ 'devices.probes' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (device of devices(); track device.id) {
              <tr>
                <td><nw-status-badge [state]="device.status" [showLabel]="false" /></td>
                <td>
                  <a [routerLink]="['/devices', device.id]">{{ device.name }}</a>
                  @if (!device.isEnabled) {
                    <span class="paused">{{ 'devices.disabled' | translate }}</span>
                  }
                </td>
                <td><span class="mono">{{ device.hostname }}</span></td>
                <td>{{ device.category }}</td>
                <td class="muted">{{ device.site ?? '—' }}</td>
                <td>{{ device.probeCount }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    .page-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 14px;
    }

    .page-head h1 {
      margin: 0;
    }

    .filters {
      display: flex;
      gap: 10px;
      margin-bottom: 14px;
    }

    .filters select {
      max-width: 200px;
    }

    .table-wrap {
      padding: 0;
      overflow-x: auto;
    }

    .paused {
      margin-inline-start: 8px;
      padding: 1px 7px;
      border-radius: 999px;
      background: var(--unknown-bg);
      color: var(--unknown);
      font-size: 0.75rem;
    }
  `,
})
export class DeviceListComponent implements OnInit {
  private readonly api = inject(NetWatchApiService);

  readonly auth = inject(AuthService);
  readonly categories = DEVICE_CATEGORIES;

  readonly devices = signal<Device[]>([]);
  readonly loading = signal(true);
  readonly search = signal('');
  readonly category = signal<DeviceCategory | ''>('');

  private debounce?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.load();
  }

  /**
   * Debounced so typing a hostname issues one request rather than one per
   * keystroke.
   */
  onSearchChange(value: string): void {
    this.search.set(value);

    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.load(), 300);
  }

  onCategoryChange(value: DeviceCategory | ''): void {
    this.category.set(value);
    this.load();
  }

  private load(): void {
    this.loading.set(true);

    this.api
      .listDevices({
        search: this.search() || undefined,
        category: this.category() || undefined,
      })
      .subscribe({
        next: (devices) => {
          this.devices.set(devices);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
