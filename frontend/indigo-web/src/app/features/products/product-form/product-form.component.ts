import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CATEGORIAS_PRODUCTO, CategoriaProducto, Product, ProductRequest } from '../../../core/models/product.model';
import { ProductService } from '../../../core/services/product.service';
import { validarImagen } from '../../../shared/imagen.util';
import { fieldErrors, isValidationProblem, problemMessage } from '../../../shared/problem-details.util';

type CampoFormulario = 'nombre' | 'precio' | 'stock' | 'categoria';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss',
})
export class ProductFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly products = inject(ProductService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = input<string>();

  readonly esEdicion = computed(() => !!this.id());
  readonly categorias = CATEGORIAS_PRODUCTO;
  readonly esAdmin = this.auth.isAdmin;

  readonly form = this.fb.group({
    nombre: this.fb.control('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    precio: this.fb.control<number | null>(null, { validators: [Validators.required, Validators.min(0.01)] }),
    stock: this.fb.control<number | null>(null, { validators: [Validators.required, Validators.min(0)] }),
    categoria: this.fb.control<CategoriaProducto | null>(null, { validators: [Validators.required] }),
  });

  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly errorImagen = signal<string | null>(null);

  readonly archivo = signal<File | null>(null);
  readonly previewUrl = signal<string | null>(null);
  readonly imagenActual = signal<string | null>(null);

  readonly imagenMostrada = computed(() => this.previewUrl() ?? this.imagenActual());

  private previewVigente: string | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => this.liberarPreview());

    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.limpiarErroresServidor());
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      return;
    }

    this.cargando.set(true);
    this.products.obtener(id).subscribe({
      next: (producto) => {
        this.form.patchValue({
          nombre: producto.nombre,
          precio: producto.precio,
          stock: producto.stock,
          categoria: producto.categoria,
        });
        this.imagenActual.set(producto.imagenUrl);
        this.cargando.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.cargando.set(false);
        this.errorMessage.set(problemMessage(error, 'No pudimos cargar el producto.'));
      },
    });
  }

  onArchivo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const archivo = input.files?.[0];
    if (!archivo) {
      return;
    }

    const problema = validarImagen(archivo);
    if (problema) {
      this.errorImagen.set(problema);
      input.value = '';
      return;
    }

    this.errorImagen.set(null);
    this.liberarPreview();
    this.previewVigente = URL.createObjectURL(archivo);
    this.previewUrl.set(this.previewVigente);
    this.archivo.set(archivo);
  }

  quitarSeleccion(input: HTMLInputElement): void {
    input.value = '';
    this.liberarPreview();
    this.previewUrl.set(null);
    this.archivo.set(null);
    this.errorImagen.set(null);
  }

  submit(): void {
    if (this.guardando()) {
      return;
    }
    this.errorMessage.set(null);

    const request = this.construirRequest();
    if (!request) {
      this.form.markAllAsTouched();
      return;
    }

    const id = this.id();
    this.guardando.set(true);

    const peticion = id ? this.products.actualizar(id, request) : this.products.crear(request);
    peticion.subscribe({
      next: (producto) => this.continuarConImagen(producto, !id),
      error: (error: HttpErrorResponse) => {
        this.guardando.set(false);
        this.manejarError(error);
      },
    });
  }

  campoError(campo: CampoFormulario): string {
    const control = this.form.controls[campo];
    if (!control.touched || control.valid) {
      return '';
    }
    if (control.hasError('required')) {
      return 'Este campo es obligatorio.';
    }
    if (control.hasError('maxlength')) {
      return 'Máximo 120 caracteres.';
    }
    if (control.hasError('min')) {
      return campo === 'precio' ? 'El precio debe ser mayor a 0.' : 'El stock no puede ser negativo.';
    }
    return (control.getError('server') as string | undefined) ?? 'Valor inválido.';
  }

  private construirRequest(): ProductRequest | null {
    const { nombre, precio, stock, categoria } = this.form.getRawValue();
    if (precio === null || stock === null || categoria === null) {
      return null;
    }
    return { nombre, precio, stock, categoria };
  }

  private continuarConImagen(producto: Product, esNuevo: boolean): void {
    const archivo = this.archivo();
    if (!archivo) {
      this.guardando.set(false);
      this.confirmar(esNuevo ? 'Producto creado.' : 'Producto actualizado.');
      return;
    }

    this.products.subirImagen(producto.id, archivo).subscribe({
      next: () => {
        this.guardando.set(false);
        this.confirmar(esNuevo ? 'Producto creado con su imagen.' : 'Producto actualizado.');
      },
      error: (error: HttpErrorResponse) => {
        this.guardando.set(false);
        const mensaje = problemMessage(error, 'No pudimos subir la imagen.');
        if (esNuevo) {
          this.snack.open(`El producto se creó sin imagen. ${mensaje}`, 'Cerrar', { duration: 8000 });
          this.router.navigate(['/productos', producto.id, 'editar']);
        } else {
          this.snack.open(`Los datos se guardaron, pero la imagen no: ${mensaje}`, 'Cerrar', { duration: 8000 });
        }
      },
    });
  }

  private confirmar(mensaje: string): void {
    this.snack.open(mensaje, 'Cerrar', { duration: 4000 });
    this.router.navigate(['/productos']);
  }

  private manejarError(error: HttpErrorResponse): void {
    if (isValidationProblem(error)) {
      let sinCampoConocido = false;
      for (const [campo, mensajes] of Object.entries(fieldErrors(error))) {
        const nombre = this.normalizarCampo(campo);
        const control = this.form.get(nombre);
        if (control) {
          control.setErrors({ server: mensajes.join(' ') });
          control.markAsTouched();
        } else {
          sinCampoConocido = true;
        }
      }
      if (sinCampoConocido) {
        this.errorMessage.set(problemMessage(error, 'Revisá los datos ingresados.'));
      }
      return;
    }

    this.errorMessage.set(problemMessage(error, 'No pudimos guardar el producto.'));
  }

  private normalizarCampo(campo: string): string {
    return campo.charAt(0).toLowerCase() + campo.slice(1);
  }

  private limpiarErroresServidor(): void {
    for (const control of Object.values(this.form.controls)) {
      if (control.hasError('server')) {
        control.setErrors(null);
      }
    }
  }

  private liberarPreview(): void {
    if (this.previewVigente) {
      URL.revokeObjectURL(this.previewVigente);
      this.previewVigente = null;
    }
  }
}
