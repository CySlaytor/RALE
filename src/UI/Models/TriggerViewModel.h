#pragma once

#include <QObject>
#include <QString>
#include <QList>
#include <QMap>
#include <memory>
#include "../../Models/AssetModels.h"
#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../../Models/ValidationModels.h"
#include "../../Services/HistoryManager.h"
#include "../../Services/AssetStrategies.h"
#include "../../Services/ILogicValidator.h"
#include "../../Services/ILogicGenerator.h"
#include "../../Common/ConditionFormatter.h"

namespace RALE {

// Responsibilty: Manages the state of the Logic being edited (Parsing, History, Generators).
// Decouples the WinForms UI from the Business Logic.
class TriggerViewModel : public QObject
{
    Q_OBJECT

public:
    // Injected Services
    ILogicValidator &_validator;
    ILogicGenerator &_generator;

    // History Manager handles Undo/Redo stacks
    HistoryManager<TriggerEditorState> _history;

    // Strategy handles Asset-Specific logic (Achievement vs Leaderboard parsing)
    std::shared_ptr<IAssetStrategy> AssetStrategy;

    // The current asset being edited (reference) - Nullable as it's not set in ctor
    std::shared_ptr<IAssetItem> BoundAsset = nullptr;

    // All segments parsed from the logic (Core, or Start/Cancel/Submit/Value)
    QMap<QString, QList<AchievementConditionGroup>> _allSegments;

    // The currently selected segment (e.g. "Core" or "Start")
    QString ActiveSegmentName = "Core";

    // Returns the list of groups for the currently selected segment
    QList<AchievementConditionGroup>& GetActiveGroups();
    const QList<AchievementConditionGroup>& GetActiveGroups() const;

    // Generator Rules (Key: GroupIndex, Value: Rules)
    QMap<int, GeneratorDefinition> GeneratorRules;

    // References for Validation (Set by View)
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> *NotesLookup = nullptr;
    const QList<ConditionFormatter::RangeEntry> *RangeCache = nullptr;

    // History Properties
    bool CanUndo() const { return _history.CanUndo(); }
    bool CanRedo() const { return _history.CanRedo(); }

    // Constructor Injection
    TriggerViewModel(ILogicValidator &validator, ILogicGenerator &generator, QObject *parent = nullptr);

    void LoadAsset(std::shared_ptr<IAssetItem> asset);
    void SelectSegment(const QString &segmentName);

    // Commits changes to the Asset and the History stack
    void UpdateLogic(bool pushUndo = true);

    // History
    void PushUndoState();
    void Undo();
    void Redo();

    // Group Management
    void AddGroup();
    void RemoveGroup(int index);
    void RemoveAllAltGroups();

    // Expose validation via the injected service
    QList<ValidationIssue> ValidateCurrentLogic();

signals:
    // Events - Nullable
    void LogicChanged();
    void SegmentsChanged(); // Fired when loading new asset
    void HistoryChanged();  // Fired when undo stack changes

private:
    // Helper to update state from text directly (e.g. paste or undo)
    void SetStateFromLogic(const QString &logic);
    void RestoreState(const TriggerEditorState &state);
};

} // namespace RALE
