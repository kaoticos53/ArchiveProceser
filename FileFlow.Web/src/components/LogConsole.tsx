import React, { useRef, useEffect } from 'react';
import { Terminal, X, Trash, ChevronDown, ChevronUp } from 'lucide-react';

export interface LogEntry {
  id: string;
  timestamp: string;
  level: 'info' | 'warn' | 'error' | 'success';
  message: string;
}

interface LogConsoleProps {
  isOpen: boolean;
  onClose: () => void;
  logs: LogEntry[];
  onClearLogs: () => void;
}

export const LogConsole: React.FC<LogConsoleProps> = ({
  isOpen,
  onClose,
  logs,
  onClearLogs,
}) => {
  const bottomRef = useRef<HTMLDivElement>(null);
  const [isMinimized, setIsMinimized] = React.useState(false);

  useEffect(() => {
    if (isOpen && !isMinimized) {
      bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [logs, isOpen, isMinimized]);

  if (!isOpen) return null;

  return (
    <footer className={`log-console-container ${isMinimized ? 'minimized' : ''}`}>
      <div className="console-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Terminal size={14} color="var(--accent-blue)" />
          <span className="console-title">Telemetría y Registro en Tiempo Real</span>
          <span className="console-badge">{logs.length} eventos</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <button 
            className="btn-icon-small" 
            onClick={onClearLogs} 
            title="Limpiar logs"
          >
            <Trash size={13} />
          </button>
          <button 
            className="btn-icon-small" 
            onClick={() => setIsMinimized(prev => !prev)} 
            title={isMinimized ? "Expandir" : "Minimizar"}
          >
            {isMinimized ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
          </button>
          <button 
            className="btn-icon-small" 
            onClick={onClose} 
            title="Cerrar consola"
          >
            <X size={14} />
          </button>
        </div>
      </div>

      {!isMinimized && (
        <div className="console-body">
          {logs.map((log) => (
            <div key={log.id} className={`log-line ${log.level}`}>
              <span className="log-time">[{log.timestamp}]</span>
              <span className={`log-badge ${log.level}`}>{log.level.toUpperCase()}</span>
              <span className="log-message">{log.message}</span>
            </div>
          ))}
          <div ref={bottomRef} />
        </div>
      )}
    </footer>
  );
};
