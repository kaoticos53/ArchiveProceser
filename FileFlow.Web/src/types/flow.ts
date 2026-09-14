export type NodeStatus = 'idle' | 'running' | 'completed' | 'faulted';

export type PortDataType = 'FileItem' | 'Boolean' | 'Text' | 'Number' | 'Image' | 'Any';

export interface FlowPort {
  id: string;
  name: string;
  type: PortDataType;
  direction: 'input' | 'output';
}

export type ParameterEditorType = 
  | 'text' 
  | 'number' 
  | 'slider' 
  | 'dropdown' 
  | 'editabledropdown' 
  | 'toggle' 
  | 'folderpath' 
  | 'filepath' 
  | 'multilinetext' 
  | 'passwordlist' 
  | 'mediapreset' 
  | 'fileversionselector';

export interface NodeActionDescriptor {
  actionId: string;
  title: string;
  icon: string;
  tooltip?: string;
}

export interface NodeParameter {
  key: string;
  displayName: string;
  editorType?: string;
  type: 'string' | 'number' | 'boolean' | 'select' | 'path' | 'multiline';
  value: any;
  defaultValue?: any;
  options?: string[];
  min?: number;
  max?: number;
  step?: number;
  helpText?: string;
  dependsOnKey?: string;
  dependsOnValues?: string[];
}

export interface FlowNodeData extends Record<string, unknown> {
  title: string;
  category: string;
  description: string;
  iconName: string;
  status: NodeStatus;
  nodeType?: string;
  inputs: FlowPort[];
  outputs: FlowPort[];
  parameters: NodeParameter[];
  customActions?: NodeActionDescriptor[];
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

export interface WorkflowSettings {
  maxConcurrency: number;
  storageProvider: 'LocalDisk' | 'VirtualMemory' | 'Hybrid';
  tempDirectory: string;
  onnxProvider: 'CPU' | 'DirectML' | 'CUDA';
  autoCleanupTemp: boolean;
  telemetryPollingIntervalMs: number;
  dryRunMode: boolean;
  watchMode: boolean;
}
