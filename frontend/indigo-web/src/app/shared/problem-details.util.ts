import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails, ValidationProblemDetails } from '../core/models/problem-details.model';

export function isValidationProblem(
  error: unknown
): error is HttpErrorResponse & { error: ValidationProblemDetails } {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return false;
  }
  const body = error.error as Partial<ValidationProblemDetails> | null;
  return !!body && typeof body.errors === 'object' && body.errors !== null;
}

export function fieldErrors(error: unknown): Record<string, string[]> {
  return isValidationProblem(error) ? error.error.errors : {};
}

export function problemMessage(
  error: unknown,
  fallback = 'Ocurrió un error inesperado. Intentá de nuevo.'
): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as Partial<ProblemDetails> | null;
    if (typeof body?.detail === 'string' && body.detail) {
      return body.detail;
    }
    if (typeof body?.title === 'string' && body.title) {
      return body.title;
    }
    if (error.status === 0) {
      return 'No hay conexión con el servidor.';
    }
    if (error.status === 403) {
      return 'Tu rol no tiene permisos para esta acción.';
    }
    if (error.status >= 500) {
      return 'Error interno del servidor. Intentá más tarde.';
    }
  }
  return fallback;
}
