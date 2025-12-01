using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Helpers
{
    // Responsibilty: Manages the state of the Logic being edited (Parsing, History, Generators).
    // Decouples the WinForms UI from the Business Logic.
    public class TriggerViewModel
    {
        // Injected Services
        private readonly ILogicValidator _validator;
        private readonly ILogicGenerator _generator;

        // History Manager handles Undo/Redo stacks
        private readonly HistoryManager<TriggerEditorState> _history = new();

        // Strategy handles Asset-Specific logic (Achievement vs Leaderboard parsing)
        public IAssetStrategy AssetStrategy { get; private set; }

        // The current asset being edited (reference) - Nullable as it's not set in ctor
        public IAssetItem? BoundAsset { get; private set; }

        // All segments parsed from the logic (Core, or Start/Cancel/Submit/Value)
        private Dictionary<string, List<AchievementConditionGroup>> _allSegments = new();

        // The currently selected segment (e.g. "Core" or "Start")
        public string ActiveSegmentName { get; private set; } = "Core";

        // Returns the list of groups for the currently selected segment
        public List<AchievementConditionGroup> ActiveGroups
        {
            get
            {
                if (_allSegments.ContainsKey(ActiveSegmentName))
                    return _allSegments[ActiveSegmentName];
                return new List<AchievementConditionGroup>();
            }
        }

        // Generator Rules (Key: GroupIndex, Value: Rules)
        public Dictionary<int, GeneratorDefinition> GeneratorRules { get; private set; } = new();

        // References for Validation (Set by View)
        public Dictionary<string, List<NoteNode>>? NotesLookup { get; set; }
        public List<(long Start, long End, NoteNode Node)>? RangeCache { get; set; }

        // Events - Nullable
        public event EventHandler? LogicChanged;
        public event EventHandler? SegmentsChanged; // Fired when loading new asset
        public event EventHandler? HistoryChanged; // Fired when undo stack changes

        // History Properties
        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        // Constructor Injection
        public TriggerViewModel(ILogicValidator validator, ILogicGenerator generator)
        {
            _validator = validator;
            _generator = generator;
            // Default strategy
            AssetStrategy = new AchievementStrategy();

            _history.HistoryChanged += (s, e) => HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void LoadAsset(IAssetItem asset)
        {
            BoundAsset = asset;
            _history.Clear();
            GeneratorRules.Clear();

            // Select Strategy based on Asset Type
            if (asset.IsLeaderboard)
                AssetStrategy = new LeaderboardStrategy();
            else
                AssetStrategy = new AchievementStrategy();

            // Parse initial logic
            _allSegments = AssetStrategy.ParseLogic(asset.Logic);

            // Set default segment
            var segments = AssetStrategy.GetSegmentNames();
            ActiveSegmentName = segments.Length > 0 ? segments[0] : "Core";

            SegmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectSegment(string segmentName)
        {
            if (_allSegments.ContainsKey(segmentName))
            {
                ActiveSegmentName = segmentName;
                // No logic change event needed here usually, the UI just repaints
            }
        }

        // Commits changes to the Asset and the History stack
        public void UpdateLogic(bool pushUndo = true)
        {
            if (BoundAsset == null) return;

            // 0. Cleanup Array Generation State
            foreach (var kvp in GeneratorRules)
            {
                int groupIndex = kvp.Key;
                var def = kvp.Value;

                if (def.IsArray && groupIndex < ActiveGroups.Count)
                {
                    var group = ActiveGroups[groupIndex];
                    if (group.Conditions.Count > def.TemplateConditionCount)
                    {
                        int itemsToRemove = group.Conditions.Count - def.TemplateConditionCount;
                        int startIndex = def.TemplateConditionCount;
                        if (startIndex < group.Conditions.Count)
                        {
                            group.Conditions.RemoveRange(startIndex, itemsToRemove);
                        }
                    }
                }
            }

            // 1. Apply Generators before building string (Using injected service)
            _generator.ApplyGeneratorRules(ActiveGroups, GeneratorRules);

            // 2. Build String
            string newLogic = AssetStrategy.BuildLogic(_allSegments);

            // 3. Push Undo State if requested
            if (pushUndo && newLogic != BoundAsset.Logic)
            {
                var state = new TriggerEditorState
                {
                    LogicString = BoundAsset.Logic,
                    GeneratorRules = GeneratorRules
                };
                _history.PushState(state.Clone());
            }

            // 4. Update Asset
            BoundAsset.Logic = newLogic;

            // 5. Notify
            LogicChanged?.Invoke(this, EventArgs.Empty);
        }

        // Helper to update state from text directly (e.g. paste or undo)
        private void SetStateFromLogic(string logic)
        {
            _allSegments = AssetStrategy.ParseLogic(logic);
            // Ensure ActiveSegmentName is valid
            if (!_allSegments.ContainsKey(ActiveSegmentName))
            {
                var names = AssetStrategy.GetSegmentNames();
                ActiveSegmentName = names.Length > 0 ? names[0] : "Core";
            }
        }

        #region History

        public void PushUndoState()
        {
            var state = new TriggerEditorState
            {
                LogicString = AssetStrategy.BuildLogic(_allSegments),
                GeneratorRules = GeneratorRules
            };
            _history.PushState(state.Clone());
        }

        public void Undo()
        {
            // Create current state wrapper
            var current = new TriggerEditorState
            {
                LogicString = AssetStrategy.BuildLogic(_allSegments),
                GeneratorRules = GeneratorRules
            };

            var previous = _history.Undo(current);
            RestoreState(previous);
        }

        public void Redo()
        {
            var current = new TriggerEditorState
            {
                LogicString = AssetStrategy.BuildLogic(_allSegments),
                GeneratorRules = GeneratorRules
            };

            var next = _history.Redo(current);
            RestoreState(next);
        }

        private void RestoreState(TriggerEditorState state)
        {
            SetStateFromLogic(state.LogicString);
            GeneratorRules = state.Clone().GeneratorRules; // Restore rules
            if (BoundAsset != null) BoundAsset.Logic = state.LogicString;
            LogicChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Group Management

        public void AddGroup()
        {
            PushUndoState();
            var groups = ActiveGroups;
            string newName = groups.Count == 0 ? "Core" :
                (AssetStrategy.IsLeaderboard ? "Group " + (groups.Count + 1) : "Alt " + groups.Count);

            groups.Add(new AchievementConditionGroup { GroupName = newName });
            UpdateLogic(false); // Already pushed undo
        }

        public void RemoveGroup(int index)
        {
            var groups = ActiveGroups;
            if (index < 0 || index >= groups.Count) return;

            PushUndoState();

            if (GeneratorRules.ContainsKey(index)) GeneratorRules.Remove(index);
            groups.RemoveAt(index);

            // Renumber Alts
            if (!AssetStrategy.IsLeaderboard)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (groups[i].GroupName.StartsWith("Alt "))
                        groups[i].GroupName = $"Alt {i}";
                }
            }

            UpdateLogic(false);
        }

        public void RemoveAllAltGroups()
        {
            var groups = ActiveGroups;
            if (groups.Count <= 1) return;

            PushUndoState();
            GeneratorRules.Clear();

            var first = groups[0];
            groups.Clear();
            groups.Add(first);

            if (!AssetStrategy.IsLeaderboard && first.GroupName != "Core")
                first.GroupName = "Core";

            UpdateLogic(false);
        }

        #endregion

        // Expose validation via the injected service
        public List<ValidationIssue> ValidateCurrentLogic()
        {
            // Pass the notes references to the validator
            return _validator.ValidateAsset(ActiveGroups, BoundAsset, NotesLookup, RangeCache);
        }
    }
}