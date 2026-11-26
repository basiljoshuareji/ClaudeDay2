import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiException } from '../../core/errors/api-exception';
import { AuthResponse } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { RegisterComponent, passwordsMatch } from './register.component';

describe('passwordsMatch validator', () => {
  const group = (password: string, confirmPassword: string) =>
    new FormGroup({ password: new FormControl(password), confirmPassword: new FormControl(confirmPassword) });

  it('passes when equal or when the confirmation is still empty', () => {
    expect(passwordsMatch(group('abc12345', 'abc12345'))).toBeNull();
    expect(passwordsMatch(group('abc12345', ''))).toBeNull();
  });

  it('reports a mismatch', () => {
    expect(passwordsMatch(group('abc12345', 'abc12346'))).toEqual({ passwordsMismatch: true });
  });
});

describe('RegisterComponent', () => {
  let fixture: ComponentFixture<RegisterComponent>;
  let el: HTMLElement;
  const auth = { register: vi.fn() };

  const type = (selector: string, value: string) => {
    const input = el.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
  };
  const fill = (password = 'Passw0rd1', confirm = password) => {
    type('#displayName', 'Ada');
    type('#email', 'ada@example.com');
    type('#password', password);
    type('#confirmPassword', confirm);
  };
  const submit = async () => {
    el.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    await fixture.whenStable();
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: auth }],
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    el = fixture.nativeElement;
    await fixture.whenStable();
  });

  it('requires every field', async () => {
    await submit();

    const text = el.textContent!;
    expect(text).toContain('Tell us what to call you.');
    expect(text).toContain('Enter your email address.');
    expect(text).toContain('Choose a password.');
    expect(text).toContain('Re-enter your password.');
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('enforces the password rules (length, letter and number)', async () => {
    fill('short1');
    await submit();
    expect(el.textContent).toContain('Use at least 8 characters.');

    fill('onlyletters');
    await submit();
    expect(el.textContent).toContain('Include at least one letter and one number.');
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('catches mismatched passwords', async () => {
    fill('Passw0rd1', 'Passw0rd2');
    await submit();

    expect(el.textContent).toContain("The passwords don't match.");
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('registers with trimmed-down payload (no confirmation field)', async () => {
    auth.register.mockReturnValue(of({} as AuthResponse));
    fill();

    await submit();

    expect(auth.register).toHaveBeenCalledWith({ displayName: 'Ada', email: 'ada@example.com', password: 'Passw0rd1' });
  });

  it('shows a server-side "email already used" error next to the email field', async () => {
    auth.register.mockReturnValue(
      throwError(() => new ApiException(409, 'conflict', 'An account with this email already exists.', { email: ['An account with this email already exists.'] })),
    );
    fill();

    await submit();

    expect(el.querySelector('#email-error')!.textContent).toContain('already exists');
    expect(el.querySelector('#email')!.getAttribute('aria-invalid')).toBe('true');
  });
});
