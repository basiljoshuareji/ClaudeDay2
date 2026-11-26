import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { toApiException } from '../errors/api-exception';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';

/**
 * Turns every failed HTTP call into an ApiException (consistent shape for components) and handles the
 * cross-cutting cases centrally:
 *  - 401 on a protected call  -> sign the user out and send them to the login page
 *  - network failure / 5xx     -> one generic toast
 * 4xx validation errors are left to the calling component, which knows which field to highlight.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const exception = toApiException(error);
      const isAuthCall = req.url.includes('/auth/');

      if (exception.status === 401 && !isAuthCall) {
        auth.handleSessionExpired();
      } else if (exception.status === 0 || exception.status >= 500) {
        toast.error(exception.message);
      }
      return throwError(() => exception);
    }),
  );
};
