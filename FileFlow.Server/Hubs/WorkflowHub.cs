using Microsoft.AspNetCore.SignalR;

namespace FileFlow.Server.Hubs;

public class WorkflowHub : Hub
{
    public async Task SendNodeStatus(string nodeId, string status, long durationMs, int processedCount)
    {
        await Clients.All.SendAsync("NodeStatusUpdated", new { nodeId, status, durationMs, processedCount });
    }

    public async Task SendLog(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        await Clients.All.SendAsync("LogReceived", new { timestamp, level, message });
    }

    public async Task SendWorkflowProgress(int processedFiles, int totalFiles, double elapsedMs)
    {
        await Clients.All.SendAsync("WorkflowProgressUpdated", new { processedFiles, totalFiles, elapsedMs });
    }
}
