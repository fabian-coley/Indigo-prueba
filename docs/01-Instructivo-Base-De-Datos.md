# Instructivo — Base de Datos

> **Estado:** cerrado (Fase 2). Esquema generado y aplicado sobre SQLite real.
>
> **Documentos relacionados:**
> - `docs/00-Analisis-Logico.md` — requisitos, alcance y decisiones de stack (§8.9 contexto único, §8.10 agregados).
> - `01-Arquitectura-de-la-Solucion.md` — modelo de datos y contrato de API congelado.

---

## 1. Qué cubre este documento

Todo lo necesario para **crear, aplicar y mantener** la base de datos del sistema. El esquema lo
entrega la capa `Indigo.Infrastructure`; la entidad `User` y la lógica de Identity son de la capa
de backend, según la frontera acordada en `docs/00-Analisis-Logico.md` §8.9.

Piezas que componen la entrega:

| Pieza | Ruta |
|---|---|
| Contexto único (negocio + Identity) | `src/Indigo.Infrastructure/Persistence/AppDbContext.cs` |
| Fábrica en tiempo de diseño | `src/Indigo.Infrastructure/Persistence/AppDbContextFactory.cs` |
| Configuración Fluent API por entidad | `src/Indigo.Infrastructure/Persistence/Configurations/` |
| Migración única | `src/Indigo.Infrastructure/Persistence/Migrations/20260919151021_InitialCreate.cs` |
| Manifiesto de herramientas | `.config/dotnet-tools.json` |

---

## 2. Requisitos previos

- **.NET SDK 8** (el proyecto fija `<TargetFramework>net8.0</TargetFramework>`; un SDK superior
  compila igual, pero las plantillas nuevas generan `net10.0` y hay que corregirlo a mano).
- Las herramientas de EF Core se restauran desde el manifiesto versionado del repo. El manifiesto
  **debe** vivir en `.config/dotnet-tools.json`: `dotnet` sube por los directorios buscando esa ruta
  exacta y no reconoce un `dotnet-tools.json` suelto en la raíz.

```bash
dotnet tool restore                 # restaura dotnet-ef 8.0.31 desde .config/dotnet-tools.json
dotnet ef --version                 # verificación: 8.0.31
```

---

## 3. Aplicar la migración (línea de comandos)

La fábrica `AppDbContextFactory` permite apuntar solo a `Indigo.Infrastructure` sin depender del
Api, que todavía no tiene el contexto cableado:

```bash
dotnet ef database update \
  --project src/Indigo.Infrastructure \
  --startup-project src/Indigo.Infrastructure
```

Para regenerar la migración desde cero (por ejemplo al cambiar el proveedor, ver §8):

```bash
dotnet ef migrations add InitialCreate \
  --project src/Indigo.Infrastructure \
  --startup-project src/Indigo.Infrastructure \
  --output-dir Persistence/Migrations
```

Ambos comandos son **independientes del directorio de trabajo**: la fábrica ancla la base de datos a
la raíz de la solución (§6), así que se los puede invocar desde cualquier carpeta dentro del repo.
La única restricción viene de la herramienta y no del comando: `dotnet ef` se resuelve por el
manifiesto `.config/dotnet-tools.json`, que `dotnet` busca **subiendo desde el directorio actual** —
invocarlo desde fuera del repo falla con *"You intended to run a global tool…"* aunque los
`--project` y `--startup-project` sean correctos.

---

## 4. Aplicación en runtime (arranque de la API)

La migración no se aplica sola al levantar la aplicación: quien arranca el Api debe aplicarla y
sembrar los datos de demostración de forma **idempotente** (RNF-05 — clonar y ejecutar sin pasos
manuales). El orden importa: primero migrar, después sembrar, y el sembrado debe poder correr dos
veces sin duplicar filas.

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();   // aplica solo las migraciones pendientes
    await Seeder.SembrarAsync(scope.ServiceProvider); // idempotente
}
```

`Migrate()` es seguro de llamar en cada arranque: registra lo aplicado en `__EFMigrationsHistory`
y no repite trabajo.

---

## 5. ⚠️ Normalización a UTC — obligatorio

**Regla: todo timestamp se persiste en UTC. Nunca guardar `DateTimeOffset.Now`.**

SQLite no tiene un tipo nativo de fecha: `Fecha` (y cualquier `DateTimeOffset`) se almacena como
**texto ISO-8601** y las comparaciones de rango y el `ORDER BY` se resuelven **lexicográficamente**
sobre ese texto. El offset forma parte de la cadena, así que dos instantes guardados con offsets
distintos se comparan mal y **la consulta no falla: devuelve datos incorrectos en silencio.**

Ejemplo del fallo, con dos ventas reales:

| Fila | Valor almacenado | Instante real (UTC) |
|---|---|---|
| A | `2026-09-19 15:10:00+02:00` | 13:10 |
| B | `2026-09-19 14:00:00+00:00` | 14:00 |

Comparando el texto, `"…15:10…"` es mayor que `"…14:00…"`, así que el orden devuelve **A antes que
B** aunque A ocurrió antes. Un filtro `Fecha >= '2026-09-19 14:00:00+00:00'` incluiría A por error.

Con todos los registros en UTC la comparación lexicográfica coincide con el orden cronológico y el
problema desaparece:

```csharp
// Correcto: instante en UTC, offset +00:00 siempre.
var fecha = DateTimeOffset.UtcNow;

