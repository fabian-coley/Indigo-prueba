import { Injectable, Provider } from '@angular/core';
import {
  DateAdapter,
  MAT_DATE_FORMATS,
  MAT_NATIVE_DATE_FORMATS,
  MatDateFormats,
  NativeDateAdapter,
} from '@angular/material/core';

export const FORMATO_FECHA_ENTRADA = 'DD/MM/YYYY';

const ENTRADA_DD_MM_YYYY = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;

@Injectable()
export class AdaptadorFechaDdMmYyyy extends NativeDateAdapter {
  override parse(value: unknown, _parseFormat: unknown): Date | null {
    if (typeof value === 'number') {
      return new Date(value);
    }
    if (typeof value !== 'string') {
      return null;
    }

    const partes = value.trim().match(ENTRADA_DD_MM_YYYY);
    if (!partes) {
      return null;
    }

    const dia = Number(partes[1]);
    const mes = Number(partes[2]);
    const anio = Number(partes[3]);
    const fecha = new Date(anio, mes - 1, dia);

    if (fecha.getFullYear() !== anio || fecha.getMonth() !== mes - 1 || fecha.getDate() !== dia) {
      return null;
    }
    return fecha;
  }

  override format(date: Date, displayFormat: object): string {
    if ((displayFormat as unknown) !== FORMATO_FECHA_ENTRADA) {
      return super.format(date, displayFormat);
    }

    const dia = `${date.getDate()}`.padStart(2, '0');
    const mes = `${date.getMonth() + 1}`.padStart(2, '0');
    return `${dia}/${mes}/${date.getFullYear()}`;
  }
}

const FORMATOS_FECHA_DD_MM_YYYY: MatDateFormats = {
  ...MAT_NATIVE_DATE_FORMATS,
  parse: { ...MAT_NATIVE_DATE_FORMATS.parse, dateInput: FORMATO_FECHA_ENTRADA },
  display: { ...MAT_NATIVE_DATE_FORMATS.display, dateInput: FORMATO_FECHA_ENTRADA },
};

export function provideAdaptadorFechaDdMmYyyy(): Provider[] {
  return [
    { provide: DateAdapter, useClass: AdaptadorFechaDdMmYyyy },
    { provide: MAT_DATE_FORMATS, useValue: FORMATOS_FECHA_DD_MM_YYYY },
  ];
}
