import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateDeviceRequest,
  CreateProbeRequest,
  DashboardSummary,
  Device,
  DeviceDetail,
  Incident,
  IncidentStatus,
  Probe,
  ProbeHistory,
  UpdateDeviceRequest,
  UpdateProbeRequest,
} from '../models/monitoring.models';

/**
 * Typed access to the NetWatch API.
 *
 * One service rather than one per resource: the surface is small, and keeping every
 * URL in a single file makes it obvious when the client and the controllers drift
 * apart.
 */
@Injectable({ providedIn: 'root' })
export class NetWatchApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  getDashboard(windowHours = 24): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/dashboard/summary`, {
      params: new HttpParams().set('windowHours', windowHours),
    });
  }

  listDevices(filters: { search?: string; category?: string; site?: string } = {}): Observable<Device[]> {
    let params = new HttpParams();

    // Empty filters are omitted rather than sent as blanks, so the URL reflects
    // what was actually asked for.
    for (const [key, value] of Object.entries(filters)) {
      if (value) {
        params = params.set(key, value);
      }
    }

    return this.http.get<Device[]>(`${this.base}/devices`, { params });
  }

  listSites(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/devices/sites`);
  }

  getDevice(id: number): Observable<DeviceDetail> {
    return this.http.get<DeviceDetail>(`${this.base}/devices/${id}`);
  }

  createDevice(request: CreateDeviceRequest): Observable<DeviceDetail> {
    return this.http.post<DeviceDetail>(`${this.base}/devices`, request);
  }

  updateDevice(id: number, request: UpdateDeviceRequest): Observable<DeviceDetail> {
    return this.http.put<DeviceDetail>(`${this.base}/devices/${id}`, request);
  }

  setDeviceEnabled(id: number, enabled: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/devices/${id}/enabled`, null, {
      params: new HttpParams().set('enabled', enabled),
    });
  }

  deleteDevice(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/devices/${id}`);
  }

  getProbe(id: number): Observable<Probe> {
    return this.http.get<Probe>(`${this.base}/probes/${id}`);
  }

  createProbe(deviceId: number, request: CreateProbeRequest): Observable<Probe> {
    return this.http.post<Probe>(`${this.base}/devices/${deviceId}/probes`, request);
  }

  updateProbe(id: number, request: UpdateProbeRequest): Observable<Probe> {
    return this.http.put<Probe>(`${this.base}/probes/${id}`, request);
  }

  setProbeEnabled(id: number, enabled: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/probes/${id}/enabled`, null, {
      params: new HttpParams().set('enabled', enabled),
    });
  }

  deleteProbe(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/probes/${id}`);
  }

  getProbeHistory(id: number, hours: number, maxPoints = 500): Observable<ProbeHistory> {
    const to = new Date();
    const from = new Date(to.getTime() - hours * 3_600_000);

    return this.http.get<ProbeHistory>(`${this.base}/probes/${id}/history`, {
      params: new HttpParams()
        .set('from', from.toISOString())
        .set('to', to.toISOString())
        .set('maxPoints', maxPoints),
    });
  }

  /** Runs a check immediately instead of waiting for the next scheduled slot. */
  runProbeNow(id: number): Observable<Probe> {
    return this.http.post<Probe>(`${this.base}/probes/${id}/run`, null);
  }

  listIncidents(filters: { status?: IncidentStatus; deviceId?: number; take?: number } = {}): Observable<Incident[]> {
    let params = new HttpParams();

    if (filters.status) {
      params = params.set('status', filters.status);
    }
    if (filters.deviceId !== undefined) {
      params = params.set('deviceId', filters.deviceId);
    }
    params = params.set('take', filters.take ?? 100);

    return this.http.get<Incident[]>(`${this.base}/incidents`, { params });
  }

  acknowledgeIncident(id: number): Observable<Incident> {
    return this.http.post<Incident>(`${this.base}/incidents/${id}/acknowledge`, null);
  }
}
