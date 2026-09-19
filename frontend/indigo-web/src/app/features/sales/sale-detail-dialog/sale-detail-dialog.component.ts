import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Sale, SaleDetail } from '../../../core/models/sale.model';
import { SaleService } from '../../../core/services/sale.service';
import { problemMessage } from '../../../shared/problem-details.util';

export interface SaleDetailData {
  venta: Sale;
}

@Component({
  selector: 'app-sale-detail-dialog',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './sale-detail-dialog.component.html',
  styleUrl: './sale-detail-dialog.component.scss',
})
export class SaleDetailDialogComponent {
  private readonly sales = inject(SaleService);
  private readonly ref = inject<MatDialogRef<SaleDetailDialogComponent>>(MatDialogRef);

  readonly data = inject<SaleDetailData>(MAT_DIALOG_DATA);

  readonly columnas = ['producto', 'cantidad', 'precioUnitario', 'subtotal'];

  readonly detalle = signal<SaleDetail | null>(null);
  readonly cargando = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor() {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.errorMessage.set(null);
    this.sales.obtener(this.data.venta.id).subscribe({
      next: (detalle) => {
        this.detalle.set(detalle);
        this.cargando.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.cargando.set(false);
        this.errorMessage.set(problemMessage(error, 'No pudimos cargar el detalle de la venta.'));
      },
    });
  }

  cerrar(): void {
    this.ref.close();
  }
}
