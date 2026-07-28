import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IncidentEvent, ProbeCheckedEvent } from '../models/monitoring.models';
import { AuthService } from './auth.service';

export type HubStatus = 'disconnected' | 'connecting' | 'connected';

/**
 * Live monitoring feed over SignalR.
 *
 * The dashboard could poll instead, but polling a 5-second scheduler means either
 * stale tiles or a request every few seconds per open tab. A push connection sends
 * exactly one message per check and nothing when nothing happens.
 */
@Injectable({ providedIn: 'root' })
export class MonitoringHubService {
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  private connection: HubConnection | null = null;

  private readonly probeChecked$ = new Subject<ProbeCheckedEvent>();
  private readonly incidentOpened$ = new Subject<IncidentEvent>();
  private readonly incidentResolved$ = new Subject<IncidentEvent>();

  /** Surfaced in the UI so a silent dashboard is visibly stale rather than wrong. */
  readonly status = signal<HubStatus>('disconnected');

  readonly probeChecked = this.probeChecked$.asObservable();
  readonly incidentOpened = this.incidentOpened$.asObservable();
  readonly incidentResolved = this.incidentResolved$.asObservable();

  constructor() {
    this.destroyRef.onDestroy(() => void this.stop());
  }

  async start(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.status.set('connecting');

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/monitoring`, {
        // A WebSocket handshake cannot carry an Authorization header, so the token
        // goes in the query string; the API accepts it there for hub paths only.
        // Read through a factory rather than captured once, so a rotated token is
        // used on reconnect instead of a stale one.
        accessTokenFactory: () => this.auth.accessToken ?? '',
      })
      // Backs off rather than hammering: a dropped connection is usually the API
      // restarting or the network flapping, and neither is helped by a retry storm.
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.on('ProbeChecked', (event: ProbeCheckedEvent) => this.probeChecked$.next(event));
    this.connection.on('IncidentOpened', (event: IncidentEvent) => this.incidentOpened$.next(event));
    this.connection.on('IncidentResolved', (event: IncidentEvent) => this.incidentResolved$.next(event));

    this.connection.onreconnecting(() => this.status.set('connecting'));
    this.connection.onreconnected(() => this.status.set('connected'));
    this.connection.onclose(() => this.status.set('disconnected'));

    try {
      await this.connection.start();
      this.status.set('connected');
    } catch {
      // A failed connection degrades the dashboard to whatever the last REST call
      // returned; it must not block rendering.
      this.status.set('disconnected');
    }
  }

  async stop(): Promise<void> {
    if (!this.connection) {
      return;
    }

    try {
      await this.connection.stop();
    } finally {
      this.connection = null;
      this.status.set('disconnected');
    }
  }
}
