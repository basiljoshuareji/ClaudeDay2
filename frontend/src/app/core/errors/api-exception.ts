import { HttpErrorResponse } from '@angular/common/http';
import { ApiErrorBody } from '../models/api.models';

/** Normalised error thrown by every HTTP call (see errorInterceptor). */
export class ApiException extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    readonly fieldErrors: Record<string, string[]> = {},
    readonly traceId?: string,
  ) {
    super(message);
    this.name = 'ApiException';
  }

  /** First server-side validation message for a field (camelCase key), if any. */
  fieldError(field: string): string | null {
    return this.fieldErrors[field]?.[0] ?? null;
  }

  get isValidation(): boolean {
    return this.status === 400 || this.status === 409;
  }
}

export function toApiException(error: HttpErrorResponse): ApiException {
  if (error.status === 0) {
    return new ApiException(0, 'network_error', 'Cannot reach the server. Check your connection and try again.');
  }

  const body = error.error as Partial<ApiErrorBody> | string | null;
  if (body && typeof body === 'object' && typeof body.message === 'string') {
    return new ApiException(
      error.status,
      body.code ?? 'error',
      body.message,
      body.errors ?? {},
      body.traceId ?? undefined,
    );
  }

  const fallback =
    error.status >= 500 ? 'The server had a problem. Please try again.' : 'The request could not be completed.';
  return new ApiException(error.status, 'error', fallback);
}

export function errorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  return error instanceof ApiException || error instanceof Error ? error.message || fallback : fallback;
}
