# ECOTANK · Conexión a MySQL — Resumen de sesión

> Fecha: 2026-05-15
> Objetivo: conectar el proyecto **.NET MAUI ECOTANK** a una base de datos MySQL,
> crear las tablas (sin datos) y dejar la capa de conexión lista.

---

## 1. Contexto del proyecto

- **ECOTANK** es una app **.NET MAUI** (net9.0 · Android, iOS, MacCatalyst, Windows) de **eco‑turismo experiencial**.
- Maquetas HTML que definen el dominio:
  - `loginRegister.html` → login/registro con roles: Turista, Anfitrión, Administrador, Negocios Locales.
  - `interfacez.html` → alojamientos, actividades, mapa (Leaflet), reservas, contacto.
- Antes de esta sesión el proyecto era la plantilla MAUI por defecto, sin base de datos.

---

## 2. Lo que se hizo

1. Diseño del **esquema MySQL completo** del dominio (solo estructura, **sin datos**).
2. Creación de la **capa de conexión en C#** con `MySqlConnector`.
3. Ejecución del esquema sobre el **MySQL/MariaDB de XAMPP** → base `ecotank` creada y verificada.

---

## 3. Archivos creados / modificados

| Archivo | Estado | Propósito |
|---|---|---|
| `database/schema.sql` | nuevo | `CREATE DATABASE ecotank` + 9 tablas con FKs, índices y CHECK. Sin `INSERT`. Idempotente (`IF NOT EXISTS`). |
| `ECOTANK/Resources/Raw/appsettings.json` | nuevo | Connection string. `localhost:3306`, user `root`, password vacío, db `ecotank`. |
| `ECOTANK/Configuration/MySqlSettings.cs` | nuevo | Carga `appsettings.json` (empaquetado como MauiAsset). |
| `ECOTANK/Data/DatabaseService.cs` | nuevo | Abre conexiones, `TestConnectionAsync()`, `InitializeDatabaseAsync()`. |
| `ECOTANK/Models/Entities.cs` | nuevo | POCOs que reflejan cada tabla. |
| `ECOTANK/ECOTANK.csproj` | modificado | NuGet `MySqlConnector 2.4.0` + `schema.sql` empaquetado como MauiAsset. |
| `ECOTANK/MauiProgram.cs` | modificado | `DatabaseService` registrado en DI (singleton, inyectable). |

---

## 4. Esquema de base de datos

**Base:** `ecotank` · InnoDB · `utf8mb4_unicode_ci`

Tablas (orden por dependencias de FK):

```
roles → usuarios → ubicaciones → alojamientos → actividades
      → negocios_locales → reservas → pagos → resenas
```

| Tabla | Descripción |
|---|---|
| `roles` | Turista, Anfitrión, Administrador, Negocios Locales |
| `usuarios` | Cuentas (1 rol por usuario), email único, `password_hash` |
| `ubicaciones` | Geolocalización lat/lng para el mapa Leaflet |
| `alojamientos` | Publicados por anfitriones |
| `actividades` | Senderismo, cascadas, camping, etc. |
| `negocios_locales` | Comercios asociados |
| `reservas` | De un alojamiento o una actividad |
| `pagos` | 1..N pagos por reserva |
| `resenas` | Calificaciones 1‑5 |

**Integridad:** 14 claves foráneas + 3 CHECK constraints
(`calificacion` entre 1 y 5; reserva/reseña debe referenciar alojamiento o actividad).

---

## 5. Base de datos creada (XAMPP) — verificado

- **Motor:** MariaDB 10.4.32 (XAMPP) en `127.0.0.1:3306`
- **Usuario:** `root` · **Password:** vacío (default XAMPP)
- **Base:** `ecotank` creada
- **9 tablas** creadas, **0 filas** cada una (sin datos, como se pidió)
- **14 FKs** y **3 CHECK** aplicados correctamente

Comando usado para crear las tablas:

```powershell
& "C:\xampp\mysql\bin\mysql.exe" -u root --host=127.0.0.1 --port=3306 < "database\schema.sql"
```

Inspección visual: **phpMyAdmin** → `http://localhost/phpmyadmin` → base `ecotank`.

---

## 6. La conexión desde la app

`appsettings.json` ya coincide con el default de XAMPP (no hay que tocar nada).
Inyectando `DatabaseService` en cualquier página/servicio:

```csharp
// Probar conectividad
var (ok, msg) = await db.TestConnectionAsync();

// (Re)crear el esquema desde la app — idempotente, no inserta datos
var (ok2, msg2) = await db.InitializeDatabaseAsync();

// Abrir una conexión para consultas
await using var conn = await db.OpenConnectionAsync();
```

> Si algún día se usa otro servidor/usuario/contraseña, editar
> `ECOTANK/Resources/Raw/appsettings.json`. No subir contraseñas reales a git.

---

## 7. Cómo correr el proyecto

El `dotnet` apunta al **`.csproj`** o al **`.sln`**, nunca a un `.cs`.

```powershell
# Correr en Windows (elige un target framework con -f)
dotnet run --project "ECOTANK\ECOTANK.csproj" -f net9.0-windows10.0.19041.0

# Solo compilar / validar enlaces (incluye MySqlConnector)
dotnet build "ECOTANK.sln"
```

**Requisitos previos** (en la sesión `dotnet` no estaba instalado):

1. **.NET 9 SDK** → https://dotnet.microsoft.com/download/dotnet/9.0
2. Workload MAUI: `dotnet workload install maui`
3. Verificar: `dotnet --version`

Con **Visual Studio 2022**: abrir `ECOTANK.sln`, target **Windows Machine**, F5.

---

## 8. Pendiente / siguientes pasos sugeridos

- [ ] Sembrar los 4 roles base en `roles` (es dato; se hace aparte cuando se pida).
- [ ] Lógica de registro/login real (hash de contraseñas) contra `usuarios`.
- [ ] Pantalla con botón "probar conexión" usando `TestConnectionAsync()`.
- [ ] Repositorios/consultas CRUD por entidad.
