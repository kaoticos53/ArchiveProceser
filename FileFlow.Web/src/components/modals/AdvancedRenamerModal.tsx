import React, { useState } from 'react';
import { 
  X, 
  Check, 
  Trash2, 
  ArrowDown, 
  ArrowUp, 
  Settings2, 
  Eye,
  Layers
} from 'lucide-react';
import type { FlowNodeData } from '../../types/flow';

interface AdvancedRenamerModalProps {
  isOpen: boolean;
  onClose: () => void;
  nodeData: FlowNodeData | null;
  onSave: (updatedParams: Record<string, any>) => void;
}

interface RenameStep {
  id: string;
  type: 'template' | 'replace' | 'casing' | 'counter' | 'trim' | 'normalize' | 'extension';
  enabled: boolean;
  pattern?: string;
  search?: string;
  replace?: string;
  useRegex?: boolean;
  matchCase?: boolean;
  casingMode?: 'upper' | 'lower' | 'title' | 'camel' | 'kebab' | 'snake';
  counterStart?: number;
  counterStep?: number;
  counterPadding?: number;
  counterPosition?: 'prefix' | 'suffix';
  trimCount?: number;
  trimPosition?: 'start' | 'end';
  stripAccents?: boolean;
  cleanSpaces?: boolean;
  sanitizeWeb?: boolean;
  newExtension?: string;
}

const SAMPLE_FILES = [
  'DSC_0492_Canon_EOS.JPG',
  'Stranger.Things.S04E01.1080p.WEBRip.mkv',
  '01 - Bohemian Rhapsody (Remastered 2024).mp3',
  'Informe Trimestral Q1 (Revisión Final).pdf',
  'screenshot-2026-09-14 at 10.30.15.png'
];

