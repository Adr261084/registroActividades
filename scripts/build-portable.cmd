@echo off
setlocal

set CONFIG=Release
set RUNTIME=win-x64

set ROOT=%~dp0..
set PROJECT=%ROOT%\RegActividades.App\RegActividades.App.csproj
set PUBLISH=%ROOT%\RegActividades.App\bin\%CONFIG%\net8.0-windows\%RUNTIME%\publish
set ARTIFACTS=%ROOT%\artifacts\portable

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set TS=%%i
set ZIP=%ARTIFACTS%\RegActividades-portable-%RUNTIME%-%TS%.zip

if not exist "%PROJECT%" (
  echo No se encontro el proyecto: %PROJECT%
  exit /b 1
)

echo Publicando aplicacion...
dotnet publish "%PROJECT%" -c %CONFIG% -r %RUNTIME% --self-contained true /p:PublishSingleFile=true
if errorlevel 1 exit /b 1

if not exist "%PUBLISH%" (
  echo No se encontro carpeta publish: %PUBLISH%
  exit /b 1
)

if not exist "%ARTIFACTS%" mkdir "%ARTIFACTS%"

echo Creando ZIP portable...
powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH%\*' -DestinationPath '%ZIP%' -CompressionLevel Optimal -Force"
if errorlevel 1 exit /b 1

echo Listo. Paquete generado:
echo %ZIP%

endlocal
