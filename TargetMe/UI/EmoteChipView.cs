using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using TargetMe.Core;

namespace TargetMe.UI;

/// <summary>Draws a viewer's recent emotes as small fading icons, from the current cursor position.</summary>
public sealed class EmoteChipView
{
    private const float Gap = 3f;

    private readonly Configuration config;
    private readonly ExcelSheet<Emote> emotes = Services.DataManager.GetExcelSheet<Emote>();

    public EmoteChipView(Configuration config) => this.config = config;

    public void Draw(ViewerRecord record, float maxX)
    {
        if (!config.ShowEmotes)
            return;

        var now = DateTime.Now;
        var chips = record.Emotes.Live(now, config.EmoteLifetimeSeconds);
        if (chips.Count == 0)
            return;

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = ImGui.GetFrameHeight();
        var size = rowHeight - 4f;
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, 2f);

        // Newest first, so the latest emote sits right next to the name.
        for (var i = chips.Count - 1; i >= 0; i--)
        {
            var chip = chips[i];
            var alpha = EmoteChips.Alpha(chip, now, config.EmoteLifetimeSeconds);
            var row = emotes.GetRowOrDefault(chip.EmoteId);
            var name = row?.Name.ExtractText() is { Length: > 0 } n ? n : $"Emote {chip.EmoteId}";
            var countText = chip.Count > 1 ? $"×{chip.Count}" : null;
            var countWidth = countText == null ? 0f : ImGui.CalcTextSize(countText).X + 1f;

            if (pos.X + size + countWidth > maxX)
                break;

            ImGui.SetCursorScreenPos(pos);
            if (row is { Icon: > 0 } r)
            {
                var texture = Services.TextureProvider.GetFromGameIcon(new GameIconLookup(r.Icon)).GetWrapOrEmpty();
                ImGui.Image(texture.Handle, new Vector2(size), Vector2.Zero, Vector2.One, new Vector4(1, 1, 1, alpha));
            }
            else
            {
                ImGui.Dummy(new Vector2(size));
                drawList.AddRectFilled(pos, pos + new Vector2(size), ImGui.GetColorU32(new Vector4(1, 1, 1, 0.25f * alpha)), 3f);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{name} · {RelativeTime.Format(now - chip.LastAt)}");

            pos.X += size;
            if (countText != null)
            {
                var textY = pos.Y + (size - ImGui.GetTextLineHeight()) / 2;
                drawList.AddText(new Vector2(pos.X + 1f, textY), ImGui.GetColorU32(new Vector4(1, 1, 1, 0.85f * alpha)), countText);
                pos.X += countWidth;
            }

            pos.X += Gap;
        }
    }
}
