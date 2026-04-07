
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void SetName(string newName)
    {
        Profile = Profile?.WithName(newName);
        SetDirty();

        if (!IsDirty)
            return;

        SpriteView.SetName(newName);
    }

    private void UpdateNameEdit()
    {
        NameEdit.Text = Profile?.Name ?? "";
    }

    private void RandomizeEverything()
    {
        Profile = HumanoidCharacterProfile.Random()
            .WithJobPriority("Civilian", JobPriority.High);
        SetProfile(Profile, CharacterSlot);
        SetDirty();
    }

    private void RandomizeName()
    {
        if (Profile == null) return;
        var name = HumanoidCharacterProfile.GetName(Profile.Species, Profile.Gender);
        SetName(name);
        UpdateNameEdit();
    }
}
