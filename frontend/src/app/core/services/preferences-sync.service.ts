import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Preferences } from '../models/api.models';
import { TokenStorage } from './token-storage.service';

/**
 * Persists user preferences (theme, sidebar state) to the backend.
 * Writes are debounced and silently skipped when signed out, in which case localStorage is the only store.
 */
@Injectable({ providedIn: 'root' })
export class PreferencesSyncService {
  private readonly http = inject(HttpClient);
  private readonly tokens = inject(TokenStorage);

  private latest: Preferences = { theme: 'light', sidebarCollapsed: false };
  private timer: ReturnType<typeof setTimeout> | undefined;

  /** Called after login with the server's copy so later patches merge on top of it. */
  setBaseline(preferences: Preferences): void {
    this.latest = { ...preferences };
  }

  push(patch: Partial<Preferences>): void {
    this.latest = { ...this.latest, ...patch };
    if (!this.tokens.token()) return;

    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.http.put<Preferences>(`${environment.apiUrl}/preferences`, this.latest).subscribe({
        error: () => {
          /* non-critical: the local copy still applies */
        },
      });
    }, 300);
  }
}
