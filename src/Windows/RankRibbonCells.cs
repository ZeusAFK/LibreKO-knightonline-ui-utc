using System.Collections.Generic;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

internal sealed class RankRibbonCells(LayoutView view)
{
    private readonly Dictionary<Label, TextureRect> ribbons = new();

    public void Show(string id, GameRankRibbon? ribbon)
    {
        if (view.Get<Label>(id) is not { } label) return;
        label.Visible = ribbon != null;
        label.Text = ribbon?.Count ?? "";
        label.TooltipText = ribbon?.Tooltip ?? "";
        label.MouseFilter = ribbon != null && ribbon.Value.Tooltip.Length > 0 ? Control.MouseFilterEnum.Pass : Control.MouseFilterEnum.Ignore;
        if (!ribbons.TryGetValue(label, out var image))
        {
            image = new TextureRect
            {
                Position = label.Position,
                Size = label.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddSibling(image);
            label.GetParent().MoveChild(image, label.GetIndex());
            ribbons[label] = image;
        }
        image.Texture = ribbon?.Ribbon;
        image.Visible = ribbon != null;
    }
}
