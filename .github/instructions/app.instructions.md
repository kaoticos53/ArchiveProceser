---
applyTo: "FileFlow.App/**/*.cs"
---
# Instrucciones para FileFlow.App (UI WPF)

- Patrón **MVVM estricto** usando `CommunityToolkit.Mvvm` (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`).
- Prohibido code-behind con lógica de negocio; solo interacción UI mínima permitida.
- Usar `ValueConverters` existentes (LogLevel, Badges, EnumToBool) antes de crear nuevos.
- Las vistas deben consumir `FileFlow.Core` y `FileFlow.Sdk` únicamente a través de los ViewModels.
