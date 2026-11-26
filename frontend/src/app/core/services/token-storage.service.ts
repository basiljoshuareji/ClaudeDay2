import { Injectable, signal } from '@angular/core';

const TOKEN_KEY = 'todo.token';
const EXPIRES_KEY = 'todo.tokenExpiresAt';

/**
 * Keeps the JWT in localStorage so a page reload keeps the user signed in.
 * (Trade-off: localStorage is readable by injected scripts; a strict CSP is the mitigation.
 * For a hardened deployment switch to an httpOnly cookie issued by the API.)
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  private readonly _token = signal<string | null>(this.read());

  /** Reactive: components can depend on whether a token exists. */
  readonly token = this._token.asReadonly();

  set(token: string, expiresAt: string): void {
    this._token.set(token);
    this.write(TOKEN_KEY, token);
    this.write(EXPIRES_KEY, expiresAt);
  }

  clear(): void {
    this._token.set(null);
    this.remove(TOKEN_KEY);
    this.remove(EXPIRES_KEY);
  }

  private read(): string | null {
    try {
      const token = localStorage.getItem(TOKEN_KEY);
      const expires = localStorage.getItem(EXPIRES_KEY);
      if (!token) return null;
      if (expires && Date.parse(expires) <= Date.now()) {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(EXPIRES_KEY);
        return null;
      }
      return token;
    } catch {
      return null;
    }
  }

  private write(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* storage unavailable (private mode): token lives in memory only */
    }
  }

  private remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      /* ignore */
    }
  }
}
