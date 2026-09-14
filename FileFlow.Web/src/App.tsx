import { useState, useCallback, useRef, useEffect } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  useNodesState,
  useEdgesState,
  addEdge,
} from '@xyflow/react';
import type { Connection, Edge, Node, NodeTypes } from '@xyflow/react';
import { FlowNode } from './components/FlowNode';
import { ControlBar } from './components/ControlBar';
import { NodeLibraryDrawer } from './components/NodeLibraryDrawer';
import { InspectorDrawer } from './components/InspectorDrawer';
import { LogConsole, type LogEntry } from './components/LogConsole';
import type { FlowNodeData, WorkflowTelemetry } from './types/flow';
import './App.css';

const nodeTypes: NodeTypes = {
  flowNode: FlowNode,
};

// Grafo inicial por defecto (Demostración interactiva lista para usar)
const initialNodes: Node<FlowNodeData>[] = [
  {
    id: 'node-1',
    type: 'flowNode',
    position: { x: 80, y: 220 },
    data: {
      title: 'Vigilante de Carpetas',
      category: 'Entrada',
      description: 'Monitorea el directorio de entrada para procesar nuevos ficheros.',
      iconName: 'folder',
      status: 'idle',
      inputs: [],
      outputs: [{ id: 'out', name: 'Archivo', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'WatchPath', displayName: 'Ruta a Vigilar', type: 'path', value: 'C:/Proyectos/Fotos_RAW' },
        { key: 'IncludeSubdirectories', displayName: 'Incluir Subdirectorios', type: 'boolean', value: true },
        { key: 'Filter', displayName: 'Filtro de Archivos', type: 'string', value: '*.png, *.jpg, *.webp' },
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
      title: 'Optimizador de Imágenes',
      category: 'Imágenes',
      description: 'Comprime WebP/MozJPEG preservando calidad perceptual.',
      iconName: 'image',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Imagen', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Optimizada', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'Quality', displayName: 'Calidad (1-100)', type: 'number', value: 85, min: 10, max: 100 },
        { key: 'PassThroughNonImages', displayName: 'Passthrough No-Imágenes', type: 'boolean', value: true },
        { key: 'KeepOriginalIfLarger', displayName: 'Mantener si es Mayor', type: 'boolean', value: true },
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
      title: 'Directorio de Salida',
      category: 'Destino',
      description: 'Escribe las imágenes optimizadas en la carpeta destino.',
      iconName: 'output',
      status: 'idle',
      inputs: [{ id: 'in', name: 'Entrada', type: 'FileItem', direction: 'input' }],
      outputs: [{ id: 'out', name: 'Confirmado', type: 'FileItem', direction: 'output' }],
      parameters: [
        { key: 'DestinationFolder', displayName: 'Carpeta Destino', type: 'path', value: 'D:/Salida/Optimizadas' },
        { key: 'CollisionMode', displayName: 'Resolución de Colisión', type: 'select', value: 'RenameWithCounter', options: ['Overwrite', 'RenameWithCounter', 'Skip'] },
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

export function App() {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node<FlowNodeData>>(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  // Estados de Paneles
  const [isLibraryOpen, setIsLibraryOpen] = useState(false);
  const [isInspectorOpen, setIsInspectorOpen] = useState(true);
  const [isConsoleOpen, setIsConsoleOpen] = useState(true);

  // Nodo Seleccionado
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>('node-2');

  // Logs y Telemetría
  const [logs, setLogs] = useState<LogEntry[]>([
    { id: '1', timestamp: '12:00:01', level: 'info', message: 'FileFlow Studio Web Engine inicializado con éxito.' },
    { id: '2', timestamp: '12:00:02', level: 'info', message: 'Cargados 11 plugins nativos en la sesión DAG.' },
  ]);

  const [telemetry, setTelemetry] = useState<WorkflowTelemetry>({
    status: 'idle',
    processedFiles: 0,
    totalFiles: 42,
    elapsedMs: 0,
    throughput: 0,
  });

  const executionTimerRef = useRef<number | null>(null);

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
        position: { x: 350 + Math.random() * 100, y: 250 + Math.random() * 100 },
        data: { ...template },
      };
      setNodes((nds) => [...nds, newNode]);
      setSelectedNodeId(newId);
      addLog('info', `Añadido nodo '${template.title}' al lienzo.`);
    },
    [setNodes, addLog]
  );

  // Modificar parámetro del nodo seleccionado
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

  // Ejecución Simula / Realtime Workflow Runner
  const handleExecute = useCallback(() => {
    if (nodes.length === 0) {
      addLog('error', 'No hay nodos configurados en el pipeline.');
      return;
    }

    addLog('info', 'Iniciando compilación y validación del grafo DAG...');
    setTelemetry((t) => ({ ...t, status: 'running', processedFiles: 0, elapsedMs: 0 }));

    // Animar las conexiones durante la ejecución (estilo ComfyUI)
    setEdges((eds) => eds.map((e) => ({ ...e, animated: true })));

    let progress = 0;
    const total = 42;
    const startTime = Date.now();

    // Simular el ciclo de los nodos
    setNodes((nds) =>
      nds.map((n, idx) => ({
        ...n,
        data: { ...n.data, status: idx === 0 ? 'running' : 'idle', durationMs: 0 },
      }))
    );

    if (executionTimerRef.current) clearInterval(executionTimerRef.current);

    executionTimerRef.current = window.setInterval(() => {
      progress += 3;
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
        addLog('success', `Pipeline DAG finalizado con éxito! (${total} elementos procesados en ${elapsed}ms).`);
      } else {
        setTelemetry((t) => ({
          ...t,
          processedFiles: progress,
          elapsedMs: elapsed,
          throughput: Number((progress / (elapsed / 1000)).toFixed(1)),
        }));

        // Actualizar estados reactivos según fase
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
  }, [nodes, setNodes, setEdges, addLog]);

  const handlePause = useCallback(() => {
    if (executionTimerRef.current) clearInterval(executionTimerRef.current);
    setTelemetry((t) => ({ ...t, status: 'paused' }));
    setEdges((eds) => eds.map((e) => ({ ...e, animated: false })));
    addLog('warn', 'Ejecución del pipeline pausada por el usuario.');
  }, [setEdges, addLog]);

  const handleCancel = useCallback(() => {
    if (executionTimerRef.current) clearInterval(executionTimerRef.current);
    setTelemetry((t) => ({ ...t, status: 'idle' }));
    setNodes((nds) => nds.map((n) => ({ ...n, data: { ...n.data, status: 'idle' } })));
    setEdges((eds) => eds.map((e) => ({ ...e, animated: false })));
    addLog('error', 'Ejecución del pipeline cancelada.');
  }, [setNodes, setEdges, addLog]);

  useEffect(() => {
    return () => {
      if (executionTimerRef.current) clearInterval(executionTimerRef.current);
    };
  }, []);

  const selectedNode = nodes.find((n) => n.id === selectedNodeId) ?? null;

  return (
    <div className="app-container">
      {/* Barra de Control Flotante */}
      <ControlBar
        telemetry={telemetry}
        onExecute={handleExecute}
        onPause={handlePause}
        onCancel={handleCancel}
        onClear={handleClear}
        onFitView={() => {}}
        isLibraryOpen={isLibraryOpen}
        setIsLibraryOpen={setIsLibraryOpen}
        isInspectorOpen={isInspectorOpen}
        setIsInspectorOpen={setIsInspectorOpen}
        isConsoleOpen={isConsoleOpen}
        setIsConsoleOpen={setIsConsoleOpen}
      />

      {/* Área del Lienzo React Flow */}
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
        />

        {/* Consola Inferior: Logs y Telemetría */}
        <LogConsole
          isOpen={isConsoleOpen}
          onClose={() => setIsConsoleOpen(false)}
          logs={logs}
          onClearLogs={() => setLogs([])}
        />
      </div>
    </div>
  );
}

export default App;
