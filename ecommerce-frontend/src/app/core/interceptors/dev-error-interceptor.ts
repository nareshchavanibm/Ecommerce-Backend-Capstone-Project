import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, isDevMode } from '@angular/core';
import { tap } from 'rxjs';

import { ToastService } from '../../shared/ui/toast/toast.service';

const DEDUPE_MS = 3000;

const lastShown = new Map<string, number>();

function hint(status: number): string {

  switch (status) {
    case 0: return 'No response. Is the API running, reachable, and allowing this origin (CORS)?';
    case 401: return 'Not authenticated. Log in again or check the gateway route policy.';
    case 403: return 'Forbidden. This account lacks the required role.';
    case 404: return 'Not found. Check the route, gateway path transform and ids.';
    case 502:
    case 503:
    case 504: return 'A backend service behind the gateway is down or unreachable.';
    default: return '';
  }
}

// Development aid: makes failing API calls visible so frontend/backend mismatches are easy to spot.
export const devErrorInterceptor: HttpInterceptorFn = (req, next) => {

  const toast = inject(ToastService);

  return next(req).pipe(
    tap({
      error: (error: unknown) => {

        if (!isDevMode() || !(error instanceof HttpErrorResponse)) {
          return;
        }

        const path = req.url.replace(/^https?:\/\/[^/]+/, '');

        // A product without a stock record is a normal state; the pages handle it.
        if (error.status === 404 && req.method === 'GET' && /^\/api\/inventory\/\d+$/.test(path)) {
          return;
        }

        const key = `${req.method} ${path} ${error.status}`;
        const now = Date.now();

        if (now - (lastShown.get(key) ?? 0) < DEDUPE_MS) {
          return;
        }

        lastShown.set(key, now);

        const serverMessage: string | undefined = error.error?.message ?? error.error?.title;
        const detail = serverMessage ?? hint(error.status);

        console.error(`[API] ${req.method} ${req.url} failed`, error);

        toast.show(
          `[dev] ${req.method} ${path} \u2192 ${error.status || 'no response'}${detail ? ': ' + detail : ''}`,
          'error',
          9000
        );
      }
    })
  );
};
