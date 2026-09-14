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

                        var parameters = new List<object>();
                        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        // 1. Descriptores formales de parámetros
                        var descriptors = instance.ParameterDescriptors ?? Array.Empty<NodeParameterDescriptor>();
                        foreach (var d in descriptors.OrderBy(d => d.DisplayOrder))
                        {
                            processedKeys.Add(d.Key);
                            object? val = instance.Parameters.TryGetValue(d.Key, out var cv) ? cv : d.DefaultValue;

                            string typeString = d.EditorType switch
                            {
                                ParameterEditorType.Toggle => "boolean",
                                ParameterEditorType.Number or ParameterEditorType.Slider => "number",
                                ParameterEditorType.Dropdown or ParameterEditorType.EditableDropdown => "select",
                                ParameterEditorType.FolderPath or ParameterEditorType.FilePath => "path",
                                ParameterEditorType.MultiLineText => "multiline",
                                _ => "string"
                            };

                            parameters.Add(new
                            {
                                key = d.Key,
                                displayName = d.Key,
                                editorType = d.EditorType.ToString().ToLowerInvariant(),
                                type = typeString,
                                value = val,
                                defaultValue = d.DefaultValue,
                                options = d.Options,
                                min = d.Min,
                                max = d.Max,
                                step = d.Step,
                                helpText = d.HelpText,
                                dependsOnKey = d.DependsOnKey,
                                dependsOnValues = d.DependsOnValues
                            });
                        }

                        // 2. Parámetros adicionales del diccionario sin descriptor explícito
                        foreach (var (k, v) in instance.Parameters)
                        {
                            if (!processedKeys.Contains(k))
                            {
                                parameters.Add(new
                                {
                                    key = k,
                                    displayName = k,
                                    editorType = v is bool ? "toggle" : (v is int or double or float ? "number" : "text"),
                                    type = v is bool ? "boolean" : (v is int or double or float ? "number" : "string"),
                                    value = v,
                                    defaultValue = v
                                });
                            }
                        }

                        var customActions = (instance.CustomActions ?? Array.Empty<NodeActionDescriptor>()).Select(a => new
                        {
                            actionId = a.ActionId,
                            title = a.Title,
                            icon = a.Icon,
                            tooltip = a.Tooltip
                        }).ToList();

                        catalog.Add(new
                        {
                            id = key,
                            type = type.FullName,
                            title = name,
                            category = category,
                            description = description,
                            inputs = inputs.ToList(),
                            outputs = outputs.ToList(),
                            parameters = parameters,
                            customActions = customActions
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
