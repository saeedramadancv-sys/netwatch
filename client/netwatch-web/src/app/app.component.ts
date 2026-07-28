import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18nService } from './core/i18n/i18n.service';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { AuthService } from './core/services/auth.service';
import { MonitoringHubService } from './core/services/monitoring-hub.service';

/**
 * Application shell: navigation, the live-connection indicator and the language
 * toggle. The chrome is hidden while signed out, so the login screen is a page of
 * its own rather than an empty frame.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    @if (auth.isAuthenticated()) {
      <header class="topbar">
        <a class="brand" routerLink="/dashboard">{{ 'app.title' | translate }}</a>

        <nav>
          <a routerLink="/dashboard" routerLinkActive="active">{{ 'nav.dashboard' | translate }}</a>
          <a routerLink="/devices" routerLinkActive="active">{{ 'nav.devices' | translate }}</a>
          <a routerLink="/incidents" routerLinkActive="active">{{ 'nav.incidents' | translate }}</a>
        </nav>

        <div class="right">
          <!-- Connection state is shown rather than hidden: a dashboard that has
               quietly stopped receiving updates is worse than one that says so. -->
          <span class="live" [class]="hub.status()" [title]="liveLabel()">
            <span class="dot" aria-hidden="true"></span>
            <span class="live-text">{{ liveLabel() }}</span>
          </span>

          <button class="link" type="button" (click)="i18n.toggle()">
            {{ i18n.language() === 'en' ? 'العربية' : 'English' }}
          </button>

          <span class="user muted">{{ auth.user()?.fullName }}</span>

          <button class="link" type="button" (click)="auth.logout()">{{ 'nav.signOut' | translate }}</button>
        </div>
      </header>
    }

    <main [class.shell]="auth.isAuthenticated()">
      <router-outlet />
    </main>
  `,
  styles: `
    .topbar {
      display: flex;
      align-items: center;
      gap: 20px;
      padding: 0 20px;
      height: 54px;
      border-bottom: 1px solid var(--border);
      background: var(--bg-raised);
    }

    .brand {
      color: var(--text);
      font-weight: 600;
      font-size: 1.05rem;
    }

    .brand:hover {
      text-decoration: none;
    }

    nav {
      display: flex;
      gap: 4px;
    }

    nav a {
      padding: 6px 12px;
      border-radius: var(--radius-sm);
      color: var(--text-muted);
      font-size: 0.92rem;
    }

    nav a:hover {
      background: var(--bg-hover);
      color: var(--text);
      text-decoration: none;
    }

    nav a.active {
      background: var(--bg-hover);
      color: var(--text);
    }

    .right {
      display: flex;
      align-items: center;
      gap: 14px;
      /* Logical margin, so the group sits at the trailing edge in both directions. */
      margin-inline-start: auto;
      font-size: 0.88rem;
    }

    .live {
      display: flex;
      align-items: center;
      gap: 6px;
      color: var(--text-muted);
    }

    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--unknown);
    }

    .live.connected .dot {
      background: var(--up);
    }

    .live.connecting .dot {
      background: var(--degraded);
      animation: pulse 1.2s ease-in-out infinite;
    }

    .live.disconnected .dot {
      background: var(--down);
    }

    @keyframes pulse {
      50% {
        opacity: 0.3;
      }
    }

    .link {
      padding: 0;
      border: none;
      background: none;
      color: var(--text-muted);
      cursor: pointer;
    }

    .link:hover {
      color: var(--text);
    }

    .shell {
      max-width: 1200px;
      margin: 0 auto;
      padding: 22px 20px 60px;
    }

    /* The user name and the live label are the first things to go on a narrow
       screen: the status dot still conveys the connection state on its own. */
    @media (max-width: 720px) {
      .user,
      .live-text {
        display: none;
      }

      .topbar {
        gap: 12px;
        padding: 0 12px;
      }
    }
  `,
})
export class AppComponent {
  readonly auth = inject(AuthService);
  readonly hub = inject(MonitoringHubService);
  readonly i18n = inject(I18nService);

  readonly liveLabel = computed(() => {
    switch (this.hub.status()) {
      case 'connected':
        return this.i18n.translate('live.connected');
      case 'connecting':
        return this.i18n.translate('live.connecting');
      default:
        return this.i18n.translate('live.disconnected');
    }
  });
}
