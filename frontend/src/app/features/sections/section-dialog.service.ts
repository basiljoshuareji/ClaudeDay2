import { Injectable, signal } from '@angular/core';
import { Section } from '../../core/models/api.models';

export type SectionDialogState = { mode: 'create' } | { mode: 'edit'; section: Section };

/** Lets any component open the (single) create/edit section dialog hosted by the shell. */
@Injectable({ providedIn: 'root' })
export class SectionDialogService {
  private readonly _state = signal<SectionDialogState | null>(null);
  readonly state = this._state.asReadonly();

  openCreate(): void {
    this._state.set({ mode: 'create' });
  }

  openEdit(section: Section): void {
    this._state.set({ mode: 'edit', section });
  }

  close(): void {
    this._state.set(null);
  }
}
