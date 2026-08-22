# Guía de Instalación, Configuración y Despliegue - FileFlow Studio

## 1. Requisitos Previos del Sistema

Para compilar, ejecutar o desarrollar en **FileFlow Studio**, el sistema debe cumplir con los siguientes requisitos:

### 1.1 Requisitos de Entorno de Desarrollo
- **Sistema Operativo:** Windows 10 (Build 19041+) o Windows 11 (x64).
- **SDK del Lenguaje:** [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (versión 9.0.100 o superior).
- **Lenguaje:** C# 13 (incluido en .NET 9 SDK).
- **IDE Recomendado:** Visual Studio 2022 (v17.12+ con carga de trabajo `.NET Desktop Development`) o JetBrains Rider / VS Code con extensión C# Dev Kit.

### 1.2 Herramientas CLI Opcionales (Recomendadas para Integraciones)
Para habilitar todas las capacidades avanzadas multimedia, de compresión y automatización, se recomienda contar con:
- **FFmpeg / FFprobe:** (Para el nodo `Transcodificar Media`).
- **7-Zip (`7z.exe`):** (Para extracción y compresión en formatos 7Z/RAR).
- **Python (v3.10+):** (Para scripts en el nodo `Ejecutar Comando CLI`).

---

## 2. Configuración del Entorno de Desarrollo Local

### 2.1 Clonar el Repositorio
```powershell
git clone https://github.com/kaoticos53/ArchiveProceser.git
cd ArchiveProceser
```

### 2.2 Restauración de Paquetes NuGet
El proyecto utiliza la nueva especificación de solución XML de .NET 9 (`FileFlow.slnx`). Ejecuta:
```powershell
dotnet restore FileFlow.slnx
```

### 2.3 Compilación Básica
Para verificar que el proyecto compila limpiamente sin advertencias ni errores:
```powershell
dotnet build FileFlow.slnx --configuration Debug
```

---

## 3. Ejecución Rápida y Automatizada (`run.ps1`)

El repositorio incluye el script automatizado PowerShell `run.ps1` en la raíz, el cual compila todos los módulos en el orden de dependencia correcto y lanza la aplicación:

```powershell
.\run.ps1
```

Este script ejecuta automáticamente los siguientes pasos:
1. Compila `FileFlow.Sdk`.
2. Compila `FileFlow.Core`.
3. Compila el conjunto de plugins (`FileFlow.Plugin.*`).
4. Copia las DLLs de plugins al directorio `Plugins/` de la aplicación.
5. Inicia la aplicación principal `FileFlow.App.exe`.

---

## 4. Estructura de Almacenamiento y Archivos de Configuración

FileFlow Studio almacena automáticamente sus preferencias y configuraciones del usuario en el directorio del perfil de Windows:

```
%APPDATA%\FileFlowStudio\
├── user_preferences.json      # Preferencias generales (Tema, Hilos CPU, Salida Global, Logs)
├── media_presets.json         # Presets de transcodificación multimedia (MP3, 1080p, WebM, etc.)
└── external_tools.json        # Rutas de herramientas externas (FFmpeg, 7z, Python)
```

### 4.1 Autobúsqueda de Herramientas Externas
Si no deseas configurar manualmente las rutas de `ffmpeg.exe` o `7z.exe`, abre la aplicación y dirígete a:
**⚙️ Configuración del Flujo > 🛠️ Herramientas Externas > 🔍 Auto-Detectar Herramientas**

El sistema escaneará automáticamente el `PATH` del sistema, variables de entorno, carpetas de programas (`Program Files`, `AppData`, `WinGet`, `Chocolatey`, `Scoop`) y el Registro de Windows.

---

## 5. Empaquetado y Publicación para Producción

Para generar un ejecutable independiente optimizado para distribución (*Self-Contained Release*):

### 5.1 Publicar Aplicación Principal
```powershell
dotnet publish FileFlow.App/FileFlow.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o ./publish/FileFlowStudio
```

### 5.2 Publicar Plugins
```powershell
$plugins = @("FileSystem", "Archives", "Images", "Integrations", "Logic", "Hashing")
foreach ($plugin in $plugins) {
    dotnet publish FileFlow.Plugin.$plugin/FileFlow.Plugin.$plugin.csproj `
      -c Release `
      -o ./publish/FileFlowStudio/Plugins
}
```

El directorio `./publish/FileFlowStudio` contendrá el ejecutable listo para distribución sin necesidad de instalar el SDK de .NET en el equipo cliente.

---

## 6. Integración en Tuberías CI/CD (GitHub Actions / Azure DevOps)

Ejemplo de flujo de integración continua para GitHub Actions (`.github/workflows/build_and_test.yml`):

```yaml
name: FileFlow Studio CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET 9
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Restore dependencies
      run: dotnet restore FileFlow.slnx

    - name: Build Solution
      run: dotnet build FileFlow.slnx --configuration Release --no-restore

    - name: Run Test Suite
      run: dotnet test FileFlow.slnx --configuration Release --no-build --verbosity normal
```
