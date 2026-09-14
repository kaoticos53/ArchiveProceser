import { useState, useCallback, useRef, useEffect } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  addEdge,
  useReactFlow,
  ReactFlowProvider,
} from '@xyflow/react';
import type { Connection, Edge, Node, NodeTypes } from '@xyflow/react';
import { FlowNode } from './components/FlowNode';
import { ControlBar } from './components/ControlBar';
import { MenuBar } from './components/MenuBar';
import { NodeLibraryDrawer } from './components/NodeLibraryDrawer';
import { InspectorDrawer } from './components/InspectorDrawer';
import { LogConsole, type LogEntry } from './components/LogConsole';

// Diálogos Modales
import { AdvancedRenamerModal } from './components/modals/AdvancedRenamerModal';
import { SyntheticDataDesignerModal } from './components/modals/SyntheticDataDesignerModal';
import { RegexHelperModal } from './components/modals/RegexHelperModal';
import { ScriptEditorModal } from './components/modals/ScriptEditorModal';
import { VlmTesterModal } from './components/modals/VlmTesterModal';
import { SettingsModal } from './components/modals/SettingsModal';
import { VfsExplorerModal } from './components/modals/VfsExplorerModal';
import { ShortcutsModal } from './components/modals/ShortcutsModal';
import { AboutModal } from './components/modals/AboutModal';
import { UserManualModal } from './components/modals/UserManualModal';
import { TelemetryModal } from './components/modals/TelemetryModal';

import type { FlowNodeData, WorkflowTelemetry, WorkflowSettings } from './types/flow';
import './App.css';

const nodeTypes: NodeTypes = {
  flowNode: FlowNode,
};

// Grafo inicial enriquecido con el Renombrador Avanzado y Generador Sintético
const initialNodes: Node<FlowNodeData>[] = [
  {
    id: 'node-1',
    type: 'flowNode',
    position: { x: 80, y: 220 },
    data: {
      title: 'Generador Sintético',
      category: 'Entrada',
      description: 'Emite archivos de prueba categorizados (Películas, Series, Música).',
      iconName: 'input',
      status: 'idle',
      inputs: [],
      outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Category', displayName: 'Categoría', type: 'select', value: 'Películas', options: ['Películas', 'Series', 'Música', 'Fotos', 'Documentos'] },
        { key: 'EmissionMode', displayName: 'Modo de Emisión', type: 'select', value: 'Virtual', options: ['Virtual', 'PhysicalMock'] },
        { key: 'MaxItems', displayName: 'Límite de Ítems', type: 'number', value: 15, min: 1, max: 100 },
        { key: 'EmissionDelayMs', displayName: 'Retardo (ms)', type: 'number', value: 50, min: 0, max: 1000 },
        { key: 'EmitDirectories', displayName: 'Emitir Directorios', type: 'boolean', value: false },
        { key: 'CustomItems', displayName: 'Ítems Personalizados', type: 'multiline', value: '' },
      ],
      customActions: [
        { actionId: 'OpenDataSetDesigner', title: '📊 Diseñador de Datasets...', icon: '📊', tooltip: 'Diseñar lotes de archivos de prueba' }
      ],
      processedCount: 0,
      durationMs: 0,
    },
  },
  {
    id: 'node-2',
    type: 'flowNode',
    position: { x: 480, y: 220 },
    data: {
      title: 'Renombrador Avanzado',
      category: 'Archivos',
      description: 'Pipeline de 7 métodos: tokens {Date}, regex, mayúsculas y normalización.',
      iconName: 'file',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [
        { id: 'out', name: 'Renombrado', type: 'FileItem', direction: 'output' },
        { id: 'skipped', name: 'Omitido', type: 'FileItem', direction: 'output' },
        { id: 'error', name: 'Error', type: 'FileItem', direction: 'output' }
      ],
      parameters: [
        { key: 'PipelineName', displayName: 'Nombre del Pipeline', type: 'string', value: 'Renombrado Inteligente 2026' },
        { key: 'RenameMode', displayName: 'Modo de Ejecución', type: 'select', value: 'Virtual', options: ['Virtual', 'DirectInPlace'] },
        { key: 'CollisionStrategy', displayName: 'Resolución de Colisión', type: 'select', value: 'AutoIncrement', options: ['AutoIncrement', 'Overwrite', 'Skip', 'Fail'] },
        { key: 'MethodSteps', displayName: 'Pasos de Renombrado (JSON)', type: 'multiline', value: '' }
      ],
      customActions: [
        { actionId: 'OpenRenamerPipeline', title: '🏷️ Pipeline de Métodos...', icon: '🏷️', tooltip: 'Abrir el Estudio de Renombrado Avanzado' }
      ],
      processedCount: 0,
      durationMs: 0,
    },
  },
  {
    id: 'node-3',
    type: 'flowNode',
    position: { x: 880, y: 220 },
    data: {
      title: 'Directorio de Destino',
      category: 'Destino',
      description: 'Ubica los archivos procesados en la carpeta de destino final.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Confirmado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'DestinationFolder', displayName: 'Carpeta Destino', type: 'path', value: 'D:/Procesados/Archivos' },
        { key: 'CollisionMode', displayName: 'Resolución de Colisión', type: 'select', value: 'AutoIncrement', options: ['Overwrite', 'AutoIncrement', 'Skip'] },
      ],
      processedCount: 0,
      durationMs: 0,
    },
  },
];

