import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DeviceDetail, Probe, ProbeType } from '../../core/models/monitoring.models';
import { AuthService } from '../../core/services/auth.service';
import { NetWatchApiService } from '../../core/services/netwatch-api.service';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'nw-device-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, StatusBadgeComponent],
  template: `
    <a class="back" routerLink="/devices">← {{ 'common.back' | translate }}</a>

    @if (device(); as target) {
      <header class="page-head">
        <div>
          <h1>
            {{ target.name }}
            <nw-status-badge [state]="target.status" />
          </h1>
          <p class="muted">
            <span class="mono">{{ target.hostname }}</span>
            · {{ target.category }}
            @if (target.site) {
              · {{ target.site }}
            }
          </p>
        </div>

        @if (auth.canEdit()) {
          <div class="head-actions">
            <a class="btn btn-sm" [routerLink]="['/devices', target.id, 'edit']">{{ 'common.edit' | translate }}</a>
            <button class="btn btn-sm" type="button" (click)="toggleEnabled(target)">
              {{ (target.isEnabled ? 'devices.disabled' : 'devices.enabled') | translate }}
            </button>
            @if (auth.isAdmin()) {
              <button class="btn btn-sm btn-danger" type="button" (click)="remove(target)">
                {{ 'common.delete' | translate }}
              </button>
            }
          </div>
        }
      </header>

      <h2>{{ 'probes.title' | translate }}</h2>

      @if (target.probes.length === 0) {
        <p class="empty-state">{{ 'probes.none' | translate }}</p>
      } @else {
        <div class="card table-wrap">
          <table>
            <thead>
              <tr>
                <th>{{ 'devices.status' | translate }}</th>
                <th>{{ 'probes.type' | translate }}</th>
                <th>{{ 'probes.target' | translate }}</th>
                <th>{{ 'probes.interval' | translate }}</th>
                <th>{{ 'probes.lastCheck' | translate }}</th>
                <th>{{ 'probes.latency' | translate }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (probe of target.probes; track probe.id) {
                <tr>
                  <td><nw-status-badge [state]="probe.state" [showLabel]="false" /></td>
                  <td>{{ probe.type }}</td>
                  <td>
                    <a [routerLink]="['/probes', probe.id]" class="mono">{{ probe.target }}</a>
                  </td>
                  <td class="muted">{{ probe.intervalSeconds }}s</td>
                  <td class="muted">{{ i18n.formatRelative(probe.lastCheckedAtUtc) }}</td>
                  <td class="muted">{{ latency(probe) }}</td>
                  <td class="row-actions">
                    @if (auth.canEdit()) {
                      <button class="btn btn-sm btn-danger" type="button" (click)="removeProbe(probe)">
                        {{ 'common.delete' | translate }}
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (auth.canEdit()) {
        <h2>{{ 'probes.add' | translate }}</h2>

        <form class="card probe-form" [formGroup]="probeForm" (ngSubmit)="addProbe(target)">
          <div class="row">
            <div class="field">
              <label for="type">{{ 'probes.type' | translate }}</label>
              <select id="type" formControlName="type">
                <option value="Icmp">ICMP</option>
                <option value="Tcp">TCP</option>
                <option value="Http">HTTP</option>
              </select>
            </div>

            <div class="field">
              <label for="interval">{{ 'probes.interval' | translate }} (s)</label>
              <input id="interval" type="number" min="5" formControlName="intervalSeconds" />
            </div>

            <div class="field">
              <label for="timeout">{{ 'probes.timeout' | translate }} (ms)</label>
              <input id="timeout" type="number" min="100" formControlName="timeoutMs" />
            </div>
          </div>

          <!-- Type-specific fields appear only where they apply: a port on an ICMP
               probe is rejected by the domain, so offering the input would be a
               guaranteed validation error. -->
          @if (probeForm.controls.type.value === 'Tcp') {
            <div class="field">
              <label for="port">{{ 'probes.port' | translate }}</label>
              <input id="port" type="number" min="1" max="65535" formControlName="port" />
            </div>
          }

          @if (probeForm.controls.type.value === 'Http') {
            <div class="row">
              <div class="field">
                <label for="path">{{ 'probes.path' | translate }}</label>
                <input id="path" formControlName="httpPath" dir="ltr" placeholder="/health" />
              </div>
              <div class="field">
                <label for="status">{{ 'probes.expectedStatus' | translate }}</label>
                <input id="status" type="number" min="100" max="599" formControlName="expectedStatusCode" />
              </div>
              <div class="field checkbox">
                <label for="https">{{ 'probes.https' | translate }}</label>
                <input id="https" type="checkbox" formControlName="useHttps" />
              </div>
            </div>
          }

          <div class="row">
            <div class="field">
              <label for="failures">{{ 'probes.failureThreshold' | translate }}</label>
              <input id="failures" type="number" min="1" max="10" formControlName="failureThreshold" />
            </div>
            <div class="field">
              <label for="recovery">{{ 'probes.recoveryThreshold' | translate }}</label>
              <input id="recovery" type="number" min="1" max="10" formControlName="recoveryThreshold" />
            </div>
            <div class="field">
              <label for="degraded">{{ 'probes.degradedLatency' | translate }}</label>
              <input id="degraded" type="number" min="1" formControlName="degradedLatencyMs" />
            </div>
          </div>

          @if (probeError()) {
            <p class="field-error" role="alert">{{ probeError() }}</p>
          }

          <button class="btn btn-primary" type="submit">{{ 'probes.add' | translate }}</button>
        </form>
      }
    } @else {
      <p class="empty-state">{{ 'common.loading' | translate }}</p>
    }
  `,
  styles: `
    .back {
      display: inline-block;
      margin-bottom: 12px;
      color: var(--text-muted);
    }

    .page-head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 22px;
    }

    .page-head h1 {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0 0 4px;
    }

    .page-head p {
      margin: 0;
      font-size: 0.9rem;
    }

    .head-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .table-wrap {
      padding: 0;
      overflow-x: auto;
    }

    .row-actions {
      text-align: end;
    }

    .probe-form .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 12px;
    }

    .field.checkbox input {
      width: auto;
    }
  `,
})
export class DeviceDetailComponent implements OnInit {
  private readonly api = inject(NetWatchApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly auth = inject(AuthService);
  readonly i18n = inject(I18nService);

  readonly device = signal<DeviceDetail | null>(null);
  readonly probeError = signal<string | null>(null);

  readonly probeForm = this.fb.nonNullable.group({
    type: ['Icmp' as ProbeType, Validators.required],
    intervalSeconds: [60, [Validators.required, Validators.min(5)]],
    timeoutMs: [5000, [Validators.required, Validators.min(100)]],
    port: [null as number | null],
    httpPath: ['/'],
    useHttps: [true],
    expectedStatusCode: [200],
    failureThreshold: [3, [Validators.min(1), Validators.max(10)]],
    recoveryThreshold: [2, [Validators.min(1), Validators.max(10)]],
    degradedLatencyMs: [null as number | null],
  });

  ngOnInit(): void {
    this.load();
  }

  latency(probe: Probe): string {
    return probe.lastResponseTimeMs === null ? '—' : `${Math.round(probe.lastResponseTimeMs)} ms`;
  }

  toggleEnabled(device: DeviceDetail): void {
    this.api.setDeviceEnabled(device.id, !device.isEnabled).subscribe(() => this.load());
  }

  remove(device: DeviceDetail): void {
    if (!confirm(this.i18n.translate('devices.confirmDelete'))) {
      return;
    }

    this.api.deleteDevice(device.id).subscribe(() => void this.router.navigate(['/devices']));
  }

  removeProbe(probe: Probe): void {
    if (!confirm(this.i18n.translate('probes.confirmDelete'))) {
      return;
    }

    this.api.deleteProbe(probe.id).subscribe(() => this.load());
  }

  addProbe(device: DeviceDetail): void {
    this.probeError.set(null);

    const value = this.probeForm.getRawValue();

    // Irrelevant fields are nulled rather than sent with defaults: the domain
    // rejects a port on an ICMP probe, and sending "/" as a path on a TCP probe
    // would store a value that means nothing.
    const request = {
      type: value.type,
      intervalSeconds: value.intervalSeconds,
      timeoutMs: value.timeoutMs,
      port: value.type === 'Tcp' ? value.port : null,
      httpPath: value.type === 'Http' ? value.httpPath || '/' : null,
      useHttps: value.useHttps,
      expectedStatusCode: value.expectedStatusCode,
      failureThreshold: value.failureThreshold,
      recoveryThreshold: value.recoveryThreshold,
      degradedLatencyMs: value.degradedLatencyMs,
    };

    this.api.createProbe(device.id, request).subscribe({
      next: () => {
        this.probeForm.reset({
          type: 'Icmp',
          intervalSeconds: 60,
          timeoutMs: 5000,
          port: null,
          httpPath: '/',
          useHttps: true,
          expectedStatusCode: 200,
          failureThreshold: 3,
          recoveryThreshold: 2,
          degradedLatencyMs: null,
        });
        this.load();
      },
      error: (error: { error?: { detail?: string; errors?: Record<string, string[]> } }) => {
        const body = error.error;
        const messages = body?.errors ? Object.values(body.errors).flat() : [];
        this.probeError.set(messages.length ? messages.join(' ') : (body?.detail ?? 'Could not add the probe.'));
      },
    });
  }

  private load(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.getDevice(id).subscribe((device) => this.device.set(device));
  }
}
