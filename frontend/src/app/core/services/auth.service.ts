import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, firstValueFrom, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, RegisterPayload, Session, User } from '../models/api.models';
import { LayoutService } from './layout.service';
import { PreferencesSyncService } from './preferences-sync.service';
import { ThemeService } from './theme.service';
import { ToastService } from './toast.service';
import { TokenStorage } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokens = inject(TokenStorage);
  private readonly router = inject(Router);
  private readonly theme = inject(ThemeService);
  private readonly layout = inject(LayoutService);
  private readonly sync = inject(PreferencesSyncService);
  private readonly toast = inject(ToastService);

  private readonly _user = signal<User | null>(null);

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this.tokens.token() !== null);

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap((response) => this.startSession(response)));
  }

  register(payload: Omit<RegisterPayload, 'theme'>): Observable<AuthResponse> {
    // New accounts start with whatever theme the visitor already picked (or their OS preference).
    const body: RegisterPayload = { ...payload, theme: this.theme.current() };
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, body)
      .pipe(tap((response) => this.startSession(response)));
  }

  /** Runs at app start: if a token is stored, validate it and load the user and their preferences. */
  restoreSession(): Promise<void> {
    if (!this.tokens.token()) return Promise.resolve();

    return firstValueFrom(
      this.http.get<Session>(`${environment.apiUrl}/auth/me`).pipe(
        tap((session) => this.applySession(session)),
        map(() => undefined),
        catchError(() => {
          this.tokens.clear();
          this._user.set(null);
          return of(undefined);
        }),
      ),
    );
  }

  logout(): void {
    this.tokens.clear();
    this._user.set(null);
    void this.router.navigate(['/login']);
  }

  /** Called by the error interceptor when the API rejects our token. */
  handleSessionExpired(): void {
    if (!this.isAuthenticated()) return;
    const returnUrl = this.router.url;
    this.tokens.clear();
    this._user.set(null);
    this.toast.info('Your session has expired. Please sign in again.');
    void this.router.navigate(['/login'], { queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : {} });
  }

  private startSession(response: AuthResponse): void {
    this.tokens.set(response.accessToken, response.expiresAt);
    this.applySession(response);
  }

  private applySession(session: Session): void {
    this._user.set(session.user);
    this.sync.setBaseline(session.preferences);
    this.theme.hydrateFromServer(session.preferences.theme);
    this.layout.hydrateFromServer(session.preferences.sidebarCollapsed);
  }
}
