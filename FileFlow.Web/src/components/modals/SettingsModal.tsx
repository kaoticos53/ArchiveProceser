import React, { useState } from 'react';
import { 
  X, 
  Check, 
  HardDrive, 
  Cpu, 
  Zap
} from 'lucide-react';
import type { WorkflowSettings } from '../../types/flow';

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  settings: WorkflowSettings;
  onSave: (newSettings: WorkflowSettings) => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  isOpen,
  onClose,
  settings,
  onSave,
}) => {
  if (!isOpen) return null;

  const [form, setForm] = useState<WorkflowSettings>({ ...settings });

  const handleSave = () => {
    onSave(form);
    onClose();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-dialog">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">⚙️</div>
            <div>
              <h2 className="modal-title">Ajustes Generales de FileFlow Studio</h2>
              <p className="modal-subtitle">
                Configuración del motor de ejecución .NET 9, aceleración por hardware y persistencia
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '16px', padding: '20px' }}>
          {/* Concurrencia */}
          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <Zap size={16} color="var(--accent-blue)" />
              <span style={{ fontWeight: 600, fontSize: '13px' }}>Concurrencia Global del DAG</span>
            </div>
            <label className="param-label">
              <span>Hilos de Procesamiento Simultáneos</span>
              <span className="param-value-tag">{form.maxConcurrency} núcleos</span>
            </label>
            <input
              type="range"
              min={1}
              max={32}
              value={form.maxConcurrency}
              onChange={(e) => setForm(f => ({ ...f, maxConcurrency: Number(e.target.value) }))}
            />
            <div style={{ fontSize: '11px', color: 'var(--text-dim)', marginTop: '4px' }}>
              Controla el número de archivos que el motor procesa en paralelo usando canales `System.Threading.Channels`.
            </div>
          </div>

          {/* Almacenamiento */}
          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <HardDrive size={16} color="var(--accent-cyan)" />
              <span style={{ fontWeight: 600, fontSize: '13px' }}>Almacenamiento y Archivos Temporales</span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
              <div>
                <label className="param-label">Proveedor de Almacenamiento</label>
                <select
                  className="param-select"
                  value={form.storageProvider}
                  onChange={(e) => setForm(f => ({ ...f, storageProvider: e.target.value as any }))}
                >
                  <option value="LocalDisk">Disco Local (Físico)</option>
                  <option value="VirtualMemory">Memoria Virtual (100% en RAM)</option>
                  <option value="Hybrid">Híbrido (Caché RAM + Desborde)</option>
                </select>
              </div>
              <div>
                <label className="param-label">Limpieza Automática</label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '8px', fontSize: '12.5px' }}>
                  <input
                    type="checkbox"
                    checked={form.autoCleanupTemp}
                    onChange={(e) => setForm(f => ({ ...f, autoCleanupTemp: e.target.checked }))}
                  />
                  Eliminar temporales al finalizar
                </label>
              </div>
            </div>
          </div>

          {/* Aceleración por Hardware / IA */}
          <div className="settings-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <Cpu size={16} color="var(--accent-purple)" />
              <span style={{ fontWeight: 600, fontSize: '13px' }}>Acelerador ONNX / Inferencia</span>
            </div>
            <div>
              <label className="param-label">Proveedor de Ejecución de IA</label>
              <select
                className="param-select"
                value={form.onnxProvider}
                onChange={(e) => setForm(f => ({ ...f, onnxProvider: e.target.value as any }))}
              >
                <option value="DirectML">DirectML (Aceleración GPU AMD / Intel / NVIDIA en Windows)</option>
                <option value="CUDA">NVIDIA CUDA (Requiere drivers CUDA instalados)</option>
                <option value="CPU">CPU estándar (Universal, sin aceleración)</option>
              </select>
            </div>
          </div>
        </div>

        <div className="modal-footer">
          <button className="btn-action secondary" onClick={onClose}>
            Cancelar
          </button>
          <button className="btn-action primary" onClick={handleSave}>
            <Check size={16} />
            <span>Guardar Ajustes</span>
          </button>
        </div>
      </div>
    </div>
  );
};
