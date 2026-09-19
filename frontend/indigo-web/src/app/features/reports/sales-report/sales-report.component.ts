import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerInputEvent, MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/auth/auth.service';
import { SalesReport } from '../../../core/models/report.model';
import { ReportService } from '../../../core/services/report.service';
import { problemMessage } from '../../../shared/problem-details.util';
import { provideAdaptadorFechaDdMmYyyy } from './adaptador-fecha-dd-mm-yyyy';

const DIAS_POR_DEFECTO = 7;

function aFechaApi(fecha: Date): string {
  const mes = `${fecha.getMonth() + 1}`.padStart(2, '0');
  const dia = `${fecha.getDate()}`.padStart(2, '0');
  return `${fecha.getFullYear()}-${mes}-${dia}`;
}

function haceDias(dias: number): Date {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() - dias);
  return fecha;
}

@Component({
  selector: 'app-sales-report',
  standalone: true,
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatTableModule,
  ],
  providers: [provideAdaptadorFechaDdMmYyyy()],
  templateUrl: './sales-report.component.html',
  styleUrl: './sales-report.component.scss',
})
export class SalesReportComponent {
  private readonly reports = inject(ReportService);
  private readonly auth = inject(AuthService);

  readonly esAdmin = this.auth.isAdmin;

  readonly columnas = ['fecha', 'ventas', 'items', 'total'];

  readonly hoy = new Date();
  readonly desde = signal<Date | null>(haceDias(DIAS_POR_DEFECTO - 1));
  readonly hasta = signal<Date | null>(new Date());

  readonly reporte = signal<SalesReport | null>(null);
  readonly cargando = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly dias = computed(() =>
    [...(this.reporte()?.porDia ?? [])].sort((a, b) => a.fecha.localeCompare(b.fecha)),
  );

  readonly rangoInvalido = computed(() => {
    const desde = this.desde();
    const hasta = this.hasta();
    return !!desde && !!hasta && aFechaApi(desde) > aFechaApi(hasta);
  });

  readonly puedeConsultar = computed(
    () => !!this.desde() && !!this.hasta() && !this.rangoInvalido() && !this.cargando(),
  );

  constructor() {
    this.consultar();
  }

  cambiarDesde(evento: MatDatepickerInputEvent<Date>): void {
    this.desde.set(evento.value);
  }

  cambiarHasta(evento: MatDatepickerInputEvent<Date>): void {
    this.hasta.set(evento.value);
  }

  aplicarUltimosDias(dias: number): void {
    this.desde.set(haceDias(dias - 1));
    this.hasta.set(new Date());
    this.consultar();
  }

  aplicarMesActual(): void {
    const hoy = new Date();
    this.desde.set(new Date(hoy.getFullYear(), hoy.getMonth(), 1));
    this.hasta.set(hoy);
    this.consultar();
  }

  consultar(): void {
    const desde = this.desde();
    const hasta = this.hasta();
    if (!desde || !hasta || this.rangoInvalido()) {
      return;
    }

    this.cargando.set(true);
    this.errorMessage.set(null);

    this.reports.obtenerVentas({ from: aFechaApi(desde), to: aFechaApi(hasta) }).subscribe({
      next: (reporte) => {
        this.reporte.set(reporte);
        this.cargando.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.cargando.set(false);
        this.reporte.set(null);
        this.errorMessage.set(problemMessage(error, 'No pudimos generar el reporte.'));
      },
    });
  }

  formatearDia(iso: string): string {
    const [anio, mes, dia] = iso.slice(0, 10).split('-');
    return `${dia}/${mes}/${anio}`;
  }
}
