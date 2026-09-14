import React from 'react';
import { X, Workflow, Zap, Sliders, Shield } from 'lucide-react';

interface UserManualModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const UserManualModal: React.FC<UserManualModalProps> = ({
  isOpen,
  onClose,
}) => {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">📖</div>
            <div>
              <h2 className="modal-title">Manual de Usuario y Guía de FileFlow Studio</h2>
              <p className="modal-subtitle">Principios de construcción y ejecución de flujos DAG de archivos</p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body" style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px', maxHeight: '520px', overflowY: 'auto' }}>
          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
              <Workflow size={16} color="var(--accent-blue)" />
              <strong style={{ fontSize: '14px' }}>1. Construcción del Grafo (Lienzo)</strong>
            </div>
            <p style={{ fontSize: '12.5px', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
              Abre la <strong>Biblioteca de Nodos</strong> (botón en la barra superior o <kbd>Ctrl+B</kbd>). Selecciona o arrastra cualquier nodo al lienzo.
              Conecta los puertos de salida (derecha) hacia los puertos de entrada compatibles (izquierda). Puedes bifurcar salidas hacia múltiples nodos en paralelo.
            </p>
          </div>

          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
              <Shield size={16} color="var(--accent-green)" />
              <strong style={{ fontSize: '14px' }}>2. Seguridad e Inmutabilidad</strong>
            </div>
            <p style={{ fontSize: '12.5px', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
              FileFlow opera con una política <strong>no destructiva por defecto</strong>. Tus archivos de origen nunca se sobreescriben ni eliminan a menos que añadas expresamente el nodo <code>OriginalFileActionNode</code> al final del pipeline.
            </p>
          </div>

          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
              <Sliders size={16} color="var(--accent-cyan)" />
              <strong style={{ fontSize: '14px' }}>3. Asistentes y Acciones Personalizadas</strong>
            </div>
            <p style={{ fontSize: '12.5px', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
              Los nodos con configuraciones complejas (como el <em>Renombrador Avanzado</em>, <em>Generador Sintético</em>, <em>Asistente Regex</em> o <em>Scripts</em>) cuentan con un botón de engranaje en su tarjeta y en el <strong>Inspector de Parámetros</strong> que abre un estudio visual dedicado con previsualización en vivo.
            </p>
          </div>

          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
              <Zap size={16} color="var(--accent-purple)" />
              <strong style={{ fontSize: '14px' }}>4. Ejecución y Modo Simulación (Dry Run)</strong>
            </div>
            <p style={{ fontSize: '12.5px', color: 'var(--text-secondary)', lineHeight: '1.5' }}>
              Presiona <kbd>F5</kbd> o el botón <strong>Ejecutar</strong> en la barra superior para procesar. Puedes activar <em>Dry Run</em> desde el menú Ejecutar para probar todo el flujo sin tocar archivos reales, inspeccionando los resultados en el <strong>Explorador VFS</strong>.
            </p>
          </div>
        </div>

        <div className="modal-footer">
          <button className="btn-action primary" onClick={onClose}>
            Cerrar Guía
          </button>
        </div>
      </div>
    </div>
  );
};
