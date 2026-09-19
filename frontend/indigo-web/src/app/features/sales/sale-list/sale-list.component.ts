import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { PagedResult } from '../../../core/models/paged-result.model';
import { Sale } from '../../../core/models/sale.model';
import { SaleService } from '../../../core/services/sale.service';
import { problemMessage } from '../../../shared/problem-details.util';
import { SaleDetailDialogComponent } from '../sale-detail-dialog/sale-detail-dialog.component';

@Component({
  selector: 'app-sale-list',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './sale-list.component.html',
  styleUrl: './sale-list.component.scss',
})
export class SaleListComponent {
  private readonly sales = inject(SaleService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly esAdmin = this.auth.isAdmin;
  readonly opcionesPagina = [5, 10, 25];

  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);

  readonly resultado = signal<PagedResult<Sale> | null>(null);
  readonly cargando = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly ventas = computed(() => this.resultado()?.items ?? []);
  readonly totalItems = computed(() => this.resultado()?.totalItems ?? 0);

  readonly columnas = computed(() =>
    this.esAdmin()
      ? ['fecha', 'usuario', 'cantidadItems', 'total', 'acciones']
      : ['fecha', 'cantidadItems', 'total', 'acciones'],
  );

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.errorMessage.set(null);
    this.sales
      .listar({ page: this.pageIndex() + 1, pageSize: this.pageSize() })
      .subscribe({
        next: (resultado) => {
          this.resultado.set(resultado);
          this.cargando.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.cargando.set(false);
          this.errorMessage.set(problemMessage(error, 'No pudimos cargar las ventas.'));
        },
      });
  }

  cambiarPagina(evento: PageEvent): void {
    this.pageIndex.set(evento.pageIndex);
    this.pageSize.set(evento.pageSize);
    this.cargar();
  }

  verDetalle(venta: Sale): void {
    this.dialog.open(SaleDetailDialogComponent, {
      data: { venta },
      width: '640px',
      maxWidth: '92vw',
      autoFocus: false,
    });
  }
}
