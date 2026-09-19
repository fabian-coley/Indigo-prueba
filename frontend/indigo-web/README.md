# IndigoWeb

Frontend de Indigo (gestión de productos y ventas): Angular 18 standalone con Angular Material.
Consume el API bajo `/api`, proxeado a `http://localhost:8080` en desarrollo (`proxy.conf.json`).

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## Decisiones de implementación

### Fechas del reporte: `dd/MM/yyyy` con adaptador propio

El `NativeDateAdapter` de Material resuelve el texto tipeado con `new Date(Date.parse(valor))`, que
en V8 interpreta `5/9/2026` como *9 de mayo* (orden `M/d/yyyy`) sin mirar el locale: tipear
`5/9/2026` terminaba mostrando y guardando `9/5/2026`. Es un problema del parseo, no del formato,
así que no alcanzaba con cambiar `MAT_DATE_FORMATS`.

La solución es `AdaptadorFechaDdMmYyyy` (`features/reports/sales-report/adaptador-fecha-dd-mm-yyyy.ts`),
que extiende `NativeDateAdapter` y sobreescribe `parse()` —y `format()`, sólo para el texto del
input— con el orden día-mes-año. Se registra como provider **local de la pantalla de reportes**
(`provideAdaptadorFechaDdMmYyyy()`), no en el bootstrap: es el único datepicker de la aplicación y
así el `provideNativeDateAdapter()` global queda intacto y el cambio no se filtra a otras pantallas.

Detalles deliberados:

- El calendario no cambia: `parse()` sólo se usa con el texto del input, y la clase base sigue
  resolviendo las etiquetas de mes y día con el locale (`es-AR`).
- `31/02/2026` se rechaza con una verificación de ida y vuelta (`Date` correría solo el día
  inexistente al mes siguiente).

Aparte, en toda la pantalla las fechas se formatean con `formatearDia()` y se serializan con
`aFechaApi()` (getters locales, nunca `toISOString()`), en lugar de `DatePipe`, para que el día no
se corra al convertir a UTC.

### Un solo canal de error por pantalla

El `errorInterceptor` global abre un snackbar con todo error que no sea 401 ni 404. Cuando la
pantalla ya expone el error —la alerta inline con **Reintentar** o los `mat-error` de cada campo—,
ese snackbar repetía el mismo mensaje en dos lugares a la vez.

Se resolvió con un `HttpContextToken` (`MANEJO_LOCAL_DE_ERRORES`, en
`core/auth/error-handling.token.ts`) y una constante `CONTEXTO_ERROR_LOCAL` ya construida que los
servicios pasan como `context` en cada request; el interceptor omite su snackbar cuando el token
está activo, y cada pantalla decide cómo mostrarlo. El `401` sigue siendo global (cierra sesión y
redirige a `/login` con `returnUrl`) porque afecta a la sesión entera, no a una pantalla.

El contrato no cambia: la validación de rango (máximo 366 días) la sigue haciendo el servidor.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory.

Los presupuestos de `angular.json` están ajustados a este stack (1 MB inicial, 5 kB por hoja de
estilo) en lugar del default del CLI: Angular Material standalone agrega el tema prebuilt y los
módulos de cada componente, así que el bundle inicial ronda los 740 kB y el SCSS más pesado los
3.7 kB. Con los valores por defecto el build terminaba siempre en warning, que deja de ser señal.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

Run `ng e2e` to execute the end-to-end tests via a platform of your choice. To use this command, you need to first add a package that implements end-to-end testing capabilities.

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
