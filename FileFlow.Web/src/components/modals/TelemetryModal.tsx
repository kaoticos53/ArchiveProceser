import React from 'react';
import { X, Activity, Clock } from 'lucide-react';
import type { WorkflowTelemetry } from '../../types/flow';

interface TelemetryModalProps {
  isOpen: boolean;
  onClose: () => void;
  telemetry: WorkflowTelemetry;
}

export const TelemetryModal: React.FC<TelemetryModalProps> = ({
  isOpen,
  onClose,
  telemetry,
}) => {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="modal-dialog">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">📊</div>
            <div>
              <h2 className="modal-title">Telemetría y Rendimiento del Flujo</h2>
              <p className="modal-subtitle">Monitoreo de throughput, tiempos de ejecución y uso de recursos</p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body" style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <div className="settings-card">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--accent-blue)', marginBottom: '4px' }}>
                <Activity size={16} />
                <span style={{ fontSize: '12px', fontWeight: 600 }}>Tasa de Procesamiento</span>
              </div>
              <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                {telemetry.throughput.toFixed(1)} <span style={{ fontSize: '13px', color: 'var(--text-dim)' }}>archivos/seg</span>
              </div>
            </div>

            <div className="settings-card">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--accent-green)', marginBottom: '4px' }}>
                <Clock size={16} />
                <span style={{ fontSize: '12px', fontWeight: 600 }}>Tiempo Transcurrido</span>
              </div>
              <div style={{ fontSize: '24px', fontWeight: 'bold' }}>
                {(telemetry.elapsedMs / 1000).toFixed(2)} <span style={{ fontSize: '13px', color: 'var(--text-dim)' }}>segundos</span>
              </div>
            </div>
          </div>

          <div className="settings-card">
            <div style={{ fontSize: '12px', fontWeight: 600, marginBottom: '8px', color: 'var(--accent-purple)' }}>
              Progreso de Lote de Archivos
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '6px' }}>
              <span>Archivos Completados:</span>
              <strong>{telemetry.processedFiles} / {telemetry.totalFiles || 0}</strong>
            </div>
            <div style={{ background: '#1f2937', height: '8px', borderRadius: '4px', overflow: 'hidden' }}>
              <div 
                style={{ 
                  background: 'linear-gradient(90deg, var(--accent-blue), var(--accent-cyan))', 
                  height: '100%', 
                  width: `${telemetry.totalFiles > 0 ? (telemetry.processedFiles / telemetry.totalFiles) * 100 : 0}%`,
                  transition: 'width 0.3s ease'
                }} 
              />
            </div>
          </div>

          <div className="settings-card">
            <div style={{ fontSize: '12px', fontWeight: 600, marginBottom: '8px' }}>
              Estado del Motor DAG .NET 9
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-secondary)' }}>
              <span>Estado del Pipeline:</span>
              <span style={{ textTransform: 'uppercase', fontWeight: 'bold', color: telemetry.status === 'running' ? '#10b981' : '#9ca3af' }}>
                {telemetry.status}
              </span>
            </div>
          </div>
        </div>

        <div className="modal-footer">
          <button className="btn-action primary" onClick={onClose}>
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
};
