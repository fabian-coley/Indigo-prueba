import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { isValidationProblem, problemMessage } from '../../shared/problem-details.util';
import { AuthService } from './auth.service';
import { MANEJO_LOCAL_DE_ERRORES } from './error-handling.token';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);
  const isAuthEndpoint = req.url.includes('/auth/');
  const manejaLaPantalla = req.context.get(MANEJO_LOCAL_DE_ERRORES);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && !isAuthEndpoint) {
        if (error.status === 401) {
          auth.logout();
          void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
        } else if (!manejaLaPantalla && error.status !== 404 && !isValidationProblem(error)) {
          snackBar.open(problemMessage(error), 'Cerrar', { duration: 5000 });
        }
      }
      return throwError(() => error);
    })
  );
};
