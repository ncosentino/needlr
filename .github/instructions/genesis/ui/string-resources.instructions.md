---
applyTo: "**/*.resx"
---

# String Resource Rules (`.resx`)

## Location

String resource files live in a `Resources/` folder within each feature project:

```
Features/Login/
  Resources/
    Strings.resx              ← default culture (English)
    Strings.fr.resx           ← French
    Strings.es.resx           ← Spanish
```

Resources are scoped per feature — not per view and not in a global shared location. When multiple views within a feature need the same string (e.g., "Back", "Next"), it belongs in that feature's `Strings.resx`. When a string is truly shared across multiple features, place it in a shared/shell feature's resources and reference it via a type alias.

## File naming

| File | Purpose |
|------|---------|
| `Strings.resx` | Default culture (invariant / English) — always present |
| `Strings.xx.resx` | Language-only locale (e.g., `Strings.fr.resx`) |
| `Strings.xx-YY.resx` | Language + region locale (e.g., `Strings.pt-BR.resx`) |

Only the default `Strings.resx` is required. Add locale-specific files only when translations are available.

## Resource key naming

Use `ViewName_Purpose` format in `PascalCase`:

```
Login_SignInButton
Login_UsernamePlaceholder
Choose_Title
Choose_NewScanLabel
Shared_BackButton
Shared_NextButton
```

- Prefix with the view or logical area name
- Suffix with the semantic purpose
- Use `Shared_` prefix for strings used across multiple views within the feature

## Views NEVER access resource classes directly

All user-visible strings flow through ViewModel properties. Views bind to the ViewModel; ViewModels reference the generated resource class:

```csharp
// ViewModel
public string TitleText => Strings.Choose_Title;
public string BackButtonText => ShellStrings.Shared_BackButton;
```

```xml
<!-- View -->
<TextBlock Text="{Binding TitleText}" />
```

```xml
<!-- ❌ WRONG — direct access from XAML bypasses the ViewModel -->
<TextBlock Text="{x:Static res:Strings.Choose_Title}" />
```

This rule exists because:
- ViewModels are testable; XAML markup references are not
- String logic (e.g., formatting, conditional text) belongs in the ViewModel
- It maintains a single consistent data flow: Resources → ViewModel → View

## Resource file content

- Set `xml:space="preserve"` on every `<data>` element to preserve whitespace
- Use `<comment>` elements for context that helps translators understand where and how the string is used
- Group related keys with XML comment separators for readability
- Never store non-string resources (images, binary data) in `.resx` — use separate asset files
