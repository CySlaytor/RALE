#pragma once

#include "ConditionHeaderView.h"
#include "ConditionTableDelegate.h"

#include "../../Common/ConditionFormatter.h"
#include "../../Models/AppEventArgs.h"
#include "../../Models/AssetModels.h"
#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../../Models/ValidationModels.h"
#include "../Dialogs/GeneratorConfigDialog.h"
#include "../Models/ConditionTableModel.h"
#include "../Models/TriggerViewModel.h"

#include <QCheckBox>
#include <QComboBox>
#include <QLabel>
#include <QLineEdit>
#include <QListWidget>
#include <QMenu>
#include <QPushButton>
#include <QTableView>
#include <QWidget>

#include <memory>

namespace RALE {

class MultiRegionDialog;
class BatchNoteMultiplierDialog;

class TriggerEditorWidget : public QWidget {
    Q_OBJECT

  public:
    explicit TriggerEditorWidget(QWidget* parent = nullptr);

    bool ShowDecimal() const;
    void SetShowDecimal(bool value);

    bool ShowAliases() const;
    void SetShowAliases(bool value);

    bool CollapseChains() const;
    void SetCollapseChains(bool value);

    void SetTriggerText(const QString& value);

    bool CanUndo() const { return _viewModel.CanUndo(); }
    bool CanRedo() const { return _viewModel.CanRedo(); }

    void SetNotes(const QList<CodeNote>& notes);
    void BindAchievement(std::shared_ptr<IAssetItem> asset, const QString& setType = "Core");
    void Clear();

    void Undo();
    void Redo();

    void AddConditionFromNote(const QString& logicString, const QString& type, const QString& size);
    void SelectCondition(const AchievementCondition* target);

    void ExtendSelectionByPattern();
    void CopySelectedRows();
    QString GetSelectedLogicString();
    void CopySelectedLogic();
    void CutSelected();
    void PasteLogic(const QString& logicToPaste = "");
    void DuplicateSelected();
    void DeleteSelected();
    void MoveSelected(int direction);
    void AddCondition();
    void CommitActiveEditor();

    void updateThemeIcons();

  signals:
    void TriggerTextChanged();
    void AchievementDataChanged();
    void AssetResetRequested();
    void AddFromCodeNoteRequested();
    void AddressSelected(const QString& addr);
    void PointerChainSelected(const PointerChainArgs& args);
    void ValidationCompleted(const QList<ValidationIssue>& issues);
    void HistoryChanged();

    void MultiRegionLogicRequested(MultiRegionDialog* dialog);
    void LogicOptimizationRequested();
    void BatchMultiplyNotesRequested(BatchNoteMultiplierDialog* dialog);

  protected:
    bool eventFilter(QObject* watched, QEvent* event) override;

  private slots:
    void onTitleChanged(const QString& text);
    void onDescriptionChanged(const QString& text);
    void onPointsChanged();
    void onTypeChanged(int index);
    void onFormatChanged(int index);
    void onLowerIsBetterChanged(bool checked);

    void onShowDecimalChanged(bool checked);
    void onAliasModeChanged(bool checked);
    void onCollapseChainsChanged(bool checked);

    void onGroupsListSelectionChanged();
    void onLbComponentsSelectionChanged();

    void onBtnResetClicked();
    void onBtnAddNoteClicked();
    void onBtnCopyDefClicked();
    void onBtnToolsClicked();

    void onAddGroupClicked();
    void onRemoveGroupClicked();
    void onRemoveAllAltGroupsClicked();

    void onCreateGeneratorClicked();
    void onCommitGeneratorClicked();
    void onMultiRegionClicked();
    void onLogicOptimizerClicked();
    void onBatchMultiplyNotesClicked();

  private:
    struct LeaderboardFormatEntry {
        QString Label;
        QString Value;
    };
    QList<LeaderboardFormatEntry> _lbFormats;

    TriggerViewModel _viewModel;

