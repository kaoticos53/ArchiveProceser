# -*- coding: utf-8 -*-
"""Migración Fase 2 (modales): botones de acento inline -> clases de componente.

Transformaciones idempotentes sobre el texto de cada .axaml:
1. <Button ... Background="{DynamicResource AccentPrimaryBrush}" ... Foreground="#FFFFFF" ...>
   -> se retiran ambos atributos y se añade Classes="primary".
2. Igual con AccentSuccessBrush -> Classes="success".
3. Restos de Foreground="#FFFFFF" -> Foreground="{DynamicResource TextOnAccentBrush}".

Sólo toca líneas que contengan <Button o <materialIcons:MaterialIcon (no campos de
edición ni superficies), y sólo si la línea no tiene ya Classes=.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent

FILES = [
    "FileFlow.App/Views/AboutDialogWindow.axaml",
    "FileFlow.Plugin.AI/UI/MultimodalVlmConfigWindow.axaml",
    "FileFlow.Plugin.Archives/UI/Views/PasswordManagerWindow.axaml",
    "FileFlow.Plugin.FileSystem/UI/Views/AdvancedRenamerEditorWindow.axaml",
    "FileFlow.Plugin.FileSystem/UI/Views/RegexHelperWindow.axaml",
    "FileFlow.Plugin.FileSystem/UI/Views/SyntheticDataSetDesignerWindow.axaml",
    "FileFlow.Plugin.Integrations/UI/Views/MediaPresetManagerWindow.axaml",
    "FileFlow.Plugin.Scripting/UI/Views/ScriptStudioWindow.axaml",
]

ACCENT = re.compile(
    r'\s*Background="\{DynamicResource (AccentPrimaryBrush|AccentSuccessBrush)\}"')
WHITE = re.compile(r'\s*Foreground="#FFFFFF"')
BUTTON_OR_ICON = re.compile(r'<(Button|materialIcons:MaterialIcon)\b')
CLASSES = re.compile(r'\bClasses="')

def migrate(text: str) -> str:
    out_lines = []
    for line in text.splitlines(keepends=True):
        newline = line

        if BUTTON_OR_ICON.search(line) and not CLASSES.search(line) and ACCENT.search(line) and WHITE.search(line):
            variant = ACCENT.search(line).group(1)
            newline = ACCENT.sub('', newline)
            newline = WHITE.sub('', newline)
            cls = 'primary' if variant == 'AccentPrimaryBrush' else 'success'
            # Insertar Classes justo tras <Button o <materialIcons:MaterialIcon
            newline = re.sub(r'(<(?:Button|materialIcons:MaterialIcon)\b)',
                             r'\1 Classes="%s"' % cls, newline, count=1)

        if WHITE.search(newline):
            newline = WHITE.sub(' Foreground="{DynamicResource TextOnAccentBrush}"', newline)

        out_lines.append(newline)

    return ''.join(out_lines)

def main() -> None:
    for rel in FILES:
        path = ROOT / rel
        if not path.exists():
            print(f'MISSING: {rel}')
            continue
        original = path.read_text(encoding='utf-8-sig')
        migrated = migrate(original)
        if migrated != original:
            path.write_text(migrated, encoding='utf-8', newline='')
            print(f'MIGRATED: {rel}')
        else:
            print(f'unchanged: {rel}')

if __name__ == '__main__':
    main()
