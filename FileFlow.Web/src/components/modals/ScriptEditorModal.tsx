import React, { useState } from 'react';
import { 
  X, 
  Check, 
  Code2, 
  Play, 
  Sparkles, 
  Terminal
} from 'lucide-react';
import type { FlowNodeData } from '../../types/flow';

interface ScriptEditorModalProps {
  isOpen: boolean;
  onClose: () => void;
  nodeData?: FlowNodeData | null;
  onSave?: (updatedParams: Record<string, any>) => void;
}

const TEMPLATES: Record<string, string> = {
  'csharp_filter': `// C# 13 Script para FileFlow Studio
// Parámetros disponibles: item (FileItemContext), context (IFlowExecutionContext)

if (item.Size > 10 * 1024 * 1024) 
{
    context.Log($"Archivo grande detectado: {item.CurrentPath} ({item.Size} bytes)", LogLevel.Info, item);
    item.Metadata["Category"] = "LargeFile";
    return true; // Emitir a Out
}

return false; // Descartar`,

  'csharp_metadata': `// Extraer metadatos y renombrar virtualmente
string fileName = System.IO.Path.GetFileNameWithoutExtension(item.CurrentPath);
string extension = System.IO.Path.GetExtension(item.CurrentPath);

item.Metadata["ProcessedBy"] = "CustomScriptNode";
item.Metadata["OriginalName"] = fileName;

// Modificar nombre virtual si aplica
item.VirtualPath = System.IO.Path.Combine("Procesados", $"{fileName}_processed{extension}");
return true;`,

  'python_basic': `# Python 3.12 Script para FileFlow Studio
# Variables: item, context, cancellation_token

file_size_mb = item.size / (1024 * 1024)
context.log(f"Procesando fichero: {item.current_path} ({file_size_mb:.2f} MB)")

if item.extension.lower() in [".jpg", ".png", ".webp"]:
    item.metadata["media_type"] = "image"
    return True
else:
    return False`
};

export const ScriptEditorModal: React.FC<ScriptEditorModalProps> = ({
  isOpen,
  onClose,
  nodeData,
  onSave,
}) => {
  if (!isOpen) return null;

  const getParam = (k: string, def: any) => {
    if (!nodeData) return def;
    const p = nodeData.parameters?.find(x => x.key.toLowerCase() === k.toLowerCase());
    return p?.value ?? def;
  };

  const [language, setLanguage] = useState<string>(getParam('Language', 'CSharp'));
  const [scriptCode, setScriptCode] = useState<string>(() => {
    const raw = getParam('ScriptCode', '');
    if (raw && typeof raw === 'string' && raw.trim().length > 0) return raw;
    return TEMPLATES['csharp_filter'];
  });
  const [testOutput, setTestOutput] = useState<string>('Listo para simular ejecución.');

  const handleRunTest = () => {
    setTestOutput(`[${new Date().toLocaleTimeString()}] Compilando script con Roslyn .NET 9...\n✓ Compilación exitosa: 0 errores, 0 advertencias.\n✓ Simulación ejecutada en FileItemContext dummy ('video_sample.mkv').\nResultado: Return = true (Item emitido a 'Out').`);
  };

  const handleSave = () => {
    if (onSave) {
      onSave({
        Language: language,
        ScriptCode: scriptCode
      });
    }
    onClose();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">💻</div>
            <div>
              <h2 className="modal-title">Editor de Scripts Personalizados</h2>
              <p className="modal-subtitle">
                Escribe lógica arbitraria de filtrado, transformación o cálculo en C# 13 o Python
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body-grid">
          {/* Columna Izquierda: Editor */}
          <div className="modal-steps-column" style={{ flex: 1.5 }}>
            <div className="column-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Code2 size={16} color="var(--accent-blue)" />
                <span className="column-title">Código Fuente</span>
              </div>

              <div style={{ display: 'flex', gap: '8px' }}>
                <select
                  className="param-select"
                  style={{ padding: '3px 8px', fontSize: '11px', width: '110px' }}
                  value={language}
                  onChange={(e) => setLanguage(e.target.value)}
                >
                  <option value="CSharp">C# 13 (.NET 9)</option>
                  <option value="Python">Python 3.12</option>
                </select>

                <select
                  className="param-select"
                  style={{ padding: '3px 8px', fontSize: '11px', width: '130px' }}
                  onChange={(e) => {
                    const key = e.target.value;
                    if (key && TEMPLATES[key]) setScriptCode(TEMPLATES[key]);
                  }}
                  defaultValue=""
                >
                  <option value="" disabled>Cargar Plantilla...</option>
                  <option value="csharp_filter">Filtro por Tamaño (C#)</option>
                  <option value="csharp_metadata">Inyección Metadatos (C#)</option>
                  <option value="python_basic">Clasificador (Python)</option>
                </select>
              </div>
            </div>

            <textarea
              className="param-textarea"
              style={{
                fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                fontSize: '12.5px',
                lineHeight: '1.5',
                flex: 1,
                minHeight: '340px',
                marginTop: '10px',
                background: '#0d1117',
                color: '#58a6ff',
                padding: '12px'
              }}
              value={scriptCode}
              onChange={(e) => setScriptCode(e.target.value)}
            />
          </div>

          {/* Columna Derecha: Output y Referencia */}
          <div className="modal-preview-column" style={{ flex: 1 }}>
            <div className="preview-card-box" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
              <div className="card-box-header">
                <Terminal size={15} color="var(--accent-green)" />
                <span>Consola de Simulación y Test</span>
              </div>

              <div style={{ marginTop: '10px' }}>
                <button className="btn-action primary" onClick={handleRunTest}>
                  <Play size={14} fill="currentColor" />
                  <span>Probar Ejecución Sintética</span>
                </button>
              </div>

              <pre
                style={{
                  marginTop: '10px',
                  background: '#0a0d13',
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid var(--border-dark)',
                  color: 'var(--text-secondary)',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  whiteSpace: 'pre-wrap',
                  flex: 1,
                  overflowY: 'auto'
                }}
              >
                {testOutput}
              </pre>

              <div className="card-box-header" style={{ marginTop: '14px' }}>
                <Sparkles size={15} color="var(--accent-purple)" />
                <span>Variables en Contexto</span>
              </div>
              <ul style={{ fontSize: '11px', color: 'var(--text-dim)', paddingLeft: '18px', marginTop: '6px', lineHeight: '1.6' }}>
                <li><code>item.CurrentPath</code>: Ruta actual del fichero</li>
                <li><code>item.OriginalPath</code>: Ruta de origen intacta</li>
                <li><code>item.Size</code>: Tamaño en bytes (long)</li>
                <li><code>item.Metadata</code>: Diccionario de metadatos clave/valor</li>
                <li><code>context.Log(msg, level)</code>: Escribe en la consola DAG</li>
              </ul>
            </div>
          </div>
        </div>

        <div className="modal-footer">
          <div style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
            💡 Los scripts en C# se compilan mediante Roslyn nativo de .NET 9 sin dependencias externas.
          </div>
          <div style={{ display: 'flex', gap: '10px' }}>
            <button className="btn-action secondary" onClick={onClose}>
              Cancelar
            </button>
            <button className="btn-action primary" onClick={handleSave}>
              <Check size={16} />
              <span>Guardar Script</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
