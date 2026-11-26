import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  message: string;
  type: ToastType;
  actionLabel?: string;
  action?: () => void;
}

export interface ToastOptions {
  type?: ToastType;
  /** Milliseconds before auto-dismiss; 0 keeps it until dismissed. */
  duration?: number;
  actionLabel?: string;
  action?: () => void;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();
  private readonly _toasts = signal<Toast[]>([]);

  readonly toasts = this._toasts.asReadonly();

  show(message: string, options: ToastOptions = {}): number {
    const type = options.type ?? 'info';
    const id = this.nextId++;
    const toast: Toast = { id, message, type, actionLabel: options.actionLabel, action: options.action };

    // Keep the stack short: drop the oldest beyond four.
    this._toasts.update((list) => [...list, toast].slice(-4));

    const duration = options.duration ?? (type === 'error' ? 7000 : 4000);
    if (duration > 0) {
      this.timers.set(
        id,
        setTimeout(() => this.dismiss(id), duration),
      );
    }
    return id;
  }

  success(message: string, options: Omit<ToastOptions, 'type'> = {}): number {
    return this.show(message, { ...options, type: 'success' });
  }

  error(message: string, options: Omit<ToastOptions, 'type'> = {}): number {
    return this.show(message, { ...options, type: 'error' });
  }

  info(message: string, options: Omit<ToastOptions, 'type'> = {}): number {
    return this.show(message, { ...options, type: 'info' });
  }

  dismiss(id: number): void {
    const timer = this.timers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
    this._toasts.update((list) => list.filter((t) => t.id !== id));
  }

  /** Runs the toast's action (e.g. Undo) and closes it. */
  runAction(toast: Toast): void {
    this.dismiss(toast.id);
    toast.action?.();
  }
}
