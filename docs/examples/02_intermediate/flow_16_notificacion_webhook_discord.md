# Notificación Webhook HTTP en Tiempo Real

**Nivel**: 🟡 Intermedio  
**Archivo de Flujo Importable**: [flow_16_notificacion_webhook_discord.json](./flow_16_notificacion_webhook_discord.json)

## 🎯 Caso de Uso Real
Alertar a un equipo de trabajo o servicio externo inmediatamente cuando se procesa un archivo clave.

## 📊 Diagrama del Flujo
```mermaid
graph LR
  A[FolderSourceNode] -->|Out| B[WebhookNotificationNode]
  B -->|Success| C[DestinationSinkNode]
```

## 🧩 Nodos Utilizados y Configuración
### `FolderSourceNode` (ID: `node-src`)
- **Parámetros**: 
  - *(Parámetros por defecto)*
### `WebhookNotificationNode` (ID: `node-web`)
- **Parámetros**: 
  - `WebhookUrl`: `https://discord.com/api/webhooks/demo`
  - `HttpMethod`: `POST`
### `DestinationSinkNode` (ID: `node-snk`)
- **Parámetros**: 
  - *(Parámetros por defecto)*

## 🔄 Paso a Paso del Procesamiento
1. `FolderSourceNode` lee archivos.
2. `WebhookNotificationNode` realiza una petición HTTP POST con el payload del archivo.
3. `DestinationSinkNode` finaliza el flujo.

## 📋 Requisitos Previos y Datos de Prueba
URL de Webhook válida o servidor de pruebas.
