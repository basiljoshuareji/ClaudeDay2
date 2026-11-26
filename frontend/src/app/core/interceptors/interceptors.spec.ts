import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ApiException } from '../errors/api-exception';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';
import { TokenStorage } from '../services/token-storage.service';
import { authInterceptor } from './auth.interceptor';
import { errorInterceptor } from './error.interceptor';

const api = environment.apiUrl;

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  const auth = { handleSessionExpired: vi.fn() };

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  describe('authInterceptor', () => {
    it('adds the bearer token to API requests', () => {
      TestBed.inject(TokenStorage).set('abc', new Date(Date.now() + 60_000).toISOString());

      http.get(`${api}/tasks`).subscribe();

      expect(controller.expectOne(`${api}/tasks`).request.headers.get('Authorization')).toBe('Bearer abc');
    });

    it('sends no header when signed out', () => {
      http.get(`${api}/tasks`).subscribe();

      expect(controller.expectOne(`${api}/tasks`).request.headers.has('Authorization')).toBe(false);
    });

    it('never leaks the token to third-party URLs', () => {
      TestBed.inject(TokenStorage).set('abc', new Date(Date.now() + 60_000).toISOString());

      http.get('https://example.org/data').subscribe();

      expect(controller.expectOne('https://example.org/data').request.headers.has('Authorization')).toBe(false);
    });
  });

  describe('errorInterceptor', () => {
    it('maps validation errors to an ApiException carrying field messages', () => {
      let caught: unknown;
      http.post(`${api}/tasks`, {}).subscribe({ error: (e) => (caught = e) });

      controller.expectOne(`${api}/tasks`).flush(
        { status: 400, code: 'validation_failed', message: 'Invalid', errors: { title: ['Title is required.'] }, traceId: 't1' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(caught).toBeInstanceOf(ApiException);
      const error = caught as ApiException;
      expect(error.status).toBe(400);
      expect(error.fieldError('title')).toBe('Title is required.');
      expect(error.traceId).toBe('t1');
      expect(auth.handleSessionExpired).not.toHaveBeenCalled();
    });

    it('signs the user out on 401 from a protected endpoint', () => {
      http.get(`${api}/tasks`).subscribe({ error: () => undefined });

      controller.expectOne(`${api}/tasks`).flush({ message: 'expired', code: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });

      expect(auth.handleSessionExpired).toHaveBeenCalledOnce();
    });

    it('does not treat a failed login (401) as an expired session', () => {
      http.post(`${api}/auth/login`, {}).subscribe({ error: () => undefined });

      controller.expectOne(`${api}/auth/login`).flush({ message: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });

      expect(auth.handleSessionExpired).not.toHaveBeenCalled();
    });

    it('shows one toast for server failures and for network failures', () => {
      const toast = TestBed.inject(ToastService);
      http.get(`${api}/tasks`).subscribe({ error: () => undefined });
      controller.expectOne(`${api}/tasks`).flush('boom', { status: 500, statusText: 'Server Error' });
      expect(toast.toasts()).toHaveLength(1);

      http.get(`${api}/sections`).subscribe({ error: () => undefined });
      controller.expectOne(`${api}/sections`).error(new ProgressEvent('error'));
      expect(toast.toasts()).toHaveLength(2);
      expect(toast.toasts()[1].message).toMatch(/reach the server/i);
    });
  });
});
