import React from 'react';
import { 
  X, 
  Sliders, 
  Trash2, 
  Folder, 
  Sparkles
} from 'lucide-react';
import type { Node } from '@xyflow/react';
import type { FlowNodeData } from '../types/flow';

interface InspectorDrawerProps {
  isOpen: boolean;
  selectedNode: Node<FlowNodeData> | null;
  onClose: () => void;
  onUpdateParameter: (nodeId: string, paramKey: string, value: any) => void;
  onDeleteNode: (nodeId: string) => void;
  onOpenAdvancedRenamer?: (node: Node<FlowNodeData>) => void;
  onOpenSyntheticDesigner?: (node: Node<FlowNodeData>) => void;
  onOpenRegexHelper?: (node: Node<FlowNodeData>) => void;
  onOpenScriptEditor?: (node: Node<FlowNodeData>) => void;
  onOpenVlmTester?: (node: Node<FlowNodeData>) => void;
}

export const InspectorDrawer: React.FC<InspectorDrawerProps> = ({
  isOpen,
  selectedNode,
  onClose,
  onUpdateParameter,
  onDeleteNode,
  onOpenAdvancedRenamer,
  onOpenSyntheticDesigner,
  onOpenRegexHelper,
  onOpenScriptEditor,
  onOpenVlmTester,
}) => {
  if (!isOpen || !selectedNode) return null;

  const { data } = selectedNode;
  const title = (data.title || '').toLowerCase();
  const desc = (data.description || '').toLowerCase();

  // Detectar capacidades especiales de configuración del nodo
  const isRenamer = title.includes('renombr') || desc.includes('renombr');
  const isSynthetic = title.includes('sintético') || title.includes('sintetico') || desc.includes('sintético');
  const isScript = title.includes('script') || desc.includes('script') || title.includes('c#') || title.includes('python');
  const isVlm = title.includes('vision') || title.includes('vlm') || title.includes('llm') || desc.includes('multimodal');
  const hasRegex = data.parameters?.some(p => p.key.toLowerCase().includes('regex') || p.key.toLowerCase().includes('pattern'));

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

        {/* Acciones Especiales / Diseñadores Modales */}
        {(data.customActions && data.customActions.length > 0 || isRenamer || isSynthetic || isScript || isVlm || hasRegex) && (
          <div className="custom-actions-section" style={{ marginBottom: '16px' }}>
            <div className="section-title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Sparkles size={14} color="var(--accent-purple)" />
              <span>Diseñadores y Asistentes</span>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '8px' }}>
              {isRenamer && onOpenAdvancedRenamer && (
                <button 
                  className="btn-special-action"
                  onClick={() => onOpenAdvancedRenamer(selectedNode)}
                >
                  <span className="special-icon">🏷️</span>
                  <span>Estudio de Renombrado Avanzado...</span>
                </button>
              )}

              {isSynthetic && onOpenSyntheticDesigner && (
                <button 
                  className="btn-special-action"
                  onClick={() => onOpenSyntheticDesigner(selectedNode)}
                >
                  <span className="special-icon">📊</span>
                  <span>Diseñador de Datasets Sintéticos...</span>
                </button>
              )}

              {hasRegex && onOpenRegexHelper && (
                <button 
                  className="btn-special-action"
                  onClick={() => onOpenRegexHelper(selectedNode)}
                >
                  <span className="special-icon">🔍</span>
                  <span>Asistente de Expresiones Regulares...</span>
                </button>
              )}

              {isScript && onOpenScriptEditor && (
                <button 
                  className="btn-special-action"
                  onClick={() => onOpenScriptEditor(selectedNode)}
                >
                  <span className="special-icon">💻</span>
                  <span>Editor de Scripts Roslyn / Python...</span>
                </button>
              )}

              {isVlm && onOpenVlmTester && (
                <button 
                  className="btn-special-action"
                  onClick={() => onOpenVlmTester(selectedNode)}
                >
                  <span className="special-icon">👁️</span>
                  <span>Probar Inferencia Visual VLM...</span>
                </button>
              )}

              {/* Acciones canónicas desde backend */}
              {data.customActions?.map(act => (
                <button
                  key={act.actionId}
                  className="btn-special-action"
                  title={act.tooltip}
                  onClick={() => {
                    if (act.actionId.toLowerCase().includes('renam') && onOpenAdvancedRenamer) onOpenAdvancedRenamer(selectedNode);
                    else if (act.actionId.toLowerCase().includes('dataset') && onOpenSyntheticDesigner) onOpenSyntheticDesigner(selectedNode);
                    else if (act.actionId.toLowerCase().includes('script') && onOpenScriptEditor) onOpenScriptEditor(selectedNode);
                    else if (act.actionId.toLowerCase().includes('vision') && onOpenVlmTester) onOpenVlmTester(selectedNode);
                  }}
                >
                  <span className="special-icon">{act.icon || '⚙️'}</span>
                  <span>{act.title}</span>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Lista de Parámetros */}
        <div className="parameters-section">
          <div className="section-title">Parámetros de Configuración</div>

          {data.parameters && data.parameters.length > 0 ? (
            data.parameters.map((param) => {
              const editorType = (param.editorType || param.type || 'text').toLowerCase();

              return (
                <div key={param.key} className="param-field">
                  <label className="param-label">
                    <span>{param.displayName || param.key}</span>
                    {(editorType === 'number' || editorType === 'slider') && (
                      <span className="param-value-tag">{param.value}</span>
                    )}
                  </label>

                  {/* 1. TEXTO BÁSICO */}
                  {(editorType === 'text' || editorType === 'string') && (
                    <input
                      type="text"
                      className="param-input"
                      value={param.value ?? ''}
                      placeholder={param.defaultValue ?? ''}
                      onChange={(e) => onUpdateParameter(selectedNode.id, param.key, e.target.value)}
                    />
                  )}

                  {/* 2. RUTA DE ARCHIVO O CARPETA (FOLDERPATH / FILEPATH / PATH) */}
                  {(editorType === 'folderpath' || editorType === 'filepath' || editorType === 'path') && (
                    <div style={{ display: 'flex', gap: '6px' }}>
                      <input
                        type="text"
                        className="param-input"
                        value={param.value ?? ''}
                        placeholder={editorType === 'folderpath' ? 'C:/Directorio/Entrada' : 'archivo.txt'}
                        onChange={(e) => onUpdateParameter(selectedNode.id, param.key, e.target.value)}
                      />
                      <button
                        type="button"
                        className="btn-tiny"
                        style={{ padding: '0 8px', background: 'var(--bg-surface-2)', border: '1px solid var(--border-dark)' }}
                        title="Explorar ubicación"
                        onClick={() => {
                          const mock = editorType === 'folderpath' ? 'C:/FileFlow/Entrada' : 'C:/FileFlow/documento.pdf';
                          onUpdateParameter(selectedNode.id, param.key, mock);
                        }}
                      >
                        <Folder size={14} />
                      </button>
                    </div>
                  )}

                  {/* 3. SLIDER NUMÉRICO */}
                  {editorType === 'slider' && (
                    <div className="slider-wrapper">
                      <input
                        type="range"
                        min={param.min ?? 0}
                        max={param.max ?? 100}
                        step={param.step ?? 1}
                        value={param.value ?? 0}
                        onChange={(e) => onUpdateParameter(selectedNode.id, param.key, Number(e.target.value))}
                      />
                    </div>
                  )}

                  {/* 4. ENTRADA NUMÉRICA DIRECTA */}
                  {editorType === 'number' && (
                    <input
                      type="number"
                      className="param-input"
                      min={param.min}
                      max={param.max}
                      step={param.step ?? 1}
                      value={param.value ?? 0}
                      onChange={(e) => onUpdateParameter(selectedNode.id, param.key, Number(e.target.value))}
                    />
                  )}

                  {/* 5. BOOLEANO / TOGGLE */}
                  {(editorType === 'boolean' || editorType === 'toggle') && (
                    <button
                      type="button"
                      className={`toggle-switch ${param.value ? 'checked' : ''}`}
                      onClick={() => onUpdateParameter(selectedNode.id, param.key, !param.value)}
                    >
                      <div className="toggle-thumb" />
                      <span className="toggle-label">{param.value ? 'Activado' : 'Desactivado'}</span>
                    </button>
                  )}

                  {/* 6. DROPDOWN SELECT */}
                  {(editorType === 'select' || editorType === 'dropdown' || editorType === 'editabledropdown') && (
                    <select
                      className="param-select"
                      value={param.value ?? (param.options ? param.options[0] : '')}
                      onChange={(e) => onUpdateParameter(selectedNode.id, param.key, e.target.value)}
                    >
                      {param.options?.map((opt) => (
                        <option key={opt} value={opt}>{opt}</option>
                      ))}
                    </select>
                  )}

                  {/* 7. ÁREA DE TEXTO MULTILÍNEA (MULTILINETEXT) */}
                  {(editorType === 'multilinetext' || editorType === 'multiline') && (
                    <textarea
                      className="param-textarea"
                      rows={3}
                      value={param.value ?? ''}
                      onChange={(e) => onUpdateParameter(selectedNode.id, param.key, e.target.value)}
                    />
                  )}

                  {param.helpText && (
                    <div style={{ fontSize: '10.5px', color: 'var(--text-dim)', marginTop: '3px' }}>
                      {param.helpText}
                    </div>
                  )}
                </div>
              );
            })
          ) : (
            <div style={{ color: 'var(--text-dim)', fontSize: '11px', padding: '8px 0' }}>
              Este nodo opera con configuración por defecto sin parámetros editables adicionales.
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
