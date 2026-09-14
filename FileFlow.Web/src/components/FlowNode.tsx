import { memo } from 'react';
import { Handle, Position } from '@xyflow/react';
import type { NodeProps, Node } from '@xyflow/react';
import type { FlowNodeData, PortDataType } from '../types/flow';
import { 
  FolderInput, 
  Archive, 
  Image as ImageIcon, 
  Sparkles, 
  FolderOutput, 
  Filter, 
  Cpu, 
  FileText,
  Clock
} from 'lucide-react';

const getCategoryIcon = (iconName: string) => {
  switch (iconName?.toLowerCase()) {
    case 'folder':
    case 'input':
      return <FolderInput size={15} />;
    case 'archive':
    case 'zip':
      return <Archive size={15} />;
    case 'image':
      return <ImageIcon size={15} />;
    case 'ai':
    case 'vlm':
      return <Sparkles size={15} />;
    case 'filter':
    case 'logic':
      return <Filter size={15} />;
    case 'output':
      return <FolderOutput size={15} />;
    case 'file':
      return <FileText size={15} />;
    default:
      return <Cpu size={15} />;
  }
};

const getPortColor = (type: PortDataType) => {
  switch (type) {
    case 'FileItem':
      return '#38bdf8'; // Blue
    case 'Image':
      return '#f472b6'; // Pink
    case 'Boolean':
      return '#c084fc'; // Purple
    case 'Number':
      return '#fbbf24'; // Amber
    case 'Text':
      return '#34d399'; // Emerald
    default:
      return '#94a3b8'; // Slate
  }
};

export type CustomNodeType = Node<FlowNodeData, 'flowNode'>;

export const FlowNode = memo(({ data, selected }: NodeProps<CustomNodeType>) => {
  const statusClass = data.status || 'idle';

  return (
    <div className={`flow-node-card ${statusClass} ${selected ? 'selected' : ''}`}>
      {/* Cabecera del Nodo */}
      <div className="flow-node-header">
        <div className="flow-node-icon">
          {getCategoryIcon(data.iconName)}
        </div>
        <div className="flow-node-title-group">
          <div className="flow-node-title" title={data.title}>
            {data.title}
          </div>
          <div className="flow-node-category">
            {data.category}
          </div>
        </div>
        <div 
          className={`flow-node-status-dot ${statusClass}`}
          title={`Estado: ${statusClass}`} 
        />
      </div>

      {/* Cuerpo del Nodo: Descripción y Métricas */}
      <div className="flow-node-body">
        {data.description && (
          <div className="flow-node-desc">
            {data.description}
          </div>
        )}

        {(data.durationMs !== undefined || data.processedCount !== undefined) && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '10px', color: 'var(--text-dim)', marginTop: '2px' }}>
            <Clock size={12} />
            <span>{data.durationMs ?? 0} ms</span>
            {data.processedCount !== undefined && (
              <span>• {data.processedCount} procesados</span>
            )}
          </div>
        )}
      </div>

      {/* Sección de Puertos: Cada puerto tiene su propia fila con su Handle correspondiente */}
      <div className="flow-node-ports-section">
        {/* Entradas */}
        {data.inputs.map((inPort) => (
          <div key={`in-row-${inPort.id}`} className="node-port-row input-row">
            <Handle
              type="target"
              position={Position.Left}
              id={inPort.id}
              className="port-handle"
              style={{ backgroundColor: getPortColor(inPort.type) }}
              title={`Entrada: ${inPort.name} (${inPort.type})`}
            />
            <span className="node-port-name">{inPort.name}</span>
            <span className="node-port-tag">{inPort.type}</span>
          </div>
        ))}

        {/* Salidas */}
        {data.outputs.map((outPort) => (
          <div key={`out-row-${outPort.id}`} className="node-port-row output-row">
            <span className="node-port-tag">{outPort.type}</span>
            <span className="node-port-name">{outPort.name}</span>
            <Handle
              type="source"
              position={Position.Right}
              id={outPort.id}
              className="port-handle"
              style={{ backgroundColor: getPortColor(outPort.type) }}
              title={`Salida: ${outPort.name} (${outPort.type})`}
            />
          </div>
        ))}
      </div>
    </div>
  );
});
