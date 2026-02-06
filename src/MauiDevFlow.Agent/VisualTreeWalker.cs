using Microsoft.Maui;
using Microsoft.Maui.Controls;
using System.Collections.Concurrent;

namespace MauiDevFlow.Agent;

/// <summary>
/// Walks the MAUI visual tree and produces ElementInfo representations.
/// Uses IVisualTreeElement.GetVisualChildren() for tree traversal.
/// Maintains a session-scoped element ID dictionary for stable references.
/// </summary>
public class VisualTreeWalker
{
    private readonly ConcurrentDictionary<string, WeakReference<IVisualTreeElement>> _elementMap = new();
    private readonly ConcurrentDictionary<IVisualTreeElement, string> _reverseMap = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Looks up a previously-mapped element by its ID.
    /// </summary>
    public IVisualTreeElement? GetElementById(string id)
    {
        if (_elementMap.TryGetValue(id, out var weakRef) && weakRef.TryGetTarget(out var element))
            return element;

        _elementMap.TryRemove(id, out _);
        return null;
    }

    /// <summary>
    /// Looks up an element by ID, re-walking the tree if not found.
    /// </summary>
    public IVisualTreeElement? GetElementById(string id, Application? app)
    {
        var el = GetElementById(id);
        if (el != null || app == null) return el;

        // Re-walk tree to refresh the element map
        WalkTree(app);
        return GetElementById(id);
    }

    /// <summary>
    /// Returns diagnostic info about the element map state.
    /// </summary>
    public string GetDiagnostics()
    {
        return $"Map has {_elementMap.Count} entries. Keys: [{string.Join(", ", _elementMap.Keys.Take(20))}]";
    }

    /// <summary>
    /// Walks the visual tree starting from the application's windows.
    /// </summary>
    public List<ElementInfo> WalkTree(Application app, int maxDepth = 0)
    {
        var results = new List<ElementInfo>();
        if (app is not IVisualTreeElement appElement)
            return results;

        foreach (var child in appElement.GetVisualChildren())
        {
            var info = WalkElement(child, null, 1, maxDepth);
            if (info != null)
                results.Add(info);
        }

        return results;
    }

    /// <summary>
    /// Walks from a specific element.
    /// </summary>
    public ElementInfo? WalkElement(IVisualTreeElement element, string? parentId, int currentDepth, int maxDepth)
    {
        var id = GetOrCreateId(element);
        var info = CreateElementInfo(element, id, parentId);

        if (maxDepth > 0 && currentDepth >= maxDepth)
            return info;

        var children = element.GetVisualChildren();
        if (children.Count > 0)
        {
            info.Children = new List<ElementInfo>();
            foreach (var child in children)
            {
                var childInfo = WalkElement(child, id, currentDepth + 1, maxDepth);
                if (childInfo != null)
                    info.Children.Add(childInfo);
            }
        }

        return info;
    }

    /// <summary>
    /// Queries elements matching the given criteria.
    /// </summary>
    public List<ElementInfo> Query(Application app, string? type = null, string? automationId = null, string? text = null)
    {
        var results = new List<ElementInfo>();
        if (app is not IVisualTreeElement appElement)
            return results;

        QueryRecursive(appElement, type, automationId, text, null, results);
        return results;
    }

    private void QueryRecursive(IVisualTreeElement element, string? type, string? automationId, string? text, string? parentId, List<ElementInfo> results)
    {
        var id = GetOrCreateId(element);
        var info = CreateElementInfo(element, id, parentId);
        bool matches = true;

        if (type != null && !info.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
            && !info.FullType.Equals(type, StringComparison.OrdinalIgnoreCase))
            matches = false;

        if (automationId != null && !string.Equals(info.AutomationId, automationId, StringComparison.OrdinalIgnoreCase))
            matches = false;

        if (text != null && (info.Text == null || !info.Text.Contains(text, StringComparison.OrdinalIgnoreCase)))
            matches = false;

        if (matches && (type != null || automationId != null || text != null))
            results.Add(info);

        foreach (var child in element.GetVisualChildren())
            QueryRecursive(child, type, automationId, text, id, results);
    }

    private string GetOrCreateId(IVisualTreeElement element)
    {
        if (_reverseMap.TryGetValue(element, out var existingId))
            return existingId;

        // Prefer AutomationId if available
        string id;
        if (element is VisualElement ve && !string.IsNullOrEmpty(ve.AutomationId))
        {
            id = ve.AutomationId;
            // Ensure uniqueness by appending suffix if needed
            if (_elementMap.ContainsKey(id))
            {
                var suffix = 1;
                while (_elementMap.ContainsKey($"{id}_{suffix}"))
                    suffix++;
                id = $"{id}_{suffix}";
            }
        }
        else
        {
            id = Guid.NewGuid().ToString("N")[..12];
        }

        _elementMap[id] = new WeakReference<IVisualTreeElement>(element);
        _reverseMap[element] = id;
        return id;
    }

    private static ElementInfo CreateElementInfo(IVisualTreeElement element, string id, string? parentId)
    {
        var info = new ElementInfo
        {
            Id = id,
            ParentId = parentId,
            Type = element.GetType().Name,
            FullType = element.GetType().FullName ?? element.GetType().Name,
        };

        if (element is VisualElement ve)
        {
            info.AutomationId = ve.AutomationId;
            info.IsVisible = ve.IsVisible;
            info.IsEnabled = ve.IsEnabled;
            info.IsFocused = ve.IsFocused;
            info.Opacity = ve.Opacity;
            info.Bounds = new BoundsInfo
            {
                X = ve.Frame.X,
                Y = ve.Frame.Y,
                Width = ve.Frame.Width,
                Height = ve.Frame.Height
            };
        }

        // Extract text from common controls
        info.Text = element switch
        {
            Label l => l.Text,
            Button b => b.Text,
            Entry e => e.Text,
            Editor ed => ed.Text,
            SearchBar sb => sb.Text,
            Span s => s.Text,
            _ => (element as VisualElement)?.GetValue(VisualElement.AutomationIdProperty) is string aid
                 ? null : null
        };

        return info;
    }

    /// <summary>
    /// Clears the element ID mappings.
    /// </summary>
    public void Reset()
    {
        _elementMap.Clear();
        _reverseMap.Clear();
    }
}
