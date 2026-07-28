import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'nw-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <div class="shell">
      <form class="card" [formGroup]="form" (ngSubmit)="submit()">
        <header>
          <h1>{{ 'app.title' | translate }}</h1>
          <p class="muted">{{ 'app.subtitle' | translate }}</p>
        </header>

        <div class="field">
          <label for="email">{{ 'login.email' | translate }}</label>
          <input id="email" type="email" formControlName="email" autocomplete="username" dir="ltr" />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <p class="field-error">{{ 'common.required' | translate }}</p>
          }
        </div>

        <div class="field">
          <label for="password">{{ 'login.password' | translate }}</label>
          <input id="password" type="password" formControlName="password" autocomplete="current-password" dir="ltr" />
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <p class="field-error">{{ 'common.required' | translate }}</p>
          }
        </div>

        <!-- role="alert" so a screen reader announces the failure instead of
             leaving the user wondering why nothing happened. -->
        @if (error()) {
          <p class="field-error" role="alert">{{ error() }}</p>
        }

        <button class="btn btn-primary submit" type="submit" [disabled]="busy()">
          @if (busy()) {
            <span class="spinner"></span>
            {{ 'login.working' | translate }}
          } @else {
            {{ 'login.submit' | translate }}
          }
        </button>

        <button class="btn btn-sm lang" type="button" (click)="i18n.toggle()">
          {{ i18n.language() === 'en' ? 'العربية' : 'English' }}
        </button>
      </form>
    </div>
  `,
  styles: `
    .shell {
      display: grid;
      place-items: center;
      min-height: 100vh;
      padding: 20px;
    }

    form {
      width: 100%;
      max-width: 380px;
    }

    header {
      margin-bottom: 20px;
      text-align: center;
    }

    header p {
      margin: 0;
      font-size: 0.9rem;
    }

    .submit {
      width: 100%;
      justify-content: center;
      margin-top: 4px;
    }

    .lang {
      display: block;
      margin: 14px auto 0;
      border: none;
      background: none;
      color: var(--text-muted);
    }
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  readonly i18n = inject(I18nService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        // Returns the user to whatever they were trying to open before the guard
        // intercepted them.
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (error: unknown) => {
        this.busy.set(false);

        // status 0 means the request never reached a server - a different problem
        // from bad credentials, and worth saying so rather than blaming the user.
        const unreachable = error instanceof HttpErrorResponse && error.status === 0;
        this.error.set(this.i18n.translate(unreachable ? 'login.unavailable' : 'login.failed'));
      },
    });
  }
}
