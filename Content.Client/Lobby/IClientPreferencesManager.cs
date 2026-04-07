using Content.Shared.Construction.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby
{
    public interface IClientPreferencesManager
    {
        event Action OnServerDataLoaded;
        event Action<int, CharacterInventoryPreviewData?> OnCharacterInventoryPreviewUpdated;

        bool ServerDataLoaded => Settings != null;

        GameSettings? Settings { get; }
        PlayerPreferences? Preferences { get; }
        CharacterInventoryPreviewData? SelectedCharacterInventoryPreview { get; }
        void Initialize();
        void SelectCharacter(HumanoidCharacterProfile profile);
        void SelectCharacter(int slot);
        void UpdateCharacter(HumanoidCharacterProfile profile, int slot);
        void CreateCharacter(HumanoidCharacterProfile profile);
        void DeleteCharacter(HumanoidCharacterProfile profile);
        void DeleteCharacter(int slot);
        void UpdateConstructionFavorites(List<ProtoId<ConstructionPrototype>> favorites);
    }
}