    ConditionTableModel _conditionModel;
    ConditionHeaderView* _headerView = nullptr;
    ConditionTableDelegate* _tableDelegate = nullptr;

    bool _isProgrammaticallyChanging = false;
    bool _showDecimal = false;
    bool _showAliases = false;
    bool _collapseChains = false;
    QString _preserveSegmentSelection = "";

    QList<ValidationIssue> _currentIssues;
    QMap<QString, QList<std::shared_ptr<NoteNode>>> _notesLookup;
    QList<ConditionFormatter::RangeEntry> _rangeCache;
    QList<CodeNote> _rawNotes;

    QWidget* panelHeader = nullptr;
    QLabel* pbBadge = nullptr;
    QLineEdit* txtTitle = nullptr;
    QLineEdit* txtDescription = nullptr;
    QComboBox* cmbPoints = nullptr;
    QComboBox* cmbType = nullptr;
    QLabel* lblAuthor = nullptr;
    QLabel* lblAssetState = nullptr;

    QLabel* lblFormat = nullptr;
    QComboBox* cmbFormat = nullptr;
    QCheckBox* chkLowerIsBetter = nullptr;

    QPushButton* btnCopyDef = nullptr;
    QPushButton* btnTools = nullptr;
    QPushButton* btnAddNote = nullptr;
    QPushButton* btnReset = nullptr;

    QListWidget* lvLeaderboardComponents = nullptr;

    QWidget* groupsContainer = nullptr;
    QLabel* lblGroupsHeader = nullptr;
    QListWidget* groupsList = nullptr;
    QWidget* btnPanel = nullptr;
    QPushButton* btnAddGroup = nullptr;
    QPushButton* btnRemoveGroup = nullptr;

    QTableView* conditionGrid = nullptr;

    QWidget* bottomBar = nullptr;
    QLabel* lblEditorStatus = nullptr;
    QCheckBox* chkShowDecimal = nullptr;
    QCheckBox* chkAliasMode = nullptr;
    QCheckBox* chkCollapseChains = nullptr;

    QMenu* toolsContextMenu = nullptr;
    QAction* actCreateGenerator = nullptr;
    QAction* actCommitGenerator = nullptr;
    QAction* actMultiRegion = nullptr;
    QAction* actLogicOptimizer = nullptr;
    QAction* actBatchMultiply = nullptr;

    QMenu* groupsContextMenu = nullptr;
    QAction* actAddGroup = nullptr;
    QAction* actRemoveGroup = nullptr;
    QAction* actRemoveAllAltGroups = nullptr;

    QMenu* gridContextMenu = nullptr;

    // Context menus for column-specific bulk property editing
    QMenu* _flagContextMenu = nullptr;
    QMenu* _typeContextMenu = nullptr;
    QMenu* _sizeContextMenu = nullptr;
    QMenu* _cmpContextMenu = nullptr;
    QMenu* _cmpArithmeticContextMenu = nullptr;
    QMenu* _cmpStrictContextMenu = nullptr;

    QIcon getThemeIcon(const QString& resourcePath) const;
    void InitializeUI();
    void InitializeLeaderboardControls();
    void InitializeColumnContextMenus();
    void SetupAchievementUI(const RaAchievement& cheevo);
    void SetupLeaderboardUI(const RaLeaderboard& lb);

    void RefreshGroupsListUI();
    void RefreshCurrentGridDisplay();
    void RunValidation();
    void SetStatus(const QString& msg);

    void HandleAddressSelection(int row, int col);
    QList<int> GetSelectedVisualRows() const;
    QList<int> GetEffectiveSelectionIndices();
    QList<AchievementCondition> GetEffectiveSelection();
    bool IsGridReadOnly() const;
    void ParseAndApplySmartPaste(const QString& raw);
    QString ValidateMemorySize(const QString& inputSize);
    void ApplySmartPadding(QList<std::shared_ptr<GeneratorConfigDialog::GeneratorItemViewModel>>& roots);
    void OnColumnContextMenuTriggered(int column, const QString& val);
};

} // namespace RALE
