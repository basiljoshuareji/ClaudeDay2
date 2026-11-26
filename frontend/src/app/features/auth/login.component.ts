import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../core/errors/api-exception';
import { AuthService } from '../../core/services/auth.service';
import { IconComponent } from '../../shared/icons/icon.component';
import { ThemeMenuComponent } from '../../shared/components/theme-menu.component';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, IconComponent, ThemeMenuComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="auth" id="main">
      <div class="auth__theme"><app-theme-menu /></div>

      <div class="auth__card card fade-in">
        <div class="brand">
          <span class="brand__logo"><app-icon name="list-check" [size]="24" /></span>
          <span class="brand__name">Todo</span>
        </div>
        <h1>Welcome back</h1>
        <p class="subtitle">Sign in to see your tasks.</p>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (serverError(); as message) {
            <div class="form-error" role="alert">{{ message }}</div>
          }

          <div class="field">
            <label for="email">Email</label>
            <input
              id="email"
              class="input"
              type="email"
              formControlName="email"
              autocomplete="email"
              inputmode="email"
              [attr.aria-invalid]="email.invalid && email.touched"
              aria-describedby="email-error"
            />
            <div id="email-error" aria-live="polite">
              @if (email.touched && email.errors?.['required']) {
                <span class="field-error">Enter your email address.</span>
              } @else if (email.touched && email.errors?.['email']) {
                <span class="field-error">Enter a valid email address, like name&#64;example.com.</span>
              }
            </div>
          </div>

          <div class="field">
            <label for="password">Password</label>
            <div class="password-wrap">
              <input
                id="password"
                class="input"
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
                autocomplete="current-password"
                [attr.aria-invalid]="password.invalid && password.touched"
                aria-describedby="password-error"
              />
              <button
                type="button"
                [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
                [attr.aria-pressed]="showPassword()"
                (click)="showPassword.set(!showPassword())"
              >
                <app-icon [name]="showPassword() ? 'eye-off' : 'eye'" />
              </button>
            </div>
            <div id="password-error" aria-live="polite">
              @if (password.touched && password.errors?.['required']) {
                <span class="field-error">Enter your password.</span>
              }
            </div>
          </div>

          <button type="submit" class="btn btn--primary submit" [disabled]="submitting()">
            @if (submitting()) {
              <span class="spinner" aria-hidden="true"></span> Signing in…
            } @else {
              Sign in
            }
          </button>
        </form>

        <p class="switch">New here? <a routerLink="/register">Create an account</a></p>
      </div>
    </main>
  `,
  styleUrl: './auth.scss',
})
export class LoginComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected get email() {
    return this.form.controls.email;
  }

  protected get password() {
    return this.form.controls.password;
  }

  protected submit(): void {
    this.serverError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl(this.safeReturnUrl()),
      error: (error: unknown) => {
        this.submitting.set(false);
        this.serverError.set(errorMessage(error, 'Could not sign you in. Please try again.'));
      },
    });
  }

  /** Only follow same-site relative return URLs (prevents open redirects). */
  private safeReturnUrl(): string {
    const url = this.route.snapshot.queryParamMap.get('returnUrl');
    return url && url.startsWith('/') && !url.startsWith('//') ? url : '/';
  }
}
