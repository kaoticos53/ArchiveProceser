import React from 'react';
import { X, Sliders, Trash2 } from 'lucide-react';
import type { Node } from '@xyflow/react';
import type { FlowNodeData } from '../types/flow';

interface InspectorDrawerProps {
  isOpen: boolean;
  selectedNode: Node<FlowNodeData> | null;
  onClose: () => void;
  onUpdateParameter: (nodeId: string, paramKey: string, value: any) => void;
  onDeleteNode: (nodeId: string) => void;
}

export const InspectorDrawer: React.FC<InspectorDrawerProps> = ({
  isOpen,
  selectedNode,
  onClose,
  onUpdateParameter,
  onDeleteNode,
}) => {
  if (!isOpen || !selectedNode) return null;

  const { data } = selectedNode;

  return (
    <aside className="inspector-drawer-container">
      <div className="drawer-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Sliders size={16} color="var(--accent-blue)" />
          <span className="drawer-title">Parámetros del Nodo</span>
        </div>
        <button className="btn-icon" onClick={onClose} title="Cerrar">
          <X size={16} />
        </button>
      </div>

      <div className="inspector-content">
        {/* Resumen del Nodo */}
        <div className="inspector-summary-card">
          <div className="inspector-node-title">{data.title}</div>
          <div className="inspector-node-cat">{data.category}</div>
          <div className="inspector-node-desc">{data.description}</div>
        </div>

        {/* Lista de Parámetros */}
        <div className="parameters-section">
          <div className="section-title">Configuración</div>

          {data.parameters && data.parameters.length > 0 ? (
            data.parameters.map((param) => (
              <div key={param.key} className="param-field">
                <label className="param-label">
                  <span>{param.displayName}</span>
                  {param.type === 'number' && (
                    <span className="param-value-tag">{param.value}</span>
                  )}
                </label>

                {/* Input de Texto / Path */}
                {(param.type === 'string' || param.type === 'path') && (
                  <input
                    type="text"
                    className="param-input"
                    value={param.value ?? ''}
                    onChange={(e) => onUpdateParameter(selectedNode.id, param.key, e.target.value)}
                  />
                )}

                {/* Slider Numérico */}
                {param.type === 'number' && (
                  <div className="slider-wrapper">
                    <input
                      type="range"
                      min={param.min ?? 0}
                      max={param.max ?? 100}
                      value={param.value ?? 0}
                      onChange={(e) => onUpdateParameter(selectedNode.id, param.key, Number(e.target.value))}
                    />
                  </div>
                )}

                {/* Switch Booleano */}
                {param.type === 'boolean' && (
                  <button
                    type="button"
                    className={`toggle-switch ${param.value ? 'checked' : ''}`}
                    onClick={() => onUpdateParameter(selectedNode.id, param.key, !param.value)}
                  >
                    <div className="toggle-thumb" />
                    <span className="toggle-label">{param.value ? 'Activado' : 'Desactivado'}</span>
                  </button>
                )}

                {/* Dropdown Select */}
                {param.type === 'select' && param.options && (
                  <select
                    className="param-select"
                    value={param.value}
                    onChange={(e) => onUpdateParameter(selectedNode.id, param.key, e.target.value)}
                  >
                    {param.options.map((opt) => (
                      <option key={opt} value={opt}>{opt}</option>
                    ))}
                  </select>
                )}
              </div>
            ))
          ) : (
            <div style={{ color: 'var(--text-dim)', fontSize: '11px', padding: '8px 0' }}>
              Este nodo no requiere parámetros adicionales.
            </div>
          )}
        </div>

        {/* Acciones del Nodo */}
        <div style={{ marginTop: 'auto', paddingTop: '20px' }}>
          <button
            className="btn-danger-outline"
            onClick={() => onDeleteNode(selectedNode.id)}
            title="Eliminar nodo seleccionado"
          >
            <Trash2 size={14} />
            <span>Eliminar Nodo del Grafo</span>
          </button>
        </div>
      </div>
    </aside>
  );
};
