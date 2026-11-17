#pragma once

#include <QMainWindow>
#include <QTableView>
#include <QComboBox>
#include <QLineEdit>
#include <QPushButton>
#include <QSplitter>
#include <QMenu>
#include <QActionGroup>
#include <memory>
#include "Models/AssetTableModel.h"
#include "Widgets/TriggerEditorWidget.h"
#include "Widgets/CodeNoteEditorWidget.h"
#include "Widgets/CodeReviewWidget.h"
#include "Styles/ThemeManager.h"
#include "../Services/ProjectRepository.h"
#include "../Services/HistoryManager.h"
#include "../Services/MultiRegionBatchProcessor.h"
#include "../Services/UserFileSerializer.h"
#include "../Services/RichPresenceService.h"
#include "../Services/RichPresenceMetadataService.h"
#include "../Services/LogicValidator.h"
#include "../Common/RecentFilesManager.h"

namespace RALogicEditor {

class MultiRegionDialog;
class BatchNoteMultiplierDialog;

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);
    ~MainWindow() override;

    void LoadFiles(const QString &primaryPath);

protected:
    void closeEvent(QCloseEvent *event) override;
    void showEvent(QShowEvent *event) override;
    bool eventFilter(QObject *watched, QEvent *event) override;

private slots:
    void onAssetSelectionChanged();
    void onAssetDoubleClicked(const QModelIndex &index);

    void onFilterChanged();
    void onSearchTextChanged(const QString &text);

    void onNewAsset();
    void onDuplicateAsset();
    void onDeleteAsset();
    void onResetAsset();
    void onMoveAssetUp();
    void onMoveAssetDown();
    void onRestoreOrder();
    void onRenumberLocalIDs();
    void onViewDiff();
    void onClearDataReset();
    void onToggleReviewer();
    void onSetTheme(ThemeManager::Theme theme);

    void onOpenFile();
    void onSaveFile();
    void onViewRpDiff();
    void onRevertRichPresence();
    void onRevertRegion();
    void onLogicOptimizer();

    void onMultiRegionBatch(MultiRegionDialog *dialog);
    void onBatchMultiplyNotes(BatchNoteMultiplierDialog *dialog);

    void onUndo();
    void onRedo();
    void onReload();

private:
    ProjectRepository _projectRepo;
    HistoryManager<UserTextParser::UserData> _mainHistory;
    RecentFilesManager _recentFiles;
    LogicValidator _validator;
    RichPresenceService _rpService;
    RichPresenceMetadataService _rpMetadata;

    UserFileSerializer _fileSerializer;
    std::unique_ptr<MultiRegionBatchProcessor> _batchProcessor;

    std::shared_ptr<IAssetItem> _selectedAsset = nullptr;
    bool _isLoading = false;
    bool _isDirty = false;
    bool _isFirstShow = true;
    QString _currentFilePath = "";
    QString _currentUserFilePath = "";
    QList<std::shared_ptr<IAssetItem>> _currentDisplayList;
    QList<ValidationIssue> _globalIssues;

    // Cache for global validation note lookup to prevent reparsing all project notes on every validation run
    QMap<QString, QList<std::shared_ptr<NoteNode>>> _cachedNotesLookup;
    QList<ConditionFormatter::RangeEntry> _cachedRangeCache;
    bool _notesCacheDirty = true;
    void RebuildNotesCache();

    QComboBox *cmbAssetType = nullptr;
    QComboBox *cmbCategory = nullptr;
    QComboBox *cmbSource = nullptr;
    QLineEdit *txtAssetSearch = nullptr;

    QTableView *achievementGrid = nullptr;
    AssetTableModel _assetModel;
    AssetTableDelegate *_assetDelegate = nullptr;

    QPushButton *btnAddAsset = nullptr;
    QPushButton *btnDuplicateAsset = nullptr;
    QPushButton *btnDeleteAsset = nullptr;

    TriggerEditorWidget *triggerEditor = nullptr;
    CodeNoteEditorWidget *codeNoteEditor = nullptr;
    CodeReviewWidget *codeReviewControl = nullptr;

    QSplitter *splitMain = nullptr;
    QSplitter *splitEditor = nullptr;
    QSplitter *splitMiddle = nullptr;

    QMenu *recentMenu = nullptr;
    QAction *actUndo = nullptr;
    QAction *actRedo = nullptr;
    QAction *actViewReviewer = nullptr;

    QMenu *themeMenu = nullptr;
    QAction *actThemeLight = nullptr;
    QAction *actThemeDark = nullptr;

    QMenu *assetContextMenu = nullptr;
    QAction *actNewAsset = nullptr;
    QAction *actDuplicateAsset = nullptr;
    QAction *actDeleteAsset = nullptr;
    QAction *actResetAsset = nullptr;
    QAction *actMoveUp = nullptr;
    QAction *actMoveDown = nullptr;
    QAction *actRestoreOrder = nullptr;
    QAction *actRenumberIDs = nullptr;
    QAction *actViewDiff = nullptr;
    QAction *actRevertRegion = nullptr;

    void InitializeUI();
    void InitializeFilters();
    void InitializeMainMenu();
    void InitializeAssetContextMenu();
    void ApplySettings();
    void SaveSettings();
    void ToggleReviewer(std::optional<bool> forceState = std::nullopt);
    void UpdateUndoRedoState();
    void UpdateRecentMenu();

    bool ConfirmDiscardChanges();

    void PushMainUndo();
    void UndoMain();
    void RedoMain();

    void ReloadDataView();
    void PopulateList();
    void LoadSelectedAsset();
    void UpdateAssetState(const std::shared_ptr<IAssetItem> &item);
    void UpdateSelectionUI();
    void SelectAssetInGrid(const std::shared_ptr<IAssetItem> &asset);
    void RunGlobalValidation();

    void EnsureAssetIsLocal(const std::shared_ptr<IAssetItem> &asset);
    void EnsureAssetIsLocal();
    QString InvertOperator(const QString &op);
    void ApplyBulkEdit(const std::shared_ptr<IAssetItem> &sourceTemplate);
    void MoveAsset(int direction);
    bool IsAssetInLocalStore(const std::shared_ptr<IAssetItem> &item);
};

} // namespace RALogicEditor
