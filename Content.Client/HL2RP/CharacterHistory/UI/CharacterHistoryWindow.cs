using Content.Client.Lobby.UI.ProfileEditorControls;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using System.Numerics;

namespace Content.Client.HL2RP.CharacterHistory.UI;

public sealed class CharacterHistoryWindow : DefaultWindow
{
    private readonly BoxContainer _entries;
    private readonly HumanoidCharacterProfile _fallbackPreviewProfile = HumanoidCharacterProfile.Random();

    public CharacterHistoryWindow()
    {
        Title = Loc.GetString("character-history-window-title");
        SetSize = new Vector2(640, 520);

        var root = new PanelContainer();
        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8
        };
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true
        };
        scroll.AddChild(_entries);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void SetEntries(IReadOnlyList<CharacterHistoryEntry> entries, HumanoidCharacterProfile? previewTemplate = null)
    {
        _entries.RemoveAllChildren();
        if (entries.Count == 0)
        {
            _entries.AddChild(new Label { Text = Loc.GetString("character-history-window-empty") });
            return;
        }

        foreach (var entry in entries)
        {
            var card = new PanelContainer();
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8
            };

            var name = string.IsNullOrWhiteSpace(entry.Surname)
                ? entry.Name
                : $"{entry.Name} {entry.Surname}";
            var profile = (previewTemplate ?? _fallbackPreviewProfile).WithName(name);
            var preview = new ProfilePreviewSpriteView
            {
                Scale = new Vector2(2, 2),
                SetSize = new Vector2(96, 96)
            };
            preview.LoadPreview(profile, inventoryPreview: entry.Preview);

            var text = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 2,
                VerticalExpand = true,
                HorizontalExpand = true
            };
            text.AddChild(new Label { Text = $"{Loc.GetString("character-history-window-round")}: {entry.RoundId}" });
            text.AddChild(new Label { Text = $"{Loc.GetString("character-history-window-date")}: {entry.RoundEndedAt:u}" });
            text.AddChild(new Label { Text = $"{Loc.GetString("character-history-window-name")}: {entry.Name}" });
            text.AddChild(new Label { Text = $"{Loc.GetString("character-history-window-surname")}: {entry.Surname}" });

            row.AddChild(preview);
            row.AddChild(text);
            card.AddChild(row);
            _entries.AddChild(card);
        }
    }
}
