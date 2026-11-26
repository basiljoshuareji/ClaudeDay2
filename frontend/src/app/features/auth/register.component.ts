import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiException, errorMessage } from '../../core/errors/api-exception';
import { AuthService } from '../../core/services/auth.service';
import { ThemeMenuComponent } from '../../shared/components/theme-menu.component';
import { IconComponent } from '../../shared/icons/icon.component';

/** Group validator: the confirmation must equal the password. */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordsMismatch: true } : null;
}

@Component({
  selector: 'app-register',
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
        <h1>Create your account</h1>
        <p class="subtitle">You'll start with Home and Work sections ready to go.</p>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (serverError(); as message) {
            <div class="form-error" role="alert">{{ message }}</div>
          }

          <div class="field">
            <label for="displayName">Your name</label>
            <input
              id="displayName"
              class="input"
              formControlName="displayName"
              autocomplete="name"
              maxlength="100"
              [attr.aria-invalid]="displayName.invalid && displayName.touched"
              aria-describedby="name-error"
            />
            <div id="name-error" aria-live="polite">
              @if (displayName.touched && displayName.errors?.['required']) {
                <span class="field-error">Tell us what to call you.</span>
              }
            </div>
          </div>

          <div class="field">
            <label for="email">Email</label>
            <input
              id="email"
              class="input"
              type="email"
              formControlName="email"
              autocomplete="email"
              inputmode="email"
              (input)="clearServerField('email')"
              [attr.aria-invalid]="(email.invalid && email.touched) || !!serverFieldError('email')"
              aria-describedby="email-error"
            />
            <div id="email-error" aria-live="polite">
              @if (serverFieldError('email'); as message) {
                <span class="field-error">{{ message }}</span>
              } @else if (email.touched && email.errors?.['required']) {
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
                autocomplete="new-password"
                maxlength="72"
                [attr.aria-invalid]="password.invalid && password.touched"
                aria-describedby="password-hint password-error"
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
            <span id="password-hint" class="hint">At least 8 characters, with a letter and a number.</span>
            <div id="password-error" aria-live="polite">
              @if (password.touched && password.errors?.['required']) {
                <span class="field-error">Choose a password.</span>
              } @else if (password.touched && password.errors?.['minlength']) {
                <span class="field-error">Use at least 8 characters.</span>
              } @else if (password.touched && password.errors?.['pattern']) {
                <span class="field-error">Include at least one letter and one number.</span>
              }
            </div>
          </div>

          <div class="field">
            <label for="confirmPassword">Confirm password</label>
            <input
              id="confirmPassword"
              class="input"
              [type]="showPassword() ? 'text' : 'password'"
              formControlName="confirmPassword"
              autocomplete="new-password"
              [attr.aria-invalid]="confirmPassword.touched && (confirmPassword.invalid || form.errors?.['passwordsMismatch'])"
              aria-describedby="confirm-error"
            />
            <div id="confirm-error" aria-live="polite">
              @if (confirmPassword.touched && confirmPassword.errors?.['required']) {
                <span class="field-error">Re-enter your password.</span>
              } @else if (confirmPassword.touched && form.errors?.['passwordsMismatch']) {
                <span class="field-error">The passwords don't match.</span>
              }
            </div>
          </div>

          <button type="submit" class="btn btn--primary submit" [disabled]="submitting()">
            @if (submitting()) {
              <span class="spinner" aria-hidden="true"></span> Creating account…
            } @else {
              Create account
            }
          </button>
        </form>

        <p class="switch">Already registered? <a routerLink="/login">Sign in</a></p>
      </div>
    </main>
  `,
  styleUrl: './auth.scss',
})
export class RegisterComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = this.fb.group(
    {
      displayName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
      password: [
        '',
        [Validators.required, Validators.minLength(8), Validators.maxLength(72), Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).+$/)],
      ],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly serverFieldErrors = signal<Record<string, string>>({});
  protected readonly showPassword = signal(false);

  protected get displayName() {
    return this.form.controls.displayName;
  }

  protected get email() {
    return this.form.controls.email;
  }

  protected get password() {
    return this.form.controls.password;
  }

  protected get confirmPassword() {
    return this.form.controls.confirmPassword;
  }

  protected serverFieldError(field: string): string | null {
    return this.serverFieldErrors()[field] ?? null;
  }

  protected clearServerField(field: string): void {
    if (this.serverFieldErrors()[field]) {
      this.serverFieldErrors.update(({ [field]: _removed, ...rest }) => rest);
    }
  }

  protected submit(): void {
    this.serverError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { displayName, email, password } = this.form.getRawValue();
    this.auth.register({ displayName, email, password }).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (error: unknown) => {
        this.submitting.set(false);
        if (error instanceof ApiException && Object.keys(error.fieldErrors).length > 0) {
          const fields: Record<string, string> = {};
          for (const [field, messages] of Object.entries(error.fieldErrors)) fields[field] = messages[0];
          this.serverFieldErrors.set(fields);
          // Fields the form has no inline slot for (e.g. theme) still surface as a banner.
          if (Object.keys(fields).some((f) => f !== 'email')) this.serverError.set(Object.values(fields).join(' '));
        } else {
          this.serverError.set(errorMessage(error, 'Could not create your account. Please try again.'));
        }
      },
    });
  }
}
