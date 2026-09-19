import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrors, problemMessage } from '../../../shared/problem-details.util';

const CREDENCIALES_DEMO = {
  admin: { email: 'admin@indigo.com', password: 'Admin123!' },
  vendedor: { email: 'vendedor@indigo.com', password: 'Vendedor123!' },
} as const;

type RolDemo = keyof typeof CREDENCIALES_DEMO;

type CampoLogin = 'email' | 'password';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly mostrarPassword = signal(false);

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.clearServerErrors());

    if (this.auth.isAuthenticated()) {
      void this.router.navigateByUrl('/productos');
    }
  }

  submit(): void {
    this.clearServerErrors();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.loading.set(true);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigateByUrl(this.destination()),
      error: (error: unknown) => {
        this.loading.set(false);
        this.handleError(error);
      },
    });
  }

  usarCredencialesDemo(rol: RolDemo): void {
    this.form.setValue({ ...CREDENCIALES_DEMO[rol] });
    this.form.markAsUntouched();
    this.errorMessage.set(null);
  }

  fieldMessage(campo: CampoLogin): string | null {
    const control = this.form.controls[campo];
    const errors = control.errors;
    if (!control.touched || !errors) {
      return null;
    }
    if (errors['server']) {
      return errors['server'] as string;
    }
    if (errors['required']) {
      return 'Este campo es obligatorio.';
    }
    if (errors['email']) {
      return 'Ingresá un email válido.';
    }
    return 'Valor inválido.';
  }

  private destination(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/productos';
  }

  private handleError(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      this.errorMessage.set('El email o la contraseña son incorrectos.');
      return;
    }

    const porCampo = fieldErrors(error);
    if (Object.keys(porCampo).length > 0) {
      this.applyServerErrors(porCampo);
      return;
    }

    this.errorMessage.set(problemMessage(error, 'No pudimos iniciar sesión. Intentá de nuevo.'));
  }

  private applyServerErrors(errors: Record<string, string[]>): void {
    let aplicado = false;
    for (const [campo, mensajes] of Object.entries(errors)) {
      const control = this.form.get(this.normalizarCampo(campo));
      if (!control || mensajes.length === 0) {
        continue;
      }
      control.setErrors({ server: mensajes[0] });
      control.markAsTouched();
      aplicado = true;
    }
    if (!aplicado) {
      this.errorMessage.set('Revisá los datos ingresados.');
    }
  }

  private clearServerErrors(): void {
    for (const control of Object.values(this.form.controls)) {
      if (!control.errors?.['server']) {
        continue;
      }
      const restantes = { ...control.errors };
      delete restantes['server'];
      control.setErrors(Object.keys(restantes).length > 0 ? restantes : null);
    }
  }

  private normalizarCampo(campo: string): string {
    return campo.charAt(0).toLowerCase() + campo.slice(1);
  }
}
