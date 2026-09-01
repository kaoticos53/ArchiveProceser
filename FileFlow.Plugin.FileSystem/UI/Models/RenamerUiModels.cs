namespace FileFlow.Plugin.FileSystem.UI.Models;

public sealed record PreviewRowItem(string OriginalName, string ResultName, bool IsModified, string StatusMessage);

public sealed record TagPickerItem(string Category, string Tag, string Description);
