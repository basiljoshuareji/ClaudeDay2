import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { ApiException } from '../../core/errors/api-exception';
import { AuthResponse } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let el: HTMLElement;
  const auth = { login: vi.fn() };

  const text = () => el.textContent ?? '';
  const type = (selector: string, value: string) => {
    const input = el.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
  };
  const submit = async () => {
    el.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: auth }],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    el = fixture.nativeElement;
    await fixture.whenStable();
  });

  it('shows inline errors and does not call the API when the form is empty', async () => {
    await submit();

    expect(text()).toContain('Enter your email address.');
    expect(text()).toContain('Enter your password.');
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('flags a malformed email address', async () => {
    type('#email', 'not-an-email');
    type('#password', 'secret');
    await submit();
    await fixture.whenStable();

    expect(text()).toContain('Enter a valid email address');
    expect(el.querySelector('#email')!.getAttribute('aria-invalid')).toBe('true');
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('signs in with the entered credentials and navigates home', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    auth.login.mockReturnValue(of({} as AuthResponse));
    type('#email', 'ada@example.com');
    type('#password', 'Passw0rd1');

    await submit();

    expect(auth.login).toHaveBeenCalledWith('ada@example.com', 'Passw0rd1');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('shows the server message when the credentials are wrong', async () => {
    auth.login.mockReturnValue(throwError(() => new ApiException(401, 'unauthorized', 'Invalid email or password.')));
    type('#email', 'ada@example.com');
    type('#password', 'wrong-pass1');

    await submit();
    await fixture.whenStable();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Invalid email or password.');
    expect(el.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
  });

  it('disables the button while the request is in flight', async () => {
    const pending = new Subject<AuthResponse>();
    auth.login.mockReturnValue(pending);
    type('#email', 'ada@example.com');
    type('#password', 'Passw0rd1');

    await submit();
    await fixture.whenStable();

    expect(el.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
    expect(text()).toContain('Signing in');
  });

  it('can reveal the password', async () => {
    const password = el.querySelector<HTMLInputElement>('#password')!;
    expect(password.type).toBe('password');

    el.querySelector<HTMLButtonElement>('.password-wrap button')!.click();
    await fixture.whenStable();

    expect(password.type).toBe('text');
  });
});
