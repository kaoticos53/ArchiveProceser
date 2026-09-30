# Guía de Instalación y Despliegue - FileFlow Studio

## 1. Requisitos Previos del Sistema

Antes de compilar o desplegar **FileFlow Studio**, asegúrate de que el entorno cumple con los siguientes requisitos.

### 1.1. Entorno de Desarrollo (Compilación desde código fuente)
- **Sistema Operativo**: Windows 10 (1809+) / Windows 11, Linux (X11 o Wayland) o macOS 12+.
- **SDK de .NET**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (versión `10.0.100` o superior).
- **Cargas de trabajo opcionales**:
  - `wasm-tools` para el target Web (`net10.0-browserwasm`).
  - `ios` + Xcode para el target iPadOS/iPhone (`net10.0-ios`, requiere macOS).
- **Herramientas de Línea de Comandos**: PowerShell 7+ (o `bash` en Linux/macOS).
- **IDE Recomendado**: Visual Studio 2022 (v17.13+, con carga de trabajo *.NET Multi-platform App UI development*), JetBrains Rider 2024.3+ o VS Code con extensión *C# Dev Kit*.

### 1.2. Entorno de Ejecución (Para usuarios finales)
- **Runtime**: el runtime de **.NET 10** si se utiliza la versión *framework-dependent*, o ninguno si se utiliza la versión autónoma (*self-contained*).
- **Dependencias de escritorio**:
  - **Windows**: Windows App SDK (se incluye en el binario autónomo).
  - **Linux**: `libfontconfig1`, `libx11-6`, `libice6`, `libsm6`, `libxext6`, `libxi6`, `libxrender1`, `libxtst6`.
  - **macOS**: macOS 12+ (Skia Desktop).
- **Herramientas Opcionales (Integraciones de Dominio)**:
  - **FFmpeg**: Necesario para el nodo `MediaTranscoderNode`. Se recomienda tener `ffmpeg` y `ffprobe` en el `PATH`.
  - **7-Zip CLI**: Opcional para formatos propietarios protegidos en `SmartUnpackNode`.

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
```powershell
.\run.ps1          # compila y arranca el host Uno (Windows)
```
En Linux/macOS:
```bash
./run.sh           # compila y arranca el host Uno (Skia Desktop)
```

### 3.2. Mediante el CLI de .NET
Para compilar la solución completa en modo Debug:
```powershell
dotnet build FileFlow.slnx -c Debug
```

Para arrancar el host Uno:
```powershell
dotnet run --project FileFlow.App.Uno/FileFlow.App.Uno.csproj -c Debug
```

Para compilar cada familia de plataformas:
```powershell
.\build-matrix.ps1                       # desktop + web
.\build-matrix.ps1 -IncludeIos           # añade iOS/iPadOS (macOS + Xcode)
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

El host Uno selecciona la plataforma con `-p:FileFlowTarget=<windows|desktop|wasm|ios>`.

### 5.1. Publicación Autónoma (Windows, WinUI 3)
```powershell
dotnet publish FileFlow.App.Uno/FileFlow.App.Uno.csproj `
  -c Release `
  -p:FileFlowTarget=windows `
  -r win-x64 `
  --self-contained true `
  -o ./publish/FileFlowStudio-win-x64
```

### 5.2. Publicación para Linux / macOS (Skia Desktop)
```bash
dotnet publish FileFlow.App.Uno/FileFlow.App.Uno.csproj \
  -c Release -p:FileFlowTarget=desktop -r linux-x64 --self-contained true \
  -o ./publish/FileFlowStudio-linux-x64
```

### 5.3. Publicación para Web (WASM)
```bash
dotnet publish FileFlow.App.Uno/FileFlow.App.Uno.csproj \
  -c Release -p:FileFlowTarget=wasm -o ./publish/FileFlowStudio-web
```

### 5.4. Publicación Dependiente del Marco (*Framework-Dependent*)
```powershell
dotnet publish FileFlow.App.Uno/FileFlow.App.Uno.csproj `
  -c Release -p:FileFlowTarget=windows -r win-x64 `
  --self-contained false `
  -o ./publish/FileFlowStudio-portable
```

---

## 6. Pipeline de Integración Continua (CI/CD)

El flujo real vive en [`.github/workflows/ci.yml`](../.github/workflows/ci.yml): valida el build y la suite en Windows, los instaladores en Linux y una **matriz de compilación multiplataforma** (desktop, wasm, ios). Esquema:

```yaml
name: FileFlow Studio CI/CD
on:
  push:
    branches: [ main, master ]
  pull_request:
    branches: [ main, master ]

jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v4
    - name: Configurar .NET 10 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - run: dotnet restore FileFlow.slnx
    - run: dotnet build FileFlow.slnx -c Release --no-restore
    - run: dotnet test FileFlow.slnx -c Release --no-build

  build-platforms:
    strategy:
      fail-fast: false
      matrix:
        include:
          - { os: ubuntu-latest, target: desktop }
          - { os: ubuntu-latest, target: wasm }
          - { os: macos-latest,  target: ios }
    runs-on: ${{ matrix.os }}
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - run: dotnet workload install wasm-tools
    - run: dotnet build FileFlow.App.Uno/FileFlow.App.Uno.csproj -c Release -p:FileFlowTarget=${{ matrix.target }}
```
