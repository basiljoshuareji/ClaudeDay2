import { ErrorHandler, Injectable, inject } from '@angular/core';
import { ToastService } from '../services/toast.service';
import { ApiException } from './api-exception';

/**
 * Last line of defence for errors nobody caught. HTTP failures are already surfaced by the interceptor or the
 * calling component, so they are only logged here; anything else gets a friendly toast.
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly toast = inject(ToastService);

  handleError(error: unknown): void {
    console.error('Unhandled error:', error);
    if (error instanceof ApiException) return;
    this.toast.error('Something unexpected happened. If it keeps happening, reload the page.');
  }
}
