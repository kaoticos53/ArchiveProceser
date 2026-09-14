import React from 'react';
import { 
  Play, 
  Square, 
  Pause, 
  RotateCcw, 
  Layers, 
  SlidersHorizontal, 
  Terminal, 
  Maximize2,
  Workflow,
  Sparkles
} from 'lucide-react';
import type { WorkflowTelemetry } from '../types/flow';

interface ControlBarProps {
  telemetry: WorkflowTelemetry;
  onExecute: () => void;
  onPause: () => void;
  onCancel: () => void;
  onClear: () => void;
  onFitView: () => void;
  isLibraryOpen: boolean;
  setIsLibraryOpen: React.Dispatch<React.SetStateAction<boolean>>;
  isInspectorOpen: boolean;
  setIsInspectorOpen: React.Dispatch<React.SetStateAction<boolean>>;
  isConsoleOpen: boolean;
  setIsConsoleOpen: React.Dispatch<React.SetStateAction<boolean>>;
}

export const ControlBar: React.FC<ControlBarProps> = ({
  telemetry,
  onExecute,
  onPause,
  onCancel,
  onClear,
  onFitView,
  isLibraryOpen,
  setIsLibraryOpen,
  isInspectorOpen,
  setIsInspectorOpen,
  isConsoleOpen,
  setIsConsoleOpen,
}) => {
  const isRunning = telemetry.status === 'running';

  return (
    <header className="control-bar-container">
      {/* Brand / Logo */}
      <div className="brand-group">
        <div className="brand-icon">
          <Workflow size={18} />
        </div>
        <div className="brand-text">
          <span className="brand-title">FileFlow</span>
          <span className="brand-badge">STUDIO WEB</span>
        </div>
      </div>

      {/* Execution Actions */}
      <div className="actions-group">
        {!isRunning ? (
          <button 
            className="btn-action primary" 
            onClick={onExecute}
            title="Ejecutar Pipeline DAG"
          >
            <Play size={15} fill="currentColor" />
            <span>Ejecutar</span>
          </button>
        ) : (
          <>
            <button 
              className="btn-action warning" 
              onClick={onPause}
              title="Pausar Ejecución"
            >
              <Pause size={15} />
              <span>Pausar</span>
            </button>
            <button 
              className="btn-action danger" 
              onClick={onCancel}
              title="Detener Pipeline"
            >
              <Square size={15} fill="currentColor" />
              <span>Detener</span>
            </button>
          </>
        )}

        <div className="divider" />

        <button 
          className="btn-action secondary" 
          onClick={onFitView}
          title="Centrar y Ajustar Lienzo"
        >
          <Maximize2 size={15} />
        </button>

        <button 
          className="btn-action secondary" 
          onClick={onClear}
          title="Limpiar Lienzo"
        >
          <RotateCcw size={15} />
        </button>
      </div>

      {/* Live Telemetry Display */}
      <div className="telemetry-group">
        <div className="telemetry-item">
          <span className="telemetry-label">Procesados</span>
          <span className="telemetry-value">
            {telemetry.processedFiles} / {telemetry.totalFiles}
          </span>
        </div>
        <div className="telemetry-item">
          <span className="telemetry-label">Tiempo</span>
          <span className="telemetry-value">
            {(telemetry.elapsedMs / 1000).toFixed(1)}s
          </span>
        </div>
        {isRunning && (
          <div className="telemetry-status-running">
            <Sparkles size={13} className="spin-slow" />
            <span>En ejecución</span>
          </div>
        )}
      </div>

      {/* Panels Toggle */}
      <div className="panels-toggle-group">
        <button 
          className={`btn-panel-toggle ${isLibraryOpen ? 'active' : ''}`}
          onClick={() => setIsLibraryOpen(prev => !prev)}
          title="Catálogo de Nodos"
        >
          <Layers size={16} />
          <span>Biblioteca</span>
        </button>

        <button 
          className={`btn-panel-toggle ${isInspectorOpen ? 'active' : ''}`}
          onClick={() => setIsInspectorOpen(prev => !prev)}
          title="Inspector de Parámetros"
        >
          <SlidersHorizontal size={16} />
          <span>Ajustes</span>
        </button>

        <button 
          className={`btn-panel-toggle ${isConsoleOpen ? 'active' : ''}`}
          onClick={() => setIsConsoleOpen(prev => !prev)}
          title="Consola de Logs"
        >
          <Terminal size={16} />
          <span>Logs</span>
        </button>
      </div>
    </header>
  );
};
