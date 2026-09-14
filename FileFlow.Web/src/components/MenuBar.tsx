import React, { useState, useEffect, useRef } from 'react';
import { ChevronRight } from 'lucide-react';

export interface MenuBarProps {
  onNewWorkflow: () => void;
  onOpenWorkflow: () => void;
  onSaveWorkflow: () => void;
  onExportJson: () => void;
  onLoadTemplate: (templateName: string) => void;
  onUndo: () => void;
  onRedo: () => void;
  onCopy: () => void;
  onPaste: () => void;
  onCut: () => void;
  onDuplicate: () => void;
  onSelectAll: () => void;
  onDeleteSelected: () => void;
  onClearCanvas: () => void;
  onToggleLibrary: () => void;
  onToggleInspector: () => void;
  onToggleConsole: () => void;
  onFitView: () => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onToggleFullscreen: () => void;
  onExecuteWorkflow: () => void;
  onDebugWorkflow: () => void;
  onPauseWorkflow: () => void;
  onStopWorkflow: () => void;
  isDryRun: boolean;
  onToggleDryRun: () => void;
  isWatching: boolean;
  onToggleWatching: () => void;
  onOpenSettings: () => void;
  onOpenVfsExplorer: () => void;
  onOpenSyntheticDesigner: () => void;
  onOpenAdvancedRenamer: () => void;
  onOpenRegexHelper: () => void;
  onOpenScriptEditor: () => void;
  onOpenVlmTester: () => void;
  onOpenTelemetry: () => void;
  onOpenShortcuts: () => void;
  onOpenManual: () => void;
  onOpenAbout: () => void;
}

