import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../core/i18n/i18n.service';
import { ProbeState } from '../core/models/monitoring.models';

/**
 * Health indicator.
 *
 * Colour is never the only signal: each state also carries a distinct glyph and a
 * text label, so the badge is still readable with colour vision deficiency or in
 * greyscale — the exact condition where a red-versus-green dashboard fails.
 */
@Component({
  selector: 'nw-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="badge" [class]="state()" [attr.title]="label()">
      <span class="glyph" aria-hidden="true">{{ glyph() }}</span>
      @if (showLabel()) {
        <span class="label">{{ label() }}</span>
      }
    </span>
  `,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 2px 9px;
      border-radius: 999px;
      font-size: 0.8rem;
      font-weight: 600;
      white-space: nowrap;
    }

    .glyph {
      font-size: 0.9em;
      line-height: 1;
    }

    .Up {
      color: var(--up);
      background: var(--up-bg);
    }
    .Degraded {
      color: var(--degraded);
      background: var(--degraded-bg);
    }
    .Down {
      color: var(--down);
      background: var(--down-bg);
    }
    .Unknown {
      color: var(--unknown);
      background: var(--unknown-bg);
    }
  `,
})
export class StatusBadgeComponent {
  private readonly i18n = inject(I18nService);

  readonly state = input.required<ProbeState>();
  readonly showLabel = input(true);

  readonly glyph = computed(() => {
    switch (this.state()) {
      case 'Up':
        return '●';
      case 'Degraded':
        return '▲';
      case 'Down':
        return '✕';
      default:
        return '○';
    }
  });

  readonly label = computed(() => {
    switch (this.state()) {
      case 'Up':
        return this.i18n.translate('status.up');
      case 'Degraded':
        return this.i18n.translate('status.degraded');
      case 'Down':
        return this.i18n.translate('status.down');
      default:
        return this.i18n.translate('status.unknown');
    }
  });
}
