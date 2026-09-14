import React, { useState, useEffect } from 'react';
import { 
  Search, 
  X, 
  Sparkles, 
  Plus,
  RefreshCw
} from 'lucide-react';
import type { FlowNodeData } from '../types/flow';

interface NodeLibraryDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  onAddNode: (template: FlowNodeData) => void;
}

interface NodeTemplateItem {
  id: string;
  name: string;
  category: string;
  description: string;
  iconName: string;
  template: FlowNodeData;
}

// Catálogo maestro completo con más de 40 nodos de FileFlow Studio
const MASTER_CATALOG: NodeTemplateItem[] = [
  // === 1. ENTRADA Y ARCHIVOS ===
  {
    id: 'FolderWatcherNode',
    name: 'Vigilante de Carpetas',
    category: 'Entrada',
    description: 'Monitorea un directorio del sistema de archivos e ingiere archivos entrantes.',
    iconName: 'folder',
    template: {
      title: 'Vigilante de Carpetas',
      category: 'Entrada',
      description: 'Monitorea carpetas para nuevos ficheros.',
      iconName: 'folder',
      status: 'idle',
      inputs: [],
      outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'WatchPath', displayName: 'Ruta a Vigilar', type: 'path', value: 'C:/Entrada' },
        { key: 'IncludeSubdirectories', displayName: 'Incluir Subdirectorios', type: 'boolean', value: true },
        { key: 'Filter', displayName: 'Filtro de Archivos', type: 'string', value: '*.*' },
      ]
    }
  },
  {
    id: 'SyntheticDataSourceNode',
    name: 'Generador Sintético de Datos',
    category: 'Entrada',
    description: 'Emite archivos de prueba categorizados (Películas, Series, Cómics, Música o Personalizados) para pruebas y depuración de pipelines sin requerir archivos reales.',
    iconName: 'input',
    template: {
      title: 'Generador Sintético',
      category: 'Entrada',
      description: 'Emite archivos de prueba categorizados para testing sin tocar el disco.',
      iconName: 'input',
      status: 'idle',
      inputs: [],
      outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Category', displayName: 'Categoría', type: 'select', value: 'Películas', options: ['Todas', 'Películas', 'Series', 'Cómics y Manga', 'Música', 'Fotos', 'Documentos', 'Personalizada'] },
        { key: 'EmissionMode', displayName: 'Modo de Emisión', type: 'select', value: 'Virtual', options: ['Virtual', 'PhysicalMock'] },
        { key: 'MaxItems', displayName: 'Límite de Ítems (0 = todos)', type: 'number', value: 10, min: 0, max: 1000 },
        { key: 'EmissionDelayMs', displayName: 'Retardo por Ítem (ms)', type: 'number', value: 0, min: 0, max: 10000 },
        { key: 'EmitDirectories', displayName: 'Emitir Directorios', type: 'boolean', value: false },
        { key: 'CustomItems', displayName: 'Ítems Personalizados', type: 'multiline', value: '' },
        { key: 'OutputFolder', displayName: 'Carpeta de Salida', type: 'path', value: '' }
      ],
      customActions: [
        { actionId: 'OpenDataSetDesigner', title: '📊 Diseñador de Datasets...', icon: '📊', tooltip: 'Abrir el Diseñador Visual de Datasets Sintéticos' }
      ]
    }
  },
  {
    id: 'AdvancedRenamerNode',
    name: 'Renombrador Avanzado con Tokens',
    category: 'Archivos',
    description: 'Renombra archivos y carpetas masivamente aplicando un pipeline acumulativo de métodos secuenciales (plantillas, regex, mayúsculas, numeración y normalización).',
    iconName: 'file',
    template: {
      title: 'Renombrador Avanzado',
      category: 'Archivos',
      description: 'Pipeline secuencial de 7 métodos de renombrado con resolución de colisiones.',
      iconName: 'file',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'out', name: 'Renombrado', type: 'FileItem', direction: 'output' },
        { id: 'skipped', name: 'Omitido', type: 'FileItem', direction: 'output' },
        { id: 'error', name: 'Error', type: 'FileItem', direction: 'output' }
      ],
      parameters: [
        { key: 'PipelineName', displayName: 'Nombre del Pipeline', type: 'string', value: 'Pipeline Predeterminado' },
        { key: 'RenameMode', displayName: 'Modo de Ejecución', type: 'select', value: 'Virtual', options: ['Virtual', 'DirectInPlace'] },
        { key: 'CollisionStrategy', displayName: 'Estrategia de Colisión', type: 'select', value: 'AutoIncrement', options: ['AutoIncrement', 'Overwrite', 'Skip', 'Fail'] },
        { key: 'MethodSteps', displayName: 'Pasos de Renombrado (JSON)', type: 'multiline', value: '' }
      ],
      customActions: [
        { actionId: 'OpenRenamerPipeline', title: '🏷️ Pipeline de Métodos...', icon: '🏷️', tooltip: 'Abrir el Estudio de Renombrado Avanzado con 7 métodos y vista previa' }
      ]
    }
  },
  {
    id: 'DirectoryInspectorNode',
    name: 'Inspector de Directorio',
    category: 'Entrada',
    description: 'Escanea y analiza un directorio calculando estadísticas de ficheros y tamaños.',
    iconName: 'folder',
    template: {
      title: 'Inspector de Directorio',
      category: 'Entrada',
      description: 'Inspecciona y clasifica la jerarquía de carpetas.',
      iconName: 'folder',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Carpeta', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Detalles', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Recursive', displayName: 'Recursivo', type: 'boolean', value: true }
      ]
    }
  },

  // === 2. COMPRESIÓN ===
  {
    id: 'SmartUnpackNode',
    name: 'Descompresor Inteligente',
    category: 'Compresión',
    description: 'Descomprime automáticamente cualquier formato (.zip, .rar, .7z, .tar.gz) detectando el motor óptimo.',
    iconName: 'archive',
    template: {
      title: 'Descompresor Inteligente',
      category: 'Compresión',
      description: 'Descompresión multi-formato automática.',
      iconName: 'archive',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Comprimido', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Extraído', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'TempDir', displayName: 'Directorio Temporal', type: 'path', value: '{TempWorkspace}' }
      ]
    }
  },
  {
    id: 'ArchiveFanOutNode',
    name: 'Desempaquetador Fan-Out',
    category: 'Compresión',
    description: 'Descomprime un archivo y emite cada elemento secuencialmente preservando rutas relativas.',
    iconName: 'archive',
    template: {
      title: 'Fan-Out Desempaquetador',
      category: 'Compresión',
      description: 'Desempaqueta y propaga elementos individuales.',
      iconName: 'archive',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Comprimido', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Elemento', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Engine', displayName: 'Estrategia', type: 'select', value: 'Universal', options: ['Universal', 'ZipNative', '7ZipCli', 'SharpCompress'] }
      ]
    }
  },
  {
    id: 'ArchiveFanInNode',
    name: 'Re-empaquetador Fan-In',
    category: 'Compresión',
    description: 'Reagrupa todos los elementos procesados de un flujo en un nuevo archivo comprimido atómico.',
    iconName: 'zip',
    template: {
      title: 'Fan-In Re-empaquetador',
      category: 'Compresión',
      description: 'Consolida elementos en un comprimido final.',
      iconName: 'zip',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Elemento', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Archivo Final', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Format', displayName: 'Formato', type: 'select', value: 'Zip', options: ['Zip', '7z', 'Tar'] },
        { key: 'CompressionLevel', displayName: 'Compresión', type: 'select', value: 'Optimal', options: ['Fastest', 'Optimal', 'Maximum'] }
      ]
    }
  },

  // === 3. IMÁGENES ===
  {
    id: 'ImageOptimizerNode',
    name: 'Optimizador de Imágenes',
    category: 'Imágenes',
    description: 'Comprime WebP, JPEG y PNG reduciendo tamaño con máxima calidad perceptual.',
    iconName: 'image',
    template: {
      title: 'Optimizador de Imágenes',
      category: 'Imágenes',
      description: 'Optimización WebP/MozJPEG sin pérdida visible.',
      iconName: 'image',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Optimizada', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Quality', displayName: 'Calidad (1-100)', type: 'number', value: 85, min: 10, max: 100 },
        { key: 'PassThroughNonImages', displayName: 'Passthrough No-Imágenes', type: 'boolean', value: true },
        { key: 'KeepOriginalIfLarger', displayName: 'Conservar Original si es Mayor', type: 'boolean', value: true }
      ]
    }
  },
  {
    id: 'ExifMetadataNode',
    name: 'Metadatos EXIF',
    category: 'Imágenes',
    description: 'Extrae metadatos de cámara, fecha, geolocalización GPS o limpia etiquetas de privacidad.',
    iconName: 'image',
    template: {
      title: 'Metadatos EXIF',
      category: 'Imágenes',
      description: 'Lectura o purga de etiquetas EXIF/IPTC.',
      iconName: 'image',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Imagen', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'StripExif', displayName: 'Purgar Metadatos EXIF', type: 'boolean', value: false }
      ]
    }
  },

  // === 4. INTELIGENCIA ARTIFICIAL ===
  {
    id: 'MultimodalVisionLlmNode',
    name: 'Visión Multimodal VLM',
    category: 'IA',
    description: 'Inferencia con modelos multimodales (OpenAI, Gemini, Ollama) con salida estructurada JSON.',
    iconName: 'vlm',
    template: {
      title: 'Visión Multimodal VLM',
      category: 'IA',
      description: 'Análisis semántico multimodal con Structured Outputs.',
      iconName: 'vlm',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Con Metadatos', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Prompt', displayName: 'Prompt de Análisis', type: 'string', value: 'Describe detalladamente la imagen y detecta etiquetas.' },
        { key: 'MaxConcurrency', displayName: 'Concurrencia Máxima', type: 'number', value: 4, min: 1, max: 16 }
      ]
    }
  },
  {
    id: 'ObjectDetectorNode',
    name: 'Detector de Objetos (YOLO)',
    category: 'IA',
    description: 'Detección y delimitación de objetos en tiempo real con tensores ONNX (YOLOv8 / YOLO-World).',
    iconName: 'vlm',
    template: {
      title: 'Detector de Objetos (YOLO)',
      category: 'IA',
      description: 'Detección de objetos ONNX local con bounding boxes.',
      iconName: 'vlm',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'detected', name: 'Detectado', type: 'FileItem', direction: 'output' },
        { id: 'not_detected', name: 'No Detectado', type: 'FileItem', direction: 'output' }
      ],
      parameters: [
        { key: 'ConfidenceThreshold', displayName: 'Umbral de Confianza', type: 'number', value: 0.45, min: 0.1, max: 1.0 }
      ]
    }
  },
  {
    id: 'BackgroundRemoverNode',
    name: 'Eliminador de Fondos (RMBG)',
    category: 'IA',
    description: 'Eliminación fotográfica de fondos con modelo RMBG / Bria ONNX con canal alfa perfecto.',
    iconName: 'vlm',
    template: {
      title: 'Eliminador de Fondos RMBG',
      category: 'IA',
      description: 'Recorte automático de siluetas y fondos.',
      iconName: 'vlm',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Transparente', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'RefineEdges', displayName: 'Refinar Bordes', type: 'boolean', value: true }
      ]
    }
  },
  {
    id: 'LocalOcrNode',
    name: 'Reconocimiento OCR (Tesseract)',
    category: 'IA',
    description: 'Extracción de texto impreso y documentos escaneados con motor local multi-idioma.',
    iconName: 'vlm',
    template: {
      title: 'Reconocimiento OCR',
      category: 'IA',
      description: 'OCR local mediante Tesseract y Leptonica.',
      iconName: 'vlm',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen/PDF', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Texto Extraído', type: 'Text', direction: 'output' }],
      parameters: [
        { key: 'Language', displayName: 'Idioma', type: 'select', value: 'spa+eng', options: ['spa+eng', 'spa', 'eng', 'fra', 'deu'] }
      ]
    }
  },

  // === 5. LÓGICA Y CONTROL DE FLUJO ===
  {
    id: 'ConditionNode',
    name: 'Filtro Condicional',
    category: 'Lógica',
    description: 'Bifurca el flujo hacia Verdadero o Falso según reglas de tamaño, tipo de archivo o metadatos.',
    iconName: 'filter',
    template: {
      title: 'Filtro Condicional',
      category: 'Lógica',
      description: 'Bifurca a Verdadero o Falso.',
      iconName: 'filter',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'true', name: 'Verdadero', type: 'FileItem', direction: 'output' },
        { id: 'false', name: 'Falso', type: 'FileItem', direction: 'output' }
      ],
      parameters: [
        { key: 'Property', displayName: 'Propiedad', type: 'select', value: 'Extension', options: ['Extension', 'FileSize', 'MimeType', 'IsImage'] },
        { key: 'Operator', displayName: 'Operador', type: 'select', value: 'In', options: ['Equals', 'Contains', 'In', 'GreaterThan'] },
        { key: 'Value', displayName: 'Valor', type: 'string', value: '.jpg, .png, .webp' }
      ]
    }
  },
  {
    id: 'SwitchCaseNode',
    name: 'Enrutador Switch-Case',
    category: 'Lógica',
    description: 'Bifurca en múltiples ramas independientes según valores clave de metadatos o expresiones.',
    iconName: 'filter',
    template: {
      title: 'Enrutador Switch-Case',
      category: 'Lógica',
      description: 'Múltiples salidas condicionales.',
      iconName: 'filter',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'case_a', name: 'Caso A', type: 'FileItem', direction: 'output' },
        { id: 'case_b', name: 'Caso B', type: 'FileItem', direction: 'output' },
        { id: 'default', name: 'Por Defecto', type: 'FileItem', direction: 'output' }
      ],
      parameters: [
        { key: 'VariableKey', displayName: 'Clave Variable', type: 'string', value: 'MimeType' }
      ]
    }
  },
  {
    id: 'FileForkNode',
    name: 'Bifurcador Fork (Duplicador)',
    category: 'Lógica',
    description: 'Duplica el contexto del archivo hacia dos ramas paralelas independientes.',
    iconName: 'filter',
    template: {
      title: 'Bifurcador Fork',
      category: 'Lógica',
      description: 'Envía una copia idéntica a dos ramas.',
      iconName: 'filter',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'branch_a', name: 'Rama A', type: 'FileItem', direction: 'output' },
        { id: 'branch_b', name: 'Rama B', type: 'FileItem', direction: 'output' }
      ],
      parameters: []
    }
  },
  {
    id: 'ForkJoinBarrierNode',
    name: 'Barrera de Sincronización Join',
    category: 'Lógica',
    description: 'Espera a que dos ramas paralelas concluyan antes de continuar el flujo aguas abajo.',
    iconName: 'filter',
    template: {
      title: 'Barrera Join',
      category: 'Lógica',
      description: 'Sincroniza dos ramas concurrentes.',
      iconName: 'filter',
      status: 'idle',
      inputs: [
        { id: 'in_a', name: 'Rama A', type: 'FileItem', direction: 'input' },
        { id: 'in_b', name: 'Rama B', type: 'FileItem', direction: 'input' }
      ],
      outputs: [{ id: 'out', name: 'Sincronizado', type: 'FileItem', direction: 'output' }],
      parameters: []
    }
  },
  {
    id: 'ThrottleDelayNode',
    name: 'Controlador de Tasa (Throttle)',
    category: 'Lógica',
    description: 'Introduce pausas o limita la tasa de emisión de archivos por segundo hacia APIs o servicios.',
    iconName: 'filter',
    template: {
      title: 'Controlador de Tasa',
      category: 'Lógica',
      description: 'Retarda o limita la tasa de elementos.',
      iconName: 'filter',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Salida', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'DelayMs', displayName: 'Pausa (ms)', type: 'number', value: 250, min: 0, max: 10000 }
      ]
    }
  },

  // === 6. HASHING Y DEDUPLICACIÓN ===
  {
    id: 'HashCalculatorNode',
    name: 'Calculador de Hashes',
    category: 'Hashes',
    description: 'Calcula sumas criptográficas (MD5, SHA-1, SHA-256, XXHash64) y las añade a los metadatos.',
    iconName: 'cpu',
    template: {
      title: 'Calculador de Hashes',
      category: 'Hashes',
      description: 'Genera SHA-256 / MD5 / XXHash64.',
      iconName: 'cpu',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Archivo', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Con Hash', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Algorithm', displayName: 'Algoritmo', type: 'select', value: 'SHA256', options: ['SHA256', 'MD5', 'SHA1', 'XXHash64'] }
      ]
    }
  },
  {
    id: 'DeduplicationFilterNode',
    name: 'Filtro de Deduplicación',
    category: 'Hashes',
    description: 'Detecta y descarta archivos duplicados basados en hash o tamaño exacto.',
    iconName: 'cpu',
    template: {
      title: 'Filtro Deduplicador',
      category: 'Hashes',
      description: 'Filtra duplicados idénticos en el lote.',
      iconName: 'cpu',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'unique', name: 'Único', type: 'FileItem', direction: 'output' },
        { id: 'duplicate', name: 'Duplicado', type: 'FileItem', direction: 'output' }
      ],
      parameters: []
    }
  },

  // === 7. DOCUMENTOS (PDF) ===
  {
    id: 'PdfTextExtractorNode',
    name: 'Extractor de Texto PDF',
    category: 'Documentos',
    description: 'Extrae texto editable y tablas de documentos PDF.',
    iconName: 'file',
    template: {
      title: 'Extractor de Texto PDF',
      category: 'Documentos',
      description: 'Extracción de texto en documentos PDF.',
      iconName: 'file',
      status: 'idle',
      inputs: [{ id: 'in', name: 'PDF', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Texto', type: 'Text', direction: 'output' }],
      parameters: []
    }
  },
  {
    id: 'PdfSplitNode',
    name: 'Divisor de PDF (Splitter)',
    category: 'Documentos',
    description: 'Divide un documento PDF en páginas individuales o por rangos.',
    iconName: 'file',
    template: {
      title: 'Divisor de PDF',
      category: 'Documentos',
      description: 'Segmenta PDFs en páginas separadas.',
      iconName: 'file',
      status: 'idle',
      inputs: [{ id: 'in', name: 'PDF', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Página', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'PageRange', displayName: 'Rango de Páginas', type: 'string', value: 'all' }
      ]
    }
  },

  // === 8. INTEGRACIONES Y SCRIPTS ===
  {
    id: 'CliExecutionNode',
    name: 'Ejecutor CLI Externo',
    category: 'Integraciones',
    description: 'Ejecuta herramientas de línea de comandos externas (ffmpeg, exiftool, 7z) pasando el archivo activo.',
    iconName: 'cpu',
    template: {
      title: 'Ejecutor CLI',
      category: 'Integraciones',
      description: 'Llama a ejecutables y scripts externos.',
      iconName: 'cpu',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Archivo', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Resultado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'ExecutablePath', displayName: 'Ejecutable', type: 'string', value: 'ffmpeg' },
        { key: 'Arguments', displayName: 'Argumentos', type: 'string', value: '-i "{FilePath}" -vf scale=1280:-1 "{OutputDir}/{FileName}.jpg"' }
      ]
    }
  },
  {
    id: 'CustomScriptNode',
    name: 'Script C# / Python',
    category: 'Integraciones',
    description: 'Permite ejecutar código arbitrario C# compilado en caliente con Roslyn.',
    iconName: 'cpu',
    template: {
      title: 'Script C# Roslyn',
      category: 'Integraciones',
      description: 'Ejecuta código C# dinámico con acceso al contexto.',
      iconName: 'cpu',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Salida', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'ScriptBody', displayName: 'Código C#', type: 'string', value: '// Modificar item.Metadata["Custom"] = "Value";\nreturn item;' }
      ]
    }
  },
  {
    id: 'WebhookNotificationNode',
    name: 'Notificador Webhook HTTP',
    category: 'Integraciones',
    description: 'Envía notificaciones POST JSON a Slack, Discord o endpoints REST de monitorización.',
    iconName: 'cpu',
    template: {
      title: 'Notificador Webhook',
      category: 'Integraciones',
      description: 'Publica telemetría a endpoints HTTP.',
      iconName: 'cpu',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Evento', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Confirmado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'EndpointUrl', displayName: 'URL de Webhook', type: 'string', value: 'https://webhook.site/...' }
      ]
    }
  },

  // === 9. DESTINO Y ACCIONES ===
  {
    id: 'AdvancedRenamerNode',
    name: 'Renombrador Avanzado con Tokens',
    category: 'Destino',
    description: 'Renombra archivos masivamente con plantillas ({FileName}, {Date}, {Archive:RelativeDir}, {Exif:Date}).',
    iconName: 'output',
    template: {
      title: 'Renombrador Avanzado',
      category: 'Destino',
      description: 'Plantillas y sustitución masiva de nombres.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Archivo', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Renombrado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Pattern', displayName: 'Patrón de Nombre', type: 'string', value: '{Date:yyyy-MM-dd}_{FileName}' }
      ]
    }
  },
  {
    id: 'OutputDirectoryNode',
    name: 'Directorio de Salida (Sink)',
    category: 'Destino',
    description: 'Escribe los archivos finales en la carpeta de destino resolviendo variables dinámicas.',
    iconName: 'output',
    template: {
      title: 'Directorio de Salida',
      category: 'Destino',
      description: 'Escribe archivos en la carpeta de destino.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Confirmado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'DestinationFolder', displayName: 'Carpeta Destino', type: 'path', value: 'D:/Salida/{RelativeDir}' },
        { key: 'CollisionMode', displayName: 'En caso de Conflicto', type: 'select', value: 'RenameWithCounter', options: ['Overwrite', 'RenameWithCounter', 'Skip'] }
      ]
    }
  },
  {
    id: 'OriginalFileActionNode',
    name: 'Acción sobre Archivo Origen',
    category: 'Destino',
    description: 'Gobierna el archivo original al concluir el flujo: conservar, mover a cuarentena, papelera o borrado seguro.',
    iconName: 'output',
    template: {
      title: 'Acción Archivo Origen',
      category: 'Destino',
      description: 'Manejo no destructivo o purga del original.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Completado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Action', displayName: 'Acción', type: 'select', value: 'Keep', options: ['Keep', 'MoveToQuarantine', 'SendToRecycleBin', 'HardDelete'] }
      ]
    }
  },
  {
    id: 'LogOutputNode',
    name: 'Registro de Salida (Log)',
    category: 'Destino',
    description: 'Imprime mensajes personalizados con variables dinámicas en la consola de telemetría.',
    iconName: 'output',
    template: {
      title: 'Registro de Salida',
      category: 'Destino',
      description: 'Emite mensajes a la consola de logs.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Continuar', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'CustomMessage', displayName: 'Mensaje', type: 'string', value: 'Procesado con éxito: {FileName} ({FileSize})' }
      ]
    }
  }
];

