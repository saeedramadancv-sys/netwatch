import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Incident, IncidentStatus } from '../../core/models/monitoring.models';
import { AuthService } from '../../core/services/auth.service';
import { NetWatchApiService } from '../../core/services/netwatch-api.service';

@Component({
  selector: 'nw-incident-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <h1>{{ 'incidents.title' | translate }}</h1>

    <div class="filters card">
      <select [ngModel]="status()" (ngModelChange)="onStatusChange($event)">
        <option value="">{{ 'incidents.all' | translate }}</option>
        <option value="Open">{{ 'incidents.open' | translate }}</option>
        <option value="Acknowledged">{{ 'incidents.acknowledged' | translate }}</option>
        <option value="Resolved">{{ 'incidents.resolved' | translate }}</option>
      </select>
    </div>

    @if (loading()) {
      <p class="empty-state">{{ 'common.loading' | translate }}</p>
    } @else if (incidents().length === 0) {
      <p class="empty-state">{{ 'incidents.empty' | translate }}</p>
    } @else {
      <div class="card table-wrap">
        <table>
          <thead>
            <tr>
              <th>{{ 'incidents.severity' | translate }}</th>
              <th>{{ 'incidents.device' | translate }}</th>
              <th>{{ 'incidents.cause' | translate }}</th>
              <th>{{ 'incidents.started' | translate }}</th>
              <th>{{ 'incidents.duration' | translate }}</th>
              <th>{{ 'incidents.failedChecks' | translate }}</th>
              <th>{{ 'incidents.status' | translate }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (incident of incidents(); track incident.id) {
              <tr [class.resolved]="incident.status === 'Resolved'">
                <td>
                  <span class="chip" [class.critical]="incident.severity === 'Critical'">
                    {{ (incident.severity === 'Critical' ? 'incidents.critical' : 'incidents.warning') | translate }}
                  </span>
                </td>
                <td>
                  <a [routerLink]="['/probes', incident.probeId]">{{ incident.deviceName }}</a>
                  <br />
                  <span class="mono muted">{{ incident.target }}</span>
                </td>
                <td class="cause">{{ incident.cause }}</td>
                <td class="muted">{{ i18n.formatDateTime(incident.startedAtUtc) }}</td>
                <td>{{ i18n.formatDuration(incident.durationSeconds) }}</td>
                <td>{{ incident.failedChecks }}</td>
                <td>{{ statusLabel(incident) }}</td>
                <td class="row-actions">
                  <!-- Acknowledging is only meaningful while an incident is still
                       open; the API rejects it once resolved. -->
                  @if (auth.canEdit() && incident.status === 'Open') {
                    <button class="btn btn-sm" type="button" (click)="acknowledge(incident)">
                      {{ 'incidents.acknowledge' | translate }}
                    </button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    .filters {
      margin-bottom: 14px;
    }

    .filters select {
      max-width: 220px;
    }

    .table-wrap {
      padding: 0;
      overflow-x: auto;
    }

    .chip {
      padding: 2px 9px;
      border-radius: 999px;
      background: var(--degraded-bg);
      color: var(--degraded);
      font-size: 0.78rem;
      font-weight: 600;
      white-space: nowrap;
    }

    .chip.critical {
      background: var(--down-bg);
      color: var(--down);
    }

    /* Resolved rows are dimmed rather than hidden: history is the point of the
       page, but what is broken right now should stand out. */
    tr.resolved {
      opacity: 0.6;
    }

    .cause {
      max-width: 320px;
      color: var(--text-muted);
      font-size: 0.88rem;
    }

    .row-actions {
      text-align: end;
    }
  `,
})
export class IncidentListComponent implements OnInit {
  private readonly api = inject(NetWatchApiService);

  readonly auth = inject(AuthService);
  readonly i18n = inject(I18nService);

  readonly incidents = signal<Incident[]>([]);
  readonly loading = signal(true);
  readonly status = signal<IncidentStatus | ''>('');

  ngOnInit(): void {
    this.load();
  }

  onStatusChange(value: IncidentStatus | ''): void {
    this.status.set(value);
    this.load();
  }

  statusLabel(incident: Incident): string {
    switch (incident.status) {
      case 'Open':
        return this.i18n.translate('incidents.open');
      case 'Acknowledged':
        return this.i18n.translate('incidents.acknowledged');
      default:
        return this.i18n.translate('incidents.resolved');
    }
  }

  acknowledge(incident: Incident): void {
    this.api.acknowledgeIncident(incident.id).subscribe((updated) =>
      // Patches the single row instead of refetching the list, so the table does
      // not flicker and any scroll position is kept.
      this.incidents.update((current) => current.map((item) => (item.id === updated.id ? updated : item))),
    );
  }

  private load(): void {
    this.loading.set(true);

    this.api.listIncidents({ status: this.status() || undefined }).subscribe({
      next: (incidents) => {
        this.incidents.set(incidents);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
