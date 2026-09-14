import React from 'react';
import { X } from 'lucide-react';

interface ShortcutsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const SHORTCUT_GROUPS = [
  {
    category: 'Ejecución y Flujo',
    items: [
      { keys: ['F5'], desc: 'Iniciar o continuar ejecución del pipeline DAG' },
      { keys: ['F10'], desc: 'Avanzar un paso (Depuración paso a paso)' },
      { keys: ['Shift', 'F5'], desc: 'Detener de forma inmediata la ejecución actual' },
      { keys: ['Ctrl', 'S'], desc: 'Guardar el flujo actual en formato JSON' },
      { keys: ['Ctrl', 'O'], desc: 'Cargar un flujo desde un archivo' },
      { keys: ['Ctrl', 'N'], desc: 'Crear un nuevo flujo limpio en blanco' },
    ]
  },
  {
    category: 'Edición en el Lienzo',
    items: [
      { keys: ['Ctrl', 'C'], desc: 'Copiar nodos seleccionados' },
      { keys: ['Ctrl', 'V'], desc: 'Pegar nodos en el cursor' },
      { keys: ['Ctrl', 'X'], desc: 'Cortar nodos seleccionados' },
      { keys: ['Ctrl', 'D'], desc: 'Duplicar nodos rápidamente' },
      { keys: ['Supr / Del'], desc: 'Eliminar los nodos o conexiones seleccionados' },
      { keys: ['Ctrl', 'A'], desc: 'Seleccionar todos los nodos del lienzo' },
    ]
  },
  {
    category: 'Vistas y Navegación',
    items: [
      { keys: ['Ctrl', '0'], desc: 'Centrar y encajar todos los nodos en la pantalla (FitView)' },
      { keys: ['Ctrl', '+'], desc: 'Acercar zoom' },
      { keys: ['Ctrl', '-'], desc: 'Alejar zoom' },
      { keys: ['Ctrl', 'B'], desc: 'Alternar panel de biblioteca de nodos' },
      { keys: ['Ctrl', 'I'], desc: 'Alternar inspector de parámetros' },
      { keys: ['Ctrl', 'J'], desc: 'Alternar consola inferior de logs' },
    ]
  }
];

export const ShortcutsModal: React.FC<ShortcutsModalProps> = ({
  isOpen,
  onClose,
}) => {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="modal-dialog">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">⌨️</div>
            <div>
              <h2 className="modal-title">Atajos de Teclado</h2>
              <p className="modal-subtitle">Atajos rápidos para máxima productividad en FileFlow Studio</p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body" style={{ padding: '16px 20px', maxHeight: '500px', overflowY: 'auto' }}>
          {SHORTCUT_GROUPS.map(group => (
            <div key={group.category} style={{ marginBottom: '20px' }}>
              <div style={{ fontSize: '12px', fontWeight: 'bold', color: 'var(--accent-blue)', marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                {group.category}
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                {group.items.map((item, idx) => (
                  <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 10px', background: 'rgba(255,255,255,0.02)', borderRadius: '6px', border: '1px solid var(--border-dark)' }}>
                    <span style={{ fontSize: '12.5px', color: 'var(--text-secondary)' }}>{item.desc}</span>
                    <div style={{ display: 'flex', gap: '4px' }}>
                      {item.keys.map((k, kIdx) => (
                        <kbd key={kIdx} style={{ background: '#1e2536', border: '1px solid #374151', borderRadius: '4px', padding: '2px 7px', fontSize: '11px', fontFamily: 'monospace', color: '#f3f4f6', boxShadow: '0 1px 2px rgba(0,0,0,0.3)' }}>
                          {k}
                        </kbd>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>

        <div className="modal-footer">
          <button className="btn-action primary" onClick={onClose}>
            Entendido
          </button>
        </div>
      </div>
    </div>
  );
};
