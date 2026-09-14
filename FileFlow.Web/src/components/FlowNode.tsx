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
      {/* Handles de Entrada */}
      {data.inputs.map((input, idx) => (
        <Handle
          key={`in-${input.id}`}
          type="target"
          position={Position.Left}
          id={input.id}
          style={{
            top: `${((idx + 1) / (data.inputs.length + 1)) * 100}%`,
            backgroundColor: getPortColor(input.type),
          }}
          title={`${input.name} (${input.type})`}
        />
      ))}

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

      {/* Cuerpo del Nodo */}
      <div className="flow-node-body">
        {data.description && (
          <div className="flow-node-desc">
            {data.description}
          </div>
        )}

        {/* Métricas en vivo si está ejecutando o completado */}
        {(data.durationMs !== undefined || data.processedCount !== undefined) && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '10px', color: 'var(--text-dim)', marginTop: '4px' }}>
            <Clock size={12} />
            <span>{data.durationMs ?? 0} ms</span>
            {data.processedCount !== undefined && (
              <span>• {data.processedCount} procesados</span>
            )}
          </div>
        )}

        {/* Resumen de Puertos */}
        <div className="flow-node-ports">
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            {data.inputs.map((inPort) => (
              <div key={inPort.id} className="flow-port-label">
                <span style={{ width: 6, height: 6, borderRadius: '50%', backgroundColor: getPortColor(inPort.type) }} />
                <span>{inPort.name}</span>
              </div>
            ))}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', alignItems: 'flex-end' }}>
            {data.outputs.map((outPort) => (
              <div key={outPort.id} className="flow-port-label">
                <span>{outPort.name}</span>
                <span style={{ width: 6, height: 6, borderRadius: '50%', backgroundColor: getPortColor(outPort.type) }} />
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Handles de Salida */}
      {data.outputs.map((output, idx) => (
        <Handle
          key={`out-${output.id}`}
          type="source"
          position={Position.Right}
          id={output.id}
          style={{
            top: `${((idx + 1) / (data.outputs.length + 1)) * 100}%`,
            backgroundColor: getPortColor(output.type),
          }}
          title={`${output.name} (${output.type})`}
        />
      ))}
    </div>
  );
});
