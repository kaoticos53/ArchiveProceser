import React, { useState } from 'react';
import { 
  X, 
  Check, 
  Bot, 
  Image as ImageIcon, 
  Play
} from 'lucide-react';
import type { FlowNodeData } from '../../types/flow';

interface VlmTesterModalProps {
  isOpen: boolean;
  onClose: () => void;
  nodeData?: FlowNodeData | null;
  onSave?: (updatedParams: Record<string, any>) => void;
}

export const VlmTesterModal: React.FC<VlmTesterModalProps> = ({
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

  const [provider, setProvider] = useState<string>(getParam('Provider', 'Ollama'));
  const [modelName, setModelName] = useState<string>(getParam('ModelName', 'llava:7b'));
  const [systemPrompt, setSystemPrompt] = useState<string>(getParam('SystemPrompt', 'Eres un clasificador visual de archivos de alta precisión.'));
  const [prompt, setPrompt] = useState<string>(getParam('Prompt', 'Describe el contenido principal de esta imagen e indica etiquetas clave en JSON.'));
  const [temperature, setTemperature] = useState<number>(getParam('Temperature', 0.2));
  const [sampleImage, setSampleImage] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<string>('Esperando imagen y ejecución de prueba...');
  const [isLoading, setIsLoading] = useState<boolean>(false);

  const handleSimulateInference = () => {
    setIsLoading(true);
    setTestResult('Conectando con el motor VLM...\nCodificando imagen en tensores...\nGenerando tokens...');
    setTimeout(() => {
      setIsLoading(false);
      setTestResult(JSON.stringify({
        description: "Fotografía de paisaje montañoso con un lago alpino al atardecer.",
        category: "Naturaleza/Paisajes",
        tags: ["montaña", "lago", "atardecer", "naturaleza", "hdr"],
        recommendedFolder: "Fotos/2026/Naturaleza",
        confidence: 0.96
      }, null, 2));
    }, 1200);
  };

  const handleSave = () => {
    if (onSave) {
      onSave({
        Provider: provider,
        ModelName: modelName,
        SystemPrompt: systemPrompt,
        Prompt: prompt,
        Temperature: temperature
      });
    }
    onClose();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-dialog modal-large">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="modal-icon-badge">👁️</div>
            <div>
              <h2 className="modal-title">Estudio de Inferencia Visual VLM (IA Multimodal)</h2>
              <p className="modal-subtitle">
                Configura y prueba modelos de visión local (Ollama / ONNX) o en la nube para clasificación semántica
              </p>
            </div>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="modal-body-grid">
          {/* Columna Izquierda: Parámetros del Modelo */}
          <div className="modal-steps-column">
            <div className="column-header">
              <span className="column-title">Configuración del Proveedor</span>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '10px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label className="param-label">Proveedor</label>
                  <select
                    className="param-select"
                    value={provider}
                    onChange={(e) => setProvider(e.target.value)}
                  >
                    <option value="Ollama">Ollama Local (llava, moondream)</option>
                    <option value="OpenAI">OpenAI Compatible (GPT-4o)</option>
                    <option value="LocalOnnx">ONNX Runtime Local DirectML</option>
                  </select>
                </div>
                <div>
                  <label className="param-label">Nombre del Modelo</label>
                  <input
                    type="text"
                    className="param-input"
                    value={modelName}
                    onChange={(e) => setModelName(e.target.value)}
                  />
                </div>
              </div>

              <div>
                <label className="param-label">Prompt del Sistema</label>
                <textarea
                  className="param-textarea"
                  rows={2}
                  value={systemPrompt}
                  onChange={(e) => setSystemPrompt(e.target.value)}
                />
              </div>

              <div>
                <label className="param-label">Instrucción / Prompt de Consulta</label>
                <textarea
                  className="param-textarea"
                  rows={3}
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                />
              </div>

              <div>
                <label className="param-label">
                  <span>Temperatura (Creatividad vs Precisión)</span>
                  <span className="param-value-tag">{temperature}</span>
                </label>
                <input
                  type="range"
                  min={0}
                  max={1}
                  step={0.05}
                  value={temperature}
                  onChange={(e) => setTemperature(Number(e.target.value))}
                />
              </div>
            </div>
          </div>

          {/* Columna Derecha: Probador en Vivo */}
          <div className="modal-preview-column">
            <div className="preview-card-box" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
              <div className="card-box-header">
                <ImageIcon size={15} color="var(--accent-purple)" />
                <span>Imagen de Muestra y Test</span>
              </div>

              <div 
                style={{ 
                  marginTop: '10px', 
                  padding: '16px', 
                  border: '1px dashed var(--border-dark)', 
                  borderRadius: '6px', 
                  textAlign: 'center',
                  background: 'rgba(255,255,255,0.02)'
                }}
              >
                <ImageIcon size={32} color="var(--text-dim)" style={{ margin: '0 auto 8px' }} />
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>
                  {sampleImage ? 'Imagen cargada para análisis' : 'Usa una imagen sintética de prueba'}
                </div>
                <button 
                  className="btn-small secondary" 
                  style={{ marginTop: '8px' }}
                  onClick={() => setSampleImage('sample_landscape.jpg')}
                >
                  Cargar Imagen de Demostración
                </button>
              </div>

              <div style={{ marginTop: '12px' }}>
                <button 
                  className="btn-action primary" 
                  disabled={isLoading}
                  onClick={handleSimulateInference}
                >
                  <Play size={14} fill="currentColor" />
                  <span>{isLoading ? 'Analizando...' : 'Ejecutar Inferencia de Prueba'}</span>
                </button>
              </div>

              <div className="card-box-header" style={{ marginTop: '14px' }}>
                <Bot size={15} color="var(--accent-cyan)" />
                <span>Respuesta del Modelo VLM</span>
              </div>

              <pre
                style={{
                  marginTop: '6px',
                  background: '#0a0d13',
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid var(--border-dark)',
                  color: '#34d399',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  whiteSpace: 'pre-wrap',
                  flex: 1,
                  overflowY: 'auto'
                }}
              >
                {testResult}
              </pre>
            </div>
          </div>
        </div>

        <div className="modal-footer">
          <div style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
            💡 Las etiquetas y campos extraídos pueden mapearse a puertos de salida y metadatos.
          </div>
          <div style={{ display: 'flex', gap: '10px' }}>
            <button className="btn-action secondary" onClick={onClose}>
              Cancelar
            </button>
            <button className="btn-action primary" onClick={handleSave}>
              <Check size={16} />
              <span>Aplicar Parámetros</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
