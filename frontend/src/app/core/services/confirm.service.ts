import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Styles the confirm button as destructive. */
  danger?: boolean;
}

export interface ConfirmRequest extends ConfirmOptions {
  resolve: (confirmed: boolean) => void;
}

/** Promise-based confirmation dialog, rendered once by <app-confirm-dialog> in the shell. */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly _request = signal<ConfirmRequest | null>(null);
  readonly request = this._request.asReadonly();

  confirm(options: ConfirmOptions): Promise<boolean> {
    // A new request cancels any dialog that is still open.
    this._request()?.resolve(false);
    return new Promise<boolean>((resolve) => this._request.set({ ...options, resolve }));
  }

  answer(confirmed: boolean): void {
    const current = this._request();
    this._request.set(null);
    current?.resolve(confirmed);
  }
}
