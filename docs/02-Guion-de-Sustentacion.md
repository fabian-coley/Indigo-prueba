# Guion de sustentación — Indigo (12–15 minutos)

> **Ajuste respecto de la plantilla original** (`01-Arquitectura-de-la-Solucion.md` §17): esa plantilla abría con `docker compose up --build`, pero el empaquetado Docker quedó **diseñado y no escrito** (ver cierre del análisis lógico §14). Este guion abre con el **modo local real y verificado** (`dotnet run` + `ng serve`), y la sección 1.3 trae la respuesta preparada por si preguntan por Docker.

## Bloque 1 — Arranque (2 min)

### 1.1 Levantar la API

```bash
dotnet run --project src/Indigo.Api
```

- "La API arranca en `http://localhost:8080`. Lo primero que hace es correr las migraciones de EF Core y el seed automáticamente: una base SQLite nueva queda lista sola."
- "`GET /` devuelve la metadata del servicio y **Swagger** está en `/swagger` con el esquema completo."

### 1.2 Levantar el frontend

```bash
cd frontend/indigo-web && npm install && npm start
```

- "Angular queda en `http://localhost:4200` y un proxy de desarrollo enruta `/api` y `/uploads` al 8080. Login con `admin@indigo.com / Admin123!`."

### 1.3 Si preguntan por Docker (preparada)

- "El empaquetado está diseñado en el documento de arquitectura §15 — Dockerfile multi-stage, nginx para el Angular, volumen para SQLite, perfil PostgreSQL opcional, un solo puerto publicado — pero no llegó a escribirse porque la máquina de desarrollo no tiene Docker. Es el primer pendiente del roadmap."

## Bloque 2 — Demo funcional (5 min)

1. **Login Admin** → panel de productos.
2. **Crear producto con imagen**: mostrar el multipart, la validación en el formulario y el `imagenUrl` relativa que devuelve el API. La imagen se sirve desde `/uploads/...` (archivo en disco, no BLOB).
3. **Login Vendedor** (otra pestaña o incógnito, `vendedor@indigo.com / Vendedor123!`): misma app, menú reducido (no puede crear ni editar productos).
4. **Registrar venta** con 2 ítems → "miren el stock bajar en vivo en la pantalla de productos" (transacción: venta + descuento atómico).
5. **Caso de error**: intentar vender más que el stock → `400` controlado, mensaje de negocio, nada parcial guardado.
6. **Reporte por fechas**: Admin ve la venta recién registrada; **el Vendedor con el mismo rango ve vacío** → "el alcance por rol lo resuelve el servidor, no es cosmética del frontend".
7. **Swagger**: "Authorize" con el token del login y probar un endpoint en vivo.

## Bloque 3 — Arquitectura (3 min)

- Mostrar el diagrama del README / análisis lógico §7: Domain → Application (puertos) ← Infrastructure ← Api.
- "Las reglas de dependencia se verifican por las referencias de proyecto: Domain no conoce a nadie, Application solo conoce a Domain, Infrastructure implementa puertos, Api es el composition root."
- Tabla SOLID → código real:

| Principio / patrón | Dónde |
|---|---|
| SRP | `ProductImageService` separado de `ProductService` (`src/Indigo.Application/Services/`) |
| OCP | Adapter `IBlobStorage` con dos implementaciones: `LocalFileStorage` e `InMemoryBlobStorage` (`src/Indigo.Infrastructure/Storage/`) — cambiar a Azure Blob es escribir una tercera |
| LSP | las implementaciones del adapter son intercambiables sin tocar ningún consumidor |
| ISP | puertos chicos: `ITokenService`, `ICurrentUserService` (`src/Indigo.Application/Abstractions/Security/`) |
| DIP | puertos en Application; Infrastructure los implementa (`DependencyInjection.cs`) y Program.cs los cablea |
| Repositorio | `ProductRepository`, `SaleRepository` (`src/Indigo.Infrastructure/Persistence/Repositories/`) |
| Unit of Work | `UnitOfWork.cs` — la venta y el descuento de stock van en la misma transacción (`SaleService.RegistrarAsync`) |
| Factory | `AppDbContextFactory` — resuelve la BD design-time (`dotnet ef`) independiente del directorio de trabajo |
| Strategy | proveedor de BD por configuración: SQLite hoy, PostgreSQL con cambiar la connection string |

