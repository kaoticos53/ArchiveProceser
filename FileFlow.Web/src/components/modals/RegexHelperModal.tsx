import React, { useState, useMemo } from 'react';
import { 
  X, 
  Check, 
  FileText
} from 'lucide-react';
import type { FlowNodeData } from '../../types/flow';

interface RegexHelperModalProps {
  isOpen: boolean;
  onClose: () => void;
  nodeData?: FlowNodeData | null;
  onSave?: (updatedParams: Record<string, any>) => void;
}

const COMMON_PATTERNS = [
  { name: 'Temporada y Episodio (S01E02)', pattern: '(?i)s(?<season>\\d{1,2})e(?<episode>\\d{1,3})' },
  { name: 'Resolución de Video', pattern: '(1080p|720p|2160p|4K|UHD)' },
  { name: 'Fecha ISO (YYYY-MM-DD)', pattern: '\\b\\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\\d|3[01])\\b' },
  { name: 'Correo Electrónico', pattern: '[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}' },
  { name: 'Dirección IPv4', pattern: '\\b(?:\\d{1,3}\\.){3}\\d{1,3}\\b' },
  { name: 'Extensiones de Imagen', pattern: '\\.(jpe?g|png|webp|gif|bmp|tiff)$' },
  { name: 'Archivos Comprimidos', pattern: '\\.(zip|rar|7z|tar|gz|bz2)$' },
  { name: 'Identificador GUID / UUID', pattern: '[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}' },
];

