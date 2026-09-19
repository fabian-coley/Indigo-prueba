import { HttpContext, HttpContextToken } from '@angular/common/http';

export const MANEJO_LOCAL_DE_ERRORES = new HttpContextToken<boolean>(() => false);

export const CONTEXTO_ERROR_LOCAL = new HttpContext().set(MANEJO_LOCAL_DE_ERRORES, true);
