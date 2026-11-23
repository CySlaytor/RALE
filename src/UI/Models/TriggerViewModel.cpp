#include "TriggerViewModel.h"

namespace RALE {

// Constructor Injection
TriggerViewModel::TriggerViewModel(ILogicValidator& validator, ILogicGenerator& generator, QObject* parent)
    : QObject(parent), _validator(validator), _generator(generator) {
    // Default strategy
    AssetStrategy = std::make_shared<AchievementStrategy>();

    _history.HistoryChanged = [this]() { emit HistoryChanged(); };
}

QList<AchievementConditionGroup>& TriggerViewModel::GetActiveGroups() { return _allSegments[ActiveSegmentName]; }

const QList<AchievementConditionGroup>& TriggerViewModel::GetActiveGroups() const {
    auto it = _allSegments.constFind(ActiveSegmentName);
    if (it != _allSegments.constEnd())
        return it.value();
    static const QList<AchievementConditionGroup> emptyList;
    return emptyList;
}

AchievementConditionGroup* TriggerViewModel::GetGroup(const QString& segmentName, int groupIndex) {
    auto it = _allSegments.find(segmentName);
    if (it == _allSegments.end())
        return nullptr;
    if (groupIndex < 0 || groupIndex >= it.value().size())
        return nullptr;
    return &it.value()[groupIndex];
}

const AchievementConditionGroup* TriggerViewModel::GetGroup(const QString& segmentName, int groupIndex) const {
    auto it = _allSegments.constFind(segmentName);
    if (it == _allSegments.constEnd())
        return nullptr;
    if (groupIndex < 0 || groupIndex >= it.value().size())
        return nullptr;
    return &it.value().at(groupIndex);
}

void TriggerViewModel::LoadAsset(std::shared_ptr<IAssetItem> asset) {
    emit AboutToChangeSegments();

    BoundAsset = asset;
    _history.Clear();
    GeneratorRules.clear();
    _segmentGeneratorRules.clear();

    // Select Strategy based on Asset Type
    if (asset != nullptr && asset->isLeaderboard())
        AssetStrategy = std::make_shared<LeaderboardStrategy>();
    else
        AssetStrategy = std::make_shared<AchievementStrategy>();

    // Parse initial logic
    _allSegments = AssetStrategy->ParseLogic(asset != nullptr ? asset->Logic : "");

    // Set default segment
    QStringList segments = AssetStrategy->GetSegmentNames();
    ActiveSegmentName = !segments.isEmpty() ? segments[0] : "Core";

    emit SegmentsChanged();
}

void TriggerViewModel::SetLogicPreservingHistory(const QString& logic) {
    if (BoundAsset == nullptr)
        return;

    emit AboutToChangeSegments();
    GeneratorRules.clear();
    _segmentGeneratorRules.clear();

    if (AssetStrategy != nullptr) {
        SetStateFromLogic(logic);
    }
    BoundAsset->Logic = logic;

    emit SegmentsChanged();
    emit LogicChanged();
}

void TriggerViewModel::SelectSegment(const QString& segmentName) {
    if (_allSegments.contains(segmentName)) {
        _segmentGeneratorRules[ActiveSegmentName] = GeneratorRules;
        ActiveSegmentName = segmentName;
        GeneratorRules = _segmentGeneratorRules.value(ActiveSegmentName);
        // No logic change event needed here usually, the UI just repaints
    }
}

// Commits changes to the Asset and the History stack
void TriggerViewModel::UpdateLogic(bool pushUndo) {
    if (BoundAsset == nullptr)
        return;

    emit AboutToChangeSegments();

    _segmentGeneratorRules[ActiveSegmentName] = GeneratorRules;
    auto& activeGroups = GetActiveGroups();

    // 0. Cleanup Array Generation State
    for (auto it = GeneratorRules.cbegin(); it != GeneratorRules.cend(); ++it) {
        int groupIndex = it.key();
        const auto& def = it.value();

        if (def.IsArray && groupIndex < activeGroups.size()) {
            auto& group = activeGroups[groupIndex];
            if (group.Conditions.size() > def.TemplateConditionCount) {
                int itemsToRemove = group.Conditions.size() - def.TemplateConditionCount;
                int startIndex = def.TemplateConditionCount;
                if (startIndex < group.Conditions.size()) {
                    for (int k = 0; k < itemsToRemove; ++k)
                        group.Conditions.removeAt(startIndex);
                }
            }
        }
    }

    // 1. Apply Generators before building string (Using injected service)
    _generator.ApplyGeneratorRules(activeGroups, GeneratorRules);

    // 2. Build String
    QString newLogic = AssetStrategy->BuildLogic(_allSegments);

    // 3. Push Undo State if requested
    if (pushUndo && newLogic != BoundAsset->Logic) {
        TriggerEditorState state = CaptureCurrentState();
        state.LogicString = BoundAsset->Logic;
        _history.PushState(state);
    }

    // 4. Update Asset
    BoundAsset->Logic = newLogic;

    // 5. Notify
    emit LogicChanged();
}

// Helper to update state from text directly (e.g. paste or undo)
void TriggerViewModel::SetStateFromLogic(const QString& logic) {
    _allSegments = AssetStrategy->ParseLogic(logic);
    // Ensure ActiveSegmentName is valid
    if (!_allSegments.contains(ActiveSegmentName)) {
        QStringList names = AssetStrategy->GetSegmentNames();
        ActiveSegmentName = !names.isEmpty() ? names[0] : "Core";
    }
}

#pragma region History

TriggerEditorState TriggerViewModel::CaptureCurrentState(int activeGroupIndex) const {
    TriggerEditorState state;
    state.Segments = _allSegments;
    state.ActiveSegmentName = ActiveSegmentName;
    state.ActiveGroupIndex = activeGroupIndex;
    state.SegmentGeneratorRules = _segmentGeneratorRules;
    state.GeneratorRules = GeneratorRules;
    state.SegmentGeneratorRules[ActiveSegmentName] = GeneratorRules;

    if (AssetStrategy != nullptr)
        state.LogicString = AssetStrategy->BuildLogic(_allSegments);
    else if (BoundAsset != nullptr)
        state.LogicString = BoundAsset->Logic;

    if (BoundAsset != nullptr) {
        state.Title = BoundAsset->Title;
        state.Description = BoundAsset->Description;
        if (!BoundAsset->isLeaderboard()) {
            const auto* ach = dynamic_cast<const RaAchievement*>(BoundAsset.get());
            if (ach != nullptr) {
                state.Points = ach->Points;
                state.Type = ach->Type;
            }
        } else {
            const auto* lb = dynamic_cast<const RaLeaderboard*>(BoundAsset.get());
            if (lb != nullptr) {
                state.Format = lb->Format;
                state.LowerIsBetter = lb->LowerIsBetter;
            }
        }
    }
    return state;
}

void TriggerViewModel::PushExplicitState(const TriggerEditorState& state) {
    _history.PushState(state);
}

void TriggerViewModel::PushUndoState(int activeGroupIndex) {
    _segmentGeneratorRules[ActiveSegmentName] = GeneratorRules;
    TriggerEditorState state = CaptureCurrentState(activeGroupIndex);
    _history.PushState(state);
}

void TriggerViewModel::Undo() {
    if (!_history.CanUndo())
        return;

    _segmentGeneratorRules[ActiveSegmentName] = GeneratorRules;
    TriggerEditorState current = CaptureCurrentState();

    _history.IsUndoingRedoing = true;
    TriggerEditorState previous = _history.Undo(current);
    RestoreState(previous);
    _history.IsUndoingRedoing = false;

    emit HistoryChanged();
}

void TriggerViewModel::Redo() {
    if (!_history.CanRedo())
        return;

    _segmentGeneratorRules[ActiveSegmentName] = GeneratorRules;
    TriggerEditorState current = CaptureCurrentState();

    _history.IsUndoingRedoing = true;
    TriggerEditorState next = _history.Redo(current);
    RestoreState(next);
    _history.IsUndoingRedoing = false;

    emit HistoryChanged();
}

void TriggerViewModel::RestoreState(const TriggerEditorState& state) {
    emit AboutToChangeSegments();

    if (!state.Segments.isEmpty()) {
        _allSegments = state.Segments;
    } else {
        SetStateFromLogic(state.LogicString);
    }

    _segmentGeneratorRules = state.SegmentGeneratorRules;
    ActiveSegmentName = state.ActiveSegmentName;
    if (_segmentGeneratorRules.contains(ActiveSegmentName))
        GeneratorRules = _segmentGeneratorRules.value(ActiveSegmentName);
    else
        GeneratorRules = state.GeneratorRules;

    if (BoundAsset != nullptr) {
        BoundAsset->Logic = state.LogicString;
        BoundAsset->Title = state.Title;
        BoundAsset->Description = state.Description;
        if (!BoundAsset->isLeaderboard()) {
            auto* ach = dynamic_cast<RaAchievement*>(BoundAsset.get());
            if (ach != nullptr) {
                ach->Points = state.Points;
                ach->Type = state.Type;
            }
        } else {
            auto* lb = dynamic_cast<RaLeaderboard*>(BoundAsset.get());
            if (lb != nullptr) {
                lb->Format = state.Format;
                lb->LowerIsBetter = state.LowerIsBetter;
            }
        }
    }
    emit LogicChanged();
    emit AssetMetadataRestored(state);
}

#pragma endregion

#pragma region Group Management

void TriggerViewModel::AddGroup() {
    PushUndoState();
    emit AboutToChangeSegments();
    auto& groups = GetActiveGroups();
    QString newName = groups.isEmpty() ? "Core"
                                       : (AssetStrategy->isLeaderboard() ? QString("Group %1").arg(groups.size() + 1)
                                                                         : QString("Alt %1").arg(groups.size()));

    AchievementConditionGroup newGroup;
    newGroup.GroupName = newName;
    groups.append(newGroup);
    UpdateLogic(false); // Already pushed undo
}

void TriggerViewModel::RemoveGroup(int index) {
    auto& groups = GetActiveGroups();
    if (index < 0 || index >= groups.size())
        return;

    PushUndoState(index);
    emit AboutToChangeSegments();

    QMap<int, GeneratorDefinition> shiftedRules;
    for (auto it = GeneratorRules.cbegin(); it != GeneratorRules.cend(); ++it) {
        if (it.key() < index)
            shiftedRules.insert(it.key(), it.value());
        else if (it.key() > index)
            shiftedRules.insert(it.key() - 1, it.value());
    }
    GeneratorRules = shiftedRules;

    groups.removeAt(index);

    // Renumber Alts
    if (!AssetStrategy->isLeaderboard()) {
        for (int i = 1; i < groups.size(); i++) {
            if (groups[i].GroupName.startsWith("Alt "))
                groups[i].GroupName = QString("Alt %1").arg(i);
        }
    }

    UpdateLogic(false);
}

void TriggerViewModel::RemoveAllAltGroups() {
    auto& groups = GetActiveGroups();
    if (groups.size() <= 1)
        return;

    PushUndoState();
    emit AboutToChangeSegments();
    GeneratorRules.clear();

    AchievementConditionGroup first = groups[0];
    groups.clear();
    groups.append(first);

    if (!AssetStrategy->isLeaderboard() && first.GroupName != "Core")
        groups[0].GroupName = "Core";

    UpdateLogic(false);
}

#pragma endregion

// Expose validation via the injected service
QList<ValidationIssue> TriggerViewModel::ValidateCurrentLogic() {
    // Pass the notes references to the validator
    if (BoundAsset != nullptr && BoundAsset->isLeaderboard()) {
        return _validator.ValidateLeaderboard(_allSegments, BoundAsset, NotesLookup, RangeCache, RawNotes);
    }
    return _validator.ValidateAsset(GetActiveGroups(), BoundAsset, NotesLookup, RangeCache, RawNotes);
}

} // namespace RALE
