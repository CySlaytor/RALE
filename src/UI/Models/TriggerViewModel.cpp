#include "TriggerViewModel.h"

namespace RALogicEditor {

// Constructor Injection
TriggerViewModel::TriggerViewModel(ILogicValidator &validator, ILogicGenerator &generator, QObject *parent)
    : QObject(parent), _validator(validator), _generator(generator)
{
    // Default strategy
    AssetStrategy = std::make_shared<AchievementStrategy>();

    _history.HistoryChanged = [this]() {
        emit HistoryChanged();
    };
}

QList<AchievementConditionGroup>& TriggerViewModel::GetActiveGroups()
{
    return _allSegments[ActiveSegmentName];
}

const QList<AchievementConditionGroup>& TriggerViewModel::GetActiveGroups() const
{
    auto it = _allSegments.constFind(ActiveSegmentName);
    if (it != _allSegments.constEnd())
        return it.value();
    static const QList<AchievementConditionGroup> emptyList;
    return emptyList;
}

void TriggerViewModel::LoadAsset(std::shared_ptr<IAssetItem> asset)
{
    BoundAsset = asset;
    _history.Clear();
    GeneratorRules.clear();

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

void TriggerViewModel::SelectSegment(const QString &segmentName)
{
    if (_allSegments.contains(segmentName))
    {
        ActiveSegmentName = segmentName;
        // No logic change event needed here usually, the UI just repaints
    }
}

// Commits changes to the Asset and the History stack
void TriggerViewModel::UpdateLogic(bool pushUndo)
{
    if (BoundAsset == nullptr) return;

    auto &activeGroups = GetActiveGroups();

    // 0. Cleanup Array Generation State
    for (auto it = GeneratorRules.cbegin(); it != GeneratorRules.cend(); ++it)
    {
        int groupIndex = it.key();
        const auto &def = it.value();

        if (def.IsArray && groupIndex < activeGroups.size())
        {
            auto &group = activeGroups[groupIndex];
            if (group.Conditions.size() > def.TemplateConditionCount)
            {
                int itemsToRemove = group.Conditions.size() - def.TemplateConditionCount;
                int startIndex = def.TemplateConditionCount;
                if (startIndex < group.Conditions.size())
                {
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
    if (pushUndo && newLogic != BoundAsset->Logic)
    {
        TriggerEditorState state;
        state.LogicString = BoundAsset->Logic;
        state.GeneratorRules = GeneratorRules;
        _history.PushState(state.Clone());
    }

    // 4. Update Asset
    BoundAsset->Logic = newLogic;

    // 5. Notify
    emit LogicChanged();
}

// Helper to update state from text directly (e.g. paste or undo)
void TriggerViewModel::SetStateFromLogic(const QString &logic)
{
    _allSegments = AssetStrategy->ParseLogic(logic);
    // Ensure ActiveSegmentName is valid
    if (!_allSegments.contains(ActiveSegmentName))
    {
        QStringList names = AssetStrategy->GetSegmentNames();
        ActiveSegmentName = !names.isEmpty() ? names[0] : "Core";
    }
}

#pragma region History

void TriggerViewModel::PushUndoState()
{
    TriggerEditorState state;
    state.LogicString = AssetStrategy->BuildLogic(_allSegments);
    state.GeneratorRules = GeneratorRules;
    _history.PushState(state.Clone());
}

void TriggerViewModel::Undo()
{
    // Create current state wrapper
    TriggerEditorState current;
    current.LogicString = AssetStrategy->BuildLogic(_allSegments);
    current.GeneratorRules = GeneratorRules;

    TriggerEditorState previous = _history.Undo(current);
    RestoreState(previous);
}

void TriggerViewModel::Redo()
{
    TriggerEditorState current;
    current.LogicString = AssetStrategy->BuildLogic(_allSegments);
    current.GeneratorRules = GeneratorRules;

    TriggerEditorState next = _history.Redo(current);
    RestoreState(next);
}

void TriggerViewModel::RestoreState(const TriggerEditorState &state)
{
    SetStateFromLogic(state.LogicString);
    GeneratorRules = state.Clone().GeneratorRules; // Restore rules
    if (BoundAsset != nullptr) BoundAsset->Logic = state.LogicString;
    emit LogicChanged();
}

#pragma endregion

#pragma region Group Management

void TriggerViewModel::AddGroup()
{
    PushUndoState();
    auto &groups = GetActiveGroups();
    QString newName = groups.isEmpty() ? "Core" :
        (AssetStrategy->isLeaderboard() ? QString("Group %1").arg(groups.size() + 1) : QString("Alt %1").arg(groups.size()));

    AchievementConditionGroup newGroup;
    newGroup.GroupName = newName;
    groups.append(newGroup);
    UpdateLogic(false); // Already pushed undo
}

void TriggerViewModel::RemoveGroup(int index)
{
    auto &groups = GetActiveGroups();
    if (index < 0 || index >= groups.size()) return;

    PushUndoState();

    if (GeneratorRules.contains(index)) GeneratorRules.remove(index);
    groups.removeAt(index);

    // Renumber Alts
    if (!AssetStrategy->isLeaderboard())
    {
        for (int i = 1; i < groups.size(); i++)
        {
            if (groups[i].GroupName.startsWith("Alt "))
                groups[i].GroupName = QString("Alt %1").arg(i);
        }
    }

    UpdateLogic(false);
}

void TriggerViewModel::RemoveAllAltGroups()
{
    auto &groups = GetActiveGroups();
    if (groups.size() <= 1) return;

    PushUndoState();
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
QList<ValidationIssue> TriggerViewModel::ValidateCurrentLogic()
{
    // Pass the notes references to the validator
    return _validator.ValidateAsset(GetActiveGroups(), BoundAsset, NotesLookup, RangeCache);
}

} // namespace RALogicEditor
