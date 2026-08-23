# Guía de Instalación y Despliegue - FileFlow Studio

## 1. Requisitos Previos del Sistema

Antes de compilar o desplegar **FileFlow Studio**, asegúrate de que el entorno cumple con los siguientes requisitos:

### 1.1. Entorno de Desarrollo (Compilación desde código fuente)
- **Sistema Operativo**: Windows 10 (versión 1809 o superior) / Windows 11 (Requerido para la interfaz gráfica WPF).
- **SDK de .NET**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (versión `9.0.100` o superior).
- **Herramientas de Línea de Comandos**: PowerShell 7+ o Windows PowerShell 5.1.
- **IDE Recomendado**: Visual Studio 2022 (v17.12+ con carga de trabajo *.NET Desktop Development*), JetBrains Rider 2024.3+, o VS Code con extensión *C# Dev Kit*.

### 1.2. Entorno de Ejecución (Para usuarios finales)
- **Runtime**: [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) (x64) si se utiliza la versión *framework-dependent*, o ninguno si se utiliza la versión autónoma (*self-contained*).
- **Herramientas Opcionales (Integraciones de Dominio)**:
  - **FFmpeg**: Necesario para el nodo `MediaTranscoderNode`. Se recomienda tener `ffmpeg.exe` y `ffprobe.exe` en el `PATH` del sistema o en el directorio raíz de la aplicación.
  - **7-Zip CLI / WinRAR**: Opcional para formatos propietarios protegidos en `SmartUnpackNode`.

---

## 2. Configuración del Entorno Local

### 2.1. Clonación del Repositorio
```powershell
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser
```

### 2.2. Restauración de Paquetes NuGet
```powershell
dotnet restore FileFlow.slnx
```

---

## 3. Compilación y Ejecución Local

### 3.1. Mediante el Script Automatizado (Recomendado)
El proyecto incluye un script de compilación y ejecución directa en PowerShell que valida dependencias y arranca el entorno de depuración:

```powershell
.\run.ps1
```

### 3.2. Mediante el CLI de .NET
Para compilar la solución completa en modo Debug:
```powershell
dotnet build FileFlow.slnx -c Debug
```

Para arrancar la aplicación de escritorio:
```powershell
dotnet run --project FileFlow.App/FileFlow.App.csproj -c Debug
```

---

## 4. Ejecución de la Batería de Pruebas Automatizadas

FileFlow Studio cuenta con una suite completa de pruebas unitarias, de integración, rendimiento y seguridad bajo xUnit, FluentAssertions y Moq:

```powershell
# Ejecutar todas las pruebas de la solución
dotnet test FileFlow.slnx

# Ejecutar pruebas con reporte detallado
dotnet test FileFlow.slnx --logger "console;verbosity=detailed"

# Ejecutar con medición de cobertura de código
dotnet test FileFlow.slnx --collect:"XPlat Code Coverage"
```

---

## 5. Empaquetado y Distribución

### 5.1. Publicación Autónomo (*Self-Contained Single-File*)
Genera un ejecutable único portable de alto rendimiento con todas las librerías de .NET 9 incluidas (no requiere que el cliente instale el runtime):

```powershell
dotnet publish FileFlow.App/FileFlow.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ./publish/FileFlowStudio-win-x64
```

### 5.2. Publicación Dependiente del Marco (*Framework-Dependent*)
Genera un paquete ligero para entornos que ya cuentan con .NET 9 Desktop Runtime instalado:

```powershell
dotnet publish FileFlow.App/FileFlow.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o ./publish/FileFlowStudio-portable
```

---

## 6. Pipeline de Integración Continua (CI/CD)

Ejemplo de flujo de trabajo en **GitHub Actions** (`.github/workflows/ci.yml`) para validación y compilación en cada Pull Request:

```yaml
name: FileFlow Studio CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: windows-latest

    steps:
    - name: Checkout del Repositorio
      uses: actions/checkout@v4

    - name: Configurar .NET 9 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Restaurar Dependencias
      run: dotnet restore FileFlow.slnx

    - name: Compilar Solución (Release)
      run: dotnet build FileFlow.slnx -c Release --no-restore

    - name: Ejecutar Suite de Tests
      run: dotnet test FileFlow.slnx -c Release --no-build --verbosity normal

    - name: Publicar Artefacto Portable
      run: |
        dotnet publish FileFlow.App/FileFlow.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist
      
    - name: Subir Binario como Artefacto
      uses: actions/upload-artifact@v4
      with:
        name: FileFlowStudio-win-x64
        path: ./dist/
```
