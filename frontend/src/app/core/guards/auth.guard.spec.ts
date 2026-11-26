import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authGuard, guestGuard } from './auth.guard';

describe('route guards', () => {
  const authenticated = signal(false);

  beforeEach(() => {
    authenticated.set(false);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isAuthenticated: authenticated } }],
    });
  });

  const run = (guard: typeof authGuard, url = '/sections/3') =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));

  it('authGuard lets signed-in users through', () => {
    authenticated.set(true);
    expect(run(authGuard)).toBe(true);
  });

  it('authGuard sends visitors to /login and remembers the page they wanted', () => {
    const result = run(authGuard) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Fsections%2F3');
  });

  it('guestGuard keeps signed-in users out of the login page', () => {
    authenticated.set(true);
    const result = run(guestGuard) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/');
  });

  it('guestGuard allows anonymous visitors', () => {
    expect(run(guestGuard)).toBe(true);
  });
});
