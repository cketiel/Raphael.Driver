# Raphael.Driver — .NET MAUI

App del conductor. Parte del ecosistema Raphael (NEMT). Reglas globales: `../CLAUDE.md`.

## Rol
El conductor ejecuta la ruta desde aquí: ve su schedule, reporta GPS, cambia estados de viaje y
captura la firma del paciente. Es el punto donde el dato de campo entra al sistema — si aquí se
pierde o se desincroniza, el viaje queda inconsistente en toda la cadena.

Targets: `net8.0-android;net8.0-ios;net8.0-maccatalyst`

## Versionado — LEER ANTES DE TOCAR EL CSPROJ

`Helpers/AppVersion.cs:13` muestra `AppInfo.Current.VersionString`, que en MAUI mapea a
`<ApplicationDisplayVersion>`.

⚠️ **Esa propiedad está declarada en 8 PropertyGroups del csproj (líneas 17, 71, 82, 86, 90, 94, 98,
102) y está así a propósito**: es el resultado de resolver errores de compilación. **En su estado
actual el proyecto compila correctamente y la estructura no se toca.**

- **El PropertyGroup que manda** para el APK que realmente se distribuye es
  `Release|net8.0-android|AnyCPU` (línea 71). Ahí vive la versión que ve el conductor.
- Al liberar: cambiar **solo los números**, en el PropertyGroup general (línea 17) y en el de
  `Release|net8.0-android` a la vez. Nunca reestructurar.
- Punto de retorno si algo se rompe: `git checkout rollback/pre-version-cleanup -- Raphael.Driver.csproj`
- Versión en producción: **1.4.0**, tag `v1.4.0`. Distribución: APK por sideload.
- ⚠️ `main` lleva sin liberar el margen de 5 min en viajes `Return` (RE-012). Sale en el próximo APK.

## Contrato con la API
- Auth: **JWT**. `Services/AuthService.cs` + `Services/AuthHeaderHandler.cs`
- Config/base URL: `PrivateSettings.cs`
- DTOs espejo: `DTOs/` (13 tipos en 10 ficheros, 10 copiados del backend) — **copias manuales** de `Raphael.Backend/Raphael.Shared/DTOs/`

⚠️ **Drift abierto:** `DTOs/ScheduleDto.cs` tiene `PassengerSignature`, que el backend no expone en
ese DTO, y le falta `Status`. Antes de construir sobre `ScheduleDto`, resolver de dónde sale cada
campo. Ver `../_meta/CONTRACT_MAP.md`.

## Anclas
- Entrada: `MauiProgram.cs` → `AppShell.xaml`
- HTTP/dominio: `Services/` (`ScheduleService`, `RunService`, `GpsService`, `ProviderService`)
- Estado: `ViewModels/` · UI: `Views/`

## Convenciones no obvias
- MVVM estricto: la lógica vive en `ViewModels/`, no en code-behind de `Views/`.
- Navegación por Shell (`AppShell.xaml`), no `Navigation.PushAsync` suelto.
- El GPS es un servicio de fondo: revisar permisos en `Platforms/Android` y `Platforms/iOS`
  antes de tocar `GpsService.cs`.
- PHI en pantalla (nombre, teléfono, dirección del paciente): nunca a log ni a telemetría.

## No leer
`bin/`, `obj/`, `Platforms/*/obj/`, `Resources/raw/`, `*.pfx`, `*.user`, `TemplateEngineHost/`

## Comandos
- Build: `dotnet build -f net8.0-android`
- Run: `dotnet build -t:Run -f net8.0-android`
- Test: no hay proyecto de tests.
