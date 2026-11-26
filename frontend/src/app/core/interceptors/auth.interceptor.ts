import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { TokenStorage } from '../services/token-storage.service';

/** Adds the JWT to requests for our own API (never to third-party URLs). */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(TokenStorage).token();
  if (!token || !req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
