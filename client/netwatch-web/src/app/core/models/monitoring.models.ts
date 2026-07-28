/**
 * Shapes returned by the NetWatch API.
 *
 * Enums are declared as string unions rather than TypeScript `enum`s: the API
 * serialises them as names, string unions are erased at compile time (no runtime
 * cost), and adding a value the client does not know about is a compile error at
 * every exhaustive `switch` instead of a silent fallthrough.
 */

export type ProbeType = 'Icmp' | 'Tcp' | 'Http';

export type ProbeState = 'Unknown' | 'Up' | 'Degraded' | 'Down';

export type ProbeOutcome =
  | 'Success'
  | 'Degraded'
  | 'Timeout'
  | 'ConnectionRefused'
  | 'DnsFailure'
  | 'Unreachable'
  | 'UnexpectedStatusCode'
  | 'TlsFailure'
  | 'Error';

export type DeviceCategory =
  | 'Router'
  | 'Switch'
  | 'Firewall'
  | 'AccessPoint'
  | 'Server'
  | 'Printer'
  | 'Website'
  | 'Other';

export type IncidentStatus = 'Open' | 'Acknowledged' | 'Resolved';

export type IncidentSeverity = 'Warning' | 'Critical';

export const DEVICE_CATEGORIES: readonly DeviceCategory[] = [
  'Router',
  'Switch',
  'Firewall',
  'AccessPoint',
  'Server',
  'Printer',
  'Website',
  'Other',
];

export interface Device {
  id: number;
  name: string;
  hostname: string;
  category: DeviceCategory;
  site: string | null;
  description: string | null;
  isEnabled: boolean;
  probeCount: number;
  /** Worst state across the device's probes. */
  status: ProbeState;
  createdAtUtc: string;
}

export interface DeviceDetail extends Omit<Device, 'probeCount'> {
  updatedAtUtc: string | null;
  probes: Probe[];
}

export interface Probe {
  id: number;
  deviceId: number;
  deviceName: string;
  type: ProbeType;
  /** Rendered target, e.g. "10.0.0.1:443" or "https://example.com/health". */
  target: string;
  port: number | null;
  httpPath: string | null;
  useHttps: boolean;
  expectedStatusCode: number;
  intervalSeconds: number;
  timeoutMs: number;
  failureThreshold: number;
  recoveryThreshold: number;
  degradedLatencyMs: number | null;
  isEnabled: boolean;
  state: ProbeState;
  lastOutcome: ProbeOutcome | null;
  lastResponseTimeMs: number | null;
  lastCheckedAtUtc: string | null;
}

export interface Incident {
  id: number;
  probeId: number;
  deviceId: number;
  deviceName: string;
  target: string;
  probeType: ProbeType;
  status: IncidentStatus;
  severity: IncidentSeverity;
  cause: string;
  failedChecks: number;
  startedAtUtc: string;
  resolvedAtUtc: string | null;
  acknowledgedByUserId: string | null;
  acknowledgedAtUtc: string | null;
  durationSeconds: number;
}

export interface ProbeHistoryPoint {
  checkedAtUtc: string;
  isSuccess: boolean;
  outcome: ProbeOutcome;
  responseTimeMs: number | null;
  statusCode: number | null;
  errorMessage: string | null;
}

export interface UptimeSummary {
  totalChecks: number;
  successfulChecks: number;
  failedChecks: number;
  /** Null when the window contains no checks, which is not the same as 0%. */
  uptimePercent: number | null;
  averageResponseTimeMs: number | null;
  p95ResponseTimeMs: number | null;
  minResponseTimeMs: number | null;
  maxResponseTimeMs: number | null;
}

export interface ProbeHistory {
  probeId: number;
  target: string;
  fromUtc: string;
  toUtc: string;
  points: ProbeHistoryPoint[];
  summary: UptimeSummary;
}

export interface DashboardSummary {
  deviceCount: number;
  probeCount: number;
  upCount: number;
  degradedCount: number;
  downCount: number;
  unknownCount: number;
  activeIncidentCount: number;
  overallUptimePercent: number | null;
  probes: Probe[];
  recentIncidents: Incident[];
}

export interface CreateDeviceRequest {
  name: string;
  hostname: string;
  category: DeviceCategory;
  site: string | null;
  description: string | null;
}

export type UpdateDeviceRequest = CreateDeviceRequest;

export interface CreateProbeRequest {
  type: ProbeType;
  intervalSeconds: number;
  timeoutMs: number;
  port: number | null;
  httpPath: string | null;
  useHttps: boolean;
  expectedStatusCode: number;
  failureThreshold: number;
  recoveryThreshold: number;
  degradedLatencyMs: number | null;
}

export type UpdateProbeRequest = Omit<CreateProbeRequest, 'type'>;

/** Pushed over SignalR after every check. */
export interface ProbeCheckedEvent {
  probeId: number;
  deviceId: number;
  deviceName: string;
  target: string;
  type: ProbeType;
  state: ProbeState;
  previousState: ProbeState;
  outcome: ProbeOutcome;
  responseTimeMs: number | null;
  checkedAtUtc: string;
}

/** Pushed over SignalR when an incident opens, escalates or resolves. */
export interface IncidentEvent {
  incidentId: number;
  probeId: number;
  deviceId: number;
  deviceName: string;
  target: string;
  severity: IncidentSeverity;
  status: IncidentStatus;
  cause: string;
  startedAtUtc: string;
  resolvedAtUtc: string | null;
}