export const RegexHelperModal: React.FC<RegexHelperModalProps> = ({
  isOpen,
  onClose,
  nodeData,
  onSave,
}) => {
  if (!isOpen) return null;

  const currentPattern = (() => {
    if (!nodeData) return '(?i)s(?<season>\\d{1,2})e(?<episode>\\d{1,3})';
    const p = nodeData.parameters?.find(x => 
      x.key.toLowerCase().includes('regex') || 
      x.key.toLowerCase().includes('pattern') ||
      x.key.toLowerCase().includes('filtro')
    );
    return (p?.value as string) || '(?i)s(?<season>\\d{1,2})e(?<episode>\\d{1,3})';
  })();

  const [pattern, setPattern] = useState(currentPattern);
  const [flags, setFlags] = useState({ ignoreCase: true, multiline: false, global: true });
  const [testText, setTestText] = useState(
    "Stranger.Things.S04E07.1080p.WEBRip.mkv\n" +
    "Breaking.Bad.s01e01.720p.HDTV.avi\n" +
    "Game.of.Thrones.S08E03.4K.HDR.mp4\n" +
    "Archivo_Sin_Episodio_2026.pdf"
  );

  // Evaluación en vivo
  const { matches, error } = useMemo(() => {
    try {
      let fl = '';
      if (flags.global) fl += 'g';
      if (flags.ignoreCase) fl += 'i';
      if (flags.multiline) fl += 'm';

      // Limpiar prefijo .NET (?i) para RegExp JS si existe
      let jsPattern = pattern;
      if (jsPattern.startsWith('(?i)')) {
        jsPattern = jsPattern.substring(4);
        if (!fl.includes('i')) fl += 'i';
      }

      const regex = new RegExp(jsPattern, fl);
      const lines = testText.split('\n');
      const results: Array<{ line: string; matched: boolean; fullMatch?: string; groups?: Record<string, string> }> = [];

      for (const line of lines) {
        if (!line) continue;
        const m = line.match(regex);
        if (m) {
          results.push({
            line,
            matched: true,
            fullMatch: m[0],
            groups: m.groups || {}
          });
        } else {
          results.push({
            line,
            matched: false
          });
        }
      }

      return { matches: results, error: null };
    } catch (e: any) {
      return { matches: [], error: e.message };
    }
  }, [pattern, flags, testText]);

  const handleSave = () => {
    if (onSave && nodeData) {
      // Buscar la clave correspondiente al regex o actualizar Pattern
      const p = nodeData.parameters?.find(x => 
        x.key.toLowerCase().includes('regex') || 
        x.key.toLowerCase().includes('pattern') ||
        x.key.toLowerCase().includes('filtro')
      );
      const key = p ? p.key : 'Pattern';
      onSave({ [key]: pattern });
    }
    onClose();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">🔍</div>
            <div>
              <h2 className="modal-title">Asistente de Expresiones Regulares</h2>
              <p className="modal-subtitle">
                Diseña, evalúa y extrae grupos de captura en tiempo real compatibles con .NET 9 Regex
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body-grid">
          {/* Columna Izquierda: Editor y Patrones */}
          <div className="modal-steps-column">
            <div className="column-header">
              <span className="column-title">Expresión Regular (.NET 9)</span>
            </div>

            <div style={{ marginTop: '10px' }}>
              <input
                type="text"
                className="param-input"
                style={{ fontFamily: 'monospace', fontSize: '13px', fontWeight: 600, color: 'var(--accent-blue)' }}
                value={pattern}
                onChange={(e) => setPattern(e.target.value)}
                placeholder="Escribe tu patrón regex..."
              />

              {error && (
                <div style={{ color: '#ef4444', fontSize: '11px', marginTop: '6px' }}>
                  ⚠️ Error de sintaxis Regex: {error}
                </div>
              )}

              {/* Flags */}
              <div style={{ display: 'flex', gap: '14px', marginTop: '10px', fontSize: '12px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <input
                    type="checkbox"
                    checked={flags.ignoreCase}
                    onChange={(e) => setFlags(f => ({ ...f, ignoreCase: e.target.checked }))}
                  />
                  Ignorar Mayúsculas (/i)
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <input
                    type="checkbox"
                    checked={flags.multiline}
                    onChange={(e) => setFlags(f => ({ ...f, multiline: e.target.checked }))}
                  />
                  Multilínea (/m)
                </label>
              </div>
            </div>

            {/* Biblioteca de Patrones Frecuentes */}
            <div style={{ marginTop: '18px' }}>
              <span className="tiny-label" style={{ marginBottom: '8px', display: 'block' }}>
                Patrones Frecuentes de FileFlow Studio
              </span>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                {COMMON_PATTERNS.map((p) => (
                  <button
                    key={p.name}
                    className="step-card"
                    style={{ textAlign: 'left', padding: '8px 10px', cursor: 'pointer' }}
                    onClick={() => setPattern(p.pattern)}
                  >
                    <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-primary)' }}>
                      {p.name}
                    </div>
                    <div style={{ fontSize: '11px', fontFamily: 'monospace', color: 'var(--text-dim)', marginTop: '2px' }}>
                      {p.pattern}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Columna Derecha: Texto de Prueba y Resultados */}
          <div className="modal-preview-column">
            <div className="preview-card-box" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
              <div className="card-box-header">
                <FileText size={15} color="var(--accent-cyan)" />
                <span>Texto de Prueba Multilínea</span>
              </div>
              <textarea
                className="param-textarea"
                rows={4}
                style={{ marginTop: '8px', fontFamily: 'monospace', fontSize: '12px' }}
                value={testText}
                onChange={(e) => setTestText(e.target.value)}
              />

              <div className="card-box-header" style={{ marginTop: '14px' }}>
                <Check size={15} color="var(--accent-green)" />
                <span>Resultados de Coincidencia en Vivo</span>
              </div>

              <div className="preview-table-container" style={{ marginTop: '8px', flex: 1 }}>
                <table className="preview-table">
                  <thead>
                    <tr>
                      <th>Estado</th>
                      <th>Línea Analizada</th>
                      <th>Coincidencia / Grupos</th>
                    </tr>
                  </thead>
                  <tbody>
                    {matches.map((m, idx) => (
                      <tr key={idx}>
                        <td style={{ width: '40px' }}>
                          {m.matched ? (
                            <span style={{ color: '#10b981', fontWeight: 'bold' }}>✓ SI</span>
                          ) : (
                            <span style={{ color: '#6b7280' }}>✕ NO</span>
                          )}
                        </td>
                        <td style={{ fontFamily: 'monospace', fontSize: '12px' }}>{m.line}</td>
                        <td>
                          {m.matched && (
                            <div>
                              <span className="result-highlight">{m.fullMatch}</span>
                              {m.groups && Object.keys(m.groups).length > 0 && (
                                <div style={{ fontSize: '10.5px', color: 'var(--text-dim)', marginTop: '4px' }}>
                                  {Object.entries(m.groups).map(([k, v]) => (
                                    <span key={k} style={{ marginRight: '8px' }}>
                                      <strong>{k}:</strong> {v}
                                    </span>
                                  ))}
                                </div>
                              )}
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>

        <div className="modal-footer">
          <div style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
            💡 Soporta sintaxis de captura nombrada `(?&lt;nombre&gt;...)` para inyección de metadatos.
          </div>
          <div style={{ display: 'flex', gap: '10px' }}>
            <button className="btn-action secondary" onClick={onClose}>
              Cancelar
            </button>
            <button className="btn-action primary" onClick={handleSave}>
              <Check size={16} />
              <span>Aplicar Patrón</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
