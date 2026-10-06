# IAP Helper — Agent Notes

UPM package (repo root = package root), installed via git URL.

## UI Toolkit: leading-icon text (do not regress)

Never put an emoji/symbol inline at the start of a `Button.text` (or a lone `Label`). On Windows the
fallback emoji glyph draws wider than Unity measures it, so the following text runs over the icon
(e.g. "Dashboard" on top of the chart glyph). Instead call `IAPHelperUIStyle.ApplyIconText(button, text)`
when creating the button, and `IAPHelperUIStyle.CreateIconLabel(text)` for title labels. This splits the
leading icon into its own `min-width` element so the two never overlap. This is the only supported way
to show an icon before a label.
