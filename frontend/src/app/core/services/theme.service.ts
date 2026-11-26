import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { ThemeName } from '../models/api.models';
import { PreferencesSyncService } from './preferences-sync.service';

export interface ThemeOption {
  id: ThemeName;
  label: string;
  description: string;
}

/** Order matters: it is the order shown in the picker. Palettes live in styles/_themes.scss. */
export const THEME_OPTIONS: readonly ThemeOption[] = [
  { id: 'light', label: 'Light', description: 'Clean and bright' },
  { id: 'dark', label: 'Dark', description: 'Easy on the eyes at night' },
  { id: 'ocean', label: 'Ocean', description: 'Calm blues' },
  { id: 'forest', label: 'Forest', description: 'Fresh greens' },
  { id: 'sunset', label: 'Sunset', description: 'Warm orange and pink' },
];

const STORAGE_KEY = 'todo.theme';

function isThemeName(value: unknown): value is ThemeName {
  return THEME_OPTIONS.some((t) => t.id === value);
}

/**
 * Runtime theme switching via a `data-theme` attribute on <html>; no reload needed.
 *
 * Resolution order on startup: saved choice (localStorage) -> OS preference (prefers-color-scheme).
 * After login the server copy wins (see hydrateFromServer). Explicit choices are saved to
 * localStorage immediately and to the backend (debounced) when signed in.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly sync = inject(PreferencesSyncService);

  private readonly _theme = signal<ThemeName>('light');
  readonly theme = this._theme.asReadonly();
  readonly options = THEME_OPTIONS;

  private hasExplicitChoice = false;

  constructor() {
    const saved = this.readSaved();
    this.hasExplicitChoice = saved !== null;
    this.apply(saved ?? this.osPreference(), false);

    // Until the user picks a theme themselves, keep following the OS light/dark setting.
    this.doc.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').addEventListener?.('change', () => {
      if (!this.hasExplicitChoice) this.apply(this.osPreference(), false);
    });
  }

  /** User picked a theme: apply, remember locally and sync to the backend. */
  set(theme: ThemeName): void {
    if (!isThemeName(theme)) return;
    this.hasExplicitChoice = true;
    this.apply(theme, true);
    this.save(theme);
    this.sync.push({ theme });
  }

  /** Server preference loaded after sign-in: apply it without echoing it back to the server. */
  hydrateFromServer(theme: ThemeName): void {
    if (!isThemeName(theme)) return;
    this.hasExplicitChoice = true;
    this.apply(theme, false);
    this.save(theme);
  }

  /** Current theme as seen by the register form so a new account starts with the visitor's choice. */
  current(): ThemeName {
    return this._theme();
  }

  private apply(theme: ThemeName, animate: boolean): void {
    const root = this.doc.documentElement;
    if (animate) {
      root.classList.add('theme-transition');
      setTimeout(() => root.classList.remove('theme-transition'), 350);
    }
    root.setAttribute('data-theme', theme);
    this._theme.set(theme);
  }

  private osPreference(): ThemeName {
    const prefersDark = this.doc.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    return prefersDark ? 'dark' : 'light';
  }

  private readSaved(): ThemeName | null {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return isThemeName(value) ? value : null;
    } catch {
      return null;
    }
  }

  private save(theme: ThemeName): void {
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      /* ignore */
    }
  }
}
