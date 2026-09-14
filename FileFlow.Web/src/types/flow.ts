export type NodeStatus = 'idle' | 'running' | 'completed' | 'faulted';

export type PortDataType = 'FileItem' | 'Boolean' | 'Text' | 'Number' | 'Image' | 'Any';

export interface FlowPort {
  id: string;
  name: string;
  type: PortDataType;
  direction: 'input' | 'output';
}

export interface NodeParameter {
  key: string;
  displayName: string;
  type: 'string' | 'number' | 'boolean' | 'select' | 'path';
  value: any;
  options?: string[];
  min?: number;
  max?: number;
  description?: string;
}

export interface FlowNodeData extends Record<string, unknown> {
  title: string;
  category: string;
  description: string;
  iconName: string;
  status: NodeStatus;
  inputs: FlowPort[];
  outputs: FlowPort[];
  parameters: NodeParameter[];
  durationMs?: number;
  processedCount?: number;
}

export interface WorkflowTelemetry {
  status: 'idle' | 'running' | 'completed' | 'paused' | 'error';
  processedFiles: number;
  totalFiles: number;
  elapsedMs: number;
  throughput: number; // files/sec
}