const initialEdges: Edge[] = [
  {
    id: 'e1-2',
    source: 'node-1',
    sourceHandle: 'out',
    target: 'node-2',
    targetHandle: 'in',
    animated: false,
  },
  {
    id: 'e2-3',
    source: 'node-2',
    sourceHandle: 'out',
    target: 'node-3',
    targetHandle: 'in',
    animated: false,
  },
];

function FlowCanvas() {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node<FlowNodeData>>(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);
  const { fitView, zoomIn, zoomOut } = useReactFlow();

  // Estados de Paneles
  const [isLibraryOpen, setIsLibraryOpen] = useState(false);
  const [isInspectorOpen, setIsInspectorOpen] = useState(true);
  const [isConsoleOpen, setIsConsoleOpen] = useState(true);

  // Modos de Ejecución
  const [isDryRun, setIsDryRun] = useState(false);
  const [isWatching, setIsWatching] = useState(false);

  // Nodo Seleccionado
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>('node-2');

  // Diálogos Modales de la Aplicación y Nodos
  const [isRenamerOpen, setIsRenamerOpen] = useState(false);
  const [isSyntheticOpen, setIsSyntheticOpen] = useState(false);
  const [isRegexOpen, setIsRegexOpen] = useState(false);
  const [isScriptOpen, setIsScriptOpen] = useState(false);
  const [isVlmOpen, setIsVlmOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isVfsOpen, setIsVfsOpen] = useState(false);
  const [isShortcutsOpen, setIsShortcutsOpen] = useState(false);
  const [isAboutOpen, setIsAboutOpen] = useState(false);
  const [isManualOpen, setIsManualOpen] = useState(false);
  const [isTelemetryOpen, setIsTelemetryOpen] = useState(false);

  const [modalNode, setModalNode] = useState<Node<FlowNodeData> | null>(null);

  // Ajustes Generales
  const [settings, setSettings] = useState<WorkflowSettings>({
    maxConcurrency: 8,
    storageProvider: 'Hybrid',
    tempDirectory: '{TempWorkspace}',
    onnxProvider: 'DirectML',
    autoCleanupTemp: true,
    telemetryPollingIntervalMs: 500,
    dryRunMode: false,
    watchMode: false
  });

  // Logs y Telemetría
  const [logs, setLogs] = useState<LogEntry[]>([
    { id: '1', timestamp: '12:00:01', level: 'info', message: 'FileFlow Studio Engine v1.0.0 listo.' },
    { id: '2', timestamp: '12:00:02', level: 'info', message: 'Lienzo DAG inicializado con 3 nodos.' },
  ]);

  const [telemetry, setTelemetry] = useState<WorkflowTelemetry>({
    status: 'idle',
    processedFiles: 0,
    totalFiles: 15,
    elapsedMs: 0,
    throughput: 0,
  });

  const executionTimerRef = useRef<number | null>(null);
  const clipboardRef = useRef<Node<FlowNodeData>[]>([]);

  const addLog = useCallback((level: LogEntry['level'], message: string) => {
    const timeStr = new Date().toTimeString().split(' ')[0];
    setLogs((prev) => [...prev, { id: Math.random().toString(), timestamp: timeStr, level, message }]);
  }, []);

  const onConnect = useCallback(
    (params: Connection) => setEdges((eds) => addEdge({ ...params, animated: false }, eds)),
    [setEdges]
  );

  const onNodeClick = useCallback((_: React.MouseEvent, node: Node) => {
    setSelectedNodeId(node.id);
    setIsInspectorOpen(true);
  }, []);

  // Añadir un nuevo nodo al canvas
  const handleAddNode = useCallback(
    (template: FlowNodeData) => {
      const newId = `node-${Date.now()}`;
      const newNode: Node<FlowNodeData> = {
        id: newId,
        type: 'flowNode',
        position: { x: 350 + Math.random() * 80, y: 250 + Math.random() * 80 },
        data: { ...template },
      };
      setNodes((nds) => [...nds, newNode]);
      setSelectedNodeId(newId);
      addLog('info', `Añadido nodo '${template.title}' al lienzo.`);
    },
    [setNodes, addLog]
  );

  // Modificar parámetro del nodo
  const handleUpdateParameter = useCallback(
    (nodeId: string, paramKey: string, value: any) => {
      setNodes((nds) =>
        nds.map((n) => {
          if (n.id === nodeId) {
            const updatedParams = n.data.parameters.map((p) =>
              p.key === paramKey ? { ...p, value } : p
            );
            return {
              ...n,
              data: {
                ...n.data,
                parameters: updatedParams,
              },
            };
          }
          return n;
        })
      );
    },
    [setNodes]
  );

  // Aplicar paquete completo de parámetros desde un modal de configuración
  const handleSaveModalParams = useCallback(
    (targetNodeId: string, updatedParams: Record<string, any>) => {
      setNodes((nds) =>
        nds.map((n) => {
          if (n.id === targetNodeId) {
            const newParamList = n.data.parameters.map((p) => {
              if (p.key in updatedParams) {
                return { ...p, value: updatedParams[p.key] };
              }
              return p;
            });

            // Añadir parámetros que no existieran previamente en la lista
            for (const [k, v] of Object.entries(updatedParams)) {
              if (!newParamList.some(p => p.key === k)) {
                newParamList.push({
                  key: k,
                  displayName: k,
                  type: typeof v === 'boolean' ? 'boolean' : typeof v === 'number' ? 'number' : 'string',
                  value: v
                });
              }
            }

            return {
              ...n,
              data: {
                ...n.data,
                parameters: newParamList
              }
            };
          }
          return n;
        })
      );
      addLog('success', `Parámetros avanzados sincronizados con el nodo '${targetNodeId}'.`);
    },
    [setNodes, addLog]
  );

  // Eliminar nodo del grafo
  const handleDeleteNode = useCallback(
    (nodeId: string) => {
      setNodes((nds) => nds.filter((n) => n.id !== nodeId));
      setEdges((eds) => eds.filter((e) => e.source !== nodeId && e.target !== nodeId));
      if (selectedNodeId === nodeId) {
        setSelectedNodeId(null);
      }
      addLog('warn', `Eliminado nodo '${nodeId}' del pipeline.`);
    },
    [setNodes, setEdges, selectedNodeId, addLog]
  );

  // Limpiar lienzo
  const handleClear = useCallback(() => {
    setNodes([]);
    setEdges([]);
    setSelectedNodeId(null);
    addLog('warn', 'Lienzo de nodos limpiado.');
  }, [setNodes, setEdges, addLog]);

  // Portapapeles (Copiar / Pegar / Duplicar)
  const handleCopy = useCallback(() => {
    if (!selectedNodeId) return;
    const n = nodes.find(x => x.id === selectedNodeId);
    if (n) {
      clipboardRef.current = [n];
      addLog('info', `Copiado nodo '${n.data.title}' al portapapeles.`);
    }
  }, [nodes, selectedNodeId, addLog]);

  const handlePaste = useCallback(() => {
    if (clipboardRef.current.length === 0) return;
    const sourceNode = clipboardRef.current[0];
    const newId = `node-${Date.now()}`;
    const clone: Node<FlowNodeData> = {
      ...sourceNode,
      id: newId,
      position: { x: sourceNode.position.x + 40, y: sourceNode.position.y + 40 },
      data: {
        ...sourceNode.data,
        status: 'idle',
        durationMs: 0,
        processedCount: 0
      }
    };
    setNodes(nds => [...nds, clone]);
    setSelectedNodeId(newId);
    addLog('info', `Pegado nodo '${clone.data.title}' en el lienzo.`);
  }, [setNodes, addLog]);

  const handleDuplicate = useCallback(() => {
    handleCopy();
    handlePaste();
  }, [handleCopy, handlePaste]);

  const handleSelectAll = useCallback(() => {
    setNodes(nds => nds.map(n => ({ ...n, selected: true })));
  }, [setNodes]);

  // Guardar y Cargar Workflow en JSON
  const handleSaveWorkflow = useCallback(() => {
    const workflowData = {
      version: '1.0.0',
      name: 'Workflow_' + new Date().toISOString().replace(/[:.]/g, '-'),
      nodes,
      edges,
      settings
    };
    const jsonStr = JSON.stringify(workflowData, null, 2);
    const blob = new Blob([jsonStr], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${workflowData.name}.flow.json`;
    a.click();
    URL.revokeObjectURL(url);
    addLog('success', `Flujo guardado con éxito (${nodes.length} nodos, ${edges.length} conexiones).`);
  }, [nodes, edges, settings, addLog]);

  const handleOpenWorkflow = useCallback(() => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,.flow';
    input.onchange = (e: any) => {
      const file = e.target.files?.[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = (event) => {
        try {
          const parsed = JSON.parse(event.target?.result as string);
          if (parsed.nodes && parsed.edges) {
            setNodes(parsed.nodes);
            setEdges(parsed.edges);
            if (parsed.settings) setSettings(parsed.settings);
            addLog('success', `Flujo cargado: ${parsed.name || 'Sin título'} (${parsed.nodes.length} nodos).`);
            setTimeout(() => fitView({ padding: 0.2 }), 100);
          }
        } catch {
          addLog('error', 'El archivo seleccionado no es un flujo JSON válido de FileFlow.');
        }
      };
      reader.readAsText(file);
    };
    input.click();
  }, [setNodes, setEdges, fitView, addLog]);

  // Cargar Plantillas de Ejemplo
  const handleLoadTemplate = useCallback((templateType: string) => {
    if (templateType === 'renamer') {
      setNodes([
        {
          id: 'n-ren-1',
          type: 'flowNode',
          position: { x: 100, y: 220 },
          data: {
            title: 'Generador Sintético',
            category: 'Entrada',
            description: 'Emite 20 nombres de fotos y videos ficticios.',
            iconName: 'input',
            status: 'idle',
            inputs: [],
            outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
            parameters: [
              { key: 'Category', displayName: 'Categoría', type: 'select', value: 'Fotos', options: ['Fotos', 'Películas', 'Series', 'Música'] },
              { key: 'MaxItems', displayName: 'Límite', type: 'number', value: 20 },
            ]
          }
        },
        {
          id: 'n-ren-2',
          type: 'flowNode',
          position: { x: 500, y: 220 },
          data: {
            title: 'Renombrador Avanzado',
            category: 'Archivos',
            description: 'Aplica plantilla {Date}_{Counter:3}_{FileName} con normalización.',
            iconName: 'file',
            status: 'idle',
            inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
            outputs: [{ id: 'out', name: 'Renombrado', type: 'FileItem', direction: 'output' }],
            parameters: [
              { key: 'PipelineName', displayName: 'Pipeline', type: 'string', value: 'Fotos Fechadas' },
              { key: 'RenameMode', displayName: 'Modo', type: 'select', value: 'Virtual', options: ['Virtual', 'DirectInPlace'] },
              { key: 'CollisionStrategy', displayName: 'Colisión', type: 'select', value: 'AutoIncrement', options: ['AutoIncrement', 'Overwrite'] },
            ]
          }
        },
        {
          id: 'n-ren-3',
          type: 'flowNode',
          position: { x: 900, y: 220 },
          data: {
            title: 'Directorio de Salida',
            category: 'Destino',
            description: 'Copia los ficheros renombrados en la carpeta organizada.',
            iconName: 'output',
            status: 'idle',
            inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
            outputs: [{ id: 'out', name: 'Salida', type: 'FileItem', direction: 'output' }],
            parameters: [
              { key: 'DestinationFolder', displayName: 'Carpeta', type: 'path', value: 'D:/Fotos_Organizadas' }
            ]
          }
        }
      ]);
      setEdges([
        { id: 'e-r1', source: 'n-ren-1', sourceHandle: 'out', target: 'n-ren-2', targetHandle: 'in' },
        { id: 'e-r2', source: 'n-ren-2', sourceHandle: 'out', target: 'n-ren-3', targetHandle: 'in' },
      ]);
      addLog('info', 'Cargada plantilla: Renombrado Masivo Inteligente.');
    } else if (templateType === 'vision') {
      setNodes([
        {
          id: 'n-vis-1',
          type: 'flowNode',
          position: { x: 100, y: 220 },
          data: {
            title: 'Vigilante de Imágenes',
            category: 'Entrada',
            description: 'Monitorea carpeta para imágenes .jpg, .png, .webp.',
            iconName: 'folder',
            status: 'idle',
            inputs: [],
            outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
            parameters: [
              { key: 'WatchPath', displayName: 'Ruta', type: 'path', value: 'C:/Imágenes_Entrada' }
            ]
          }
        },
        {
          id: 'n-vis-2',
          type: 'flowNode',
          position: { x: 500, y: 220 },
          data: {
            title: 'Modelo de Visión VLM',
            category: 'IA',
            description: 'Inferencia visual con Ollama LLaVA para clasificación semántica.',
            iconName: 'ai',
            status: 'idle',
            inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
            outputs: [{ id: 'out', name: 'Etiquetado', type: 'FileItem', direction: 'output' }],
            parameters: [
              { key: 'Provider', displayName: 'Proveedor', type: 'select', value: 'Ollama', options: ['Ollama', 'OpenAI', 'LocalOnnx'] },
              { key: 'ModelName', displayName: 'Modelo', type: 'string', value: 'llava:7b' },
              { key: 'Prompt', displayName: 'Prompt', type: 'multiline', value: 'Clasifica esta imagen y devuelve etiquetas en JSON.' }
            ]
          }
        },
        {
          id: 'n-vis-3',
          type: 'flowNode',
          position: { x: 900, y: 220 },
          data: {
            title: 'Clasificador por Metadatos',
            category: 'Lógica',
            description: 'Enruta según las etiquetas devueltas por el modelo VLM.',
            iconName: 'filter',
            status: 'idle',
            inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
            outputs: [{ id: 'out', name: 'Clasificado', type: 'FileItem', direction: 'output' }],
            parameters: []
          }
        }
      ]);
      setEdges([
        { id: 'e-v1', source: 'n-vis-1', sourceHandle: 'out', target: 'n-vis-2', targetHandle: 'in' },
        { id: 'e-v2', source: 'n-vis-2', sourceHandle: 'out', target: 'n-vis-3', targetHandle: 'in' },
      ]);
      addLog('info', 'Cargada plantilla: Inferencia Visual Multimodal (VLM).');
    }
    setTimeout(() => fitView({ padding: 0.2 }), 100);
  }, [setNodes, setEdges, fitView, addLog]);

  // Ejecución Simula / Realtime Workflow Runner
  const handleExecute = useCallback(() => {
    if (nodes.length === 0) {
      addLog('error', 'No hay nodos configurados en el pipeline.');
      return;
    }

    addLog('info', `Iniciando ejecución DAG (${isDryRun ? 'Modo Simulación Dry Run' : 'Modo Estándar'})...`);
    setTelemetry((t) => ({ ...t, status: 'running', processedFiles: 0, elapsedMs: 0 }));
    setEdges((eds) => eds.map((e) => ({ ...e, animated: true })));

    let progress = 0;
    const total = 15;
    const startTime = Date.now();

    setNodes((nds) =>
      nds.map((n, idx) => ({
        ...n,
        data: { ...n.data, status: idx === 0 ? 'running' : 'idle', durationMs: 0 },
      }))
    );

    if (executionTimerRef.current) clearInterval(executionTimerRef.current);

    executionTimerRef.current = window.setInterval(() => {
      progress += 2;
      const elapsed = Date.now() - startTime;

      if (progress >= total) {
        if (executionTimerRef.current) clearInterval(executionTimerRef.current);
        setTelemetry({
          status: 'completed',
          processedFiles: total,
          totalFiles: total,
          elapsedMs: elapsed,
          throughput: Number((total / (elapsed / 1000)).toFixed(1)),
        });

        setNodes((nds) =>
          nds.map((n) => ({
            ...n,
            data: { ...n.data, status: 'completed', durationMs: Math.round(elapsed / nds.length) },
          }))
        );

        setEdges((eds) => eds.map((e) => ({ ...e, animated: false })));
        addLog('success', `Pipeline DAG completado exitosamente: ${total} archivos procesados.`);
      } else {
        setTelemetry((t) => ({
          ...t,
          processedFiles: progress,
          elapsedMs: elapsed,
          throughput: Number((progress / (elapsed / 1000)).toFixed(1)),
        }));

        setNodes((nds) =>
          nds.map((n, idx) => {
            const stepThreshold = (idx + 1) * (total / nds.length);
            let status: FlowNodeData['status'] = 'idle';
            if (progress >= stepThreshold) status = 'completed';
            else if (progress >= (idx * total) / nds.length) status = 'running';

            return {
              ...n,
              data: { ...n.data, status, processedCount: progress },
            };
          })
        );
      }
    }, 250);
  }, [nodes, setNodes, setEdges, isDryRun, addLog]);

  const handlePause = useCallback(() => {
    if (executionTimerRef.current) clearInterval(executionTimerRef.current);
    setTelemetry((t) => ({ ...t, status: 'paused' }));
    setEdges((eds) => eds.map((e) => ({ ...e, animated: false })));
    addLog('warn', 'Pipeline pausado.');
  }, [setEdges, addLog]);

  const handleCancel = useCallback(() => {
    if (executionTimerRef.current) clearInterval(executionTimerRef.current);
    setTelemetry((t) => ({ ...t, status: 'idle' }));
    setNodes((nds) => nds.map((n) => ({ ...n, data: { ...n.data, status: 'idle' } })));
    setEdges((eds) => eds.map((e) => ({ ...e, animated: false })));
    addLog('error', 'Pipeline detenido.');
  }, [setNodes, setEdges, addLog]);

  // Atajos de Teclado Globales (F5, F10, Ctrl+S, Ctrl+O, etc.)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F5' && !e.shiftKey) {
        e.preventDefault();
        handleExecute();
      } else if (e.key === 'F5' && e.shiftKey) {
        e.preventDefault();
        handleCancel();
      } else if (e.key === 's' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        handleSaveWorkflow();
      } else if (e.key === 'o' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        handleOpenWorkflow();
      } else if (e.key === 'n' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        handleClear();
      } else if (e.key === 'b' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        setIsLibraryOpen(v => !v);
      } else if (e.key === 'i' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        setIsInspectorOpen(v => !v);
      } else if (e.key === 'j' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        setIsConsoleOpen(v => !v);
      } else if (e.key === '0' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        fitView({ padding: 0.2 });
      } else if (e.key === 'Delete' || e.key === 'Backspace') {
        if (selectedNodeId && document.activeElement?.tagName !== 'INPUT' && document.activeElement?.tagName !== 'TEXTAREA') {
          handleDeleteNode(selectedNodeId);
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleExecute, handleCancel, handleSaveWorkflow, handleOpenWorkflow, handleClear, fitView, selectedNodeId, handleDeleteNode]);

  const selectedNode = nodes.find((n) => n.id === selectedNodeId) ?? null;

  // Apertura de modales especializados para el nodo
  const openRenamerModal = (targetNode?: Node<FlowNodeData> | null) => {
    setModalNode(targetNode || selectedNode);
    setIsRenamerOpen(true);
  };

  const openSyntheticModal = (targetNode?: Node<FlowNodeData> | null) => {
    setModalNode(targetNode || selectedNode);
    setIsSyntheticOpen(true);
  };

  const openRegexModal = (targetNode?: Node<FlowNodeData> | null) => {
    setModalNode(targetNode || selectedNode);
    setIsRegexOpen(true);
  };

  const openScriptModal = (targetNode?: Node<FlowNodeData> | null) => {
    setModalNode(targetNode || selectedNode);
    setIsScriptOpen(true);
  };

  const openVlmModal = (targetNode?: Node<FlowNodeData> | null) => {
    setModalNode(targetNode || selectedNode);
    setIsVlmOpen(true);
  };

  return (
    <div className="app-container">
      {/* 1. Barra de Menús Superior Clásica / Dropdown */}
      <MenuBar
        onNewWorkflow={handleClear}
        onOpenWorkflow={handleOpenWorkflow}
        onSaveWorkflow={handleSaveWorkflow}
        onExportJson={handleSaveWorkflow}
        onLoadTemplate={handleLoadTemplate}
        onUndo={() => addLog('info', 'Deshacer último cambio.')}
        onRedo={() => addLog('info', 'Rehacer cambio.')}
        onCopy={handleCopy}
        onPaste={handlePaste}
        onCut={() => { handleCopy(); if (selectedNodeId) handleDeleteNode(selectedNodeId); }}
        onDuplicate={handleDuplicate}
        onSelectAll={handleSelectAll}
        onDeleteSelected={() => { if (selectedNodeId) handleDeleteNode(selectedNodeId); }}
        onClearCanvas={handleClear}
        onToggleLibrary={() => setIsLibraryOpen(v => !v)}
        onToggleInspector={() => setIsInspectorOpen(v => !v)}
        onToggleConsole={() => setIsConsoleOpen(v => !v)}
        onFitView={() => fitView({ padding: 0.2 })}
        onZoomIn={() => zoomIn()}
        onZoomOut={() => zoomOut()}
        onToggleFullscreen={() => {
          if (!document.fullscreenElement) document.documentElement.requestFullscreen();
          else document.exitFullscreen();
        }}
        onExecuteWorkflow={handleExecute}
        onDebugWorkflow={() => { addLog('info', 'Iniciando modo depuración paso a paso (F10).'); handleExecute(); }}
        onPauseWorkflow={handlePause}
        onStopWorkflow={handleCancel}
        isDryRun={isDryRun}
        onToggleDryRun={() => setIsDryRun(v => !v)}
        isWatching={isWatching}
        onToggleWatching={() => setIsWatching(v => !v)}
        onOpenSettings={() => setIsSettingsOpen(true)}
        onOpenVfsExplorer={() => setIsVfsOpen(true)}
        onOpenSyntheticDesigner={() => openSyntheticModal()}
        onOpenAdvancedRenamer={() => openRenamerModal()}
        onOpenRegexHelper={() => openRegexModal()}
        onOpenScriptEditor={() => openScriptModal()}
        onOpenVlmTester={() => openVlmModal()}
        onOpenTelemetry={() => setIsTelemetryOpen(true)}
        onOpenShortcuts={() => setIsShortcutsOpen(true)}
        onOpenManual={() => setIsManualOpen(true)}
        onOpenAbout={() => setIsAboutOpen(true)}
      />

      {/* 2. Barra de Control Rápida (Floating Glassmorphism) */}
      <ControlBar
        telemetry={telemetry}
        onExecute={handleExecute}
        onPause={handlePause}
        onCancel={handleCancel}
        onClear={handleClear}
        onFitView={() => fitView({ padding: 0.2 })}
        isLibraryOpen={isLibraryOpen}
        setIsLibraryOpen={setIsLibraryOpen}
        isInspectorOpen={isInspectorOpen}
        setIsInspectorOpen={setIsInspectorOpen}
        isConsoleOpen={isConsoleOpen}
        setIsConsoleOpen={setIsConsoleOpen}
        isDryRun={isDryRun}
        onToggleDryRun={() => setIsDryRun(v => !v)}
        isWatching={isWatching}
        onToggleWatching={() => setIsWatching(v => !v)}
        onOpenVfs={() => setIsVfsOpen(true)}
        onOpenSettings={() => setIsSettingsOpen(true)}
        virtualFilesCount={3}
      />

      {/* 3. Área del Lienzo React Flow */}
      <div className="canvas-area">
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onConnect={onConnect}
          onNodeClick={onNodeClick}
          nodeTypes={nodeTypes}
          fitView
          attributionPosition="bottom-left"
          minZoom={0.2}
          maxZoom={2.5}
        >
          <Background color="rgba(255, 255, 255, 0.05)" gap={20} size={1.5} />
          <Controls showInteractive={false} />
          <MiniMap
            nodeStrokeColor="#3b82f6"
            nodeColor="rgba(37, 99, 235, 0.4)"
            maskColor="rgba(8, 10, 15, 0.75)"
          />
        </ReactFlow>

        {/* Drawer Izquierdo: Catálogo de Nodos */}
        <NodeLibraryDrawer
          isOpen={isLibraryOpen}
          onClose={() => setIsLibraryOpen(false)}
          onAddNode={handleAddNode}
        />

        {/* Drawer Derecho: Inspector de Parámetros */}
        <InspectorDrawer
          isOpen={isInspectorOpen}
          selectedNode={selectedNode}
          onClose={() => setIsInspectorOpen(false)}
          onUpdateParameter={handleUpdateParameter}
          onDeleteNode={handleDeleteNode}
          onOpenAdvancedRenamer={openRenamerModal}
          onOpenSyntheticDesigner={openSyntheticModal}
          onOpenRegexHelper={openRegexModal}
          onOpenScriptEditor={openScriptModal}
          onOpenVlmTester={openVlmModal}
        />

        {/* Consola Inferior: Logs y Telemetría */}
        <LogConsole
          isOpen={isConsoleOpen}
          onClose={() => setIsConsoleOpen(false)}
          logs={logs}
          onClearLogs={() => setLogs([])}
        />
      </div>

      {/* === Diálogos Modales de Nodos y de la Aplicación === */}
      <AdvancedRenamerModal
        isOpen={isRenamerOpen}
        onClose={() => setIsRenamerOpen(false)}
        nodeData={modalNode ? modalNode.data : selectedNode ? selectedNode.data : null}
        onSave={(updated) => {
          const targetId = modalNode ? modalNode.id : selectedNodeId;
          if (targetId) handleSaveModalParams(targetId, updated);
        }}
      />

      <SyntheticDataDesignerModal
        isOpen={isSyntheticOpen}
        onClose={() => setIsSyntheticOpen(false)}
        nodeData={modalNode ? modalNode.data : selectedNode ? selectedNode.data : null}
        onSave={(updated) => {
          const targetId = modalNode ? modalNode.id : selectedNodeId;
          if (targetId) handleSaveModalParams(targetId, updated);
        }}
      />

      <RegexHelperModal
        isOpen={isRegexOpen}
        onClose={() => setIsRegexOpen(false)}
        nodeData={modalNode ? modalNode.data : selectedNode ? selectedNode.data : null}
        onSave={(updated) => {
          const targetId = modalNode ? modalNode.id : selectedNodeId;
          if (targetId) handleSaveModalParams(targetId, updated);
        }}
      />

      <ScriptEditorModal
        isOpen={isScriptOpen}
        onClose={() => setIsScriptOpen(false)}
        nodeData={modalNode ? modalNode.data : selectedNode ? selectedNode.data : null}
        onSave={(updated) => {
          const targetId = modalNode ? modalNode.id : selectedNodeId;
          if (targetId) handleSaveModalParams(targetId, updated);
        }}
      />

      <VlmTesterModal
        isOpen={isVlmOpen}
        onClose={() => setIsVlmOpen(false)}
        nodeData={modalNode ? modalNode.data : selectedNode ? selectedNode.data : null}
        onSave={(updated) => {
          const targetId = modalNode ? modalNode.id : selectedNodeId;
          if (targetId) handleSaveModalParams(targetId, updated);
        }}
      />

      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        settings={settings}
        onSave={(newSet) => {
          setSettings(newSet);
          addLog('info', 'Ajustes globales actualizados.');
        }}
      />

      <VfsExplorerModal
        isOpen={isVfsOpen}
        onClose={() => setIsVfsOpen(false)}
      />

      <ShortcutsModal
        isOpen={isShortcutsOpen}
        onClose={() => setIsShortcutsOpen(false)}
      />

      <AboutModal
        isOpen={isAboutOpen}
        onClose={() => setIsAboutOpen(false)}
      />

      <UserManualModal
        isOpen={isManualOpen}
        onClose={() => setIsManualOpen(false)}
      />

      <TelemetryModal
        isOpen={isTelemetryOpen}
        onClose={() => setIsTelemetryOpen(false)}
        telemetry={telemetry}
      />
    </div>
  );
}

export function App() {
  return (
    <ReactFlowProvider>
      <FlowCanvas />
    </ReactFlowProvider>
  );
}

export default App;
