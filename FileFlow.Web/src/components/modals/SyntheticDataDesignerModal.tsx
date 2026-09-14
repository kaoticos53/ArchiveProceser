import React, { useState } from 'react';
import { 
  X, 
  Check, 
  Plus, 
  Trash2, 
  FileCode
} from 'lucide-react';
import type { FlowNodeData } from '../../types/flow';

interface SyntheticDataDesignerModalProps {
  isOpen: boolean;
  onClose: () => void;
  nodeData?: FlowNodeData | null;
  onSave?: (updatedParams: Record<string, any>) => void;
}

const CATEGORY_PRESETS: Record<string, string[]> = {
  'Películas': [
    'Inception (2010).mkv',
    'Interstellar.2014.IMAX.2160p.UHD.BluRay.x265.mkv',
    'The Dark Knight (2008) 1080p BluRay.mp4',
    'Pulp Fiction (1994) Special Edition.avi',
    'Blade Runner 2049 (2017).mkv'
  ],
  'Series': [
    'Breaking Bad - S01E01 - Pilot.mkv',
    'Stranger Things - S04E07 - Chapter Seven.mp4',
    'Game of Thrones - S08E03 - The Long Night.mkv',
    'Better Call Saul - S06E13 - Saul Gone.mkv'
  ],
  'Cómics y Manga': [
    'Batman - Year One (1987) #01.cbz',
    'One Piece - Cap 1044 [Español].cbr',
    'Spider-Man - Kraven Last Hunt.cbz',
    'Berserk - Tomo 01.cbr'
  ],
  'Música': [
    '01 - Bohemian Rhapsody.flac',
    '02 - Hotel California.mp3',
    '03 - Stairway to Heaven.wav',
    '04 - Billie Jean.flac'
  ],
  'Fotos': [
    'DSC_0042.JPG',
    'IMG_20260914_120000.RAW',
    'Retrato_Familia.PNG',
    'Paisaje_Montaña_HDR.TIFF'
  ],
  'Documentos': [
    'Contrato_Servicios_2026.pdf',
    'Balance_Financiero_Anual.xlsx',
    'Presentacion_Estrategica.pptx',
    'Notas_Reunion_Directorio.docx'
  ]
};

