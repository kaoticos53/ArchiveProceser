import React from 'react';
import { X } from 'lucide-react';

interface AboutModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const AboutModal: React.FC<AboutModalProps> = ({
  isOpen,
  onClose,
}) => {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="modal-dialog">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">⚡</div>
            <div>
              <h2 className="modal-title">Acerca de FileFlow Studio</h2>
              <p className="modal-subtitle">Orquestador de Pipelines DAG de Archivos de Alto Rendimiento</p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body" style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div style={{ textAlign: 'center', padding: '10px 0' }}>
            <div style={{ fontSize: '24px', fontWeight: 'bold', letterSpacing: '-0.5px' }}>
              FileFlow Studio <span style={{ fontSize: '13px', background: 'var(--accent-blue)', padding: '2px 8px', borderRadius: '12px', verticalAlign: 'middle' }}>v1.0.0 Multiplataforma</span>
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-dim)', marginTop: '4px' }}>
              Edición Cross-Platform (Linux, macOS, Windows & Web)
            </div>
          </div>

          <div className="settings-card">
            <div style={{ fontWeight: 600, fontSize: '13px', marginBottom: '8px' }}>Arquitectura Tecnológica</div>
            <ul style={{ fontSize: '12px', color: 'var(--text-secondary)', paddingLeft: '18px', lineHeight: '1.6' }}>
              <li><strong>Motor Central:</strong> .NET 9 con C# 13, Microkernel modular y concurrencia por canales TPL Dataflow.</li>
              <li><strong>Lienzo Visual:</strong> React 19 + TypeScript + @xyflow/react (React Flow v12) con aceleración WebGL/SVG.</li>
              <li><strong>Cliente de Escritorio:</strong> Photino.NET ligero y sin sobrecarga de Chromium pesado.</li>
              <li><strong>Inmutabilidad:</strong> Pipeline no destructivo por defecto; preservación del origen (<code>OriginalPath</code>).</li>
            </ul>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-dim)', borderTop: '1px solid var(--border-dark)', paddingTop: '12px' }}>
            <span>© 2026 RGLara • Todos los derechos reservados</span>
            <span>Licencia MIT</span>
          </div>
        </div>

        <div className="modal-footer">
          <button className="btn-action primary" onClick={onClose}>
            Aceptar
          </button>
        </div>
      </div>
    </div>
  );
};
