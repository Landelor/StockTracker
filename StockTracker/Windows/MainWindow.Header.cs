using System.Linq;
using System.Numerics;
using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private const float HeaderHeight = 52f;
    private const float HeaderPadX = 16f;
    private const float HeaderButtonSize = 30f;
    private const float HeaderButtonGap = 6f;

    private void DrawHeaderBar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = HeaderHeight * scale;
        var padX = HeaderPadX * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        AdtPaint.Fill(draw, pos, pos + new Vector2(avail, height), AdtStyling.WithAlpha(AdtStyling.Surface1, 0.55f),
            AdtStyling.WindowRounding * scale, ImDrawFlags.RoundCornersTop);
        AdtPaint.Hairline(draw, new Vector2(pos.X, pos.Y + height - 0.5f), new Vector2(pos.X + avail, pos.Y + height - 0.5f));

        var midY = pos.Y + height * 0.5f;
        var buttonsWidth = HeaderButtonSize * 3f * scale + HeaderButtonGap * 2f * scale;
        var buttonsLeft = pos.X + avail - padX - buttonsWidth;
        var x = pos.X + padX;

        const string title = "Stock Tracker";
        using (AdtFonts.PushHeadline())
        {
            var titleSize = AdtTextDraw.Measure(title);
            if (x + titleSize.X <= buttonsLeft)
            {
                AdtTextDraw.At(title, new Vector2(x, midY - titleSize.Y * 0.5f), AdtStyling.TextStrong);
                x += titleSize.X + 14f * scale;
            }
        }

        var (statusWord, statusColor, statusSoft, summary) = BuildHeaderSummary();
        var pillEnd = DrawStatusPill(draw, statusWord, statusColor, statusSoft, x, buttonsLeft, midY);
        if (pillEnd > x) x = pillEnd + 14f * scale;

        using (AdtFonts.PushCaption())
        {
            var summarySize = AdtTextDraw.Measure(summary);
            AdtTextDraw.At(AdtTextDraw.Truncate(summary, buttonsLeft - 14f * scale - x), new Vector2(x, midY - summarySize.Y * 0.5f), AdtStyling.TextDim);
        }

        DrawHeaderButtons(pos + new Vector2(avail, 0f), midY);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + height + 8f * scale));
    }

    private void DrawHeaderButtons(Vector2 topRight, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = HeaderPadX * scale;
        var buttonSize = HeaderButtonSize * scale;
        var stride = buttonSize + HeaderButtonGap * scale;
        var top = midY - buttonSize * 0.5f;

        ImGui.SetCursorScreenPos(new Vector2(topRight.X - padX - buttonSize, top));
        if (AdtIconButton.Draw(FontAwesomeIcon.Times, "##st_header_close", buttonSize, tooltip: "Close"))
            IsOpen = false;

        ImGui.SetCursorScreenPos(new Vector2(topRight.X - padX - buttonSize - stride, top));
        if (AdtIconButton.Draw(headerCollapsed ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp,
                "##st_header_collapse", buttonSize, tooltip: headerCollapsed ? "Restore" : "Minimize"))
            headerCollapsed = !headerCollapsed;

        ImGui.SetCursorScreenPos(new Vector2(topRight.X - padX - buttonSize - stride * 2f, top));
        if (AdtIconButton.Draw(resizeLocked ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen,
                "##st_header_resize_lock", buttonSize, tooltip: resizeLocked ? "Unlock resizing" : "Lock resizing"))
        {
            resizeLocked = !resizeLocked;
            if (resizeLocked)
                manualSize ??= ImGui.GetWindowSize() / ImGuiHelpers.GlobalScale;

            plugin.Config.WindowResizeLocked = resizeLocked;
            plugin.Config.WindowLockedWidth = manualSize?.X;
            plugin.Config.WindowLockedHeight = manualSize?.Y;
            plugin.SaveConfig();
        }
    }

    private static float DrawStatusPill(ImDrawListPtr dl, string label, Vector4 accent, Vector4 accentSoft, float x, float rightLimit, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = 10f * scale;
        var dotRadius = 3.5f * scale;

        using (AdtFonts.PushCaption())
        {
            var labelSize = AdtTextDraw.Measure(label);
            var pillHeight = labelSize.Y + 8f * scale;
            var pillMin = new Vector2(x, midY - pillHeight * 0.5f);
            var pillMax = pillMin + new Vector2(padX * 2f + dotRadius * 2f + 6f * scale + labelSize.X, pillHeight);
            if (pillMax.X > rightLimit) return x;

            AdtPaint.Pill(dl, pillMin, pillMax, AdtStyling.WithAlpha(accent, 0.16f), AdtStyling.WithAlpha(accent, 0.45f));
            dl.AddCircleFilled(new Vector2(pillMin.X + padX + dotRadius, midY), dotRadius, AdtPaint.Col(accent));
            AdtTextDraw.At(label, new Vector2(pillMin.X + padX + dotRadius * 2f + 6f * scale, midY - labelSize.Y * 0.5f), accentSoft);
            return pillMax.X;
        }
    }

    /// <summary>
    /// Status word/colors/summary driven entirely by real plugin state: Allagan Tools dependency
    /// readiness first (nothing else can be trusted without it), then how many tracked items
    /// currently need recrafting.
    /// </summary>
    private (string Word, Vector4 Accent, Vector4 AccentSoft, string Summary) BuildHeaderSummary()
    {
        if (!plugin.AllaganTools.IsAvailable)
            return ("SETUP NEEDED", AdtStyling.AccentRose, AdtStyling.Lighten(AdtStyling.AccentRose, 0.2f), "Allagan Tools is required but not loaded");

        var needsRecraft = plugin.NeedsRecraft.Count();
        if (needsRecraft > 0)
            return ("ACTION NEEDED", AdtStyling.AccentAmber, AdtStyling.Lighten(AdtStyling.AccentAmber, 0.2f), $"{needsRecraft:N0} item(s) need recrafting");

        var tracked = plugin.Config.Lists.Sum(list => list.Items.Count);
        if (tracked == 0)
            return ("IDLE", AdtStyling.AccentSlate, AdtStyling.Lighten(AdtStyling.AccentSlate, 0.2f), "No items tracked yet");

        return ("STOCKED", AdtStyling.AccentMint, AdtStyling.Lighten(AdtStyling.AccentMint, 0.2f), $"All {tracked:N0} tracked item(s) fully stocked");
    }
}
