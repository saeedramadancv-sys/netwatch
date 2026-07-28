import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DEVICE_CATEGORIES, DeviceCategory } from '../../core/models/monitoring.models';
import { NetWatchApiService } from '../../core/services/netwatch-api.service';

/**
 * Create and edit form for a device. One component for both: the fields are
 * identical, and the only difference is whether an id was supplied in the route.
 */
@Component({
  selector: 'nw-device-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe],
  template: `
    <a class="back" routerLink="/devices">← {{ 'common.back' | translate }}</a>

    <form class="card" [formGroup]="form" (ngSubmit)="submit()">
      <h1>{{ (editing() ? 'common.edit' : 'devices.add') | translate }}</h1>

      <div class="field">
        <label for="name">{{ 'devices.name' | translate }}</label>
        <input id="name" formControlName="name" />
        @if (form.controls.name.touched && form.controls.name.invalid) {
          <p class="field-error">{{ 'common.required' | translate }}</p>
        }
      </div>

      <div class="field">
        <label for="hostname">{{ 'devices.host' | translate }}</label>
        <!-- Forced LTR: an IP or hostname reordered by the bidi algorithm in an
             Arabic layout would be unreadable and easy to mistype. -->
        <input id="hostname" formControlName="hostname" dir="ltr" placeholder="10.0.0.1" />
        @if (form.controls.hostname.touched && form.controls.hostname.invalid) {
          <p class="field-error">{{ 'common.required' | translate }}</p>
        }
      </div>

      <div class="field">
        <label for="category">{{ 'devices.category' | translate }}</label>
        <select id="category" formControlName="category">
          @for (option of categories; track option) {
            <option [value]="option">{{ option }}</option>
          }
        </select>
      </div>

      <div class="field">
        <label for="site">{{ 'devices.site' | translate }}</label>
        <input id="site" formControlName="site" />
      </div>

      <div class="field">
        <label for="description">{{ 'devices.description' | translate }}</label>
        <textarea id="description" rows="3" formControlName="description"></textarea>
      </div>

      @if (error()) {
        <p class="field-error" role="alert">{{ error() }}</p>
      }

      <div class="actions">
        <button class="btn btn-primary" type="submit" [disabled]="busy()">{{ 'common.save' | translate }}</button>
        <a class="btn" routerLink="/devices">{{ 'common.cancel' | translate }}</a>
      </div>
    </form>
  `,
  styles: `
    form {
      max-width: 520px;
    }

    .back {
      display: inline-block;
      margin-bottom: 12px;
      color: var(--text-muted);
    }

    .actions {
      display: flex;
      gap: 10px;
      margin-top: 6px;
    }
  `,
})
export class DeviceFormComponent implements OnInit {
  private readonly api = inject(NetWatchApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  readonly categories = DEVICE_CATEGORIES;
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly editing = signal(false);

  private deviceId: number | null = null;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    hostname: ['', [Validators.required, Validators.maxLength(253)]],
    category: ['Server' as DeviceCategory, Validators.required],
    site: [''],
    description: [''],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.deviceId = Number(id);
    this.editing.set(true);

    this.api.getDevice(this.deviceId).subscribe((device) =>
      this.form.patchValue({
        name: device.name,
        hostname: device.hostname,
        category: device.category,
        site: device.site ?? '',
        description: device.description ?? '',
      }),
    );
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    const request = {
      name: value.name,
      hostname: value.hostname,
      category: value.category,
      // Empty strings become null so the API stores "not set" rather than "".
      site: value.site || null,
      description: value.description || null,
    };

    const call = this.deviceId
      ? this.api.updateDevice(this.deviceId, request)
      : this.api.createDevice(request);

    call.subscribe({
      next: (device) => void this.router.navigate(['/devices', device.id]),
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(describeError(error));
      },
    });
  }
}

/**
 * Surfaces the server's own message where there is one.
 *
 * The API returns RFC 9457 problem documents, so a duplicate hostname arrives as a
 * specific, human-readable 409 detail. Replacing that with a generic "save failed"
 * would throw away the only useful part of the response.
 */
function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { detail?: string; title?: string; errors?: Record<string, string[]> } | null;

    if (body?.errors) {
      const messages = Object.values(body.errors).flat();
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }

    return body?.detail ?? body?.title ?? error.message;
  }

  return 'Unexpected error.';
}