## Bloque 4 — Calidad (2 min)

- **`dotnet test Indigo.slnx` en vivo**: 232/232 — 58 Domain + 83 Application + 91 de integración con `WebApplicationFactory` y SQLite temporal aislado por test. Build con 0 warnings.
- **Logs estructurados (Serilog)**: mostrar en `logs/` la entrada de la venta recién registrada — request, duración, `UserId` desde el token, timestamp UTC.
- **Paginación**: 12 productos de seed muestran dos páginas; búsqueda y filtro por categoría van al servidor, no al cliente.

## Bloque 5 — Decisiones difíciles (2 min)

1. **Snapshot de precio/nombre en los ítems**: el reporte histórico es inmune a ediciones y bajas de producto.
2. **Soft delete**: nunca se pierde la trazabilidad de una venta.
3. **SQLite vs PostgreSQL**: SQLite da una demo cero-instalación; el swap a Postgres es configuración, no código.
4. **Imagen en archivo vs BLOB en BD**: URLs limpias, BD liviana, y el adapter esconde la decisión.
5. **CORS no configurado a propósito**: same-origin por diseño (proxy en dev, reverse proxy en producción). CORS no es autenticación; la seguridad la da el JWT con roles.

**Cierre (1 min)**: qué quedó fuera de alcance y cómo seguiría — refresh tokens, Docker (diseñado, pendiente de escribir), E2E automatizado del frontend con Playwright.

## Preguntas probables (respuestas preparadas)

| # | Pregunta | Respuesta |
|---|---|---|
| 1 | ¿Clean Architecture no es mucho para un proyecto chico? | "Es la consigna misma de la prueba: patrones reconocibles. El costo extra fue una capa, y se paga con testabilidad — 232 tests sin tocar una sola base real. Además es la estructura que escala si el sistema crece." |
| 2 | ¿Por qué SQLite y no SQL Server/Postgres? | "Para que el clon sea demo en un minuto sin instalar nada. El modelo es relacional estándar y el swap a Postgres es configuración (perfil ya diseñado). En producción no usaría SQLite." |
| 3 | ¿Concurrencia de stock: dos ventas simultáneas? | "El descuento es `UPDATE` atómico dentro de una transacción con verificación previa de existencia; SQLite serializa escrituras por archivo. Para producción con Postgres agregaría concurrencia optimista (`rowversion`)." |
| 4 | ¿Por qué snapshot y no join al producto vivo? | "Si se edita el precio o se da de baja el producto, un reporte con join cambia retroactivamente. El snapshot fija la foto del momento de la venta." |
| 5 | ¿Por qué soft delete? | "Las ventas referencian productos. Borrar físicamente rompe la trazabilidad o exige cascadas peligrosas. La baja lógica es un flag y el listado la filtra." |
| 6 | ¿Cómo escalaría esto? | "BD única → read replicas o CQRS para reportes; Blob Storage real vía el adapter; API Gateway; y los reportes agregados podrían ir a una tabla materializada con job nocturno." |
| 7 | ¿Es seguro el JWT? | "HS256 con secreto fuera del repo (RNF-11), vencimiento corto, claims mínimos, roles verificados en servidor con `[Authorize(Roles)]`, y el id de usuario se toma del token (`ICurrentUserService`), nunca del body — un vendedor no puede leer ni reportar ventas ajenas por manipular el request." |
| 8 | ¿Y las imágenes? | "Multipart con validación de tipo/tamaño, guardadas en disco con nombre generado, servidas con extensión segura. En producción: el mismo contrato, pero `IBlobStorage` apunta a Blob Storage o S3." |
| 9 | ¿Por qué el reporte se agrega en memoria? | "SQLite no traduce `decimal` con precisión en `SUM`; la agregación en memoria sobre la franja es exacta. El tope de 366 días por reporte la mantiene acotada — y empuja a paginar/particionar consultas largas en vez de un rango infinito." |
| 10 | ¿Por qué el Vendedor no ve todo el reporte? | "Regla del contrato: la visibilidad por rol aplica a ventas y reportes. Es la diferencia entre un sistema multiusuario y un demo de una sola cuenta." |