export const SyntheticDataDesignerModal: React.FC<SyntheticDataDesignerModalProps> = ({
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

  const [category, setCategory] = useState<string>(getParam('Category', 'Películas'));
  const [emissionMode, setEmissionMode] = useState<string>(getParam('EmissionMode', 'Virtual'));
  const [maxItems, setMaxItems] = useState<number>(getParam('MaxItems', 10));
  const [delayMs, setDelayMs] = useState<number>(getParam('EmissionDelayMs', 0));
  const [emitDirectories, setEmitDirectories] = useState<boolean>(getParam('EmitDirectories', false));
  const [outputFolder, setOutputFolder] = useState<string>(getParam('OutputFolder', ''));
  
  const [items, setItems] = useState<string[]>(() => {
    const raw = getParam('CustomItems', '');
    if (raw && typeof raw === 'string' && raw.trim().length > 0) {
      return raw.split('\n').map(s => s.trim()).filter(Boolean);
    }
    return CATEGORY_PRESETS['Películas'] || [];
  });

  const [newItem, setNewItem] = useState('');

  const handleCategoryChange = (newCat: string) => {
    setCategory(newCat);
    if (newCat in CATEGORY_PRESETS) {
      setItems([...CATEGORY_PRESETS[newCat]]);
    }
  };

  const handleAddItem = () => {
    if (newItem.trim()) {
      setItems(prev => [...prev, newItem.trim()]);
      setNewItem('');
    }
  };

  const handleRemoveItem = (index: number) => {
    setItems(prev => prev.filter((_, i) => i !== index));
  };

  const handleSave = () => {
    if (onSave) {
      onSave({
        Category: category,
        EmissionMode: emissionMode,
        MaxItems: maxItems,
        EmissionDelayMs: delayMs,
        EmitDirectories: emitDirectories,
        OutputFolder: outputFolder,
        CustomItems: items.join('\n')
      });
    }
    onClose();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        {/* Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">📊</div>
            <div>
              <h2 className="modal-title">Diseñador de Datos Sintéticos y Datasets</h2>
              <p className="modal-subtitle">
                Crea y emite conjuntos de archivos y directorios ficticios para pruebas de estrés y validación DAG
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        {/* Content */}
        <div className="modal-body-grid">
          {/* Left Column: Properties */}
          <div className="modal-steps-column">
            <div className="column-header">
              <span className="column-title">Parámetros de Emisión</span>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', marginTop: '10px' }}>
              <div>
                <label className="param-label">Categoría Predefinida</label>
                <select
                  className="param-select"
                  value={category}
                  onChange={(e) => handleCategoryChange(e.target.value)}
                >
                  <option value="Películas">🎬 Películas</option>
                  <option value="Series">📺 Series de TV</option>
                  <option value="Cómics y Manga">📚 Cómics y Manga</option>
                  <option value="Música">🎵 Música</option>
                  <option value="Fotos">📷 Fotografías</option>
                  <option value="Documentos">📄 Documentos y Ofimática</option>
                  <option value="Personalizada">⚙️ Personalizada</option>
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label className="param-label">Modo de Emisión</label>
                  <select
                    className="param-select"
                    value={emissionMode}
                    onChange={(e) => setEmissionMode(e.target.value)}
                  >
                    <option value="Virtual">Virtual (En Memoria VFS)</option>
                    <option value="PhysicalMock">PhysicalMock (En Disco)</option>
                  </select>
                </div>
                <div>
                  <label className="param-label">Límite de Ítems (0 = sin límite)</label>
                  <input
                    type="number"
                    className="param-input"
                    value={maxItems}
                    min={0}
                    max={1000}
                    onChange={(e) => setMaxItems(Number(e.target.value))}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label className="param-label">Retardo por Ítem (ms)</label>
                  <input
                    type="number"
                    className="param-input"
                    value={delayMs}
                    min={0}
                    max={5000}
                    onChange={(e) => setDelayMs(Number(e.target.value))}
                  />
                </div>
                <div>
                  <label className="param-label">Estructura</label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '8px', fontSize: '13px' }}>
                    <input
                      type="checkbox"
                      checked={emitDirectories}
                      onChange={(e) => setEmitDirectories(e.target.checked)}
                    />
                    Emitir Directorios Jerárquicos
                  </label>
                </div>
              </div>

              {emissionMode === 'PhysicalMock' && (
                <div>
                  <label className="param-label">Carpeta de Salida Física</label>
                  <input
                    type="text"
                    className="param-input"
                    placeholder="C:/Temp/SyntheticFiles"
                    value={outputFolder}
                    onChange={(e) => setOutputFolder(e.target.value)}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Right Column: Custom Items List */}
          <div className="modal-preview-column">
            <div className="preview-card-box" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
              <div className="card-box-header">
                <FileCode size={15} color="var(--accent-blue)" />
                <span>Lista de Archivos a Emitir ({items.length})</span>
              </div>

              {/* Add item bar */}
              <div style={{ display: 'flex', gap: '8px', marginTop: '10px' }}>
                <input
                  type="text"
                  className="param-input"
                  placeholder="Escribe el nombre de un archivo o carpeta sintética..."
                  value={newItem}
                  onChange={(e) => setNewItem(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleAddItem()}
                />
                <button className="btn-action primary" onClick={handleAddItem}>
                  <Plus size={15} />
                </button>
              </div>

              {/* Items scroll */}
              <div className="preview-table-container" style={{ marginTop: '10px', flex: 1 }}>
                <table className="preview-table">
                  <thead>
                    <tr>
                      <th style={{ width: '40px' }}>#</th>
                      <th>Nombre de Archivo Sintético</th>
                      <th style={{ width: '50px' }}>Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item, idx) => (
                      <tr key={`${item}-${idx}`}>
                        <td style={{ color: 'var(--text-dim)', fontSize: '11px' }}>{idx + 1}</td>
                        <td style={{ fontFamily: 'monospace', fontSize: '12px' }}>{item}</td>
                        <td>
                          <button
                            className="btn-tiny danger"
                            onClick={() => handleRemoveItem(idx)}
                            title="Eliminar elemento"
                          >
                            <Trash2 size={12} />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="modal-footer">
          <div style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
            💡 Estos archivos se emitirán como contexto `FileItemContext` seguro sin alterar tu disco.
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
