import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Ingresar · Indigo',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component').then((m) => m.MainLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'productos' },
      {
        path: 'productos',
        title: 'Productos · Indigo',
        loadComponent: () =>
          import('./features/products/product-list/product-list.component').then(
            (m) => m.ProductListComponent
          ),
      },
      {
        path: 'productos/nuevo',
        title: 'Nuevo producto · Indigo',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/products/product-form/product-form.component').then(
            (m) => m.ProductFormComponent
          ),
      },
      {
        path: 'productos/:id/editar',
        title: 'Editar producto · Indigo',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/products/product-form/product-form.component').then(
            (m) => m.ProductFormComponent
          ),
      },
      {
        path: 'ventas',
        title: 'Ventas · Indigo',
        loadComponent: () =>
          import('./features/sales/sale-list/sale-list.component').then((m) => m.SaleListComponent),
      },
      {
        path: 'ventas/nueva',
        title: 'Nueva venta · Indigo',
        loadComponent: () =>
          import('./features/sales/sale-create/sale-create.component').then(
            (m) => m.SaleCreateComponent
          ),
      },
      {
        path: 'reportes',
        title: 'Reportes · Indigo',
        loadComponent: () =>
          import('./features/reports/sales-report/sales-report.component').then(
            (m) => m.SalesReportComponent
          ),
      },
      { path: '**', redirectTo: 'productos' },
    ],
  },
  { path: '**', redirectTo: '' },
];
