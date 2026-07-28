import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Probe, ProbeHistory } from '../../core/models/monitoring.models';
import { AuthService } from '../../core/services/auth.service';
import { NetWatchApiService } from '../../core/services/netwatch-api.service';
import { LatencyChartComponent } from '../../shared/latency-chart.component';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'nw-probe-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, StatusBadgeComponent, LatencyChartComponent],
  template: `
    @if (probe(); as target) {
      <a class="back" [routerLink]="['/devices', target.deviceId]">← {{ target.deviceName }}</a>

      <header class="page-head">
        <div>
          <h1>
            <span class="mono">{{ target.target }}</span>
            <nw-status-badge [state]="target.state" />
          </h1>
          <p class="muted">
            {{ target.type }} · {{ 'probes.interval' | translate }} {{ target.intervalSeconds }}s ·
            {{ 'probes.lastCheck' | translate }} {{ i18n.formatRelative(target.lastCheckedAtUtc) }}
          </p>
        </div>

        @if (auth.canEdit()) {
          <button class="btn btn-sm" type="button" [disabled]="checking()" (click)="runNow()">
            @if (checking()) {
              <span class="spinner"></span>
              {{ 'probes.running' | translate }}
            } @else {
              {{ 'probes.runNow' | translate }}
            }
          </button>
        }
      </header>

      <div class="window">
        <label for="window">{{ 'history.window' | translate }}</label>
        <select id="window" [ngModel]="hours()" (ngModelChange)="onWindowChange($event)">
          <option [value]="1">{{ 'history.1h' | translate }}</option>
          <option [value]="24">{{ 'history.24h' | translate }}</option>
          <option [value]="168">{{ 'history.7d' | translate }}</option>
          <option [value]="720">{{ 'history.30d' | translate }}</option>
        </select>
      </div>

      @if (history(); as data) {
        <section class="stats">
          <div class="card stat">
            <span class="value">{{ data.summary.uptimePercent === null ? '—' : data.summary.uptimePercent + '%' }}</span>
            <span class="muted">{{ 'history.uptime' | translate }}</span>
          </div>
          <div class="card stat">
            <span class="value">{{ data.summary.totalChecks }}</span>
            <span class="muted">{{ 'history.checks' | translate }}</span>
          </div>
          <div class="card stat" [class.alarm]="data.summary.failedChecks > 0">
            <span class="value">{{ data.summary.failedChecks }}</span>
            <span class="muted">{{ 'history.failed' | translate }}</span>
          </div>
          <div class="card stat">
            <span class="value">{{ ms(data.summary.averageResponseTimeMs) }}</span>
            <span class="muted">{{ 'history.average' | translate }}</span>
          </div>
          <div class="card stat">
            <span class="value">{{ ms(data.summary.p95ResponseTimeMs) }}</span>
            <span class="muted">{{ 'history.p95' | translate }}</span>
          </div>
          <div class="card stat">
            <span class="value">{{ ms(data.summary.maxResponseTimeMs) }}</span>
            <span class="muted">{{ 'history.max' | translate }}</span>
          </div>
        </section>

        <h2>{{ 'history.title' | translate }}</h2>
        <div class="card">
          <nw-latency-chart [points]="data.points" />
        </div>
      } @else {
        <p class="empty-state">{{ 'common.loading' | translate }}</p>
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
      margin-bottom: 16px;
    }

    .page-head h1 {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0 0 4px;
      font-size: 1.25rem;
    }

    .page-head p {
      margin: 0;
      font-size: 0.88rem;
    }

    .window {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 16px;
    }

    .window label {
      margin: 0;
    }

    .window select {
      max-width: 200px;
    }

    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
      gap: 12px;
      margin-bottom: 22px;
    }

    .stat {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .stat .value {
      font-size: 1.35rem;
      font-weight: 600;
      line-height: 1.2;
    }

    .stat.alarm .value {
      color: var(--down);
    }
  `,
})
export class ProbeDetailComponent implements OnInit {
  private readonly api = inject(NetWatchApiService);
  private readonly route = inject(ActivatedRoute);

  readonly auth = inject(AuthService);
  readonly i18n = inject(I18nService);

  readonly probe = signal<Probe | null>(null);
  readonly history = signal<ProbeHistory | null>(null);
  readonly hours = signal(24);
  readonly checking = signal(false);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.getProbe(id).subscribe((probe) => this.probe.set(probe));
    this.loadHistory();
  }

  onWindowChange(value: string | number): void {
    this.hours.set(Number(value));
    this.loadHistory();
  }

  /** Formats a nullable millisecond figure, distinguishing "none" from zero. */
  ms(value: number | null): string {
    return value === null ? '—' : `${Math.round(value)} ms`;
  }

  runNow(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.checking.set(true);

    this.api.runProbeNow(id).subscribe({
      next: (probe) => {
        this.probe.set(probe);
        this.checking.set(false);
        // The manual check produced a new measurement, so the chart is now stale.
        this.loadHistory();
      },
      error: () => this.checking.set(false),
    });
  }

  private loadHistory(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.history.set(null);

    this.api.getProbeHistory(id, this.hours()).subscribe((history) => this.history.set(history));
  }
}
