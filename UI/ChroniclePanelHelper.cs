using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Shared helper for rendering hyperlinked Chronicle feeds inside UI inspector panels
/// and wiring Galcon Empire's default entity resolvers and panel openers onto ChronicleManager.
/// </summary>
public static class ChroniclePanelHelper
{
    private static readonly Regex UrlTagRegex = new Regex(
        @"\[url=([^\]]+)\](.*?)\[/url\]",
        RegexOptions.Compiled
    );

    public static void EnsureDefaultBindings(ChronicleManager manager = null)
    {
        manager ??= ChronicleManager.Instance;
        if (manager == null)
            return;

        if (!manager.HasLinkHandler("empire"))
        {
            manager.RegisterLinkHandler<Empire>("empire", (empire, ctx) =>
            {
                EmpireInfoBox.OpenForEmpire(empire, ctx);
            });
        }

        if (!manager.HasLinkHandler("star"))
        {
            manager.RegisterLinkHandler<Star>("star", (star, ctx) =>
            {
                StarInfoBox.OpenForStar(star, ctx);
            });
        }

        if (!manager.HasLinkHandler("fleet"))
        {
            manager.RegisterLinkHandler<Fleet>("fleet", (fleet, ctx) =>
            {
                FleetInfoBox.OpenForFleet(fleet, ctx);
            });
        }

        if (!manager.HasLinkHandler("ship"))
        {
            manager.RegisterLinkHandler<Ship>("ship", (ship, ctx) =>
            {
                ShipInfoBox.OpenForShip(ship, ctx);
            });
        }

        if (!manager.HasDefaultTombstoneHandler)
        {
            manager.RegisterDefaultTombstoneHandler((tombstone, ctx) =>
            {
                TombstoneInfoBox.OpenForTombstone(tombstone, ctx);
            });
        }

        manager.RegisterEntityResolver("empire", id =>
        {
            if (!EmpireManager.HasInstance || EmpireManager.Instance.SpawnedEmpires == null)
                return null;
            foreach (var empire in EmpireManager.Instance.SpawnedEmpires)
            {
                if (empire != null && GodotObject.IsInstanceValid(empire) &&
                    string.Equals(empire.ChronicleId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return empire;
                }
            }
            return null;
        });

        manager.RegisterEntityResolver("star", id =>
        {
            if (!StarManager.HasInstance)
                return null;
            foreach (var star in StarManager.Instance.GetSpawnedStars())
            {
                if (star != null && GodotObject.IsInstanceValid(star) &&
                    string.Equals(star.ChronicleId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return star;
                }
            }
            return null;
        });

        manager.RegisterEntityResolver("fleet", id =>
        {
            if (!FleetManager.HasInstance)
                return null;
            foreach (var fleet in FleetManager.Instance.ActiveFleets)
            {
                if (fleet != null && GodotObject.IsInstanceValid(fleet) &&
                    string.Equals(fleet.ChronicleId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return fleet;
                }
            }
            return null;
        });

        manager.RegisterEntityResolver("ship", id =>
        {
            if (!FleetManager.HasInstance)
                return null;
            foreach (var fleet in FleetManager.Instance.ActiveFleets)
            {
                if (fleet == null || !GodotObject.IsInstanceValid(fleet))
                    continue;
                foreach (var ship in fleet.Ships)
                {
                    if (ship != null &&
                        string.Equals(ship.ChronicleId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return ship;
                    }
                }
            }
            return null;
        });
    }

    public static bool IsPointerOverChronicle(Control chronicleList)
    {
        if (chronicleList == null || !GodotObject.IsInstanceValid(chronicleList))
            return false;

        Control target = chronicleList.GetParent() as Control ?? chronicleList;
        return target.GetGlobalRect().HasPoint(target.GetGlobalMousePosition());
    }

    public static void ConfigureFilterButton(
        Button filterButton,
        ChronicleViewMode viewMode,
        Action onPressed
    )
    {
        if (filterButton == null)
            return;

        var normalStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_normal.tres");
        var hoverStyle = GD.Load<StyleBox>("res://UI/Styles/scifi_button_hover.tres");
        if (normalStyle != null)
        {
            filterButton.AddThemeStyleboxOverride("normal", normalStyle);
            filterButton.AddThemeStyleboxOverride("focus", normalStyle);
        }
        if (hoverStyle != null)
            filterButton.AddThemeStyleboxOverride("hover", hoverStyle);

        filterButton.Text = ChronicleFilter.GetViewModeLabel(viewMode);
        filterButton.Pressed += onPressed;
    }

    public static void PopulateChronicle(
        VBoxContainer chronicleList,
        IChronicleEntity entity,
        ChronicleViewMode viewMode,
        string emptyMessage,
        Node contextNode,
        ref int lastCount,
        ref long lastLatestId
    )
    {
        if (chronicleList == null || entity == null)
            return;

        EnsureDefaultBindings();

        var filter = new ChronicleFilter { EntityId = entity.ChronicleId, Limit = 25 };
        filter.ApplyViewMode(viewMode);

        List<ChronicleRecord> events = ChronicleManager.Instance != null
            ? ChronicleManager.Instance.QueryEvents(filter)
            : new List<ChronicleRecord>();

        long latestId = events.Count > 0 ? events[0].Id : -1L;
        if (events.Count == lastCount && latestId == lastLatestId && lastCount >= 0)
            return;

        lastCount = events.Count;
        lastLatestId = latestId;

        foreach (Node child in chronicleList.GetChildren())
        {
            chronicleList.RemoveChild(child);
            child.QueueFree();
        }

        if (events.Count == 0)
        {
            var emptyLabel = new Label { Text = emptyMessage };
            emptyLabel.AddThemeFontSizeOverride("font_size", 11);
            emptyLabel.AddThemeColorOverride("font_color", new Color("556b82"));
            chronicleList.AddChild(emptyLabel);
            return;
        }

        foreach (var ev in events)
        {
            string body = !string.IsNullOrEmpty(ev.FormattedBbcode) ? ev.FormattedBbcode : ev.RawText;
            string styledBody = StyleEntityLinks(body);
            string safeTitle = (ev.Title ?? string.Empty).Replace("[", "[lb]");

            var richLabel = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                SelectionEnabled = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(10, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Pass,
                Text = $"[{TickManager.FormatShortDate(ev.Tick)}] [b]{safeTitle}:[/b] {styledBody}",
            };
            richLabel.AddThemeFontSizeOverride("normal_font_size", 11);
            richLabel.AddThemeFontSizeOverride("bold_font_size", 11);
            richLabel.AddThemeColorOverride("default_color", new Color("8fa3bf"));

            richLabel.MetaClicked += meta =>
            {
                EnsureDefaultBindings();
                ChronicleManager.Instance?.TryHandleLinkClick(meta.AsString(), contextNode);
            };

            chronicleList.AddChild(richLabel);
        }
    }

    private static string StyleEntityLinks(string bbcode)
    {
        if (string.IsNullOrEmpty(bbcode))
            return string.Empty;

        return UrlTagRegex.Replace(bbcode, match =>
        {
            string url = match.Groups[1].Value;
            string label = match.Groups[2].Value;
            bool isLive = ChronicleManager.Instance?.ResolveEntity(url) != null;
            bool hasTombstone = !isLive && ChronicleManager.Instance?.GetTombstone(url) != null;
            string colorHex = isLive ? "#00e5ff" : (hasTombstone ? "#ffb74d" : "#6c8096");
            return $"[color={colorHex}][url={url}]{label}[/url][/color]";
        });
    }
}
