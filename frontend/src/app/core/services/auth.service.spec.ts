import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthResponse } from '../models/api.models';
import { AuthService } from './auth.service';
import { LayoutService } from './layout.service';
import { PreferencesSyncService } from './preferences-sync.service';
import { ThemeService } from './theme.service';
import { ToastService } from './toast.service';
import { TokenStorage } from './token-storage.service';

const api = environment.apiUrl;

const response: AuthResponse = {
  accessToken: 'jwt-token',
  expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  user: { id: 1, email: 'ada@example.com', displayName: 'Ada' },
  preferences: { theme: 'ocean', sidebarCollapsed: true },
};

describe('AuthService', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.fn>;
  const theme = { hydrateFromServer: vi.fn(), current: vi.fn().mockReturnValue('forest') };
  const layout = { hydrateFromServer: vi.fn() };
  const sync = { setBaseline: vi.fn(), push: vi.fn() };

  function setup(): AuthService {
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate, url: '/all' } },
        { provide: ThemeService, useValue: theme },
        { provide: LayoutService, useValue: layout },
        { provide: PreferencesSyncService, useValue: sync },
        ToastService,
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  afterEach(() => http.verify());

  it('login stores the token, exposes the user and applies the server preferences', () => {
    const auth = setup();

    auth.login('ada@example.com', 'Passw0rd1').subscribe();
    const req = http.expectOne(`${api}/auth/login`);
    expect(req.request.body).toEqual({ email: 'ada@example.com', password: 'Passw0rd1' });
    req.flush(response);

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.user()?.displayName).toBe('Ada');
    expect(TestBed.inject(TokenStorage).token()).toBe('jwt-token');
    expect(theme.hydrateFromServer).toHaveBeenCalledWith('ocean');
    expect(layout.hydrateFromServer).toHaveBeenCalledWith(true);
    expect(sync.setBaseline).toHaveBeenCalledWith(response.preferences);
  });

  it("register sends the visitor's current theme so the new account starts with it", () => {
    const auth = setup();

    auth.register({ email: 'ada@example.com', password: 'Passw0rd1', displayName: 'Ada' }).subscribe();

    const req = http.expectOne(`${api}/auth/register`);
    expect(req.request.body.theme).toBe('forest');
    req.flush(response);
  });

  it('restoreSession does nothing without a token', async () => {
    const auth = setup();

    await auth.restoreSession();

    http.expectNone(`${api}/auth/me`);
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('restoreSession loads the user for a stored token', async () => {
    localStorage.setItem('todo.token', 'stored');
    const auth = setup();

    const done = auth.restoreSession();
    http.expectOne(`${api}/auth/me`).flush({ user: response.user, preferences: response.preferences });
    await done;

    expect(auth.user()?.email).toBe('ada@example.com');
  });

  it('restoreSession discards a token the server rejects', async () => {
    localStorage.setItem('todo.token', 'expired');
    const auth = setup();

    const done = auth.restoreSession();
    http.expectOne(`${api}/auth/me`).flush({ message: 'nope' }, { status: 401, statusText: 'Unauthorized' });
    await done;

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('todo.token')).toBeNull();
  });

  it('logout clears the session and goes to the login page', () => {
    const auth = setup();
    auth.login('a@b.co', 'x').subscribe();
    http.expectOne(`${api}/auth/login`).flush(response);

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('an expired session signs the user out, explains why and remembers where they were', () => {
    const auth = setup();
    auth.login('a@b.co', 'x').subscribe();
    http.expectOne(`${api}/auth/login`).flush(response);

    auth.handleSessionExpired();

    expect(auth.isAuthenticated()).toBe(false);
    expect(TestBed.inject(ToastService).toasts()[0].message).toMatch(/expired/i);
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/all' } });
  });
});
