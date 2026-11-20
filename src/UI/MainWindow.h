#pragma once

#include "../Common/RecentFilesManager.h"
#include "../Services/HistoryManager.h"
#include "../Services/LogicValidator.h"
#include "../Services/MultiRegionBatchProcessor.h"
#include "../Services/ProjectRepository.h"
#include "../Services/RichPresenceMetadataService.h"
#include "../Services/RichPresenceService.h"
#include "../Services/UpdateChecker.h"
#include "../Services/UserFileSerializer.h"
#include "Models/AssetTableModel.h"
#include "Styles/ThemeManager.h"
#include "Widgets/CodeNoteEditorWidget.h"
#include "Widgets/CodeReviewWidget.h"
#include "Widgets/TriggerEditorWidget.h"

#include <QComboBox>
#include <QLineEdit>
#include <QMainWindow>
#include <QMenu>
#include <QPointer>
#include <QProgressDialog>
#include <QPushButton>
#include <QSplitter>
#include <QTableView>

#include <memory>

namespace RALE {

class MultiRegionDialog;
class UpdateDialog;

class MainWindow : public QMainWindow {
    Q_OBJECT

  public:
    explicit MainWindow(QWidget* parent = nullptr);
    ~MainWindow() override;

    void LoadFiles(const QString& primaryPath);
    void updateThemeIcons();

  protected:
    void closeEvent(QCloseEvent* event) override;
    void showEvent(QShowEvent* event) override;
    bool eventFilter(QObject* watched, QEvent* event) override;

  private slots:
    void onAssetSelectionChanged();
    void onAssetDoubleClicked(const QModelIndex& index);

    void onFilterChanged();
    void onSearchTextChanged(const QString& text);

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
    void onSettings();
    void onCheckForUpdates();
    void onAbout();

    void onUpdateCheckFinished(bool updateAvailable, const QString& latestVersion, const QString& releaseNotes, const QString& downloadUrl,
                               bool silentOnLatest);
    void onUpdateCheckFailed(const QString& errorMessage, bool silentOnLatest);

    void onOpenFile();
    void onSaveFile();
    void onViewRpDiff();
    void onRevertRichPresence();
    void onRevertRegion();
    void onLogicOptimizer();

    void onMultiRegionBatch(MultiRegionDialog* dialog);

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
    UpdateChecker _updateChecker;

    UserFileSerializer _fileSerializer;
    std::unique_ptr<MultiRegionBatchProcessor> _batchProcessor;

    std::shared_ptr<IAssetItem> _selectedAsset = nullptr;
    bool _isLoading = false;
    bool _isDirty = false;
    bool _isFirstShow = true;
    bool _splittersRestored = false;
    bool _updateCheckCanceled = false;
    QString _currentFilePath = "";
    QString _currentUserFilePath = "";
    QList<std::shared_ptr<IAssetItem>> _currentDisplayList;
    QList<ValidationIssue> _globalIssues;

    QPointer<QProgressDialog> _updateProgressDialog;
    QPointer<UpdateDialog> _updateDialog;

    // Cache for global validation note lookup to prevent reparsing all project notes on every validation run
    QMap<QString, QList<std::shared_ptr<NoteNode>>> _cachedNotesLookup;
    QList<ConditionFormatter::RangeEntry> _cachedRangeCache;
    bool _notesCacheDirty = true;
    void RebuildNotesCache();

    QComboBox* cmbAssetType = nullptr;
    QComboBox* cmbCategory = nullptr;
    QComboBox* cmbSource = nullptr;
    QLineEdit* txtAssetSearch = nullptr;

    QTableView* achievementGrid = nullptr;
    AssetTableModel _assetModel;
    AssetTableDelegate* _assetDelegate = nullptr;

    QPushButton* btnAddAsset = nullptr;
    QPushButton* btnDuplicateAsset = nullptr;
    QPushButton* btnDeleteAsset = nullptr;

    TriggerEditorWidget* triggerEditor = nullptr;
    CodeNoteEditorWidget* codeNoteEditor = nullptr;
    CodeReviewWidget* codeReviewControl = nullptr;

    QSplitter* splitMain = nullptr;
    QSplitter* splitEditor = nullptr;
    QSplitter* splitMiddle = nullptr;

    QMenu* recentMenu = nullptr;
    QAction* actUndo = nullptr;
    QAction* actRedo = nullptr;
    QAction* actViewReviewer = nullptr;
    QAction* actCheckForUpdates = nullptr;
    QAction* actAbout = nullptr;

    QMenu* assetContextMenu = nullptr;
    QAction* actNewAsset = nullptr;
    QAction* actDuplicateAsset = nullptr;
    QAction* actDeleteAsset = nullptr;
    QAction* actResetAsset = nullptr;
    QAction* actMoveUp = nullptr;
    QAction* actMoveDown = nullptr;
    QAction* actRestoreOrder = nullptr;
    QAction* actRenumberIDs = nullptr;
    QAction* actViewDiff = nullptr;
    QAction* actRevertRegion = nullptr;

    QIcon getThemeIcon(const QString& resourcePath) const;
    void InitializeUI();
    void InitializeFilters();
    void InitializeMainMenu();
    void InitializeAssetContextMenu();
    void ApplySettings();
    void SaveSettings();
    void RestoreSplitterSizes();
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
    void UpdateAssetState(const std::shared_ptr<IAssetItem>& item);
    void UpdateSelectionUI();
    void SelectAssetInGrid(const std::shared_ptr<IAssetItem>& asset);
    void RunGlobalValidation();
    void RefreshSelectedAssetAfterStoreChange();

    void EnsureAssetIsLocal(const std::shared_ptr<IAssetItem>& asset);
    void EnsureAssetIsLocal();
    QString InvertOperator(const QString& op);
    void ApplyBulkEdit(const std::shared_ptr<IAssetItem>& sourceTemplate);
    void MoveAsset(int direction);
    bool IsAssetInLocalStore(const std::shared_ptr<IAssetItem>& item);
    bool IsServerAssetId(int id, bool isLeaderboard) const;
};

} // namespace RALE
