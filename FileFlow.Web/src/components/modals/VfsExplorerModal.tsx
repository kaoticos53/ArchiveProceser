import React, { useState } from 'react';
import { 
  X, 
  Folder, 
  File, 
  Download, 
  Info
} from 'lucide-react';

interface VfsExplorerModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface VirtualFileEntry {
  id: string;
  name: string;
  virtualPath: string;
  sizeBytes: number;
  mimeType: string;
  status: 'virtual' | 'persisted' | 'modified';
  metadata: Record<string, string>;
}

const SAMPLE_VFS_FILES: VirtualFileEntry[] = [
  {
    id: '1',
    name: '2026-09-14_Inception_HD.mkv',
    virtualPath: 'VFS://Películas/2026-09-14_Inception_HD.mkv',
    sizeBytes: 1450000000,
    mimeType: 'video/x-matroska',
    status: 'virtual',
    metadata: { 'Resolution': '1080p', 'Codec': 'x264', 'Pipeline': 'AdvancedRenamer' }
  },
  {
    id: '2',
    name: 'DSC_0042_processed.webp',
    virtualPath: 'VFS://Fotos/Optimizadas/DSC_0042_processed.webp',
    sizeBytes: 245000,
    mimeType: 'image/webp',
    status: 'virtual',
    metadata: { 'Quality': '85%', 'StrippedExif': 'True', 'Engine': 'ImageSharp' }
  },
  {
    id: '3',
    name: 'Reporte_Consolidado.pdf',
    virtualPath: 'VFS://Documentos/Reporte_Consolidado.pdf',
    sizeBytes: 1024000,
    mimeType: 'application/pdf',
    status: 'virtual',
    metadata: { 'Author': 'FileFlow Studio', 'Generated': '2026-09-14' }
  }
];

export const VfsExplorerModal: React.FC<VfsExplorerModalProps> = ({
  isOpen,
  onClose,
}) => {
  if (!isOpen) return null;

  const [files] = useState<VirtualFileEntry[]>(SAMPLE_VFS_FILES);
  const [selectedFile, setSelectedFile] = useState<VirtualFileEntry | null>(files[0] || null);
  const [searchTerm, setSearchTerm] = useState('');

  const filtered = files.filter(f => 
    f.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    f.virtualPath.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">🗂️</div>
            <div>
              <h2 className="modal-title">Explorador de Archivos Virtuales (VFS)</h2>
              <p className="modal-subtitle">
                Inspecciona y exporta archivos generados en memoria durante ejecuciones simuladas o pruebas de flujo
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body-grid">
          {/* Columna Izquierda: Lista de Archivos */}
          <div className="modal-steps-column">
            <div className="column-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Folder size={15} color="var(--accent-blue)" />
                <span className="column-title">Archivos Virtuales ({filtered.length})</span>
              </div>
            </div>

            <div style={{ margin: '8px 0' }}>
              <input
                type="text"
                className="param-input"
                placeholder="Buscar archivos en VFS..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>

            <div className="steps-scroll-area">
              {filtered.map(f => (
                <div
                  key={f.id}
                  className={`step-card ${selectedFile?.id === f.id ? 'active' : ''}`}
                  style={{ cursor: 'pointer', padding: '10px' }}
                  onClick={() => setSelectedFile(f)}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <File size={16} color="var(--accent-cyan)" />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: '12px', fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {f.name}
                      </div>
                      <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>
                        {(f.sizeBytes / 1024).toFixed(1)} KB • {f.mimeType}
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Columna Derecha: Detalle y Metadatos */}
          <div className="modal-preview-column">
            {selectedFile ? (
              <div className="preview-card-box" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                <div className="card-box-header">
                  <Info size={15} color="var(--accent-purple)" />
                  <span>Detalles de {selectedFile.name}</span>
                </div>

                <div style={{ marginTop: '12px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  <div>
                    <label className="tiny-label">Ruta Virtual Canónica</label>
                    <div style={{ fontFamily: 'monospace', fontSize: '12px', color: 'var(--accent-cyan)' }}>
                      {selectedFile.virtualPath}
                    </div>
                  </div>

                  <div>
                    <label className="tiny-label">Tipo MIME</label>
                    <div style={{ fontSize: '12px' }}>{selectedFile.mimeType}</div>
                  </div>

                  <div>
                    <label className="tiny-label">Diccionario de Metadatos</label>
                    <div className="preview-table-container" style={{ marginTop: '6px' }}>
                      <table className="preview-table">
                        <thead>
                          <tr>
                            <th>Clave</th>
                            <th>Valor</th>
                          </tr>
                        </thead>
                        <tbody>
                          {Object.entries(selectedFile.metadata).map(([k, v]) => (
                            <tr key={k}>
                              <td style={{ fontWeight: 600 }}>{k}</td>
                              <td style={{ fontFamily: 'monospace' }}>{v}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </div>

                <div style={{ marginTop: 'auto', paddingTop: '16px' }}>
                  <button className="btn-action primary" onClick={() => alert(`Exportando ${selectedFile.name} a disco físico...`)}>
                    <Download size={14} />
                    <span>Exportar Archivo a Disco Físico</span>
                  </button>
                </div>
              </div>
            ) : (
              <div className="empty-steps-hint">Selecciona un archivo virtual para ver sus detalles.</div>
            )}
          </div>
        </div>

        <div className="modal-footer">
          <div style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
            💡 Los archivos en VFS se liberan automáticamente al reiniciar el servidor a menos que se persistan.
          </div>
          <button className="btn-action secondary" onClick={onClose}>
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
};
