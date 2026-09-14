import React, { useState } from 'react';
import { 
  Search, 
  X, 
  Sparkles, 
  Plus
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

const AVAILABLE_TEMPLATES: NodeTemplateItem[] = [
  // 1. Entrada / Archivos
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
    name: 'Generador Sintético',
    category: 'Entrada',
    description: 'Genera lotes de datos y archivos simulados para pruebas de estrés del pipeline.',
    iconName: 'input',
    template: {
      title: 'Generador Sintético',
      category: 'Entrada',
      description: 'Genera archivos de prueba sintéticos.',
      iconName: 'input',
      status: 'idle',
      inputs: [],
      outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'FileCount', displayName: 'Cantidad de Archivos', type: 'number', value: 10, min: 1, max: 1000 },
        { key: 'FileSizeKb', displayName: 'Tamaño (KB)', type: 'number', value: 50, min: 1, max: 10000 },
      ]
    }
  },

  // 2. Compresión
  {
    id: 'ArchiveFanOutNode',
    name: 'Desempaquetador Fan-Out',
    category: 'Compresión',
    description: 'Descomprime archivos (Zip, Rar, 7z) y emite cada elemento preservando la estructura de carpetas.',
    iconName: 'archive',
    template: {
      title: 'Fan-Out Desempaquetador',
      category: 'Compresión',
      description: 'Descomprime archivos y emite cada elemento secuencialmente.',
      iconName: 'archive',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Comprimido', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Elemento Extraído', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Engine', displayName: 'Motor de Descompresión', type: 'select', value: 'Universal', options: ['Universal', 'ZipNative', '7ZipCli', 'SharpCompress'] },
        { key: 'ExtractNested', displayName: 'Extraer Comprimidos Anidados', type: 'boolean', value: false },
      ]
    }
  },
  {
    id: 'ArchiveFanInNode',
    name: 'Re-empaquetador Fan-In',
    category: 'Compresión',
    description: 'Consolida todos los elementos procesados de un flujo y crea un archivo comprimido nuevo atómico.',
    iconName: 'zip',
    template: {
      title: 'Fan-In Re-empaquetador',
      category: 'Compresión',
      description: 'Reagrupa los elementos procesados en un único archivo comprimido.',
      iconName: 'zip',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Elemento', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Comprimido Final', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Format', displayName: 'Formato de Salida', type: 'select', value: 'Zip', options: ['Zip', '7z', 'Tar'] },
        { key: 'CompressionLevel', displayName: 'Nivel de Compresión', type: 'select', value: 'Optimal', options: ['Fastest', 'Optimal', 'Maximum'] },
      ]
    }
  },

  // 3. Imágenes
  {
    id: 'ImageOptimizerNode',
    name: 'Optimizador de Imágenes',
    category: 'Imágenes',
    description: 'Comprime y optimiza imágenes PNG, JPEG y WebP sin pérdida apreciable de calidad.',
    iconName: 'image',
    template: {
      title: 'Optimizador de Imágenes',
      category: 'Imágenes',
      description: 'Optimización WebP/MozJPEG con cero pérdida visible.',
      iconName: 'image',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Optimizada', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Quality', displayName: 'Calidad (1-100)', type: 'number', value: 82, min: 10, max: 100 },
        { key: 'PassThroughNonImages', displayName: 'Passthrough No-Imágenes', type: 'boolean', value: true },
        { key: 'KeepOriginalIfLarger', displayName: 'Mantener si es Mayor', type: 'boolean', value: true },
      ]
    }
  },

  // 4. Lógica y Filtros
  {
    id: 'ConditionNode',
    name: 'Condición / Filtro',
    category: 'Lógica',
    description: 'Bifurca el flujo según reglas de tamaño, extensión, fecha o metadatos dinámicos.',
    iconName: 'filter',
    template: {
      title: 'Filtro Condicional',
      category: 'Lógica',
      description: 'Evalúa condiciones lógicas y bifurca a Verdadero o Falso.',
      iconName: 'filter',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'true', name: 'Verdadero', type: 'FileItem', direction: 'output' },
        { id: 'false', name: 'Falso', type: 'FileItem', direction: 'output' }
      ],
      parameters: [
        { key: 'Property', displayName: 'Propiedad a Evaluar', type: 'select', value: 'Extension', options: ['Extension', 'FileSize', 'MimeType', 'IsImage'] },
        { key: 'Operator', displayName: 'Operador', type: 'select', value: 'In', options: ['Equals', 'Contains', 'In', 'GreaterThan'] },
        { key: 'Value', displayName: 'Valor de Comparación', type: 'string', value: '.jpg, .png, .webp' },
      ]
    }
  },

  // 5. Inteligencia Artificial
  {
    id: 'MultimodalVisionLlmNode',
    name: 'Visión Multimodal (VLM)',
    category: 'IA',
    description: 'Etiqueta y extrae descripciones o JSON estructurado mediante modelos visuales locales o en la nube.',
    iconName: 'vlm',
    template: {
      title: 'Visión Multimodal VLM',
      category: 'IA',
      description: 'Inferencia VLM multimodal (Structured Outputs JSON).',
      iconName: 'vlm',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Con Metadatos', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Prompt', displayName: 'Prompt de Análisis', type: 'string', value: 'Describe detalladamente la imagen y detecta texto relevante.' },
        { key: 'MaxConcurrency', displayName: 'Concurrencia Máxima', type: 'number', value: 4, min: 1, max: 16 },
      ]
    }
  },

  // 6. Salida / Destino
  {
    id: 'OutputDirectoryNode',
    name: 'Directorio de Salida',
    category: 'Destino',
    description: 'Escribe los archivos finales en la carpeta de destino resolviendo variables dinámicas.',
    iconName: 'output',
    template: {
      title: 'Directorio de Salida',
      category: 'Destino',
      description: 'Escribe archivos en destino con resolución de variables.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Confirmado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'DestinationFolder', displayName: 'Carpeta Destino', type: 'path', value: 'C:/Salida/{RelativeDir}' },
        { key: 'CollisionMode', displayName: 'En caso de Conflicto', type: 'select', value: 'RenameWithCounter', options: ['Overwrite', 'RenameWithCounter', 'Skip'] },
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

  if (!isOpen) return null;

  const categories = ['all', 'Entrada', 'Compresión', 'Imágenes', 'Lógica', 'IA', 'Destino'];

  const filteredTemplates = AVAILABLE_TEMPLATES.filter(item => {
    const matchesSearch = item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                          item.description.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCat = selectedCategory === 'all' || item.category === selectedCategory;
    return matchesSearch && matchesCat;
  });

  return (
    <aside className="library-drawer-container">
      <div className="drawer-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Sparkles size={16} color="var(--accent-blue)" />
          <span className="drawer-title">Biblioteca de Nodos</span>
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
