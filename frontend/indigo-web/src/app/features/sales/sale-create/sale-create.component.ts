import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { PagedResult } from '../../../core/models/paged-result.model';
import { Product } from '../../../core/models/product.model';
import { SaleItemRequest, SaleRequest } from '../../../core/models/sale.model';
import { ProductService } from '../../../core/services/product.service';
import { SaleService } from '../../../core/services/sale.service';
import { problemMessage } from '../../../shared/problem-details.util';

const DEBOUNCE_BUSQUEDA_MS = 300;

interface LineaCarrito {
  producto: Product;
  cantidad: number;
}

@Component({
  selector: 'app-sale-create',
  standalone: true,
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  providers: [CurrencyPipe],
  templateUrl: './sale-create.component.html',
  styleUrl: './sale-create.component.scss',
})
export class SaleCreateComponent {
  private readonly products = inject(ProductService);
  private readonly sales = inject(SaleService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly currency = inject(CurrencyPipe);

  readonly columnasCatalogo = ['nombre', 'precio', 'stock', 'accion'];
  readonly opcionesPagina = [5, 10, 25];

  readonly busqueda = new FormControl('', { nonNullable: true });

  readonly pageIndex = signal(0);
  readonly pageSize = signal(5);

  readonly resultado = signal<PagedResult<Product> | null>(null);
  readonly cargando = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly lineas = signal<LineaCarrito[]>([]);
  readonly registrando = signal(false);

  readonly catalogo = computed(() => this.resultado()?.items ?? []);
  readonly totalCatalogo = computed(() => this.resultado()?.totalItems ?? 0);

  readonly cantidadTotal = computed(() =>
    this.lineas().reduce((acumulado, linea) => acumulado + linea.cantidad, 0),
  );

  readonly total = computed(() =>
    this.lineas().reduce((acumulado, linea) => acumulado + linea.producto.precio * linea.cantidad, 0),
  );

  readonly lineasExcedidas = computed(() =>
    this.lineas().filter((linea) => linea.cantidad > linea.producto.stock),
  );

  readonly hayExceso = computed(() => this.lineasExcedidas().length > 0);

  constructor() {
    this.busqueda.valueChanges
      .pipe(debounceTime(DEBOUNCE_BUSQUEDA_MS), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.pageIndex.set(0);
        this.cargarCatalogo();
      });
  }

  ngOnInit(): void {
    this.cargarCatalogo();
  }

  cargarCatalogo(): void {
    this.cargando.set(true);
    this.errorMessage.set(null);
    this.products
      .listar({
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        search: this.busqueda.value.trim() || undefined,
      })
      .subscribe({
        next: (resultado) => {
          this.resultado.set(resultado);
          this.cargando.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.cargando.set(false);
          this.errorMessage.set(problemMessage(error, 'No pudimos cargar el catálogo.'));
        },
      });
  }

  cambiarPagina(evento: PageEvent): void {
    this.pageIndex.set(evento.pageIndex);
    this.pageSize.set(evento.pageSize);
    this.cargarCatalogo();
  }

  enCarrito(productoId: string): number {
    return this.lineas().find((linea) => linea.producto.id === productoId)?.cantidad ?? 0;
  }

  agregar(producto: Product): void {
    this.lineas.update((lineas) => {
      const existente = lineas.find((linea) => linea.producto.id === producto.id);
      if (existente) {
        return lineas.map((linea) =>
          linea.producto.id === producto.id ? { ...linea, cantidad: linea.cantidad + 1 } : linea,
        );
      }
      return [...lineas, { producto, cantidad: 1 }];
    });
  }

  ajustar(linea: LineaCarrito, delta: number): void {
    this.fijarCantidad(linea, linea.cantidad + delta);
  }

  fijarCantidad(linea: LineaCarrito, valor: string | number): void {
    const cantidad = Math.max(1, Math.trunc(Number(valor)) || 1);
    this.lineas.update((lineas) =>
      lineas.map((actual) =>
        actual.producto.id === linea.producto.id ? { ...actual, cantidad } : actual,
      ),
    );
  }

  quitar(productoId: string): void {
    this.lineas.update((lineas) => lineas.filter((linea) => linea.producto.id !== productoId));
  }

  vaciar(): void {
    this.lineas.set([]);
  }

  registrar(): void {
    if (this.registrando() || this.lineas().length === 0) {
      return;
    }

    const request = this.construirRequest();
    this.registrando.set(true);

    this.sales.crear(request).subscribe({
      next: (venta) => {
        this.registrando.set(false);
        this.snack.open(
          `Venta registrada por ${this.currency.transform(venta.total)}.`,
          'Cerrar',
          { duration: 5000 },
        );
        this.router.navigate(['/ventas']);
      },
      error: (error: HttpErrorResponse) => {
        this.registrando.set(false);
        this.snack.open(problemMessage(error, 'No pudimos registrar la venta.'), 'Cerrar', {
          duration: 8000,
        });
      },
    });
  }

  private construirRequest(): SaleRequest {
    const items: SaleItemRequest[] = this.lineas().map((linea) => ({
      productoId: linea.producto.id,
      cantidad: linea.cantidad,
    }));
    return { items };
  }
}