export const MenuBar: React.FC<MenuBarProps> = ({
  onNewWorkflow,
  onOpenWorkflow,
  onSaveWorkflow,
  onExportJson,
  onLoadTemplate,
  onUndo,
  onRedo,
  onCopy,
  onPaste,
  onCut,
  onDuplicate,
  onSelectAll,
  onDeleteSelected,
  onClearCanvas,
  onToggleLibrary,
  onToggleInspector,
  onToggleConsole,
  onFitView,
  onZoomIn,
  onZoomOut,
  onToggleFullscreen,
  onExecuteWorkflow,
  onDebugWorkflow,
  onPauseWorkflow,
  onStopWorkflow,
  isDryRun,
  onToggleDryRun,
  isWatching,
  onToggleWatching,
  onOpenSettings,
  onOpenVfsExplorer,
  onOpenSyntheticDesigner,
  onOpenAdvancedRenamer,
  onOpenRegexHelper,
  onOpenScriptEditor,
  onOpenVlmTester,
  onOpenTelemetry,
  onOpenShortcuts,
  onOpenManual,
  onOpenAbout,
}) => {
  const [activeMenu, setActiveMenu] = useState<string | null>(null);
  const menuBarRef = useRef<HTMLDivElement>(null);

  // Cerrar menús al hacer click fuera
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuBarRef.current && !menuBarRef.current.contains(e.target as Node)) {
        setActiveMenu(null);
      }
    };
    window.addEventListener('mousedown', handleClickOutside);
    return () => window.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const toggleMenu = (menuName: string) => {
    setActiveMenu(prev => (prev === menuName ? null : menuName));
  };

  const handleAction = (callback: () => void) => {
    setActiveMenu(null);
    callback();
  };

  return (
    <nav className="app-menubar" ref={menuBarRef}>
      {/* 1. ARCHIVO */}
      <div className="menubar-item-wrapper">
        <button
          className={`menubar-btn ${activeMenu === 'file' ? 'active' : ''}`}
          onClick={() => toggleMenu('file')}
          onMouseEnter={() => activeMenu && setActiveMenu('file')}
        >
          Archivo
        </button>
        {activeMenu === 'file' && (
          <div className="menubar-dropdown">
            <button className="dropdown-item" onClick={() => handleAction(onNewWorkflow)}>
              <span>Nuevo Flujo</span>
              <span className="shortcut-hint">Ctrl+N</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenWorkflow)}>
              <span>Abrir Flujo...</span>
              <span className="shortcut-hint">Ctrl+O</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onSaveWorkflow)}>
              <span>Guardar Flujo</span>
              <span className="shortcut-hint">Ctrl+S</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onExportJson)}>
              <span>Exportar como JSON</span>
            </button>

            <div className="dropdown-separator" />

            <div className="dropdown-submenu-parent">
              <span style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
                <span>Plantillas de Ejemplo</span>
                <ChevronRight size={14} />
              </span>
              <div className="dropdown-submenu">
                <button className="dropdown-item" onClick={() => handleAction(() => onLoadTemplate('classifier'))}>
                  Clasificador por Extensión
                </button>
                <button className="dropdown-item" onClick={() => handleAction(() => onLoadTemplate('renamer'))}>
                  Renombrado Inteligente con Tokens
                </button>
                <button className="dropdown-item" onClick={() => handleAction(() => onLoadTemplate('unpack'))}>
                  Descompresión y Limpieza
                </button>
                <button className="dropdown-item" onClick={() => handleAction(() => onLoadTemplate('vision'))}>
                  Inferencia con IA Visual (VLM)
                </button>
              </div>
            </div>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(() => window.close())}>
              <span>Salir</span>
              <span className="shortcut-hint">Alt+F4</span>
            </button>
          </div>
        )}
      </div>

      {/* 2. EDITAR */}
      <div className="menubar-item-wrapper">
        <button
          className={`menubar-btn ${activeMenu === 'edit' ? 'active' : ''}`}
          onClick={() => toggleMenu('edit')}
          onMouseEnter={() => activeMenu && setActiveMenu('edit')}
        >
          Editar
        </button>
        {activeMenu === 'edit' && (
          <div className="menubar-dropdown">
            <button className="dropdown-item" onClick={() => handleAction(onUndo)}>
              <span>Deshacer</span>
              <span className="shortcut-hint">Ctrl+Z</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onRedo)}>
              <span>Rehacer</span>
              <span className="shortcut-hint">Ctrl+Y</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onCut)}>
              <span>Cortar</span>
              <span className="shortcut-hint">Ctrl+X</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onCopy)}>
              <span>Copiar</span>
              <span className="shortcut-hint">Ctrl+C</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onPaste)}>
              <span>Pegar</span>
              <span className="shortcut-hint">Ctrl+V</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onDuplicate)}>
              <span>Duplicar Selección</span>
              <span className="shortcut-hint">Ctrl+D</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onSelectAll)}>
              <span>Seleccionar Todo</span>
              <span className="shortcut-hint">Ctrl+A</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onDeleteSelected)}>
              <span>Eliminar Selección</span>
              <span className="shortcut-hint">Supr</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onClearCanvas)}>
              <span>Limpiar Lienzo</span>
            </button>
          </div>
        )}
      </div>

      {/* 3. VER */}
      <div className="menubar-item-wrapper">
        <button
          className={`menubar-btn ${activeMenu === 'view' ? 'active' : ''}`}
          onClick={() => toggleMenu('view')}
          onMouseEnter={() => activeMenu && setActiveMenu('view')}
        >
          Ver
        </button>
        {activeMenu === 'view' && (
          <div className="menubar-dropdown">
            <button className="dropdown-item" onClick={() => handleAction(onToggleLibrary)}>
              <span>Biblioteca de Nodos</span>
              <span className="shortcut-hint">Ctrl+B</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onToggleInspector)}>
              <span>Inspector de Parámetros</span>
              <span className="shortcut-hint">Ctrl+I</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onToggleConsole)}>
              <span>Consola de Logs</span>
              <span className="shortcut-hint">Ctrl+J</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onFitView)}>
              <span>Ajustar Vista a Pantalla</span>
              <span className="shortcut-hint">Ctrl+0</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onZoomIn)}>
              <span>Acercar Zoom</span>
              <span className="shortcut-hint">Ctrl++</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onZoomOut)}>
              <span>Alejar Zoom</span>
              <span className="shortcut-hint">Ctrl+-</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onToggleFullscreen)}>
              <span>Pantalla Completa</span>
              <span className="shortcut-hint">F11</span>
            </button>
          </div>
        )}
      </div>

      {/* 4. EJECUTAR */}
      <div className="menubar-item-wrapper">
        <button
          className={`menubar-btn ${activeMenu === 'run' ? 'active' : ''}`}
          onClick={() => toggleMenu('run')}
          onMouseEnter={() => activeMenu && setActiveMenu('run')}
        >
          Ejecutar
        </button>
        {activeMenu === 'run' && (
          <div className="menubar-dropdown">
            <button className="dropdown-item" onClick={() => handleAction(onExecuteWorkflow)}>
              <span>Ejecutar Flujo DAG</span>
              <span className="shortcut-hint">F5</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onDebugWorkflow)}>
              <span>Depuración Paso a Paso</span>
              <span className="shortcut-hint">F10</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onToggleDryRun)}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input type="checkbox" checked={isDryRun} readOnly />
                <span>Modo Simulación (Dry Run)</span>
              </span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onToggleWatching)}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input type="checkbox" checked={isWatching} readOnly />
                <span>Modo Vigilante Activo</span>
              </span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onPauseWorkflow)}>
              <span>Pausar / Reanudar</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onStopWorkflow)}>
              <span>Detener Ejecución</span>
              <span className="shortcut-hint">Shift+F5</span>
            </button>
          </div>
        )}
      </div>

      {/* 5. HERRAMIENTAS */}
      <div className="menubar-item-wrapper">
        <button
          className={`menubar-btn ${activeMenu === 'tools' ? 'active' : ''}`}
          onClick={() => toggleMenu('tools')}
          onMouseEnter={() => activeMenu && setActiveMenu('tools')}
        >
          Herramientas
        </button>
        {activeMenu === 'tools' && (
          <div className="menubar-dropdown">
            <button className="dropdown-item" onClick={() => handleAction(onOpenSettings)}>
              <span>⚙️ Ajustes Generales del Flujo...</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenVfsExplorer)}>
              <span>🗂️ Explorador de Archivos Virtuales (VFS)...</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenSyntheticDesigner)}>
              <span>📊 Diseñador de Datasets Sintéticos...</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenAdvancedRenamer)}>
              <span>🏷️ Estudio de Renombrado Avanzado...</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenRegexHelper)}>
              <span>🔍 Asistente de Expresiones Regulares...</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenScriptEditor)}>
              <span>💻 Editor de Scripts C# y Python...</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenVlmTester)}>
              <span>👁️ Probador de Modelos de Visión VLM...</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onOpenTelemetry)}>
              <span>📈 Monitor de Telemetría y Rendimiento...</span>
            </button>
          </div>
        )}
      </div>

      {/* 6. AYUDA */}
      <div className="menubar-item-wrapper">
        <button
          className={`menubar-btn ${activeMenu === 'help' ? 'active' : ''}`}
          onClick={() => toggleMenu('help')}
          onMouseEnter={() => activeMenu && setActiveMenu('help')}
        >
          Ayuda
        </button>
        {activeMenu === 'help' && (
          <div className="menubar-dropdown">
            <button className="dropdown-item" onClick={() => handleAction(onOpenShortcuts)}>
              <span>⌨️ Atajos de Teclado</span>
            </button>
            <button className="dropdown-item" onClick={() => handleAction(onOpenManual)}>
              <span>📖 Manual de Usuario y Guía de Nodos</span>
            </button>

            <div className="dropdown-separator" />

            <button className="dropdown-item" onClick={() => handleAction(onOpenAbout)}>
              <span>ℹ️ Acerca de FileFlow Studio</span>
            </button>
          </div>
        )}
      </div>
    </nav>
  );
};