// Incorrecto: guarda el offset local y rompe los rangos del reporte.
var fecha = DateTimeOffset.Now;
```

Esto afecta de lleno al reporte por rango de fechas (§11 del contrato): `from` y `to` se
interpretan como días UTC y se traducen a un intervalo semiabierto, así que las dos puntas del
rango y las fechas almacenadas tienen que hablar el mismo idioma.

---

## 6. Ubicación del archivo `app.db`

`Data Source` es relativo **al proceso que abre la conexión**, no al proyecto. Una ruta relativa
haría que el tiempo de diseño y el runtime operaran sobre archivos distintos: se migra y siembra
sobre uno y la aplicación levanta contra otro —una base vacía que parece un bug de datos—. La
fábrica resuelve el lado del tiempo de diseño anclando la ruta a la **raíz de la solución**.

**Tiempo de diseño (`dotnet ef`).** `AppDbContextFactory` sube desde `AppContext.BaseDirectory`
hasta el directorio que contiene `Indigo.slnx` y arma la ruta absoluta con
`SqliteConnectionStringBuilder` (que escapa bien rutas con `;` o comillas). El resultado no depende
del directorio de trabajo: los comandos de §3 escriben y leen siempre el mismo archivo, se los
invoque desde donde se los invoque.

**Runtime (Fase 3, capa Api).** El Api resuelve su cadena contra `ContentRootPath`; con
`dotnet run` eso es `src/Indigo.Api/`. Son, por lo tanto, dos archivos:

| Cómo se ejecuta | Archivo resultante |
|---|---|
| `dotnet ef …` desde cualquier directorio del repo | `<raíz>/app.db` |
| Api en ejecución (`dotnet run`) | `src/Indigo.Api/app.db` |

Esto es aceptable **por diseño**: la aplicación llama a `Migrate()` en cada arranque (§4), así que
nunca puede levantar contra una base vacía — aplica la migración pendiente sobre el archivo que
tenga configurado y queda completa. La única consecuencia es de inspección: para mirar con `sqlite3`
los datos que está usando la aplicación hay que apuntar a su archivo, no al de la raíz.

Si se prefiere un único archivo compartido, basta exportar `ConnectionStrings__Default` con una ruta
absoluta: tanto esa variable de entorno como la clave `ConnectionStrings:Default` de
`appsettings.json` tienen precedencia sobre el valor por defecto de la fábrica.

**Fallback:** si no se encuentra `Indigo.slnx` —por ejemplo en una salida publicada, donde la
solución no se copia— la fábrica deja la ruta relativa al directorio de trabajo, que es el
comportamiento previo. La publicación no es un escenario de este entregable.

`.gitignore` excluye `*.db`, `*.db-shm` y `*.db-wal`: el archivo no se versiona y se regenera con
`dotnet ef database update`.

---

## 7. Agregados sobre `decimal`

`Precio`, `Total`, `PrecioUnitario` y `Subtotal` se declaran `decimal(18,2)`, pero SQLite los
almacena como **texto**, así que `SUM` y `ORDER BY` nativos **no** se comportan numéricamente. Los
agregados del reporte se resuelven **en memoria** (§8.10 del análisis lógico). La precisión se
mantiene a propósito: sobre PostgreSQL —donde sí hay tipo numérico nativo— el mismo esquema
agrega en la base sin cambiar una línea de configuración.

### Orden alfabético y colaciones (verificado)

`NOCASE` y `lower()` de SQLite son **solo ASCII**: no pliegan acentos. `'a' < 'á'` es verdadero, así
que un nombre que empiece con vocal acentuada ordena **después de `Z`**. Con los 12 nombres del
catálogo sembrado, `ORDER BY Nombre`, `ORDER BY Nombre COLLATE NOCASE` y `ORDER BY lower(Nombre)`
devuelven **exactamente el mismo orden**, y ese orden coincide con el alfabético español: los 12
arrancan con letra distinta salvo `Cafetera Express` / `Cámara Digital`, que divergen recién en el
segundo carácter (`a` frente a `á`) y las tres variantes resuelven igual.

`EF.Functions.Collate(x, "NOCASE")` **se ejecuta** contra SQLite: `NOCASE` es una colación
incorporada y no hay que registrarla, de modo que no existe el fallo de *colación desconocida*,
que es el riesgo real de las colaciones propias. Traduce a `ORDER BY "Nombre" COLLATE NOCASE`.
Verificado ejecutando EF Core 8.0.31 contra un archivo SQLite real, no solo compilando.

Ninguna variante aprovecha `IX_Products_Nombre`: el índice es BINARY, y una expresión o una
colación distinta obligan a ordenar por separado. Sobre 12 filas es irrelevante; si el catálogo
creciera y el orden alfabético estricto importara, la salida sería ordenar en memoria con una
cultura española, no depender de la colación de la base.

---

## 8. Cambiar el proveedor a PostgreSQL

Las migraciones son **específicas del proveedor**: no se reutiliza la de SQLite. Pasos:

1. Añadir el paquete `Npgsql.EntityFrameworkCore.PostgreSQL` a `Indigo.Infrastructure`.
2. Cambiar `UseSqlite(...)` por `UseNpgsql(...)` en la fábrica y en el cableado del Api.
3. **Borrar** `Persistence/Migrations/` y regenerar `InitialCreate` con `dotnet ef migrations add`
   (§3). El `ModelSnapshot` también es del proveedor y se regenera con la migración.
4. Revisar los CHECK: se traducen a restricciones nativas, pero conviene confirmar que PostgreSQL
   acepta la sintaxis generada.

El perfil opcional de `docker-compose.yml` usa este camino.

---

## 9. Esquema resultante

Una sola migración crea las tablas de negocio y las de Identity, con **FK real** de
`Sales.UsuarioId` a `AspNetUsers.Id`: el vendedor de cada venta se resuelve por join, y al no haber
dos contextos no hay dos historiales de migración sobre el mismo archivo.

**Tablas de negocio**

| Tabla | Notas |
|---|---|
| `Products` | `Categoria` como texto (`Electrónica`, `Hogar`, `Alimentos`, `Ropa`, `Otros`); `Activo` es el borrado lógico; CHECK de precio positivo y stock no negativo |
| `Sales` | `Fecha` (UTC), `UsuarioId`, `Total`; CHECK de total positivo; `UsuarioId` → `AspNetUsers` con borrado **Restrict** |
| `SaleItems` | `ProductoNombre` y `PrecioUnitario` son **instantáneas** del momento de la venta, no joins; CHECK de cantidad, precio y subtotal; `SaleId` → `Sales` en **Cascade**, `ProductId` → `Products` en **Restrict** |

**Identity:** `AspNetUsers` (con `NombreCompleto`, máx. 200), `AspNetRoles`, `AspNetUserRoles`,
`AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`.

**Índices:** `Products(Nombre)`, `Products(Categoria)`, `Sales(Fecha)`, `Sales(UsuarioId, Fecha)`,
`SaleItems(SaleId)`, `SaleItems(ProductId)`.

**Convenciones de nombres:** `UsuarioId` (no `UserId`) y `LongitudMaximaUsuarioId = 450`, que es la
longitud con la que Identity define su clave primaria.

---

## 10. Puntos abiertos

- **`launchSettings.json` del Api** todavía usa los puertos de plantilla (`5186` / `7219`) en lugar
  del **8080** acordado en §15 de la arquitectura. Es de la capa Api, no del esquema: queda asignado
  a la Fase 3.
- **Cadena de conexión en el Api**: la clave es `ConnectionStrings:Default` (variable de entorno
  `ConnectionStrings__Default`). El `appsettings.json` del Api aún no la declara — la agrega el
  cableado de la Fase 3. Si se la declara con una ruta absoluta, el Api y `dotnet ef` pasan a
  compartir un único archivo (§6).

**Resuelto en el cierre de la Fase 2:** la ubicación del archivo `app.db` (§6). El tiempo de diseño
la ancla a la raíz de la solución y la divergencia con el runtime queda cubierta por `Migrate()` en
cada arranque.
