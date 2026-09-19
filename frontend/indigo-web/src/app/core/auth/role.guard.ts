import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Rol } from '../models/auth.model';
import { AuthService } from './auth.service';

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const requeridos = (route.data['roles'] as Rol[] | undefined) ?? [];

  if (requeridos.length === 0 || requeridos.some((rol) => auth.hasRole(rol))) {
    return true;
  }

  inject(MatSnackBar).open('Tu rol no tiene acceso a esa pantalla.', 'Cerrar', { duration: 4000 });

  return router.createUrlTree(['/productos']);
};
