import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../core/i18n/i18n.service';
import { ProbeHistoryPoint } from '../core/models/monitoring.models';

interface PlottedPoint {
  x: number;
  y: number;
  point: ProbeHistoryPoint;
}

const VIEW_WIDTH = 800;
const VIEW_HEIGHT = 220;
const PADDING = { top: 12, right: 12, bottom: 26, left: 46 };

/**
 * Response-time chart, drawn as inline SVG.
 *
 * Hand-rolled rather than pulling in a charting library: this needs one series, a
 * few gridlines and failure markers, and a dependency would add hundreds of
 * kilobytes plus a wrapper to keep it in sync with Angular's change detection.
 *
 * The plot is deliberately laid out left-to-right even in Arabic — time on the x
 * axis reads forward regardless of script, and mirroring it would put "now" on the
 * left, which no monitoring tool does.
 *
 * Failed checks have no latency to plot, so they appear as markers along the
 * baseline instead of being silently dropped. A gap in the line would suggest
 * "no data" when the truth is "it was down".
 */
@Component({
  selector: 'nw-latency-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (plotted().length === 0) {
      <p class="empty-state">{{ i18n.translate('history.empty') }}</p>
    } @else {
      <svg
        [attr.viewBox]="'0 0 ' + viewWidth + ' ' + viewHeight"
        preserveAspectRatio="none"
        role="img"
        [attr.aria-label]="ariaLabel()"
      >
        <!-- Horizontal gridlines with their millisecond values. -->
        @for (tick of yTicks(); track tick.value) {
          <line class="grid" [attr.x1]="padding.left" [attr.x2]="viewWidth - padding.right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          <text class="axis" [attr.x]="padding.left - 8" [attr.y]="tick.y + 4" text-anchor="end">{{ tick.value }}</text>
        }

        <!-- Shaded area under the curve, purely to make the trend easier to read. -->
        <path class="area" [attr.d]="areaPath()" />
        <path class="line" [attr.d]="linePath()" />

        <!-- Failures, pinned to the baseline. -->
        @for (marker of failures(); track marker.point.checkedAtUtc) {
          <circle class="failure" [attr.cx]="marker.x" [attr.cy]="viewHeight - padding.bottom" r="3.5">
            <title>{{ marker.point.errorMessage ?? marker.point.outcome }}</title>
          </circle>
        }

        <!-- Endpoints of the time axis. -->
        <text class="axis" [attr.x]="padding.left" [attr.y]="viewHeight - 6">{{ firstLabel() }}</text>
        <text class="axis" [attr.x]="viewWidth - padding.right" [attr.y]="viewHeight - 6" text-anchor="end">
          {{ lastLabel() }}
        </text>
      </svg>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    svg {
      width: 100%;
      height: 220px;
      /* The time axis always runs left to right, even when the page is mirrored. */
      direction: ltr;
    }

    .grid {
      stroke: var(--border);
      stroke-width: 1;
    }

    .axis {
      fill: var(--text-faint);
      font-size: 11px;
      font-family: var(--font-mono);
    }

    .line {
      fill: none;
      stroke: var(--accent);
      stroke-width: 2;
      vector-effect: non-scaling-stroke;
    }

    .area {
      fill: rgba(47, 129, 247, 0.12);
      stroke: none;
    }

    .failure {
      fill: var(--down);
    }
  `,
})
export class LatencyChartComponent {
  readonly i18n = inject(I18nService);

  readonly points = input.required<ProbeHistoryPoint[]>();

  readonly viewWidth = VIEW_WIDTH;
  readonly viewHeight = VIEW_HEIGHT;
  readonly padding = PADDING;

  /** Successful checks, mapped into view coordinates. */
  private readonly plottedSuccesses = computed<PlottedPoint[]>(() => {
    const all = this.points();
    const max = this.maxLatency();

    return all
      .map((point, index) => ({ point, index }))
      .filter((entry) => entry.point.responseTimeMs !== null)
      .map((entry) => ({
        x: this.xFor(entry.index, all.length),
        y: this.yFor(entry.point.responseTimeMs as number, max),
        point: entry.point,
      }));
  });

  readonly plotted = computed(() => this.points());

  readonly failures = computed<PlottedPoint[]>(() => {
    const all = this.points();

    return all
      .map((point, index) => ({ point, index }))
      .filter((entry) => !entry.point.isSuccess)
      .map((entry) => ({
        x: this.xFor(entry.index, all.length),
        y: VIEW_HEIGHT - PADDING.bottom,
        point: entry.point,
      }));
  });

  readonly linePath = computed(() => {
    const points = this.plottedSuccesses();
    if (points.length === 0) {
      return '';
    }

    return points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  });

  readonly areaPath = computed(() => {
    const points = this.plottedSuccesses();
    if (points.length === 0) {
      return '';
    }

    const baseline = VIEW_HEIGHT - PADDING.bottom;
    const first = points[0];
    const last = points[points.length - 1];

    return `${this.linePath()} L${last.x.toFixed(1)},${baseline} L${first.x.toFixed(1)},${baseline} Z`;
  });

  readonly yTicks = computed(() => {
    const max = this.maxLatency();

    return [0, 0.25, 0.5, 0.75, 1].map((fraction) => ({
      value: Math.round(max * fraction),
      y: this.yFor(max * fraction, max),
    }));
  });

  readonly firstLabel = computed(() => {
    const all = this.points();
    return all.length ? this.i18n.formatTime(all[0].checkedAtUtc) : '';
  });

  readonly lastLabel = computed(() => {
    const all = this.points();
    return all.length ? this.i18n.formatTime(all[all.length - 1].checkedAtUtc) : '';
  });

  /**
   * Screen readers cannot read a path, so the chart is summarised in text.
   */
  readonly ariaLabel = computed(() => {
    const all = this.points();
    const failed = all.filter((p) => !p.isSuccess).length;
    return `Response time chart: ${all.length} checks, ${failed} failed.`;
  });

  /**
   * Upper bound of the y axis.
   *
   * Rounded up to a readable step so the top gridline is a number a human would
   * write (500, not 487), and floored at 10ms so a perfectly fast probe does not
   * produce a degenerate zero-height scale.
   */
  private readonly maxLatency = computed(() => {
    const values = this.points()
      .map((p) => p.responseTimeMs)
      .filter((value): value is number => value !== null);

    if (values.length === 0) {
      return 10;
    }

    const peak = Math.max(...values, 1);
    const magnitude = Math.pow(10, Math.floor(Math.log10(peak)));
    return Math.max(10, Math.ceil(peak / magnitude) * magnitude);
  });

  private xFor(index: number, total: number): number {
    const usable = VIEW_WIDTH - PADDING.left - PADDING.right;

    // A single point sits at the start rather than dividing by zero.
    if (total <= 1) {
      return PADDING.left;
    }

    return PADDING.left + (index / (total - 1)) * usable;
  }

  private yFor(value: number, max: number): number {
    const usable = VIEW_HEIGHT - PADDING.top - PADDING.bottom;
    const ratio = max === 0 ? 0 : Math.min(1, value / max);

    // SVG y grows downward, so the axis is inverted here.
    return PADDING.top + (1 - ratio) * usable;
  }
}
