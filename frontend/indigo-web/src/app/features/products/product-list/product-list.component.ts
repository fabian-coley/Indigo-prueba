import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, firstValueFrom } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { PagedResult } from '../../../core/models/paged-result.model';
import { CATEGORIAS_PRODUCTO, CategoriaProducto, Product } from '../../../core/models/product.model';
import { ProductService } from '../../../core/services/product.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { problemMessage } from '../../../shared/problem-details.util';

const DEBOUNCE_BUSQUEDA_MS = 300;

@Component({
  selector: 'app-product-list',
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
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
})
export class ProductListComponent {
  private readonly products = inject(ProductService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  readonly esAdmin = this.auth.isAdmin;
  readonly categorias = CATEGORIAS_PRODUCTO;
  readonly opcionesPagina = [5, 10, 25];

  readonly busqueda = new FormControl('', { nonNullable: true });

  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly categoria = signal<CategoriaProducto | null>(null);

  readonly resultado = signal<PagedResult<Product> | null>(null);
  readonly cargando = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly productos = computed(() => this.resultado()?.items ?? []);
  readonly totalItems = computed(() => this.resultado()?.totalItems ?? 0);
  readonly columnas = computed(() =>
    this.esAdmin()
      ? ['imagen', 'nombre', 'categoria', 'precio', 'stock', 'acciones']
      : ['imagen', 'nombre', 'categoria', 'precio', 'stock'],
  );

  constructor() {
    this.busqueda.valueChanges
      .pipe(debounceTime(DEBOUNCE_BUSQUEDA_MS), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.pageIndex.set(0);
        this.cargar();
      });
  }

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.errorMessage.set(null);
    this.products
      .listar({
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        search: this.busqueda.value.trim() || undefined,
        categoria: this.categoria() ?? undefined,
      })
      .subscribe({
        next: (resultado) => {
          this.resultado.set(resultado);
          this.cargando.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.cargando.set(false);
          this.errorMessage.set(problemMessage(error, 'No pudimos cargar los productos.'));
        },
      });
  }

  cambiarPagina(evento: PageEvent): void {
    this.pageIndex.set(evento.pageIndex);
    this.pageSize.set(evento.pageSize);
    this.cargar();
  }

  cambiarCategoria(categoria: CategoriaProducto | null): void {
    this.categoria.set(categoria);
    this.pageIndex.set(0);
    this.cargar();
  }

  async eliminar(producto: Product): Promise<void> {
    const ref = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data: {
        titulo: 'Eliminar producto',
        mensaje: `¿Confirmás que querés eliminar "${producto.nombre}"? Deja de aparecer en el listado y en el selector de ventas.`,
        confirmar: 'Eliminar',
      },
    });
    const confirmado = await firstValueFrom(ref.afterClosed());
    if (confirmado !== true) {
      return;
    }

    this.products.eliminar(producto.id).subscribe({
      next: () => {
        this.snack.open(`Producto "${producto.nombre}" eliminado.`, 'Cerrar', { duration: 4000 });
        if (this.productos().length === 1 && this.pageIndex() > 0) {
          this.pageIndex.set(this.pageIndex() - 1);
        }
        this.cargar();
      },
      error: (error: HttpErrorResponse) => {
        this.snack.open(problemMessage(error, 'No pudimos eliminar el producto.'), 'Cerrar', { duration: 6000 });
      },
    });
  }
}