export const NodeLibraryDrawer: React.FC<NodeLibraryDrawerProps> = ({
  isOpen,
  onClose,
  onAddNode,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [catalogItems, setCatalogItems] = useState<NodeTemplateItem[]>(MASTER_CATALOG);
  const [isLoadingCatalog, setIsLoadingCatalog] = useState(false);

  // Auto-descubrimiento desde el backend si está activo
  useEffect(() => {
    if (isOpen) {
      setIsLoadingCatalog(true);
      fetch('/api/nodes/catalog')
        .then(res => {
          if (res.ok) return res.json();
          throw new Error('Servidor no disponible');
        })
        .then(data => {
          if (Array.isArray(data) && data.length > 0) {
            const serverItems: NodeTemplateItem[] = data.map((d: any) => ({
              id: d.id,
              name: d.title || d.id,
              category: d.category || 'General',
              description: d.description || '',
              iconName: d.category?.toLowerCase() || 'cpu',
              template: {
                title: d.title || d.id,
                category: d.category || 'General',
                description: d.description || '',
                iconName: d.category?.toLowerCase() || 'cpu',
                status: 'idle',
                inputs: d.inputs || [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
                outputs: d.outputs || [{ id: 'out', name: 'Salida', type: 'FileItem', direction: 'output' }],
                parameters: d.parameters || [],
                customActions: d.customActions || []
              }
            }));
            setCatalogItems(serverItems);
          }
        })
        .catch(() => {
          // Fallback seguro a catálogo local completo
          setCatalogItems(MASTER_CATALOG);
        })
        .finally(() => {
          setIsLoadingCatalog(false);
        });
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const categories = ['all', 'Entrada', 'Compresión', 'Imágenes', 'IA', 'Lógica', 'Hashes', 'Documentos', 'Integraciones', 'Destino'];

  const filteredTemplates = catalogItems.filter(item => {
    const matchesSearch = item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                          item.description.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCat = selectedCategory === 'all' || item.category.toLowerCase().includes(selectedCategory.toLowerCase());
    return matchesSearch && matchesCat;
  });

  return (
    <aside className="library-drawer-container">
      <div className="drawer-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Sparkles size={16} color="var(--accent-blue)" />
          <span className="drawer-title">Biblioteca de Nodos</span>
          <span style={{ fontSize: '10px', color: 'var(--text-dim)' }}>({catalogItems.length} disponibles)</span>
        </div>
        <button className="btn-icon" onClick={onClose} title="Cerrar">
          <X size={16} />
        </button>
      </div>

      {/* Buscador */}
      <div className="search-box">
        <Search size={14} color="var(--text-dim)" />
        <input 
          type="text" 
          placeholder="Buscar nodos por nombre o función..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
        {isLoadingCatalog && <RefreshCw size={13} className="spin-slow" color="var(--accent-blue)" />}
      </div>

      {/* Categorías Pills */}
      <div className="category-pills">
        {categories.map(cat => (
          <button
            key={cat}
            className={`pill-btn ${selectedCategory === cat ? 'active' : ''}`}
            onClick={() => setSelectedCategory(cat)}
          >
            {cat === 'all' ? 'Todos' : cat}
          </button>
        ))}
      </div>

      {/* Lista de Nodos */}
      <div className="library-items-list">
        {filteredTemplates.map(item => (
          <div key={item.id} className="library-node-card">
            <div className="library-node-card-header">
              <span className="card-node-title">{item.name}</span>
              <span className="card-node-cat">{item.category}</span>
            </div>
            <p className="card-node-desc">{item.description}</p>
            <div style={{ display: 'flex', gap: '6px', fontSize: '10px', color: 'var(--text-dim)', marginBottom: '8px' }}>
              <span>Entradas: {item.template.inputs.length}</span>
              <span>•</span>
              <span>Salidas: {item.template.outputs.length}</span>
            </div>
            <button 
              className="btn-add-node"
              onClick={() => onAddNode(item.template)}
              title="Añadir al centro del lienzo"
            >
              <Plus size={13} />
              <span>Añadir al Lienzo</span>
            </button>
          </div>
        ))}

        {filteredTemplates.length === 0 && (
          <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-dim)', fontSize: '12px' }}>
            No se encontraron nodos que coincidan con la búsqueda.
          </div>
        )}
      </div>
    </aside>
  );
};
