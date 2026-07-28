import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DashboardSummary, Incident, Probe, ProbeCheckedEvent } from '../../core/models/monitoring.models';
import { MonitoringHubService } from '../../core/services/monitoring-hub.service';
import { NetWatchApiService } from '../../core/services/netwatch-api.service';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'nw-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, StatusBadgeComponent],
  // The loaded summary is aliased inside a nested block: Angular's `as` binding is
  // available on an opening if-block only, not on an else-if.
  template: `
    <h1>{{ 'dashboard.title' | translate }}</h1>

    @if (loading()) {
      <p class="empty-state">{{ 'common.loading' | translate }}</p>
    } @else if (error()) {
      <div class="card">
        <p>{{ 'common.error' | translate }}</p>
        <button class="btn" type="button" (click)="load()">{{ 'common.retry' | translate }}</button>
      </div>
    } @else {
      @if (summary(); as data) {
      <section class="stats">
        <div class="card stat">
          <span class="value">{{ data.deviceCount }}</span>
          <span class="muted">{{ 'dashboard.devices' | translate }}</span>
        </div>
        <div class="card stat">
          <span class="value">{{ data.probeCount }}</span>
          <span class="muted">{{ 'dashboard.probes' | translate }}</span>
        </div>
        <div class="card stat">
          <span class="value">{{ uptimeLabel() }}</span>
          <span class="muted">{{ 'dashboard.uptime' | translate }}</span>
        </div>
        <div class="card stat" [class.alarm]="data.activeIncidentCount > 0">
          <span class="value">{{ data.activeIncidentCount }}</span>
          <span class="muted">{{ 'dashboard.activeIncidents' | translate }}</span>
        </div>
      </section>

      <section class="breakdown card">
        <span class="chip up">{{ 'status.up' | translate }} {{ data.upCount }}</span>
        <span class="chip degraded">{{ 'status.degraded' | translate }} {{ data.degradedCount }}</span>
        <span class="chip down">{{ 'status.down' | translate }} {{ data.downCount }}</span>
        <span class="chip unknown">{{ 'status.unknown' | translate }} {{ data.unknownCount }}</span>
      </section>

      <h2>{{ 'dashboard.probeGrid' | translate }}</h2>

      @if (probes().length === 0) {
        <p class="empty-state">{{ 'dashboard.empty' | translate }}</p>
      } @else {
        <section class="grid">
          @for (probe of probes(); track probe.id) {
            <a class="card tile" [class.flash]="flashing().has(probe.id)" [routerLink]="['/probes', probe.id]">
              <div class="tile-head">
                <strong>{{ probe.deviceName }}</strong>
                <nw-status-badge [state]="probe.state" [showLabel]="false" />
              </div>
              <span class="mono target">{{ probe.target }}</span>
              <div class="tile-foot muted">
                <span>{{ probe.type }}</span>
                <span>{{ latencyLabel(probe) }}</span>
              </div>
            </a>
          }
        </section>
      }

      <h2>{{ 'dashboard.recentIncidents' | translate }}</h2>

      @if (incidents().length === 0) {
        <p class="empty-state">{{ 'dashboard.noIncidents' | translate }}</p>
      } @else {
        <div class="card table-wrap">
          <table>
            <thead>
              <tr>
                <th>{{ 'incidents.device' | translate }}</th>
                <th>{{ 'incidents.severity' | translate }}</th>
                <th>{{ 'incidents.status' | translate }}</th>
                <th>{{ 'incidents.cause' | translate }}</th>
                <th>{{ 'incidents.duration' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (incident of incidents(); track incident.id) {
                <tr>
                  <td>
                    {{ incident.deviceName }}
                    <br />
                    <span class="mono muted">{{ incident.target }}</span>
                  </td>
                  <td>
                    <span class="chip" [class.down]="incident.severity === 'Critical'" [class.degraded]="incident.severity === 'Warning'">
                      {{ (incident.severity === 'Critical' ? 'incidents.critical' : 'incidents.warning') | translate }}
                    </span>
                  </td>
                  <td>{{ statusLabel(incident) }}</td>
                  <td class="cause">{{ incident.cause }}</td>
                  <td>{{ i18n.formatDuration(incident.durationSeconds) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
      }
    }
  `,
  styles: `
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 12px;
      margin-bottom: 12px;
    }

    .stat {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .stat .value {
      font-size: 1.8rem;
      font-weight: 600;
      line-height: 1.1;
    }

    .stat.alarm .value {
      color: var(--down);
    }

    .breakdown {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 24px;
    }

    .chip {
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 0.82rem;
      font-weight: 600;
    }

    .chip.up {
      color: var(--up);
      background: var(--up-bg);
    }
    .chip.degraded {
      color: var(--degraded);
      background: var(--degraded-bg);
    }
    .chip.down {
      color: var(--down);
      background: var(--down-bg);
    }
    .chip.unknown {
      color: var(--unknown);
      background: var(--unknown-bg);
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));
      gap: 12px;
      margin-bottom: 24px;
    }

    .tile {
      display: flex;
      flex-direction: column;
      gap: 6px;
      color: inherit;
      transition: border-color 0.15s ease;
    }

    .tile:hover {
      border-color: var(--border-strong);
      text-decoration: none;
    }

    /* Brief highlight when a live result arrives, so the eye can tell which tile
       just updated on a wall display nobody is staring at. */
    .tile.flash {
      animation: flash 1s ease-out;
    }

    @keyframes flash {
      from {
        border-color: var(--accent);
      }
      to {
        border-color: var(--border);
      }
    }

    .tile-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }

    .target {
      color: var(--text-muted);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .tile-foot {
      display: flex;
      justify-content: space-between;
      font-size: 0.82rem;
    }

    .table-wrap {
      padding: 0;
      overflow-x: auto;
    }

    .cause {
      max-width: 340px;
      color: var(--text-muted);
      font-size: 0.88rem;
    }
  `,
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(NetWatchApiService);
  private readonly hub = inject(MonitoringHubService);

  readonly i18n = inject(I18nService);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);

  /** Probe ids whose tile should briefly highlight after a live update. */
  readonly flashing = signal<ReadonlySet<number>>(new Set());

  readonly probes = computed(() => this.summary()?.probes ?? []);
  readonly incidents = computed(() => this.summary()?.recentIncidents ?? []);

  readonly uptimeLabel = computed(() => {
    const value = this.summary()?.overallUptimePercent;
    // Null means nothing was measured in the window, which is not 0%.
    return value === null || value === undefined ? '—' : `${value}%`;
  });

  constructor() {
    // Live results patch the already-loaded summary rather than triggering a
    // refetch: a busy estate produces several events a second, and refetching on
    // each would hammer the API for data the event already contains.
    this.hub.probeChecked.pipe(takeUntilDestroyed()).subscribe((event) => this.applyCheck(event));

    // Incidents do change counts and the recent list, so those are worth a reload.
    this.hub.incidentOpened.pipe(takeUntilDestroyed()).subscribe(() => this.load(false));
    this.hub.incidentResolved.pipe(takeUntilDestroyed()).subscribe(() => this.load(false));
  }

  ngOnInit(): void {
    this.load();
    void this.hub.start();
  }

  load(showSpinner = true): void {
    if (showSpinner) {
      this.loading.set(true);
    }
    this.error.set(false);

    this.api.getDashboard().subscribe({
      next: (data) => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }

  latencyLabel(probe: Probe): string {
    return probe.lastResponseTimeMs === null ? '—' : `${Math.round(probe.lastResponseTimeMs)} ms`;
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

  /**
   * Merges one live check into the loaded summary, keeping the state counters
   * consistent without another round trip.
   */
  private applyCheck(event: ProbeCheckedEvent): void {
    this.summary.update((current) => {
      if (!current) {
        return current;
      }

      const probes = current.probes.map((probe) =>
        probe.id === event.probeId
          ? {
              ...probe,
              state: event.state,
              lastOutcome: event.outcome,
              lastResponseTimeMs: event.responseTimeMs,
              lastCheckedAtUtc: event.checkedAtUtc,
            }
          : probe,
      );

      return {
        ...current,
        probes,
        upCount: probes.filter((p) => p.state === 'Up').length,
        degradedCount: probes.filter((p) => p.state === 'Degraded').length,
        downCount: probes.filter((p) => p.state === 'Down').length,
        unknownCount: probes.filter((p) => p.state === 'Unknown').length,
      };
    });

    this.flash(event.probeId);
  }

  private flash(probeId: number): void {
    this.flashing.update((current) => new Set(current).add(probeId));

    setTimeout(() => {
      this.flashing.update((current) => {
        const next = new Set(current);
        next.delete(probeId);
        return next;
      });
    }, 1_000);
  }
}
