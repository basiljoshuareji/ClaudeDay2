import { Injectable, inject, signal } from '@angular/core';
import { PreferencesSyncService } from './preferences-sync.service';

const KEY = 'todo.sidebarCollapsed';

/** Sidebar state: collapsed (desktop icon rail) and open (mobile drawer). */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly sync = inject(PreferencesSyncService);

  private readonly _collapsed = signal(this.read());
  private readonly _mobileOpen = signal(false);

  readonly collapsed = this._collapsed.asReadonly();
  readonly mobileOpen = this._mobileOpen.asReadonly();

  toggleCollapsed(): void {
    this.setCollapsed(!this._collapsed());
    this.sync.push({ sidebarCollapsed: this._collapsed() });
  }

  hydrateFromServer(collapsed: boolean): void {
    this.setCollapsed(collapsed);
  }

  openMobile(): void {
    this._mobileOpen.set(true);
  }

  closeMobile(): void {
    this._mobileOpen.set(false);
  }

  private setCollapsed(value: boolean): void {
    this._collapsed.set(value);
    try {
      localStorage.setItem(KEY, String(value));
    } catch {
      /* ignore */
    }
  }

  private read(): boolean {
    try {
      return localStorage.getItem(KEY) === 'true';
    } catch {
      return false;
    }
  }
}
