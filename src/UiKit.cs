using System.Text.Json;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi;

public sealed class UiKit
{
    public const string LayoutDir = "assets/layouts";
    public const string TextureDir = "assets/textures";
    public const string IndexFile = "assets/index.json";
    public const int FontSizeBoost = 1;
    public const float BoldStrength = 0.6f;

    public PluginContext Context { get; }
    public PluginGame Game => Context.Game;
    public PluginLog Log => Context.Log;
    public int LayoutCount { get; }
    public int TextureCount { get; }

    private readonly Dictionary<string, LayoutNode> _layouts = new(StringComparer.OrdinalIgnoreCase);
    private Font? _regular;
    private Font? _bold;

    public UiKit(PluginContext context)
    {
        Context = context;
        using var index = context.Assets.Json(IndexFile);
        if (index != null)
        {
            var root = index.RootElement;
            if (root.TryGetProperty("layouts", out var layouts)) LayoutCount = layouts.EnumerateObject().Count();
            if (root.TryGetProperty("textures", out var textures)) TextureCount = textures.EnumerateObject().Count();
        }
    }

    public LayoutNode Layout(string name)
    {
        if (_layouts.TryGetValue(name, out var cached)) return cached;
        using var doc = Context.Assets.Json($"{LayoutDir}/{name}.json")
                        ?? throw new FileNotFoundException($"layout {name} is missing from {LayoutDir}");
        var node = LayoutNode.Parse(doc.RootElement);
        _layouts[name] = node;
        return node;
    }

    public Texture2D? Texture(string png) => Context.Assets.Texture($"{TextureDir}/{png}");

    public Font Regular => _regular ??= ThemeDB.FallbackFont;

    public Font Bold => _bold ??= new FontVariation { BaseFont = Regular, VariationEmbolden = BoldStrength };

    public Font FontFor(LayoutNode node) => node.Bold ? Bold : Regular;

    public static int FontSize(LayoutNode node) => Math.Max(8, node.Size + FontSizeBoost);
}
