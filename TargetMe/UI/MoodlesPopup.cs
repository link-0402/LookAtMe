using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TargetMe.Core;
using TargetMe.Integrations;
using TargetMe.Tracking;

namespace TargetMe.UI;

/// <summary>A small window listing one viewer's Moodles statuses, refreshed while open.</summary>
public sealed partial class MoodlesPopup : Window
{
    private readonly MoodlesIpc moodles;
    private readonly ViewerTracker tracker;
    private ViewerRecord? record;

    public MoodlesPopup(MoodlesIpc moodles, ViewerTracker tracker)
        : base("Moodles##TargetMeMoodles", ImGuiWindowFlags.NoCollapse)
    {
        this.moodles = moodles;
        this.tracker = tracker;
        Size = new Vector2(380, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Show(ViewerRecord viewer)
    {
        record = viewer;
        WindowName = $"Moodles: {viewer.Name}##TargetMeMoodles";
        IsOpen = true;
    }

    public override void Draw()
    {
        if (record == null || tracker.Resolve(record) is not { } pc)
        {
            ImGui.TextDisabled("They're no longer nearby.");
            return;
        }

        IReadOnlyList<MoodleStatus> statuses = moodles.Get(record, pc);
        if (statuses.Count == 0)
        {
            ImGui.TextDisabled("No Moodles.");
            return;
        }

        for (var i = 0; i < statuses.Count; i++)
        {
            if (i > 0)
                ImGui.Separator();
            DrawStatus(statuses[i]);
        }
    }

    private static void DrawStatus(MoodleStatus status)
    {
        var title = StripTags(status.Title);
        ImGui.TextColored(TypeColor(status.Type), title.Length > 0 ? title : "(Untitled Moodle)");
        if (status.Stacks > 1)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"×{status.Stacks}");
        }

        var description = StripTags(status.Description);
        if (description.Length > 0)
            ImGui.TextWrapped(description);

        var applier = StripTags(status.Applier);
        if (applier.Length > 0)
            ImGui.TextDisabled($"Applied by {applier}");
    }

    private static Vector4 TypeColor(MoodleType type) => type switch
    {
        MoodleType.Positive => new Vector4(0.45f, 0.9f, 0.45f, 1f),
        MoodleType.Negative => new Vector4(1f, 0.4f, 0.4f, 1f),
        _ => new Vector4(0.55f, 0.75f, 1f, 1f),
    };

    // Moodles text uses BBCode-like tags such as [color=Red]...[/color].
    private static string StripTags(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : TagRegex().Replace(text, string.Empty).Trim();

    [GeneratedRegex(@"\[/?(?:color|glow|i|b|u|s)(?:=[^\]]+)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();
}
