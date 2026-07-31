# RegActividades

Aplicacion de escritorio para Windows (WPF, .NET 8) que permite registrar actividades en una base de datos local SQLite.
Cada registro guarda:

- Texto de la actividad
- Fecha y hora de creacion (timestamp)

## Tecnologias

- .NET 8
- WPF
- SQLite (Microsoft.Data.Sqlite)
- Dapper
- Inno Setup (para instalador)

## Funcionalidades

- Crear entradas de actividad con boton Guardar o tecla Enter
- Persistencia local en SQLite
- Tabla de historial de registros
- Filtros por texto y por rango de fechas
- Exportacion CSV (respetando filtros activos)
- Opcion Iniciar con Windows
- Integracion con bandeja del sistema (area de notificaciones)

## Estructura del proyecto

- `RegActividades.App/`: proyecto principal WPF
- `installer/RegActividades.iss`: script de Inno Setup
- `scripts/build-portable.ps1`: genera publicacion portable + ZIP
- `scripts/build-portable.cmd`: alternativa portable sin bloqueo de scripts PowerShell

## Requisitos

- Windows 10/11
- .NET 8 SDK (para compilar)

Verifica la instalacion de .NET:

```powershell
dotnet --info
```

## Ejecutar en modo desarrollo

Desde la raiz del repo:

```powershell
dotnet run --project .\RegActividades.App\RegActividades.App.csproj
```

## Compilar

```powershell
dotnet build .\RegActividades.App\RegActividades.App.csproj -c Release
```

## Publicar ejecutable para Windows

```powershell
dotnet publish .\RegActividades.App\RegActividades.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Salida esperada:

- `RegActividades.App/bin/Release/net8.0-windows/win-x64/publish/RegActividades.App.exe`

## Generar version portable (sin instalador)

### Opcion recomendada en entorno empresarial (.cmd)

```bat
scripts\build-portable.cmd
```

### Opcion PowerShell (.ps1)

```powershell
.\scripts\build-portable.ps1
```

El script crea un ZIP en:

- `artifacts/portable/RegActividades-portable-win-x64-<timestamp>.zip`

Ese ZIP se puede compartir y ejecutar sin instalar (descomprimir y abrir `RegActividades.App.exe`).

Nota: en algunos equipos corporativos la politica de ejecucion bloquea scripts `.ps1` no firmados.
Si te ocurre, usa `build-portable.cmd`.

## Descargar portable listo para usar

Si no quieres compilar, puedes descargar el paquete portable ya generado:

- [artifacts/portable/RegActividades-portable-win-x64-20260731_143110.zip](artifacts/portable/RegActividades-portable-win-x64-20260731_143110.zip)

Uso rapido:

1. Descarga el ZIP del enlace.
2. Descomprime en una carpeta local.
3. Ejecuta `RegActividades.App.exe`.

Si la ventana no aparece o la aplicacion se cierra, revisa este log:

- `%LocalAppData%\RegActividades\startup.log`

## Crear instalador (.exe) con Inno Setup

1. Instala Inno Setup en tu equipo.
2. Asegurate de haber ejecutado antes el `dotnet publish`.
3. Abre y compila el script:
   - `installer/RegActividades.iss`
4. Se generara un instalador tipo wizard para Windows.

## Ubicacion de la base de datos local

La aplicacion crea la base en:

- `%LocalAppData%\RegActividades\actividades.db`

## Nota sobre GitHub y ejecutables grandes

GitHub no permite archivos mayores a 100 MB en commits normales.
Si el `.exe` publicado supera ese limite, usa una de estas opciones:

- GitHub Releases (assets)
- Git LFS
- Almacenamiento externo (OneDrive, Drive, etc.)

## Comandos Git utiles

Agregar remoto y primer push:

```powershell
git remote add origin <URL_REPO>
git branch -M main
git push -u origin main
```

## Estado actual del proyecto

- Repositorio conectado a GitHub
- Rama principal: `main`
- Script de instalador listo
- App funcional con SQLite + Dapper