export const AdvancedRenamerModal: React.FC<AdvancedRenamerModalProps> = ({
  isOpen,
  onClose,
  nodeData,
  onSave,
}) => {
  if (!isOpen || !nodeData) return null;

  // Extraer parámetros actuales
  const getParam = (k: string, def: any) => {
    const p = nodeData.parameters.find(x => x.key.toLowerCase() === k.toLowerCase());
    return p?.value ?? def;
  };

  const [pipelineName, setPipelineName] = useState<string>(getParam('PipelineName', 'Pipeline de Renombrado'));
  const [renameMode, setRenameMode] = useState<'Virtual' | 'DirectInPlace'>(getParam('RenameMode', 'Virtual'));
  const [collisionStrategy, setCollisionStrategy] = useState<'AutoIncrement' | 'Overwrite' | 'Skip' | 'Fail'>(
    getParam('CollisionStrategy', 'AutoIncrement')
  );

  // Inicializar pasos desde MethodSteps o default
  const [steps, setSteps] = useState<RenameStep[]>(() => {
    try {
      const raw = getParam('MethodSteps', '');
      if (raw && typeof raw === 'string') {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed) && parsed.length > 0) return parsed;
      }
    } catch {
      // Ignorar y usar pasos por defecto
    }
    return [
      {
        id: '1',
        type: 'template',
        enabled: true,
        pattern: '{Date:yyyy-MM-dd}_{FileName}'
      },
      {
        id: '2',
        type: 'normalize',
        enabled: true,
        stripAccents: true,
        cleanSpaces: true,
        sanitizeWeb: false
      }
    ];
  });

  // Simulación en tiempo real del pipeline de renombrado
  const applyStepsToFile = (originalName: string, index: number): string => {
    let extIndex = originalName.lastIndexOf('.');
    let base = extIndex !== -1 ? originalName.substring(0, extIndex) : originalName;
    let ext = extIndex !== -1 ? originalName.substring(extIndex + 1) : '';

    for (const step of steps) {
      if (!step.enabled) continue;

      switch (step.type) {
        case 'template': {
          if (step.pattern) {
            const today = new Date().toISOString().split('T')[0];
            const counter = String(index + 1).padStart(3, '0');
            base = step.pattern
              .replace(/\{FileName\}/gi, base)
              .replace(/\{Extension\}/gi, ext)
              .replace(/\{Date:[^}]+\}/gi, today)
              .replace(/\{Counter:[^}]+\}/gi, counter)
              .replace(/\{ParentDir\}/gi, 'Documentos');
          }
          break;
        }
        case 'replace': {
          if (step.search) {
            if (step.useRegex) {
              try {
                const flags = step.matchCase ? 'g' : 'gi';
                const regex = new RegExp(step.search, flags);
                base = base.replace(regex, step.replace ?? '');
              } catch {
                // Regex inválido
              }
            } else {
              base = base.split(step.search).join(step.replace ?? '');
            }
          }
          break;
        }
        case 'casing': {
          if (step.casingMode === 'upper') base = base.toUpperCase();
          else if (step.casingMode === 'lower') base = base.toLowerCase();
          else if (step.casingMode === 'title') {
            base = base.replace(/\b\w/g, l => l.toUpperCase());
          } else if (step.casingMode === 'kebab') {
            base = base.toLowerCase().replace(/[\s_]+/g, '-');
          } else if (step.casingMode === 'snake') {
            base = base.toLowerCase().replace(/[\s-]+/g, '_');
          }
          break;
        }
        case 'counter': {
          const start = step.counterStart ?? 1;
          const stepVal = step.counterStep ?? 1;
          const num = start + index * stepVal;
          const padded = String(num).padStart(step.counterPadding ?? 3, '0');
          if (step.counterPosition === 'prefix') {
            base = `${padded}_${base}`;
          } else {
            base = `${base}_${padded}`;
          }
          break;
        }
        case 'trim': {
          const count = step.trimCount ?? 0;
          if (count > 0 && base.length > count) {
            if (step.trimPosition === 'start') {
              base = base.substring(count);
            } else {
              base = base.substring(0, base.length - count);
            }
          }
          break;
        }
        case 'normalize': {
          if (step.stripAccents) {
            base = base.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
          }
          if (step.cleanSpaces) {
            base = base.replace(/\s+/g, ' ').trim();
          }
          if (step.sanitizeWeb) {
            base = base.replace(/[^a-zA-Z0-9._-]/g, '_');
          }
          break;
        }
        case 'extension': {
          if (step.newExtension) {
            ext = step.newExtension.replace(/^\./, '');
          }
          break;
        }
      }
    }

    return ext ? `${base}.${ext}` : base;
  };

  const addStep = (type: RenameStep['type']) => {
    const newStep: RenameStep = {
      id: String(Date.now()),
      type,
      enabled: true,
      ...(type === 'template' ? { pattern: '{FileName}_{Counter:3}' } : {}),
      ...(type === 'replace' ? { search: '_', replace: ' ', useRegex: false } : {}),
      ...(type === 'casing' ? { casingMode: 'title' } : {}),
      ...(type === 'counter' ? { counterStart: 1, counterStep: 1, counterPadding: 3, counterPosition: 'prefix' } : {}),
      ...(type === 'trim' ? { trimCount: 3, trimPosition: 'start' } : {}),
      ...(type === 'normalize' ? { stripAccents: true, cleanSpaces: true, sanitizeWeb: false } : {}),
      ...(type === 'extension' ? { newExtension: 'jpg' } : {})
    };
    setSteps(prev => [...prev, newStep]);
  };

  const removeStep = (id: string) => {
    setSteps(prev => prev.filter(s => s.id !== id));
  };

  const moveStep = (index: number, direction: 'up' | 'down') => {
    setSteps(prev => {
      const next = [...prev];
      const targetIndex = direction === 'up' ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= next.length) return prev;
      const [moved] = next.splice(index, 1);
      next.splice(targetIndex, 0, moved);
      return next;
    });
  };

  const handleSave = () => {
    onSave({
      PipelineName: pipelineName,
      RenameMode: renameMode,
      CollisionStrategy: collisionStrategy,
      MethodSteps: JSON.stringify(steps)
    });
    onClose();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        {/* Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">🏷️</div>
            <div>
              <h2 className="modal-title">Estudio de Renombrado Avanzado</h2>
              <p className="modal-subtitle">
                Pipeline secuencial de 7 métodos acumulativos con vista previa en tiempo real
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        {/* Modal Body */}
        <div className="modal-body-grid">
          {/* Left Column: Pipeline Steps */}
          <div className="modal-steps-column">
            <div className="column-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Layers size={16} color="var(--accent-blue)" />
                <span className="column-title">Métodos Secuenciales</span>
              </div>
              
              <div className="step-add-buttons">
                <button className="btn-small secondary" onClick={() => addStep('template')}>+ Token</button>
                <button className="btn-small secondary" onClick={() => addStep('replace')}>+ Reemplazo</button>
                <button className="btn-small secondary" onClick={() => addStep('casing')}>+ Mayúsculas</button>
                <button className="btn-small secondary" onClick={() => addStep('counter')}>+ Contador</button>
                <button className="btn-small secondary" onClick={() => addStep('normalize')}>+ Normalizar</button>
              </div>
            </div>

            <div className="steps-scroll-area">
              {steps.length === 0 ? (
                <div className="empty-steps-hint">
                  No hay métodos agregados. Haz clic en los botones superiores para añadir una regla.
                </div>
              ) : (
                steps.map((step, idx) => (
                  <div key={step.id} className={`step-card ${step.enabled ? '' : 'disabled'}`}>
                    <div className="step-card-header">
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <input
                          type="checkbox"
                          checked={step.enabled}
                          onChange={(e) => {
                            const val = e.target.checked;
                            setSteps(prev => prev.map(s => s.id === step.id ? { ...s, enabled: val } : s));
                          }}
                        />
                        <span className="step-badge">{idx + 1}</span>
                        <span className="step-name">
                          {step.type === 'template' && 'Plantilla con Tokens'}
                          {step.type === 'replace' && 'Buscar y Reemplazar'}
                          {step.type === 'casing' && 'Capitalización'}
                          {step.type === 'counter' && 'Numeración Secuencial'}
                          {step.type === 'trim' && 'Recorte de Caracteres'}
                          {step.type === 'normalize' && 'Normalización y Limpieza'}
                          {step.type === 'extension' && 'Cambio de Extensión'}
                        </span>
                      </div>

                      <div className="step-actions">
                        <button 
                          className="btn-tiny" 
                          disabled={idx === 0} 
                          onClick={() => moveStep(idx, 'up')}
                          title="Subir método"
                        >
                          <ArrowUp size={12} />
                        </button>
                        <button 
                          className="btn-tiny" 
                          disabled={idx === steps.length - 1} 
                          onClick={() => moveStep(idx, 'down')}
                          title="Bajar método"
                        >
                          <ArrowDown size={12} />
                        </button>
                        <button 
                          className="btn-tiny danger" 
                          onClick={() => removeStep(step.id)}
                          title="Eliminar método"
                        >
                          <Trash2 size={12} />
                        </button>
                      </div>
                    </div>

                    <div className="step-card-body">
                      {/* Método 1: Template */}
                      {step.type === 'template' && (
                        <div>
                          <input
                            type="text"
                            className="param-input"
                            value={step.pattern ?? ''}
                            placeholder="{Date:yyyy-MM-dd}_{FileName}"
                            onChange={(e) => {
                              const val = e.target.value;
                              setSteps(prev => prev.map(s => s.id === step.id ? { ...s, pattern: val } : s));
                            }}
                          />
                          <div className="token-pills-row">
                            {['{FileName}', '{Extension}', '{Date:yyyy-MM-dd}', '{Counter:3}', '{ParentDir}'].map(tok => (
                              <button
                                key={tok}
                                type="button"
                                className="token-pill"
                                onClick={() => {
                                  setSteps(prev => prev.map(s => s.id === step.id ? { ...s, pattern: (s.pattern ?? '') + tok } : s));
                                }}
                              >
                                {tok}
                              </button>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Método 2: Replace */}
                      {step.type === 'replace' && (
                        <div className="step-grid-2">
                          <input
                            type="text"
                            className="param-input"
                            placeholder="Buscar texto..."
                            value={step.search ?? ''}
                            onChange={(e) => {
                              const val = e.target.value;
                              setSteps(prev => prev.map(s => s.id === step.id ? { ...s, search: val } : s));
                            }}
                          />
                          <input
                            type="text"
                            className="param-input"
                            placeholder="Reemplazar por..."
                            value={step.replace ?? ''}
                            onChange={(e) => {
                              const val = e.target.value;
                              setSteps(prev => prev.map(s => s.id === step.id ? { ...s, replace: val } : s));
                            }}
                          />
                          <div style={{ display: 'flex', gap: '12px', gridColumn: '1 / -1', fontSize: '11px' }}>
                            <label style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                              <input
                                type="checkbox"
                                checked={step.useRegex ?? false}
                                onChange={(e) => {
                                  const val = e.target.checked;
                                  setSteps(prev => prev.map(s => s.id === step.id ? { ...s, useRegex: val } : s));
                                }}
                              />
                              Usar Regex
                            </label>
                            <label style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                              <input
                                type="checkbox"
                                checked={step.matchCase ?? false}
                                onChange={(e) => {
                                  const val = e.target.checked;
                                  setSteps(prev => prev.map(s => s.id === step.id ? { ...s, matchCase: val } : s));
                                }}
                              />
                              Mayúsculas y minúsculas
                            </label>
                          </div>
                        </div>
                      )}

                      {/* Método 3: Casing */}
                      {step.type === 'casing' && (
                        <div className="step-grid-2">
                          <select
                            className="param-select"
                            value={step.casingMode ?? 'title'}
                            onChange={(e) => {
                              const val = e.target.value as any;
                              setSteps(prev => prev.map(s => s.id === step.id ? { ...s, casingMode: val } : s));
                            }}
                          >
                            <option value="title">Tipo Título (Capitalizar Palabras)</option>
                            <option value="upper">MAYÚSCULAS</option>
                            <option value="lower">minúsculas</option>
                            <option value="kebab">kebab-case (separado-con-guiones)</option>
                            <option value="snake">snake_case (separado_con_guiones_bajos)</option>
                          </select>
                        </div>
                      )}

                      {/* Método 4: Counter */}
                      {step.type === 'counter' && (
                        <div className="step-grid-3">
                          <div>
                            <label className="tiny-label">Inicio</label>
                            <input
                              type="number"
                              className="param-input"
                              value={step.counterStart ?? 1}
                              onChange={(e) => {
                                const val = Number(e.target.value);
                                setSteps(prev => prev.map(s => s.id === step.id ? { ...s, counterStart: val } : s));
                              }}
                            />
                          </div>
                          <div>
                            <label className="tiny-label">Ceros (Padded)</label>
                            <input
                              type="number"
                              className="param-input"
                              value={step.counterPadding ?? 3}
                              min={1}
                              max={10}
                              onChange={(e) => {
                                const val = Number(e.target.value);
                                setSteps(prev => prev.map(s => s.id === step.id ? { ...s, counterPadding: val } : s));
                              }}
                            />
                          </div>
                          <div>
                            <label className="tiny-label">Posición</label>
                            <select
                              className="param-select"
                              value={step.counterPosition ?? 'prefix'}
                              onChange={(e) => {
                                const val = e.target.value as any;
                                setSteps(prev => prev.map(s => s.id === step.id ? { ...s, counterPosition: val } : s));
                              }}
                            >
                              <option value="prefix">Prefijo (001_Nombre)</option>
                              <option value="suffix">Sufijo (Nombre_001)</option>
                            </select>
                          </div>
                        </div>
                      )}

                      {/* Método 6: Normalize */}
                      {step.type === 'normalize' && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '11px' }}>
                          <label style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <input
                              type="checkbox"
                              checked={step.stripAccents ?? true}
                              onChange={(e) => {
                                const val = e.target.checked;
                                setSteps(prev => prev.map(s => s.id === step.id ? { ...s, stripAccents: val } : s));
                              }}
                            />
                            Eliminar tildes y diacríticos (á -&gt; a, ñ -&gt; n)
                          </label>
                          <label style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <input
                              type="checkbox"
                              checked={step.cleanSpaces ?? true}
                              onChange={(e) => {
                                const val = e.target.checked;
                                setSteps(prev => prev.map(s => s.id === step.id ? { ...s, cleanSpaces: val } : s));
                              }}
                            />
                            Limpiar espacios duplicados y extremos
                          </label>
                          <label style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <input
                              type="checkbox"
                              checked={step.sanitizeWeb ?? false}
                              onChange={(e) => {
                                const val = e.target.checked;
                                setSteps(prev => prev.map(s => s.id === step.id ? { ...s, sanitizeWeb: val } : s));
                              }}
                            />
                            Sanitizar nombres para web (reemplazar caracteres extraños por _)
                          </label>
                        </div>
                      )}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Right Column: Live Preview & Pipeline Settings */}
          <div className="modal-preview-column">
            {/* Pipeline Configuration Settings */}
            <div className="preview-card-box">
              <div className="card-box-header">
                <Settings2 size={15} color="var(--accent-cyan)" />
                <span>Configuración de Destino</span>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginTop: '8px' }}>
                <div>
                  <label className="tiny-label">Nombre del Pipeline</label>
                  <input
                    type="text"
                    className="param-input"
                    value={pipelineName}
                    onChange={(e) => setPipelineName(e.target.value)}
                  />
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                  <div>
                    <label className="tiny-label">Modo de Ejecución</label>
                    <select
                      className="param-select"
                      value={renameMode}
                      onChange={(e) => setRenameMode(e.target.value as any)}
                    >
                      <option value="Virtual">Virtual (Seguro / En Memoria)</option>
                      <option value="DirectInPlace">DirectInPlace (En Disco Físico)</option>
                    </select>
                  </div>
                  <div>
                    <label className="tiny-label">Resolución de Colisiones</label>
                    <select
                      className="param-select"
                      value={collisionStrategy}
                      onChange={(e) => setCollisionStrategy(e.target.value as any)}
                    >
                      <option value="AutoIncrement">AutoIncrement (foto (1).jpg)</option>
                      <option value="Overwrite">Sobrescribir</option>
                      <option value="Skip">Omitir</option>
                      <option value="Fail">Fallar y Registrar Error</option>
                    </select>
                  </div>
                </div>
              </div>
            </div>

            {/* Live Preview Table */}
            <div className="preview-card-box" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
              <div className="card-box-header">
                <Eye size={15} color="var(--accent-purple)" />
                <span>Vista Previa en Tiempo Real</span>
              </div>
              <div className="preview-table-container">
                <table className="preview-table">
                  <thead>
                    <tr>
                      <th>Nombre Original</th>
                      <th>Resultado Transformado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {SAMPLE_FILES.map((orig, i) => {
                      const transformed = applyStepsToFile(orig, i);
                      return (
                        <tr key={orig}>
                          <td className="cell-orig" title={orig}>{orig}</td>
                          <td className="cell-result" title={transformed}>
                            <span className="result-highlight">{transformed}</span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>

        {/* Modal Footer */}
        <div className="modal-footer">
          <div style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
            💡 Los cambios se sincronizarán con los parámetros del nodo actual en el flujo.
          </div>
          <div style={{ display: 'flex', gap: '10px' }}>
            <button className="btn-action secondary" onClick={onClose}>
              Cancelar
            </button>
            <button className="btn-action primary" onClick={handleSave}>
              <Check size={16} />
              <span>Aplicar al Nodo</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
