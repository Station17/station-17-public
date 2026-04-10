using System;
using Content.Shared.Construction.Prototypes;
using Content.Shared.HL2RP.CharacterPersistence;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences
{
    /// <summary>
    ///     Contains all player characters and the index of the currently selected character.
    ///     Serialized both over the network and to disk.
    /// </summary>
    [Serializable]
    [NetSerializable]
    public sealed class PlayerPreferences
    {
        private Dictionary<int, HumanoidCharacterProfile> _characters;
        private readonly Dictionary<int, List<CharacterHistoryEntry>> _characterHistory;

        public PlayerPreferences(
            IEnumerable<KeyValuePair<int, HumanoidCharacterProfile>> characters,
            int selectedCharacterIndex,
            Color adminOOCColor,
            List<ProtoId<ConstructionPrototype>> constructionFavorites,
            Dictionary<int, List<CharacterHistoryEntry>>? characterHistory = null)
        {
            _characters = new Dictionary<int, HumanoidCharacterProfile>(characters);
            _characterHistory = characterHistory ?? new Dictionary<int, List<CharacterHistoryEntry>>();
            SelectedCharacterIndex = selectedCharacterIndex;
            AdminOOCColor = adminOOCColor;
            ConstructionFavorites = constructionFavorites;
        }

        /// <summary>
        ///     All player characters.
        /// </summary>
        public IReadOnlyDictionary<int, HumanoidCharacterProfile> Characters => _characters;

        public IReadOnlyDictionary<int, List<CharacterHistoryEntry>> CharacterHistory => _characterHistory;

        public HumanoidCharacterProfile GetProfile(int index)
        {
            return _characters[index];
        }

        /// <summary>
        ///     Index of the currently selected character.
        /// </summary>
        public int SelectedCharacterIndex { get; }

        /// <summary>
        ///     The currently selected character.
        /// </summary>
        public HumanoidCharacterProfile SelectedCharacter => Characters[SelectedCharacterIndex];

        public Color AdminOOCColor { get; set; }

        /// <summary>
        ///    List of favorite items in the construction menu.
        /// </summary>
        public List<ProtoId<ConstructionPrototype>> ConstructionFavorites { get; set; } = [];

        public int IndexOfCharacter(HumanoidCharacterProfile profile)
        {
            return _characters.FirstOrNull(p => p.Value == profile)?.Key ?? -1;
        }

        public bool TryIndexOfCharacter(HumanoidCharacterProfile profile, out int index)
        {
            return (index = IndexOfCharacter(profile)) != -1;
        }
    }

    [Serializable]
    [NetSerializable]
    public sealed class CharacterHistoryEntry
    {
        public int RoundId { get; init; }
        public DateTime RoundEndedAt { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Surname { get; init; } = string.Empty;
        public CharacterInventoryPreviewData? Preview { get; init; }
    }
}
