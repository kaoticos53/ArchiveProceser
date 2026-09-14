using System.Reflection;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FileFlow.Server;

public static class FileFlowServerRunner
{
    public static WebApplication BuildServer(string[]? args = null, string? listenUrl = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        if (!string.IsNullOrEmpty(listenUrl))
        {
            builder.WebHost.UseUrls(listenUrl);
        }

        // 1. Configurar Servicios
        builder.Services.AddSignalR();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowLocalClient", policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // Registrar e inicializar PluginLoader
        var pluginLoader = new PluginLoader();
        pluginLoader.ScanCurrentAppDomain();

        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "Plugins");
        if (Directory.Exists(pluginsDir))
        {
            pluginLoader.LoadPluginDirectory(pluginsDir);
        }

        builder.Services.AddSingleton(pluginLoader);

        var app = builder.Build();

        app.UseCors("AllowLocalClient");

        // 2. Servir la SPA de React Flow (FileFlow.Web/dist o wwwroot)
        var baseDir = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDir, "wwwroot"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FileFlow.Web", "dist")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FileFlow.Server", "wwwroot")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "FileFlow.Server", "wwwroot")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "FileFlow.Server", "wwwroot")),
            Path.GetFullPath(Path.Combine(baseDir, "dist")),
        };

        string? staticDir = null;
        foreach (var path in candidatePaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html")))
            {
                staticDir = path;
                Console.WriteLine($"[FileFlow.Server] Servidor estatico sirviendo desde: {staticDir}");
                break;
            }
        }

        if (staticDir != null)
        {
            var fileProvider = new PhysicalFileProvider(staticDir);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
        }

        // 3. SignalR Hub
        app.MapHub<WorkflowHub>("/hub/workflow");

        // 4. Endpoints REST
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "healthy",
            engine = "FileFlow.Core .NET 9",
            version = "1.0.0",
            timestamp = DateTime.UtcNow
        }));

        app.MapGet("/api/nodes/catalog", (PluginLoader loader) =>
        {
            var catalog = new List<object>();

            foreach (var (key, type) in loader.DiscoveredNodeTypes)
            {
                try
                {
                    var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
                    var name = defAttr?.Name ?? type.Name;
                    var category = defAttr?.Category ?? "General";
                    var description = defAttr?.Description ?? string.Empty;

                    if (Activator.CreateInstance(type) is IFlowNode instance)
                    {
                        name = !string.IsNullOrEmpty(instance.Name) ? instance.Name : name;
                        category = !string.IsNullOrEmpty(instance.Category) ? instance.Category : category;
                        description = !string.IsNullOrEmpty(instance.Description) ? instance.Description : description;

                        var inputs = instance.Inputs.Select(p => new
                        {
                            id = p.Name.ToLowerInvariant(),
                            name = p.Name,
                            type = p.DataType?.Name ?? "FileItem",
                            direction = "input"
                        });

                        var outputs = instance.Outputs.Select(p => new
                        {
                            id = p.Name.ToLowerInvariant(),
                            name = p.Name,
                            type = p.DataType?.Name ?? "FileItem",
                            direction = "output"
                        });

                        var parameters = instance.Parameters.Select(kv => new
                        {
                            key = kv.Key,
                            displayName = kv.Key,
                            type = kv.Value is bool ? "boolean" : (kv.Value is int or double or float ? "number" : "string"),
                            value = kv.Value
                        });

                        catalog.Add(new
                        {
                            id = key,
                            type = type.FullName,
                            title = name,
                            category = category,
                            description = description,
                            inputs = inputs.ToList(),
                            outputs = outputs.ToList(),
                            parameters = parameters.ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Catalog] Error catalogando nodo {type.FullName}: {ex.Message}");
                }
            }

            return Results.Ok(catalog);
        });

        app.MapGet("/api/nodes/categories", (PluginLoader loader) =>
        {
            var categories = loader.DiscoveredNodeTypes.Values
                .Select(t => t.GetCustomAttribute<NodeDefinitionAttribute>()?.Category ?? "General")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            return Results.Ok(categories);
        });

        app.MapPost("/api/workflows/validate", () => Results.Ok(new { valid = true, errors = Array.Empty<string>() }));

        if (staticDir != null)
        {
            app.MapFallbackToFile("index.html", new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(staticDir)
            });
        }

        return app;
    }
}
