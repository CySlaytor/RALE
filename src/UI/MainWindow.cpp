#include "MainWindow.h"

#include "../Common/AppSettings.h"
#include "../Common/ImageCache.h"
#include "../Parsers/PointerTreeParser.h"
#include "../Parsers/RichPresenceParser.h"
#include "../Utilities/LogicFormatter.h"
#include "Dialogs/AboutDialog.h"
#include "Dialogs/AssetDiffDialog.h"
#include "Dialogs/LogicOptimizerDialog.h"
#include "Dialogs/MultiRegionDialog.h"
#include "Dialogs/RevertRegionDialog.h"
#include "Dialogs/SettingsDialog.h"
#include "Dialogs/SuggestedFixDialog.h"
#include "Dialogs/TextDiffDialog.h"
#include "Dialogs/UpdateDialog.h"

#include <QApplication>
#include <QCloseEvent>
#include <QFileDialog>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QKeyEvent>
#include <QLayout>
#include <QMenuBar>
#include <QMessageBox>
#include <QPlainTextEdit>
#include <QProcess>
#include <QRegularExpression>
#include <QShowEvent>
#include <QStatusBar>
#include <QTextEdit>
#include <QTimer>
#include <QVBoxLayout>

#include <algorithm>

#ifndef VERSION_STRING
#define VERSION_STRING "2.0.0"
#endif

namespace RALE {

namespace {

QString NormalizeVersionTag(const QString& version) {
    QString clean = version.trimmed();
    if (clean.startsWith('v', Qt::CaseInsensitive))
        clean.remove(0, 1);
    return clean;
}

} // namespace

MainWindow::MainWindow(QWidget* parent) : QMainWindow(parent) {
    // Load UI settings first so they are available to controls during construction
    AppSettings::Load();

    InitializeUI();

    // Initialize services that require dependencies
    _batchProcessor = std::make_unique<MultiRegionBatchProcessor>(_projectRepo.Metadata);

    ApplySettings();

    // --- Event Wiring ---

    // Trigger Editor (Logic)
    connect(triggerEditor, &TriggerEditorWidget::TriggerTextChanged, this, [this]() {
        if (_isLoading || _selectedAsset == nullptr)
            return;
        _projectRepo.EnsureLocalDataExists();
        if (!_currentAssetDirty) {
            PushMainUndo();
            _currentAssetDirty = true;
        }
        EnsureAssetIsLocal();
        UpdateAssetState(_selectedAsset);
        _isDirty = true;
        UpdateSelectionUI();
        UpdateTriggerActionButtons();
    });

    connect(triggerEditor, &TriggerEditorWidget::AchievementDataChanged, this, [this]() {
        if (_isLoading || _selectedAsset == nullptr)
            return;
        _projectRepo.EnsureLocalDataExists();

        if (!_currentAssetDirty) {
            PushMainUndo();
            _currentAssetDirty = true;
        }
        EnsureAssetIsLocal();
        UpdateAssetState(_selectedAsset);

        auto selectedRows = achievementGrid->selectionModel()->selectedRows();
        if (selectedRows.size() > 1)
            ApplyBulkEdit(_selectedAsset);

        _isDirty = true;
        _assetModel.NotifyAssetChanged(_selectedAsset);
        RefreshSetLevelIssues();
        UpdateSelectionUI();
        UpdateTriggerActionButtons();
    });

    // The editor's Reset button resets current unsaved changes for the open asset only.
    connect(triggerEditor, &TriggerEditorWidget::AssetResetRequested, this, [this]() {
        if (_selectedAsset == nullptr || _projectRepo.LocalData == nullptr)
            return;
        if (!_projectRepo.HasUnsavedAssetChanges(_selectedAsset.get()))
            return;

        QMessageBox msgBox(this);
        msgBox.setWindowTitle("Confirm Reset");
        msgBox.setText(QString("Discard current unsaved changes for '%1' (ID: %2)?").arg(_selectedAsset->Title).arg(_selectedAsset->ID));
        msgBox.setInformativeText("Unsaved modifications made since the last load or save to User.txt will be discarded, restoring "
                                  "this asset to its last saved state (User.txt override if saved, or server JSON baseline).");
        msgBox.setIcon(QMessageBox::Warning);

        QPushButton* resetBtn = msgBox.addButton("Reset", QMessageBox::DestructiveRole);
        QPushButton* cancelBtn = msgBox.addButton("Cancel", QMessageBox::RejectRole);
        msgBox.setDefaultButton(cancelBtn);
        msgBox.exec();

        if (msgBox.clickedButton() != resetBtn)
            return;

        PushMainUndo();
        _projectRepo.ResetAssetToSaved(_selectedAsset->ID, _selectedAsset->isLeaderboard());

        _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
        _notesCacheDirty = true;
        ReloadDataView();
        RefreshSelectedAssetAfterStoreChange();
        RunGlobalValidation();
    });

    // The editor's Revert button restores the original values from the loaded JSON server snapshot.
    connect(triggerEditor, &TriggerEditorWidget::AssetRevertRequested, this, [this]() {
        if (_selectedAsset == nullptr || _projectRepo.LocalData == nullptr)
            return;
        if (!IsServerAssetId(_selectedAsset->ID, _selectedAsset->isLeaderboard()) || _selectedAsset->State != AssetState::Modified)
            return;

        QMessageBox msgBox(this);
        msgBox.setWindowTitle("Confirm Revert to Server");
        msgBox.setText(
            QString("Discard local changes and revert '%1' (ID: %2) to the server snapshot?").arg(_selectedAsset->Title).arg(_selectedAsset->ID));
        msgBox.setInformativeText("Warning: All current modifications and any local User.txt overrides for this asset will be "
                                  "discarded, restoring the original values from the loaded JSON server snapshot.");
        msgBox.setIcon(QMessageBox::Warning);

        QPushButton* revertBtn = msgBox.addButton("Revert", QMessageBox::DestructiveRole);
        QPushButton* cancelBtn = msgBox.addButton("Cancel", QMessageBox::RejectRole);
        msgBox.setDefaultButton(cancelBtn);
        msgBox.exec();

        if (msgBox.clickedButton() != revertBtn)
            return;

        PushMainUndo();
        _projectRepo.RevertAssetToServer(_selectedAsset->ID, _selectedAsset->isLeaderboard());

        _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
        _notesCacheDirty = true;
        ReloadDataView();
        RefreshSelectedAssetAfterStoreChange();
        RunGlobalValidation();
    });

    connect(triggerEditor, &TriggerEditorWidget::AddFromCodeNoteRequested, this, [this]() {
        auto data = codeNoteEditor->GetSelectedLogic();
        if (!std::get<0>(data).isEmpty())
            triggerEditor->AddConditionFromNote(std::get<0>(data), std::get<1>(data), std::get<2>(data));
        else
            QMessageBox::information(this, "Add Note", "No valid address selected.");
    });

    connect(triggerEditor, &TriggerEditorWidget::ValidationCompleted, this, [this](const QList<ValidationIssue>& issues) {
        if (_selectedAsset == nullptr)
            return;
        for (int i = _globalIssues.size() - 1; i >= 0; --i) {
            if (_globalIssues[i].Asset != nullptr && _globalIssues[i].Asset->ID == _selectedAsset->ID &&
                _globalIssues[i].Asset->isLeaderboard() == _selectedAsset->isLeaderboard())
                _globalIssues.removeAt(i);
        }
        _globalIssues.append(issues);
        if (!_isLoading)
            RefreshSetLevelIssues();
    });

    connect(triggerEditor, &TriggerEditorWidget::MultiRegionLogicRequested, this, &MainWindow::onMultiRegionBatch);

    connect(triggerEditor, &TriggerEditorWidget::LogicOptimizationRequested, this, &MainWindow::onLogicOptimizer);

    // Bridge Trigger Editor -> Code Note Editor (Highlighting)
    connect(triggerEditor, &TriggerEditorWidget::AddressSelected, this,
            [this](const QString& addr) { codeNoteEditor->HighlightAddress(addr); });

    connect(triggerEditor, &TriggerEditorWidget::PointerChainSelected, this, [this](const PointerChainArgs& e) {
        codeNoteEditor->HighlightAddress(e.BaseAddress);
        if (!e.Offsets.isEmpty())
            codeNoteEditor->HighlightVariable(e.Offsets);
    });

    // Trigger Editor -> Code Note Editor Bridge for Clone Stride
    connect(triggerEditor, &TriggerEditorWidget::CloneStrideRequested, this, [this]() { codeNoteEditor->OpenCloneStrideDialog(); });

    // Code Reviewer
    connect(codeReviewControl, &CodeReviewWidget::IssueSelected, this, [this](ValidationIssue issue) {
        if (!issue.NoteAddress.isEmpty()) {
            codeNoteEditor->HighlightAddress(issue.NoteAddress);
            return;
        }
        if (issue.Asset == nullptr)
            return;
        if (_selectedAsset == nullptr || _selectedAsset->ID != issue.Asset->ID ||
            _selectedAsset->isLeaderboard() != issue.Asset->isLeaderboard())
            SelectAssetInGrid(issue.Asset);
        if (issue.Target.has_value())
            triggerEditor->SelectCondition(&issue.Target.value(), issue.SegmentName, issue.GroupIndex);
        else if (!issue.TargetField.isEmpty())
            triggerEditor->FocusHeaderField(issue.TargetField);
    });

    connect(codeReviewControl, &CodeReviewWidget::SuggestedFixRequested, this, &MainWindow::onApplySuggestedFix);

    // Code Note Editor actions
    connect(codeNoteEditor, &CodeNoteEditorWidget::ReloadRequested, this, &MainWindow::onReload);
    connect(codeNoteEditor, &CodeNoteEditorWidget::NoteDirtyStateChanged, this, [this]() {
        if (!_isLoading)
            _isDirty = true;
    });
    connect(codeNoteEditor, &CodeNoteEditorWidget::BeforeBatchNotesChange, this, [this]() {
        _projectRepo.EnsureLocalDataExists();
        PushMainUndo();
    });
    connect(codeNoteEditor, &CodeNoteEditorWidget::BatchNotesApplied, this, [this]() {
        _notesCacheDirty = true;
        RunGlobalValidation();
    });

    // Real-time grid updates & bidirectional note synchronization
    connect(codeNoteEditor, &CodeNoteEditorWidget::NoteUpdated, this, [this]() {
        if (_isLoading || _projectRepo.LocalData == nullptr)
            return;

        // NoteContentChanged also fires for selection changes. Only treat this as a
        // real edit when the note list actually differs; otherwise merely browsing
        // notes would mark the project dirty and prompt on close.
        const QList<CodeNote> notes = codeNoteEditor->GetAllNotes();
        if (_projectRepo.LocalData->Notes == notes)
            return;

        _projectRepo.LocalData->Notes = notes;
        _notesCacheDirty = true;
        _isDirty = true;
        triggerEditor->SetNotes(notes);
        RunGlobalValidation();
    });

    // Resets current unsaved note edits back to the last saved disk state
    connect(codeNoteEditor, &CodeNoteEditorWidget::NoteResetRequested, this, [this](const QString& address) {
        if (_projectRepo.LocalData == nullptr)
            return;

        PushMainUndo();
        _projectRepo.ResetNote(address);

        auto notes = _projectRepo.LocalData->Notes;
        codeNoteEditor->SetNotes(notes);
        triggerEditor->SetNotes(notes);
        _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
        _notesCacheDirty = true;
        RunGlobalValidation();
    });

    // Reverts local note override back to the JSON server snapshot
    connect(codeNoteEditor, &CodeNoteEditorWidget::NoteRevertRequested, this, [this](const QString& address) {
        if (_projectRepo.LocalData == nullptr)
            return;

        PushMainUndo();
        _projectRepo.RevertNote(address);

        auto notes = _projectRepo.LocalData->Notes;
        codeNoteEditor->SetNotes(notes);
        triggerEditor->SetNotes(notes);
        _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
        _notesCacheDirty = true;
        RunGlobalValidation();
    });

    // Undo/Redo State Listeners
    _mainHistory.HistoryChanged = [this]() { UpdateUndoRedoState(); };
    connect(triggerEditor, &TriggerEditorWidget::HistoryChanged, this, &MainWindow::UpdateUndoRedoState);
    connect(codeNoteEditor, &CodeNoteEditorWidget::HistoryChanged, this, &MainWindow::UpdateUndoRedoState);
    connect(qApp, &QApplication::focusChanged, this, [this](QWidget*, QWidget*) { UpdateUndoRedoState(); });

    // Update Checker
    connect(&_updateChecker, &UpdateChecker::updateCheckFinished, this, &MainWindow::onUpdateCheckFinished);
    connect(&_updateChecker, &UpdateChecker::updateCheckFailed, this, &MainWindow::onUpdateCheckFailed);

    // Initial Load
    _recentFiles.Load();
    UpdateRecentMenu();
    ToggleReviewer(AppSettings::Current.ShowLogicReviewer);
    UpdateUndoRedoState();

    _updateChecker.checkForUpdates(true);
}

MainWindow::~MainWindow() = default;

QIcon MainWindow::getThemeIcon(const QString& resourcePath) const {
    return ThemeManager::getIcon(resourcePath, ThemeManager::CurrentTheme());
}

void MainWindow::updateThemeIcons() {
    const auto actionsList = findChildren<QAction*>();
    for (QAction* action : actionsList) {
        QVariant iconPathVar = action->property("iconPath");
        if (iconPathVar.isValid()) {
            action->setIcon(getThemeIcon(iconPathVar.toString()));
        }
    }
    if (triggerEditor)
        triggerEditor->updateThemeIcons();
    if (codeNoteEditor)
        codeNoteEditor->updateThemeIcons();
}

void MainWindow::InitializeUI() {
    setWindowTitle("RetroAchievements Logic Editor");
    resize(1200, 700);

    auto* centralWidget = new QWidget(this);
    setCentralWidget(centralWidget);
    auto* centralLayout = new QVBoxLayout(centralWidget);
    centralLayout->setContentsMargins(0, 0, 0, 0);

    splitMain = new QSplitter(Qt::Horizontal, centralWidget);

    // Left Panel: Grid & Filters
    auto* leftPanel = new QWidget(splitMain);
    leftPanel->setMinimumWidth(260);
    auto* leftLayout = new QVBoxLayout(leftPanel);
    leftLayout->setContentsMargins(2, 2, 2, 2);
    leftLayout->setSpacing(4);

    // Top Filter Bar (Fixed 60px height)
    auto* panelGridTop = new QWidget(leftPanel);
    panelGridTop->setFixedHeight(60);
    auto* topGridVBox = new QVBoxLayout(panelGridTop);
    topGridVBox->setContentsMargins(0, 0, 0, 0);
    topGridVBox->setSpacing(2);

    auto* filterRow1 = new QHBoxLayout();
    filterRow1->setContentsMargins(0, 0, 0, 0);
    filterRow1->setSpacing(4);

    // Proportional stretch allocation and explicit minimum widths prevent text truncation (e.g. "JSON", "Unofficial")
    cmbAssetType = new QComboBox(panelGridTop);
    cmbAssetType->setMinimumWidth(100);
    ThemeManager::SetupComboBox(cmbAssetType, 14);
    cmbAssetType->addItems({"Achievements", "Leaderboards"});
    filterRow1->addWidget(cmbAssetType, 11);

    cmbCategory = new QComboBox(panelGridTop);
    cmbCategory->setMinimumWidth(76);
    ThemeManager::SetupComboBox(cmbCategory, 14);
    cmbCategory->addItems({"Core", "Unofficial", "Local"});
    filterRow1->addWidget(cmbCategory, 8);

    cmbSource = new QComboBox(panelGridTop);
    cmbSource->setMinimumWidth(68);
    ThemeManager::SetupComboBox(cmbSource, 14);
    cmbSource->addItems({"Local", "JSON", "New"});
    filterRow1->addWidget(cmbSource, 7);
    topGridVBox->addLayout(filterRow1);

    auto* filterRow2 = new QHBoxLayout();
    filterRow2->setContentsMargins(0, 0, 0, 0);
    filterRow2->setSpacing(4);
    auto* lblSearch = new QLabel("Search:", panelGridTop);
    lblSearch->setFixedWidth(45);
    filterRow2->addWidget(lblSearch);
    txtAssetSearch = new QLineEdit(panelGridTop);
    txtAssetSearch->setFixedHeight(23);
    connect(txtAssetSearch, &QLineEdit::textChanged, this, &MainWindow::onSearchTextChanged);
    filterRow2->addWidget(txtAssetSearch, 1);
    topGridVBox->addLayout(filterRow2);

    leftLayout->addWidget(panelGridTop);

    achievementGrid = new QTableView(leftPanel);
    achievementGrid->setObjectName("achievementGrid");
    achievementGrid->setModel(&_assetModel);
    _assetDelegate = new AssetTableDelegate(achievementGrid);
    achievementGrid->setItemDelegate(_assetDelegate);
    achievementGrid->setSelectionBehavior(QAbstractItemView::SelectRows);
    achievementGrid->setSelectionMode(QAbstractItemView::ExtendedSelection);
    achievementGrid->verticalHeader()->setVisible(false);
    achievementGrid->verticalHeader()->setDefaultSectionSize(22);
    // Custom delegate draws the clean 1px thin gridline
    achievementGrid->setShowGrid(false);
    achievementGrid->horizontalHeader()->setFixedHeight(24);
    achievementGrid->horizontalHeader()->setHighlightSections(true);
    achievementGrid->horizontalHeader()->setSectionResizeMode(1, QHeaderView::Stretch);
    achievementGrid->setColumnWidth(0, 80);
    achievementGrid->setSortingEnabled(true);
    achievementGrid->installEventFilter(this);

    connect(achievementGrid->selectionModel(), &QItemSelectionModel::selectionChanged, this, &MainWindow::onAssetSelectionChanged);
    connect(achievementGrid, &QTableView::doubleClicked, this, &MainWindow::onAssetDoubleClicked);

    leftLayout->addWidget(achievementGrid, 1);

    // Bottom Button Bar (Fixed 35px height)
    auto* panelGridBottom = new QWidget(leftPanel);
    panelGridBottom->setFixedHeight(35);
    auto* bottomBtnLayout = new QHBoxLayout(panelGridBottom);
    bottomBtnLayout->setContentsMargins(3, 6, 3, 6);
    bottomBtnLayout->setSpacing(6);

    btnAddAsset = new QPushButton("Add", panelGridBottom);
    btnAddAsset->setFixedSize(73, 23);
    connect(btnAddAsset, &QPushButton::clicked, this, &MainWindow::onNewAsset);
    bottomBtnLayout->addWidget(btnAddAsset);

    bottomBtnLayout->addStretch();

    btnDuplicateAsset = new QPushButton("Duplicate", panelGridBottom);
    btnDuplicateAsset->setFixedSize(75, 23);
    btnDuplicateAsset->setEnabled(false);
    connect(btnDuplicateAsset, &QPushButton::clicked, this, &MainWindow::onDuplicateAsset);
    bottomBtnLayout->addWidget(btnDuplicateAsset);

    btnDeleteAsset = new QPushButton("Delete", panelGridBottom);
    btnDeleteAsset->setFixedSize(75, 23);
    btnDeleteAsset->setEnabled(false);
    connect(btnDeleteAsset, &QPushButton::clicked, this, &MainWindow::onDeleteAsset);
    bottomBtnLayout->addWidget(btnDeleteAsset);

    leftLayout->addWidget(panelGridBottom);
    splitMain->addWidget(leftPanel);

    // Right Splitter: Editor & Code Notes
    splitEditor = new QSplitter(Qt::Horizontal, splitMain);

    // Middle: Trigger Editor + Reviewer
    splitMiddle = new QSplitter(Qt::Vertical, splitEditor);
    splitMiddle->setCollapsible(0, false);
    splitMiddle->setCollapsible(1, false);
    triggerEditor = new TriggerEditorWidget(splitMiddle);
    splitMiddle->addWidget(triggerEditor);
    codeReviewControl = new CodeReviewWidget(splitMiddle);
    splitMiddle->addWidget(codeReviewControl);
    splitEditor->addWidget(splitMiddle);

    codeNoteEditor = new CodeNoteEditorWidget(splitEditor);
    splitEditor->addWidget(codeNoteEditor);

    splitMain->addWidget(splitEditor);
    splitMain->setStretchFactor(0, 0);
    splitMain->setStretchFactor(1, 1);
    splitMain->setSizes({260, 940});
    splitEditor->setStretchFactor(0, 1);
    splitEditor->setStretchFactor(1, 0);
    splitEditor->setSizes({600, 340});
    splitMiddle->setStretchFactor(0, 1);
    splitMiddle->setStretchFactor(1, 0);
    splitMiddle->setSizes({400, 250});

    centralLayout->addWidget(splitMain);

    InitializeFilters();
    InitializeMainMenu();
    InitializeAssetContextMenu();
}

void MainWindow::InitializeFilters() {
    cmbSource->clear();
    cmbSource->addItems({"JSON", "Local", "New"});
    cmbCategory->clear();
    cmbCategory->addItems({"Core", "Unofficial"});

    // Restore previous filter state
    int lastType = AppSettings::Current.LastAssetTypeIndex;
    if (lastType >= 0 && lastType < cmbAssetType->count())
        cmbAssetType->setCurrentIndex(lastType);

    int lastCat = AppSettings::Current.LastCategoryIndex;
    if (lastCat >= 0 && lastCat < cmbCategory->count())
        cmbCategory->setCurrentIndex(lastCat);

    int lastSrc = AppSettings::Current.LastSourceIndex;
    if (lastSrc >= 0 && lastSrc < cmbSource->count())
        cmbSource->setCurrentIndex(lastSrc);

    // Wiring
    connect(cmbAssetType, QOverload<int>::of(&QComboBox::currentIndexChanged), this, [this]() { ReloadDataView(); });
    connect(cmbSource, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &MainWindow::onFilterChanged);
    connect(cmbCategory, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &MainWindow::onFilterChanged);
}

void MainWindow::InitializeMainMenu() {
    auto* menuBar = this->menuBar();

    // File Menu
    auto* fileMenu = menuBar->addMenu("&File");
    auto* actOpen = fileMenu->addAction(getThemeIcon(":/icons/load.svg"), "&Open...", this, &MainWindow::onOpenFile);
    actOpen->setShortcut(QKeySequence::Open);
    actOpen->setProperty("iconPath", ":/icons/load.svg");

    auto* actSave = fileMenu->addAction(getThemeIcon(":/icons/save.svg"), "&Save User File", this, &MainWindow::onSaveFile);
    actSave->setShortcut(QKeySequence::Save);
    actSave->setProperty("iconPath", ":/icons/save.svg");

    recentMenu = fileMenu->addMenu("Recent Files");

    // Edit Menu
    auto* editMenu = menuBar->addMenu("&Edit");
    actUndo = editMenu->addAction(getThemeIcon(":/icons/undo.svg"), "Undo", this, &MainWindow::onUndo);
    actUndo->setShortcut(QKeySequence::Undo);
    actUndo->setProperty("iconPath", ":/icons/undo.svg");

    actRedo = editMenu->addAction(getThemeIcon(":/icons/redo.svg"), "Redo", this, &MainWindow::onRedo);
    actRedo->setShortcut(QKeySequence::Redo);
    actRedo->setProperty("iconPath", ":/icons/redo.svg");

    auto* actRenumber =
        editMenu->addAction(getThemeIcon(":/icons/renumber.svg"), "Renumber Local IDs", this, &MainWindow::onRenumberLocalIDs);
    actRenumber->setProperty("iconPath", ":/icons/renumber.svg");

    auto* actClearData =
        editMenu->addAction(getThemeIcon(":/icons/refresh.svg"), "Clear Data / Reset...", this, &MainWindow::onClearDataReset);
    actClearData->setProperty("iconPath", ":/icons/refresh.svg");

    editMenu->addSeparator();

    auto* actRevertRp =
        editMenu->addAction(getThemeIcon(":/icons/undo.svg"), "Revert Rich Presence", this, &MainWindow::onRevertRichPresence);
    actRevertRp->setProperty("iconPath", ":/icons/undo.svg");

    editMenu->addSeparator();

    auto* actSettings = editMenu->addAction(getThemeIcon(":/icons/settings.svg"), "Settings...", this, &MainWindow::onSettings);
    actSettings->setShortcut(QKeySequence("Ctrl+,"));
    actSettings->setProperty("iconPath", ":/icons/settings.svg");

    // View Menu
    auto* viewMenu = menuBar->addMenu("&View");
    actViewReviewer =
        viewMenu->addAction(getThemeIcon(":/icons/logic_reviewer.svg"), "Logic Reviewer", this, &MainWindow::onToggleReviewer);
    actViewReviewer->setShortcut(QKeySequence(Qt::Key_F8));
    actViewReviewer->setCheckable(true);
    actViewReviewer->setProperty("iconPath", ":/icons/logic_reviewer.svg");

    auto* actRpDiff = viewMenu->addAction(getThemeIcon(":/icons/diff.svg"), "View RP Diff", this, &MainWindow::onViewRpDiff);
    actRpDiff->setProperty("iconPath", ":/icons/diff.svg");

    // Help Menu
    auto* helpMenu = menuBar->addMenu("&Help");
    actCheckForUpdates =
        helpMenu->addAction(getThemeIcon(":/icons/update.svg"), "Check for &Updates...", this, &MainWindow::onCheckForUpdates);
    actCheckForUpdates->setProperty("iconPath", ":/icons/update.svg");

    helpMenu->addSeparator();

    actAbout = helpMenu->addAction(getThemeIcon(":/icons/about.svg"), "About RetroAchievements Logic Editor", this, &MainWindow::onAbout);
    actAbout->setProperty("iconPath", ":/icons/about.svg");
}

void MainWindow::onCheckForUpdates() {
    if (_updateProgressDialog) {
        return;
    }

    _updateCheckCanceled = false;
    _updateProgressDialog = new QProgressDialog("Checking for updates...", "Cancel", 0, 0, this);
    _updateProgressDialog->setWindowTitle("Software Update");
    _updateProgressDialog->setWindowModality(Qt::WindowModal);
    _updateProgressDialog->setMinimumDuration(0);
    _updateProgressDialog->setAutoClose(false);
    _updateProgressDialog->setAutoReset(false);
    _updateProgressDialog->setAttribute(Qt::WA_DeleteOnClose);
    connect(_updateProgressDialog, &QProgressDialog::canceled, this, [this]() {
        _updateCheckCanceled = true;
        _updateChecker.cancelManualCheck();
    });
    _updateProgressDialog->show();

    _updateChecker.checkForUpdates(false);
}

void MainWindow::onUpdateCheckFinished(bool updateAvailable, const QString& latestVersion, const QString& releaseNotes,
                                       const QString& downloadUrl, bool silentOnLatest) {
    if (!silentOnLatest && _updateProgressDialog) {
        // Disconnect before hiding so QProgressDialog::closeEvent does not emit canceled().
        _updateProgressDialog->disconnect(this);
        _updateProgressDialog->hide();
        _updateProgressDialog->deleteLater();
        _updateProgressDialog = nullptr;
    }

    if (!silentOnLatest && _updateCheckCanceled) {
        return;
    }

    if (updateAvailable) {
        const QString normLatest = NormalizeVersionTag(latestVersion);
        const QString normSkipped = NormalizeVersionTag(AppSettings::Current.SkippedUpdateVersion);

        if (silentOnLatest && !normSkipped.isEmpty() && normLatest.compare(normSkipped, Qt::CaseInsensitive) == 0) {
            return;
        }

        if (_updateDialog) {
            _updateDialog->raise();
            _updateDialog->activateWindow();
            return;
        }

        _updateDialog = new UpdateDialog(VERSION_STRING, latestVersion, releaseNotes, downloadUrl, this);
        _updateDialog->setAttribute(Qt::WA_DeleteOnClose);
        connect(_updateDialog, &QDialog::finished, this, [this, latestVersion, normLatest](int) {
            if (!_updateDialog)
                return;

            const auto action = _updateDialog->SelectedAction();
            if (action == UpdateDialog::Action::SkipUpdate) {
                AppSettings::Current.SkippedUpdateVersion = latestVersion;
                AppSettings::Save();
            } else if (action == UpdateDialog::Action::RemindLater) {
                const QString normSkippedCurrent = NormalizeVersionTag(AppSettings::Current.SkippedUpdateVersion);
                if (!normSkippedCurrent.isEmpty() && normLatest.compare(normSkippedCurrent, Qt::CaseInsensitive) == 0) {
                    AppSettings::Current.SkippedUpdateVersion.clear();
                    AppSettings::Save();
                }
            }
        });
        _updateDialog->show();
    } else if (!silentOnLatest) {
        QMessageBox::information(
            this, "Software Update",
            QString("You're running the latest version of RALE.\n\nCurrent Version: %1").arg(VERSION_STRING));
    }
}

void MainWindow::onUpdateCheckFailed(const QString& errorMessage, bool silentOnLatest) {
    if (!silentOnLatest && _updateProgressDialog) {
        _updateProgressDialog->disconnect(this);
        _updateProgressDialog->hide();
        _updateProgressDialog->deleteLater();
        _updateProgressDialog = nullptr;
    }

    if (!silentOnLatest && !_updateCheckCanceled) {
        QMessageBox::warning(this, "Update Check Failed", QString("Could not reach update server:\n%1").arg(errorMessage));
    }
}

void MainWindow::onAbout() {
    AboutDialog dialog(this);
    dialog.exec();
}

void MainWindow::onSettings() {
    SettingsDialog dialog(this);
    dialog.exec();

    // Re-apply live settings to CodeNoteEditor
    codeNoteEditor->SetUnicodeTree(AppSettings::Current.CodeNoteUnicodeTree);
    codeNoteEditor->SetIndentValues(AppSettings::Current.CodeNoteIndentDescriptions);
    updateThemeIcons();
}

void MainWindow::InitializeAssetContextMenu() {
    assetContextMenu = new QMenu(this);

    actNewAsset = assetContextMenu->addAction(getThemeIcon(":/icons/add.svg"), "New", this, &MainWindow::onNewAsset);
    actNewAsset->setProperty("iconPath", ":/icons/add.svg");

    actDuplicateAsset = assetContextMenu->addAction(getThemeIcon(":/icons/copy.svg"), "Duplicate", this, &MainWindow::onDuplicateAsset);
    actDuplicateAsset->setShortcut(QKeySequence("Ctrl+D"));
    actDuplicateAsset->setProperty("iconPath", ":/icons/copy.svg");

    actDeleteAsset = assetContextMenu->addAction(getThemeIcon(":/icons/delete.svg"), "Delete", this, &MainWindow::onDeleteAsset);
    actDeleteAsset->setShortcut(QKeySequence::Delete);
    actDeleteAsset->setProperty("iconPath", ":/icons/delete.svg");

    actResetAsset = assetContextMenu->addAction(getThemeIcon(":/icons/refresh.svg"), "Reset", this, &MainWindow::onResetAsset);
    actResetAsset->setProperty("iconPath", ":/icons/refresh.svg");

    actRevertAsset =
        assetContextMenu->addAction(getThemeIcon(":/icons/undo.svg"), "Revert to Server", this, &MainWindow::onRevertAsset);
    actRevertAsset->setProperty("iconPath", ":/icons/undo.svg");

    assetContextMenu->addSeparator();

    actMoveUp = assetContextMenu->addAction(getThemeIcon(":/icons/sort.svg"), "Move Up", this, &MainWindow::onMoveAssetUp);
    actMoveUp->setShortcut(QKeySequence("Alt+Up"));
    actMoveUp->setProperty("iconPath", ":/icons/sort.svg");

    actMoveDown = assetContextMenu->addAction(getThemeIcon(":/icons/sort.svg"), "Move Down", this, &MainWindow::onMoveAssetDown);
    actMoveDown->setShortcut(QKeySequence("Alt+Down"));
    actMoveDown->setProperty("iconPath", ":/icons/sort.svg");

    assetContextMenu->addSeparator();

    actRestoreOrder =
        assetContextMenu->addAction(getThemeIcon(":/icons/refresh.svg"), "Restore Default Order", this, &MainWindow::onRestoreOrder);
    actRestoreOrder->setProperty("iconPath", ":/icons/refresh.svg");

    assetContextMenu->addSeparator();

    actRenumberIDs =
        assetContextMenu->addAction(getThemeIcon(":/icons/renumber.svg"), "Renumber Local IDs", this, &MainWindow::onRenumberLocalIDs);
    actRenumberIDs->setProperty("iconPath", ":/icons/renumber.svg");

    actViewDiff =
        assetContextMenu->addAction(getThemeIcon(":/icons/diff.svg"), "View Diff (Server/Local)", this, &MainWindow::onViewDiff);
    actViewDiff->setProperty("iconPath", ":/icons/diff.svg");

    assetContextMenu->addSeparator();

    actRevertRegion =
        assetContextMenu->addAction(getThemeIcon(":/icons/undo.svg"), "Revert Multi-Region...", this, &MainWindow::onRevertRegion);
    actRevertRegion->setProperty("iconPath", ":/icons/undo.svg");

    achievementGrid->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(achievementGrid, &QWidget::customContextMenuRequested, this, [this](const QPoint& pos) {
        UpdateSelectionUI();
        assetContextMenu->exec(achievementGrid->viewport()->mapToGlobal(pos));
    });
}

bool MainWindow::eventFilter(QObject* watched, QEvent* event) {
    if (watched == achievementGrid && event->type() == QEvent::KeyPress) {
        auto* keyEvent = static_cast<QKeyEvent*>(event);

        if (keyEvent->key() == Qt::Key_Return || keyEvent->key() == Qt::Key_Enter) {
            LoadSelectedAsset();
            return true;
        } else if (keyEvent->modifiers() == Qt::AltModifier && keyEvent->key() == Qt::Key_Up) {
            if (actMoveUp && actMoveUp->isEnabled())
                onMoveAssetUp();
            return true;
        } else if (keyEvent->modifiers() == Qt::AltModifier && keyEvent->key() == Qt::Key_Down) {
            if (actMoveDown && actMoveDown->isEnabled())
                onMoveAssetDown();
            return true;
        } else if (keyEvent->key() == Qt::Key_Delete) {
            // C# deletes the Local subset of a mixed selection, so only require a selection.
            if (!achievementGrid->selectionModel()->selectedRows().isEmpty())
                onDeleteAsset();
            return true;
        } else if (keyEvent->modifiers() == Qt::ControlModifier && keyEvent->key() == Qt::Key_D) {
            if (btnDuplicateAsset && btnDuplicateAsset->isEnabled())
                onDuplicateAsset();
            return true;
        }
    }

    return QMainWindow::eventFilter(watched, event);
}

void MainWindow::showEvent(QShowEvent* event) {
    QMainWindow::showEvent(event);
    if (_isFirstShow) {
        _isFirstShow = false;
        // Defer splitter restoration until after ShowWindow / Qt::WindowMaximized layout completes
        QTimer::singleShot(0, this, [this]() {
            RestoreSplitterSizes();
            _splittersRestored = true;
        });
    }
}

void MainWindow::RestoreSplitterSizes() {
    if (AppSettings::Current.MainSplitterDistance > 0 && splitMain->width() > 0) {
        int avail = std::max(460, splitMain->width() - splitMain->handleWidth());
        int maxLeft = std::max(260, avail - 200);
        int leftW = std::clamp(AppSettings::Current.MainSplitterDistance, 260, maxLeft);
        int rightW = std::max(200, avail - leftW);
        splitMain->setSizes({leftW, rightW});
    }

    if (splitMain->layout() != nullptr)
        splitMain->layout()->activate();

    int editorTotalW = splitEditor->width();
    if (editorTotalW <= 0 && splitMain->sizes().size() >= 2)
        editorTotalW = splitMain->sizes().at(1);

    if (editorTotalW > 0) {
        int avail = std::max(320, editorTotalW - splitEditor->handleWidth());
        int rightW = -1;
        if (AppSettings::Current.CodeNoteSplitterDistance > 0) {
            int maxRight = std::max(120, avail - 200);
            rightW = std::clamp(AppSettings::Current.CodeNoteSplitterDistance, 120, maxRight);
        } else if (AppSettings::Current.EditorSplitterDistance > 0) {
            int maxLeft = std::max(200, avail - 120);
            int leftW = std::clamp(AppSettings::Current.EditorSplitterDistance, 200, maxLeft);
            rightW = std::max(120, avail - leftW);
        }

        if (rightW > 0) {
            int leftW = std::max(200, avail - rightW);
            splitEditor->setSizes({leftW, rightW});
        }
    }

    if (codeReviewControl != nullptr && codeReviewControl->isVisible() && splitMiddle->height() > 0) {
        int avail = std::max(160, splitMiddle->height() - splitMiddle->handleWidth());
        int bottomH = -1;
        if (AppSettings::Current.VerticalSplitterBottomDistance > 0) {
            int maxBottom = std::max(60, avail - 100);
            bottomH = std::clamp(AppSettings::Current.VerticalSplitterBottomDistance, 60, maxBottom);
        } else if (AppSettings::Current.VerticalSplitterDistance > 0) {
            int maxTop = std::max(100, avail - 60);
            int topH = std::clamp(AppSettings::Current.VerticalSplitterDistance, 100, maxTop);
            bottomH = std::max(60, avail - topH);
        }

        if (bottomH > 0) {
            int topH = std::max(100, avail - bottomH);
            splitMiddle->setSizes({topH, bottomH});
        }
    }
}

void MainWindow::ApplySettings() {
    AppSettings::LoadWindowGeometry("MainWindow", this);

    triggerEditor->SetShowDecimal(AppSettings::Current.Trigger_ShowDecimal);
    triggerEditor->SetShowAliases(AppSettings::Current.Trigger_ShowAliases);
    triggerEditor->SetCollapseChains(AppSettings::Current.Trigger_CollapseChains);
    codeNoteEditor->SetShowLeafsOnly(AppSettings::Current.Notes_ShowLeafsOnly);
    codeNoteEditor->SetIndentValues(AppSettings::Current.CodeNoteIndentDescriptions);
    codeNoteEditor->SetUnicodeTree(AppSettings::Current.CodeNoteUnicodeTree);
    codeReviewControl->SetFilterIndex(AppSettings::Current.CodeReview_FilterIndex);
}

void MainWindow::SaveSettings() {
    const bool minimized = isMinimized();

    AppSettings::SaveWindowGeometry("MainWindow", this);

    // Never persist splitter sizes while minimized (Qt reports collapsed sizes).
    if (!minimized && _splittersRestored) {
        const QList<int> mainSizes = splitMain->sizes();
        if (mainSizes.size() >= 2)
            AppSettings::Current.MainSplitterDistance = mainSizes.at(0);

        const QList<int> editorSizes = splitEditor->sizes();
        if (editorSizes.size() >= 2) {
            AppSettings::Current.EditorSplitterDistance = editorSizes.at(0);
            AppSettings::Current.CodeNoteSplitterDistance = editorSizes.at(1);
        }

        if (codeReviewControl != nullptr && codeReviewControl->isVisible()) {
            const QList<int> midSizes = splitMiddle->sizes();
            if (midSizes.size() >= 2) {
                AppSettings::Current.VerticalSplitterDistance = midSizes.at(0);
                AppSettings::Current.VerticalSplitterBottomDistance = midSizes.at(1);
            }
        }
    }

    AppSettings::Current.LastAssetTypeIndex = cmbAssetType->currentIndex();
    AppSettings::Current.LastCategoryIndex = cmbCategory->currentIndex();
    AppSettings::Current.LastSourceIndex = cmbSource->currentIndex();
    AppSettings::Current.CodeReview_FilterIndex = codeReviewControl->FilterIndex();

    AppSettings::Current.Trigger_ShowDecimal = triggerEditor->ShowDecimal();
    AppSettings::Current.Trigger_ShowAliases = triggerEditor->ShowAliases();
    AppSettings::Current.Trigger_CollapseChains = triggerEditor->CollapseChains();

    AppSettings::Current.Notes_ShowLeafsOnly = codeNoteEditor->ShowLeafsOnly();
    AppSettings::Current.CodeNoteIndentDescriptions = codeNoteEditor->IndentValues();
    AppSettings::Current.CodeNoteUnicodeTree = codeNoteEditor->UnicodeTree();
    AppSettings::Current.ShowLogicReviewer = actViewReviewer->isChecked();

    AppSettings::Save();
}

void MainWindow::ToggleReviewer(std::optional<bool> forceState) {
    bool currentlyVisible = codeReviewControl->isVisible();
    bool newState = forceState.has_value() ? forceState.value() : !currentlyVisible;

    if (!newState && currentlyVisible && _splittersRestored) {
        const QList<int> midSizes = splitMiddle->sizes();
        if (midSizes.size() >= 2) {
            AppSettings::Current.VerticalSplitterDistance = midSizes.at(0);
            AppSettings::Current.VerticalSplitterBottomDistance = midSizes.at(1);
        }
    }

    actViewReviewer->setChecked(newState);
    codeReviewControl->setVisible(newState);

    if (newState) {
        int totalH = std::max(160, splitMiddle->height() - splitMiddle->handleWidth());
        int bottomH = -1;
        if (AppSettings::Current.VerticalSplitterBottomDistance > 0) {
            int maxBottom = std::max(60, totalH - 100);
            bottomH = std::clamp(AppSettings::Current.VerticalSplitterBottomDistance, 60, maxBottom);
        } else if (AppSettings::Current.VerticalSplitterDistance > 0 && AppSettings::Current.VerticalSplitterDistance < totalH - 60) {
            bottomH = std::max(60, totalH - AppSettings::Current.VerticalSplitterDistance);
        } else {
            bottomH = std::max(60, totalH * 40 / 100);
        }
        int topH = std::max(100, totalH - bottomH);
        splitMiddle->setSizes({topH, bottomH});
    }
}

void MainWindow::UpdateUndoRedoState() {
    QWidget* focus = QApplication::focusWidget();
    bool isTriggerContext = focus && (focus == triggerEditor || triggerEditor->isAncestorOf(focus));
    bool isNoteContext = focus && (focus == codeNoteEditor || codeNoteEditor->isAncestorOf(focus));

    if (isTriggerContext) {
        actUndo->setEnabled(triggerEditor->CanUndo() || _mainHistory.CanUndo());
        actRedo->setEnabled(triggerEditor->CanRedo() || _mainHistory.CanRedo());
    } else if (isNoteContext) {
        actUndo->setEnabled(codeNoteEditor->CanUndo() || _mainHistory.CanUndo());
        actRedo->setEnabled(codeNoteEditor->CanRedo() || _mainHistory.CanRedo());
    } else {
        actUndo->setEnabled(_mainHistory.CanUndo());
        actRedo->setEnabled(_mainHistory.CanRedo());
    }
}

void MainWindow::UpdateRecentMenu() {
    recentMenu->clear();
    for (const auto& entry : _recentFiles.Files) {
        QString text = entry.GameTitle.isEmpty() ? entry.Path : QString("%1 | %2").arg(entry.GameTitle, entry.Path);
        QString path = entry.Path;
        auto* action = recentMenu->addAction(text, this, [this, path]() {
            // Defer: LoadFiles rebuilds this menu, which would delete the QAction
            // whose triggered() signal is still on the stack.
            QTimer::singleShot(0, this, [this, path]() {
                if (ConfirmDiscardChanges()) {
                    txtAssetSearch->clear();
                    LoadFiles(path);
                }
            });
        });
        action->setIcon(getThemeIcon(":/icons/load.svg"));
        action->setProperty("iconPath", ":/icons/load.svg");
    }
}

bool MainWindow::ConfirmDiscardChanges() {
    if (!_isDirty)
        return true;

    auto result =
        QMessageBox::warning(this, "Unsaved Changes", "You have unsaved changes. Any unsaved local changes will be lost.\n\nContinue?",
                             QMessageBox::Yes | QMessageBox::No, QMessageBox::No);

    return result == QMessageBox::Yes;
}

void MainWindow::closeEvent(QCloseEvent* event) {
    if (!ConfirmDiscardChanges()) {
        event->ignore();
        return;
    }
    SaveSettings();
    event->accept();
}

// Full Project Load Logic
void MainWindow::LoadFiles(const QString& primaryPath) {
    _isLoading = true;

    try {
        ImageCache::Clear();
        ImageCache::ConfigureLocalPaths(primaryPath);

        _recentFiles.Add(primaryPath);
        UpdateRecentMenu();

        _currentFilePath = primaryPath;
        _selectedAsset = nullptr;
        _currentAssetDirty = false;
        _currentDisplayList.clear();
        _mainHistory.Clear();
        // Drop the old model/selection before loading; otherwise ReloadDataView can
        // carry an ID over and auto-select an unrelated asset in the new project.
        achievementGrid->clearSelection();
        _assetModel.SetAssets({});
        triggerEditor->Clear();

        try {
            _projectRepo.LoadFiles(primaryPath, _currentUserFilePath);
            _rpMetadata.Load(_projectRepo.CurrentGameId);
        } catch (const std::exception& ex) {
            QMessageBox::critical(this, "Load Error", QString("Error loading files: %1").arg(ex.what()));
            _isLoading = false;
            return;
        }

        if (_projectRepo.ServerData != nullptr)
            this->setWindowTitle(QString("RetroAchievements Logic Editor - %1").arg(_projectRepo.CurrentTitle));
        else
            this->setWindowTitle(QString("RetroAchievements Logic Editor - %1").arg(QFileInfo(primaryPath).fileName()));

        int consoleId = (_projectRepo.ServerData != nullptr) ? _projectRepo.ServerData->ConsoleId : 0;
        codeNoteEditor->SetConsoleId(consoleId);

        bool hasServerAssets = false;
        if (_projectRepo.ServerData != nullptr) {
            for (const auto& s : _projectRepo.ServerData->Sets) {
                if (!s.Achievements.isEmpty() || !s.Leaderboards.isEmpty()) {
                    hasServerAssets = true;
                    break;
                }
            }
        }

        QString targetSource = hasServerAssets ? "JSON" : "New";

        if (cmbSource->currentText() != targetSource) {
            int idx = cmbSource->findText(targetSource);
            if (idx != -1)
                cmbSource->setCurrentIndex(idx);
        }

        ReloadDataView();

        auto notes = (_projectRepo.LocalData != nullptr) ? _projectRepo.LocalData->Notes : QList<CodeNote>();
        codeNoteEditor->SetServerNotes(_projectRepo.GetServerNotes());
        codeNoteEditor->SetSavedNotes(_projectRepo.GetSavedNotes());
        codeNoteEditor->SetNotes(notes);
        triggerEditor->SetNotes(notes);

        _notesCacheDirty = true;
        RunGlobalValidation();
        UpdateUndoRedoState();
        UpdateTriggerActionButtons();

        _rpDirty = false;
        _isDirty = false;
    } catch (...) {
    }

    _isLoading = false;
}

void MainWindow::onFilterChanged() { ReloadDataView(); }

void MainWindow::onSearchTextChanged(const QString& /*text*/) { ReloadDataView(); }

void MainWindow::ReloadDataView() {
    int savedId = -1;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty()) {
        auto item = _assetModel.GetAsset(selectedRows.first().row());
        if (item != nullptr)
            savedId = item->ID;
    }

    PopulateList();

    if (savedId != -1) {
        int row = _assetModel.FindRowByAssetId(savedId);
        if (row != -1) {
            achievementGrid->selectRow(row);
        }
    }
}

void MainWindow::PopulateList() {
    _currentDisplayList.clear();

    bool showLeaderboards = (cmbAssetType->currentIndex() == 1);
    QString sourceMode = cmbSource->currentText();
    QString categoryFilter = cmbCategory->currentText();
    QString filterText = txtAssetSearch->text().trimmed();

    auto mergedList = _projectRepo.GetMergedAssets(showLeaderboards);

    for (const auto& item : mergedList) {
        UpdateAssetState(item);

        if (sourceMode == "New") {
            if (item->State != AssetState::Local)
                continue;
        } else if (sourceMode == "JSON") {
            if (item->State == AssetState::Local)
                continue;
        } else {
            if (item->State == AssetState::Synced)
                continue;
        }

        if (sourceMode != "New") {
            QString itemCat = item->Category.isEmpty() ? "Local" : item->Category;
            if (itemCat.compare(categoryFilter, Qt::CaseInsensitive) != 0)
                continue;
        }

        if (!filterText.isEmpty()) {
            bool matchesId = QString::number(item->ID).contains(filterText);
            bool matchesTitle = item->Title.contains(filterText, Qt::CaseInsensitive);
            if (!matchesId && !matchesTitle)
                continue;
        }

        _currentDisplayList.append(item);
    }

    _assetModel.SetAssets(_currentDisplayList);
    UpdateSelectionUI();
}

void MainWindow::LoadSelectedAsset() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty()) {
        int rowIdx = selectedRows.first().row();
        auto asset = _assetModel.GetAsset(rowIdx);
        if (asset != nullptr) {
            _isLoading = true;
            _currentAssetDirty = false;
            if (asset->State == AssetState::Synced) {
                auto clone = asset->clone(false);
                _selectedAsset = clone;
                int dispIdx = _currentDisplayList.indexOf(asset);
                if (dispIdx != -1)
                    _currentDisplayList[dispIdx] = clone;
                _assetModel.ReplaceAsset(rowIdx, clone);
            } else {
                _selectedAsset = asset;
            }

            std::unique_ptr<IAssetStrategy> strategy;
            if (_selectedAsset->isLeaderboard())
                strategy = std::make_unique<LeaderboardStrategy>();
            else
                strategy = std::make_unique<AchievementStrategy>();

            auto segments = strategy->ParseLogic(_selectedAsset->Logic);
            for (auto& groupList : segments)
                _projectRepo.Metadata.ApplyNamesToAsset(_selectedAsset->ID, groupList);

            QString displayCat =
                _selectedAsset->Category.isEmpty() ? "Core" : _selectedAsset->Category.left(1).toUpper() + _selectedAsset->Category.mid(1);

            triggerEditor->BindAchievement(_selectedAsset, displayCat);
            _isLoading = false;
        }
    }
    UpdateSelectionUI();
    UpdateTriggerActionButtons();
}

void MainWindow::RefreshSelectedAssetAfterStoreChange() {
    if (_selectedAsset == nullptr) {
        LoadSelectedAsset();
        return;
    }

    const int targetId = _selectedAsset->ID;
    const bool isLeaderboard = _selectedAsset->isLeaderboard();
    int row = _assetModel.FindRowByAssetId(targetId);
    if (row != -1) {
        achievementGrid->selectRow(row);
        LoadSelectedAsset();
        return;
    }

    std::shared_ptr<IAssetItem> updatedAsset = nullptr;
    for (const auto& item : _projectRepo.GetMergedAssets(isLeaderboard)) {
        if (item->ID == targetId) {
            updatedAsset = item;
            break;
        }
    }

    if (updatedAsset != nullptr) {
        _isLoading = true;
        _currentAssetDirty = false;
        _selectedAsset = (updatedAsset->State == AssetState::Synced) ? updatedAsset->clone(false) : updatedAsset;
        QString displayCat =
            _selectedAsset->Category.isEmpty() ? "Core" : _selectedAsset->Category.left(1).toUpper() + _selectedAsset->Category.mid(1);
        triggerEditor->BindAchievement(_selectedAsset, displayCat);
        _isLoading = false;
        UpdateSelectionUI();
        UpdateTriggerActionButtons();
    } else {
        _selectedAsset = nullptr;
        _currentAssetDirty = false;
        triggerEditor->Clear();
        UpdateSelectionUI();
        UpdateTriggerActionButtons();
    }
}

void MainWindow::UpdateAssetState(const std::shared_ptr<IAssetItem>& item) {
    if (item == nullptr)
        return;
    if (_projectRepo.ServerData == nullptr) {
        item->State = AssetState::Local;
        return;
    }

    std::shared_ptr<IAssetItem> serverItem = nullptr;
    for (const auto& s : _projectRepo.ServerData->Sets) {
        if (item->isLeaderboard()) {
            for (const auto& lb : s.Leaderboards)
                if (lb->ID == item->ID) {
                    serverItem = lb;
                    break;
                }
        } else {
            for (const auto& ach : s.Achievements)
                if (ach->ID == item->ID) {
                    serverItem = ach;
                    break;
                }
        }
        if (serverItem != nullptr)
            break;
    }

    if (serverItem == nullptr) {
        item->State = AssetState::Local;
    } else {
        bool equal = (item->Logic == serverItem->Logic && item->Title == serverItem->Title && item->Description == serverItem->Description);
        if (!item->isLeaderboard() && !serverItem->isLeaderboard()) {
            auto a = std::static_pointer_cast<RaAchievement>(item);
            auto b = std::static_pointer_cast<RaAchievement>(serverItem);
            if (a->Points != b->Points || a->Type != b->Type)
                equal = false;
        } else if (item->isLeaderboard() && serverItem->isLeaderboard()) {
            auto la = std::static_pointer_cast<RaLeaderboard>(item);
            auto lb = std::static_pointer_cast<RaLeaderboard>(serverItem);
            if (la->Format != lb->Format || la->LowerIsBetter != lb->LowerIsBetter)
                equal = false;
        }
        item->State = equal ? AssetState::Synced : AssetState::Modified;
    }

    // Immediately notify the asset model so modified rows update color in real time
    _assetModel.NotifyAssetChanged(item);
}

void MainWindow::UpdateSelectionUI() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    bool hasSelection = !selectedRows.isEmpty();
    bool allLocal = true;
    bool anyModified = false;
    bool anyUnsavedChanges = false;

    for (const auto& idx : selectedRows) {
        auto asset = _assetModel.GetAsset(idx.row());
        if (asset != nullptr) {
            if (asset->State != AssetState::Local)
                allLocal = false;
            if (asset->State == AssetState::Modified)
                anyModified = true;
            if (_projectRepo.HasUnsavedAssetChanges(asset.get()))
                anyUnsavedChanges = true;
        }
    }

    btnDuplicateAsset->setEnabled(hasSelection);
    btnDeleteAsset->setEnabled(hasSelection && allLocal);

    if (actDuplicateAsset)
        actDuplicateAsset->setEnabled(hasSelection);
    if (actDeleteAsset)
        actDeleteAsset->setEnabled(hasSelection && allLocal);
    if (actResetAsset)
        actResetAsset->setEnabled(hasSelection && anyUnsavedChanges);
    if (actRevertAsset)
        actRevertAsset->setEnabled(hasSelection && anyModified);
    if (actMoveUp)
        actMoveUp->setEnabled(hasSelection && allLocal);
    if (actMoveDown)
        actMoveDown->setEnabled(hasSelection && allLocal);
    if (actViewDiff)
        actViewDiff->setEnabled(selectedRows.size() == 1 && anyModified);
    if (actRenumberIDs)
        actRenumberIDs->setEnabled(_projectRepo.LocalData != nullptr &&
                                   (!_projectRepo.LocalData->Achievements.isEmpty() || !_projectRepo.LocalData->Leaderboards.isEmpty()));
}

void MainWindow::UpdateTriggerActionButtons() {
    if (_selectedAsset == nullptr) {
        triggerEditor->SetActionButtonStates(false, false, false);
        return;
    }

    const bool hasServer = IsServerAssetId(_selectedAsset->ID, _selectedAsset->isLeaderboard());
    const bool canRevert = hasServer && (_selectedAsset->State == AssetState::Modified);
    const bool canReset = _projectRepo.HasUnsavedAssetChanges(_selectedAsset.get());
    triggerEditor->SetActionButtonStates(hasServer, canRevert, canReset);
}

void MainWindow::onAssetSelectionChanged() { UpdateSelectionUI(); }

void MainWindow::onAssetDoubleClicked(const QModelIndex& /*index*/) { LoadSelectedAsset(); }

void MainWindow::SelectAssetInGrid(const std::shared_ptr<IAssetItem>& asset) {
    if (asset == nullptr)
        return;

    int typeIndex = asset->isLeaderboard() ? 1 : 0;
    if (cmbAssetType->currentIndex() != typeIndex)
        cmbAssetType->setCurrentIndex(typeIndex);

    QString targetCat = asset->Category.isEmpty() ? "Local" : asset->Category;
    int catIdx = cmbCategory->findText(targetCat, Qt::MatchFixedString);
    if (catIdx != -1 && cmbCategory->currentIndex() != catIdx)
        cmbCategory->setCurrentIndex(catIdx);

    int sourceIndex = cmbSource->currentIndex();
    switch (asset->State) {
    case AssetState::Local:
        sourceIndex = 2;
        break;
    case AssetState::Modified:
        sourceIndex = 1;
        break;
    case AssetState::Synced:
        sourceIndex = 0;
        break;
    }
    if (cmbSource->currentIndex() != sourceIndex)
        cmbSource->setCurrentIndex(sourceIndex);

    ReloadDataView();

    int row = _assetModel.FindRowByAssetId(asset->ID);
    if (row == -1 && !txtAssetSearch->text().isEmpty()) {
        txtAssetSearch->clear();
        ReloadDataView();
        row = _assetModel.FindRowByAssetId(asset->ID);
    }
    if (row != -1) {
        achievementGrid->selectRow(row);
        LoadSelectedAsset();
    }
}

void MainWindow::RebuildNotesCache() {
    _cachedNotesLookup.clear();
    _cachedRangeCache.clear();
    PointerTreeParser parser;

    if (_projectRepo.LocalData != nullptr) {
        QSet<int64_t> rangedStarts;

        // Pass 1: Parse all real notes
        for (const auto& n : _projectRepo.LocalData->Notes) {
            QString key = LogicFormatter::NormalizeAddress(n.Address);
            auto nodes = parser.ParseNoteText(n.Note);
            if (!_cachedNotesLookup.contains(key))
                _cachedNotesLookup.insert(key, nodes);

            if (!nodes.isEmpty() && nodes[0]->Size > 1) {
                bool ok = false;
                QString clean = key;
                clean.remove("0x", Qt::CaseInsensitive);
                int64_t startAddr = clean.toLongLong(&ok, 16);
                if (ok && !rangedStarts.contains(startAddr)) {
                    rangedStarts.insert(startAddr);
                    _cachedRangeCache.append(ConditionFormatter::RangeEntry{startAddr, startAddr + nodes[0]->Size, nodes[0]});
                }
            }
        }

        // Pass 2: Resolve redirects (refer to $0x...) to eliminate false-positive validation warnings
        static const QRegularExpression redirectRegex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
        static const QRegularExpression structRegex(QString::fromUtf8(R"(^[.\+\s\x{2500}-\x{257F}])"), QRegularExpression::MultilineOption);

        for (const auto& note : _projectRepo.LocalData->Notes) {
            bool hasStructure = structRegex.match(note.Note).hasMatch();
            if (!hasStructure) {
                auto match = redirectRegex.match(note.Note);
                if (match.hasMatch()) {
                    QString targetAddr = LogicFormatter::NormalizeAddress(match.captured(1));
                    QString sourceKey = LogicFormatter::NormalizeAddress(note.Address);

                    if (_cachedNotesLookup.contains(targetAddr)) {
                        const auto targetNodes = _cachedNotesLookup.value(targetAddr);
                        _cachedNotesLookup.insert(sourceKey, targetNodes);
                        if (!targetNodes.isEmpty() && targetNodes[0]->Size > 1) {
                            bool ok = false;
                            QString clean = sourceKey;
                            clean.remove("0x", Qt::CaseInsensitive);
                            int64_t startAddr = clean.toLongLong(&ok, 16);
                            if (ok && !rangedStarts.contains(startAddr)) {
                                rangedStarts.insert(startAddr);
                                _cachedRangeCache.append(
                                    ConditionFormatter::RangeEntry{startAddr, startAddr + targetNodes[0]->Size, targetNodes[0]});
                            }
                        }
                    }
                }
            }
        }
    }
    _notesCacheDirty = false;
}

void MainWindow::RefreshSetLevelIssues() {
    for (int i = _globalIssues.size() - 1; i >= 0; --i) {
        if (_globalIssues[i].Category == "Set Design")
            _globalIssues.removeAt(i);
    }

    QList<std::shared_ptr<IAssetItem>> allAssets = _projectRepo.GetMergedAssets(false);
    allAssets.append(_projectRepo.GetMergedAssets(true));
    _globalIssues.append(_validator.ValidateSet(allAssets));

    if (!_isLoading)
        codeReviewControl->SetIssues(_globalIssues);
}

void MainWindow::RunGlobalValidation() {
    _globalIssues.clear();

    if (_notesCacheDirty) {
        RebuildNotesCache();
    }

    const QList<CodeNote> notes = (_projectRepo.LocalData != nullptr) ? _projectRepo.LocalData->Notes : QList<CodeNote>();
    const auto parsedNotes = LogicValidator::BuildParsedNotes(notes);
    const int consoleId = (_projectRepo.ServerData != nullptr) ? _projectRepo.ServerData->ConsoleId : 0;

    AchievementStrategy achStrategy;
    LeaderboardStrategy lbStrategy;

    const auto allAchievements = _projectRepo.GetMergedAssets(false);
    for (const auto& ach : allAchievements) {
        auto segments = achStrategy.ParseLogic(ach->Logic);
        _globalIssues.append(_validator.ValidateAchievementWithParsedNotes(segments.value("Core"), ach, &_cachedNotesLookup,
                                                                           &_cachedRangeCache, parsedNotes));
    }

    const auto allLeaderboards = _projectRepo.GetMergedAssets(true);
    for (const auto& lb : allLeaderboards) {
        auto segments = lbStrategy.ParseLogic(lb->Logic);
        _globalIssues.append(
            _validator.ValidateLeaderboardWithParsedNotes(segments, lb, &_cachedNotesLookup, &_cachedRangeCache, parsedNotes));
    }

    _globalIssues.append(_validator.ValidateCodeNotes(notes, consoleId));

    QList<std::shared_ptr<IAssetItem>> allAssets = allAchievements;
    allAssets.append(allLeaderboards);
    _globalIssues.append(_validator.ValidateSet(allAssets));

    codeReviewControl->SetIssues(_globalIssues);
}

void MainWindow::onApplySuggestedFix(const ValidationIssue& issue) {
    if (!issue.HasSuggestedFix() || issue.Asset == nullptr)
        return;

    if (_notesCacheDirty)
        RebuildNotesCache();

    std::shared_ptr<IAssetItem> targetAsset = nullptr;
    const bool isSelected = (_selectedAsset != nullptr && _selectedAsset->ID == issue.Asset->ID &&
                             _selectedAsset->isLeaderboard() == issue.Asset->isLeaderboard());
    if (isSelected) {
        targetAsset = _selectedAsset;
    } else {
        for (const auto& item : _projectRepo.GetMergedAssets(issue.Asset->isLeaderboard())) {
            if (item->ID == issue.Asset->ID) {
                targetAsset = item;
                break;
            }
        }
    }

    if (targetAsset == nullptr)
        return;

    SuggestedFixDialog dialog(issue, targetAsset, &_cachedNotesLookup, &_cachedRangeCache, triggerEditor->ShowDecimal(),
                              triggerEditor->ShowAliases(), this);
    if (dialog.exec() != QDialog::Accepted || !dialog.HasValidFix())
        return;

    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    if (targetAsset->State == AssetState::Synced) {
        auto clone = targetAsset->clone(false);
        if (isSelected)
            _selectedAsset = clone;
        targetAsset = clone;
    }

    if (!targetAsset->isLeaderboard()) {
        auto ra = std::static_pointer_cast<RaAchievement>(targetAsset);
        std::shared_ptr<RaAchievement> localAch = nullptr;
        for (const auto& a : _projectRepo.LocalData->Achievements) {
            if (a->ID == ra->ID) {
                localAch = a;
                break;
            }
        }
        if (localAch == nullptr) {
            localAch = ra;
            _projectRepo.LocalData->Achievements.append(localAch);
        }
        targetAsset = localAch;
        if (isSelected)
            _selectedAsset = localAch;
    } else {
        auto rl = std::static_pointer_cast<RaLeaderboard>(targetAsset);
        std::shared_ptr<RaLeaderboard> localLb = nullptr;
        for (const auto& l : _projectRepo.LocalData->Leaderboards) {
            if (l->ID == rl->ID) {
                localLb = l;
                break;
            }
        }
        if (localLb == nullptr) {
            localLb = rl;
            _projectRepo.LocalData->Leaderboards.append(localLb);
        }
        targetAsset = localLb;
        if (isSelected)
            _selectedAsset = localLb;
    }

    switch (dialog.FixType()) {
    case SuggestedFixType::InvertChain:
    case SuggestedFixType::ClearResetHitCount:
    case SuggestedFixType::RemoveUselessAndNext:
        targetAsset->Logic = dialog.ResultingLogic();
        break;
    case SuggestedFixType::ApplyTitleCase:
        targetAsset->Title = dialog.ResultingTitle();
        break;
    case SuggestedFixType::NormalizeSmartQuotes:
        if (dialog.TargetField() == "desc")
            targetAsset->Description = dialog.ResultingDescription();
        else
            targetAsset->Title = dialog.ResultingTitle();
        break;
    default:
        return;
    }

    UpdateAssetState(targetAsset);
    _isDirty = true;
    _currentAssetDirty = false;
    ReloadDataView();
    if (_selectedAsset != nullptr)
        RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
    UpdateSelectionUI();
    UpdateTriggerActionButtons();
}

void MainWindow::PushMainUndo() {
    UserTextParser::UserData snapshot;
    if (_projectRepo.LocalData != nullptr) {
        for (const auto& ach : _projectRepo.LocalData->Achievements)
            snapshot.Achievements.append(ach->cloneAchievement(false));
        for (const auto& lb : _projectRepo.LocalData->Leaderboards)
            snapshot.Leaderboards.append(lb->cloneLeaderboard(false));
        for (const auto& n : _projectRepo.LocalData->Notes)
            snapshot.Notes.append(CodeNote{n.User, n.Address, n.Note});
    }
    _mainHistory.PushState(snapshot);
}

void MainWindow::UndoMain() {
    if (_projectRepo.LocalData == nullptr)
        return;
    auto previousState = _mainHistory.Undo(*_projectRepo.LocalData);
    _projectRepo.LocalData->Achievements = previousState.Achievements;
    _projectRepo.LocalData->Leaderboards = previousState.Leaderboards;
    _projectRepo.LocalData->Notes = previousState.Notes;
    _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
    _notesCacheDirty = true;
    _currentAssetDirty = false;
    // Reload the note editors too; otherwise the next note selection emits
    // NoteUpdated and writes the pre-undo note list straight back into LocalData.
    codeNoteEditor->SetNotes(_projectRepo.LocalData->Notes);
    triggerEditor->SetNotes(_projectRepo.LocalData->Notes);
    ReloadDataView();
    RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
}

void MainWindow::RedoMain() {
    if (_projectRepo.LocalData == nullptr)
        return;
    auto nextState = _mainHistory.Redo(*_projectRepo.LocalData);
    _projectRepo.LocalData->Achievements = nextState.Achievements;
    _projectRepo.LocalData->Leaderboards = nextState.Leaderboards;
    _projectRepo.LocalData->Notes = nextState.Notes;
    _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
    _notesCacheDirty = true;
    _currentAssetDirty = false;
    codeNoteEditor->SetNotes(_projectRepo.LocalData->Notes);
    triggerEditor->SetNotes(_projectRepo.LocalData->Notes);
    ReloadDataView();
    RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
}

void MainWindow::onUndo() {
    QWidget* focus = QApplication::focusWidget();
    if (focus != nullptr) {
        if (auto* le = qobject_cast<QLineEdit*>(focus)) {
            if (le->isUndoAvailable()) {
                le->undo();
                return;
            }
        } else if (auto* te = qobject_cast<QPlainTextEdit*>(focus)) {
            if (te->document()->isUndoAvailable()) {
                te->undo();
                return;
            }
        } else if (auto* qte = qobject_cast<QTextEdit*>(focus)) {
            if (qte->document()->isUndoAvailable()) {
                qte->undo();
                return;
            }
        }
    }

    bool isTriggerContext = focus && (focus == triggerEditor || triggerEditor->isAncestorOf(focus));
    bool isNoteContext = focus && (focus == codeNoteEditor || codeNoteEditor->isAncestorOf(focus));

    if (isTriggerContext) {
        if (triggerEditor->CanUndo())
            triggerEditor->Undo();
        else
            UndoMain();
    } else if (isNoteContext) {
        if (codeNoteEditor->CanUndo())
            codeNoteEditor->Undo();
        else
            UndoMain();
    } else {
        UndoMain();
    }
}

void MainWindow::onRedo() {
    QWidget* focus = QApplication::focusWidget();
    if (focus != nullptr) {
        if (auto* le = qobject_cast<QLineEdit*>(focus)) {
            if (le->isRedoAvailable()) {
                le->redo();
                return;
            }
        } else if (auto* te = qobject_cast<QPlainTextEdit*>(focus)) {
            if (te->document()->isRedoAvailable()) {
                te->redo();
                return;
            }
        } else if (auto* qte = qobject_cast<QTextEdit*>(focus)) {
            if (qte->document()->isRedoAvailable()) {
                qte->redo();
                return;
            }
        }
    }

    bool isTriggerContext = focus && (focus == triggerEditor || triggerEditor->isAncestorOf(focus));
    bool isNoteContext = focus && (focus == codeNoteEditor || codeNoteEditor->isAncestorOf(focus));

    if (isTriggerContext) {
        if (triggerEditor->CanRedo())
            triggerEditor->Redo();
        else
            RedoMain();
    } else if (isNoteContext) {
        if (codeNoteEditor->CanRedo())
            codeNoteEditor->Redo();
        else
            RedoMain();
    } else {
        RedoMain();
    }
}

void MainWindow::onReload() {
    if (_currentFilePath.isEmpty())
        return;
    if (!ConfirmDiscardChanges())
        return;

    int selectedId = -1;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty()) {
        auto item = _assetModel.GetAsset(selectedRows.first().row());
        if (item != nullptr)
            selectedId = item->ID;
    }

    int savedSource = cmbSource->currentIndex();
    int savedCategory = cmbCategory->currentIndex();
    int savedType = cmbAssetType->currentIndex();

    LoadFiles(_currentFilePath);

    cmbAssetType->setCurrentIndex(savedType);
    cmbCategory->setCurrentIndex(savedCategory);
    cmbSource->setCurrentIndex(savedSource);

    if (selectedId != -1) {
        int row = _assetModel.FindRowByAssetId(selectedId);
        if (row != -1) {
            achievementGrid->selectRow(row);
            LoadSelectedAsset();
        }
    }
}

void MainWindow::EnsureAssetIsLocal(const std::shared_ptr<IAssetItem>& asset) {
    if (asset == nullptr || _projectRepo.LocalData == nullptr)
        return;
    if (asset->State == AssetState::Local || asset->State == AssetState::Modified)
        return;

    if (!asset->isLeaderboard()) {
        auto ra = std::static_pointer_cast<RaAchievement>(asset);
        bool exists = false;
        for (const auto& a : _projectRepo.LocalData->Achievements)
            if (a->ID == ra->ID) {
                exists = true;
                break;
            }

        if (!exists) {
            if (asset == _selectedAsset)
                _projectRepo.LocalData->Achievements.append(ra);
            else
                _projectRepo.LocalData->Achievements.append(ra->cloneAchievement(false));
        }
    } else {
        auto rl = std::static_pointer_cast<RaLeaderboard>(asset);
        bool exists = false;
        for (const auto& l : _projectRepo.LocalData->Leaderboards)
            if (l->ID == rl->ID) {
                exists = true;
                break;
            }

        if (!exists) {
            if (asset == _selectedAsset)
                _projectRepo.LocalData->Leaderboards.append(rl);
            else
                _projectRepo.LocalData->Leaderboards.append(rl->cloneLeaderboard(false));
        }
    }
}

void MainWindow::EnsureAssetIsLocal() { EnsureAssetIsLocal(_selectedAsset); }

QString MainWindow::InvertOperator(const QString& op) {
    if (op == "=")
        return "!=";
    if (op == "!=")
        return "=";
    if (op == "<")
        return ">=";
    if (op == "<=")
        return ">";
    if (op == ">")
        return "<=";
    if (op == ">=")
        return "<";
    return op;
}

void MainWindow::ApplyBulkEdit(const std::shared_ptr<IAssetItem>& sourceTemplate) {
    if (sourceTemplate == nullptr || sourceTemplate->isLeaderboard())
        return;
    auto srcAch = std::static_pointer_cast<RaAchievement>(sourceTemplate);

    for (const auto& idx : achievementGrid->selectionModel()->selectedRows()) {
        auto target = _assetModel.GetAsset(idx.row());
        if (target != nullptr && target != sourceTemplate && !target->isLeaderboard()) {
            auto targetAch = std::static_pointer_cast<RaAchievement>(target);
            EnsureAssetIsLocal(targetAch);

            std::shared_ptr<RaAchievement> localAch = nullptr;
            for (const auto& a : _projectRepo.LocalData->Achievements) {
                if (a->ID == targetAch->ID) {
                    localAch = a;
                    break;
                }
            }

            if (localAch != nullptr) {
                localAch->Points = srcAch->Points;
                localAch->Type = srcAch->Type;
                UpdateAssetState(localAch);
                int dispIdx = _currentDisplayList.indexOf(target);
                if (dispIdx != -1)
                    _currentDisplayList[dispIdx] = localAch;
                _assetModel.ReplaceAsset(idx.row(), localAch);
            }
        }
    }
}

void MainWindow::onNewAsset() {
    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    int newId = 111000001;
    if (_projectRepo.LocalData != nullptr) {
        QList<int> localIds;
        for (const auto& a : _projectRepo.LocalData->Achievements)
            if (!IsServerAssetId(a->ID, false))
                localIds.append(a->ID);
        for (const auto& l : _projectRepo.LocalData->Leaderboards)
            if (!IsServerAssetId(l->ID, true))
                localIds.append(l->ID);

        if (!localIds.isEmpty()) {
            int maxId = *std::max_element(localIds.begin(), localIds.end());
            newId = (maxId < 111000000) ? 111000001 : maxId + 1;
        }
    }

    bool isLeaderboard = (cmbAssetType->currentIndex() == 1);
    if (isLeaderboard) {
        auto lb = std::make_shared<RaLeaderboard>();
        lb->ID = newId;
        lb->Title = "New Leaderboard";
        lb->State = AssetState::Local;
        lb->Category = "Local";
        _projectRepo.LocalData->Leaderboards.append(lb);
    } else {
        auto ach = std::make_shared<RaAchievement>();
        ach->ID = newId;
        ach->Title = "New Achievement";
        ach->State = AssetState::Local;
        ach->Category = "Local";
        _projectRepo.LocalData->Achievements.append(ach);
    }

    if (cmbSource->currentText() != "New") {
        int idx = cmbSource->findText("New");
        if (idx != -1)
            cmbSource->setCurrentIndex(idx);
    }

    _isDirty = true;
    ReloadDataView();

    int row = _assetModel.FindRowByAssetId(newId);
    if (row != -1) {
        achievementGrid->selectRow(row);
        LoadSelectedAsset();
    }
    RunGlobalValidation();
}

void MainWindow::onDuplicateAsset() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty())
        return;

    std::sort(selectedRows.begin(), selectedRows.end(), [](const QModelIndex& a, const QModelIndex& b) { return a.row() < b.row(); });

    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    int nextId = 111000001;
    if (_projectRepo.LocalData != nullptr) {
        QList<int> localIds;
        for (const auto& a : _projectRepo.LocalData->Achievements)
            if (!IsServerAssetId(a->ID, false))
                localIds.append(a->ID);
        for (const auto& l : _projectRepo.LocalData->Leaderboards)
            if (!IsServerAssetId(l->ID, true))
                localIds.append(l->ID);

        if (!localIds.isEmpty()) {
            int maxId = *std::max_element(localIds.begin(), localIds.end());
            nextId = (maxId >= 111000000) ? maxId + 1 : 111000001;
        }
    }

    QList<int> newIds;
    for (const auto& idx : selectedRows) {
        auto source = _assetModel.GetAsset(idx.row());
        if (source != nullptr) {
            if (!source->isLeaderboard()) {
                auto c = std::static_pointer_cast<RaAchievement>(source->clone(false));
                c->Title += " (Copy)";
                c->ID = nextId++;
                c->State = AssetState::Local;
                c->Category = "Local";
                _projectRepo.LocalData->Achievements.append(c);
                newIds.append(c->ID);
            } else {
                auto c = std::static_pointer_cast<RaLeaderboard>(source->clone(false));
                c->Title += " (Copy)";
                c->ID = nextId++;
                c->State = AssetState::Local;
                c->Category = "Local";
                _projectRepo.LocalData->Leaderboards.append(c);
                newIds.append(c->ID);
            }
        }
    }

    int sIdx = cmbSource->findText("New");
    if (sIdx != -1)
        cmbSource->setCurrentIndex(sIdx);

    _isDirty = true;
    ReloadDataView();

    achievementGrid->clearSelection();
    QItemSelection newSelection;
    int firstNewRow = -1;
    for (int id : newIds) {
        int r = _assetModel.FindRowByAssetId(id);
        if (r == -1)
            continue;
        newSelection.select(_assetModel.index(r, 0), _assetModel.index(r, _assetModel.columnCount() - 1));
        if (firstNewRow == -1 || r < firstNewRow)
            firstNewRow = r;
    }
    if (!newSelection.isEmpty()) {
        achievementGrid->selectionModel()->select(newSelection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);
        if (firstNewRow != -1) {
            achievementGrid->selectionModel()->setCurrentIndex(_assetModel.index(firstNewRow, 0), QItemSelectionModel::NoUpdate);
            achievementGrid->scrollTo(_assetModel.index(firstNewRow, 0));
        }
    }
    RunGlobalValidation();
}

void MainWindow::onDeleteAsset() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr)
        return;

    PushMainUndo();
    bool deletedSelected = false;

    for (const auto& idx : selectedRows) {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr && item->State == AssetState::Local) {
            if (_selectedAsset != nullptr && _selectedAsset->ID == item->ID && _selectedAsset->isLeaderboard() == item->isLeaderboard())
                deletedSelected = true;

            if (!item->isLeaderboard()) {
                for (int i = _projectRepo.LocalData->Achievements.size() - 1; i >= 0; --i) {
                    if (_projectRepo.LocalData->Achievements[i]->ID == item->ID)
                        _projectRepo.LocalData->Achievements.removeAt(i);
                }
            } else {
                for (int i = _projectRepo.LocalData->Leaderboards.size() - 1; i >= 0; --i) {
                    if (_projectRepo.LocalData->Leaderboards[i]->ID == item->ID)
                        _projectRepo.LocalData->Leaderboards.removeAt(i);
                }
            }
        }
    }

    _isDirty = true;
    ReloadDataView();

    if (deletedSelected) {
        _selectedAsset = nullptr;
        _currentAssetDirty = false;
        triggerEditor->Clear();
        UpdateSelectionUI();
        UpdateTriggerActionButtons();
    }
    RunGlobalValidation();
}

void MainWindow::onResetAsset() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr)
        return;

    QList<std::shared_ptr<IAssetItem>> targets;
    for (const auto& idx : selectedRows) {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr && _projectRepo.HasUnsavedAssetChanges(item.get()))
            targets.append(item);
    }

    if (targets.isEmpty())
        return;

    QMessageBox msgBox(this);
    msgBox.setWindowTitle("Confirm Reset");
    if (targets.size() == 1) {
        msgBox.setText(QString("Discard current unsaved changes for '%1' (ID: %2)?").arg(targets.first()->Title).arg(targets.first()->ID));
    } else {
        msgBox.setText(QString("Discard current unsaved changes for %1 selected asset(s)?").arg(targets.size()));
    }
    msgBox.setInformativeText("Unsaved modifications made since the last load or save to User.txt will be discarded, restoring the "
                              "selected asset(s) to their last saved state (User.txt override if saved, or server JSON baseline).");
    msgBox.setIcon(QMessageBox::Warning);

    QPushButton* resetBtn = msgBox.addButton("Reset", QMessageBox::DestructiveRole);
    QPushButton* cancelBtn = msgBox.addButton("Cancel", QMessageBox::RejectRole);
    msgBox.setDefaultButton(cancelBtn);
    msgBox.exec();

    if (msgBox.clickedButton() != resetBtn)
        return;

    PushMainUndo();

    for (const auto& item : targets) {
        _projectRepo.ResetAssetToSaved(item->ID, item->isLeaderboard());
    }

    _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
    _notesCacheDirty = true;
    ReloadDataView();
    RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
}

void MainWindow::onRevertAsset() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr)
        return;

    QList<std::shared_ptr<IAssetItem>> targets;
    for (const auto& idx : selectedRows) {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr && item->State == AssetState::Modified)
            targets.append(item);
    }

    if (targets.isEmpty())
        return;

    QMessageBox msgBox(this);
    msgBox.setWindowTitle("Confirm Revert to Server");
    if (targets.size() == 1) {
        msgBox.setText(
            QString("Discard local changes and revert '%1' (ID: %2) to the server snapshot?").arg(targets.first()->Title).arg(targets.first()->ID));
    } else {
        msgBox.setText(QString("Discard local changes and revert %1 selected asset(s) to the server snapshot?").arg(targets.size()));
    }
    msgBox.setInformativeText("Warning: All current modifications and any local User.txt overrides for the selected asset(s) will be "
                              "discarded, restoring the original values from the loaded JSON server snapshot.");
    msgBox.setIcon(QMessageBox::Warning);

    QPushButton* revertBtn = msgBox.addButton("Revert", QMessageBox::DestructiveRole);
    QPushButton* cancelBtn = msgBox.addButton("Cancel", QMessageBox::RejectRole);
    msgBox.setDefaultButton(cancelBtn);
    msgBox.exec();

    if (msgBox.clickedButton() != revertBtn)
        return;

    PushMainUndo();

    for (const auto& item : targets) {
        _projectRepo.RevertAssetToServer(item->ID, item->isLeaderboard());
    }

    _isDirty = _rpDirty || _projectRepo.HasUnsavedChanges();
    _notesCacheDirty = true;
    ReloadDataView();
    RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
}

void MainWindow::onMoveAssetUp() { MoveAsset(-1); }
void MainWindow::onMoveAssetDown() { MoveAsset(1); }

void MainWindow::MoveAsset(int direction) {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr)
        return;

    QList<int> selectedIndices;
    for (const auto& idx : selectedRows)
        selectedIndices.append(idx.row());
    std::sort(selectedIndices.begin(), selectedIndices.end());

    // Read the working order from the model, not _currentDisplayList: the model can
    // be sorted by a header click while the display list stays in natural order.
    QList<std::shared_ptr<IAssetItem>> workingList;
    for (int r = 0; r < _assetModel.rowCount(); ++r)
        workingList.append(_assetModel.GetAsset(r));

    if (workingList.isEmpty())
        return;
    if (direction < 0 && selectedIndices.first() == 0)
        return;
    if (direction > 0 && selectedIndices.last() >= workingList.size() - 1)
        return;

    for (int idx : selectedIndices) {
        if (workingList[idx] == nullptr || workingList[idx]->State != AssetState::Local)
            return;
    }

    // Capture object pointers before moving to restore selection accurately after reordering
    QSet<std::shared_ptr<IAssetItem>> objectsToSelect;
    for (int idx : selectedIndices) {
        objectsToSelect.insert(workingList[idx]);
    }

    // Clear active sort column so Qt does not automatically re-sort manual moves back
    achievementGrid->horizontalHeader()->setSortIndicator(-1, Qt::AscendingOrder);

    PushMainUndo();

    if (direction < 0) {
        for (int idx : selectedIndices)
            workingList.swapItemsAt(idx, idx - 1);
    } else {
        for (int i = selectedIndices.size() - 1; i >= 0; --i) {
            int idx = selectedIndices[i];
            workingList.swapItemsAt(idx, idx + 1);
        }
    }

    bool isLeaderboardView = (cmbAssetType->currentIndex() == 1);
    QSet<int> displayedIds;
    for (const auto& it : workingList)
        if (it != nullptr)
            displayedIds.insert(it->ID);

    int currentId = 111000001;

    if (isLeaderboardView) {
        // Preserve any local or modified leaderboards not currently in the displayed filter
        QList<std::shared_ptr<RaLeaderboard>> preservedLocal;
        QList<std::shared_ptr<RaLeaderboard>> preservedModified;
        for (const auto& l : _projectRepo.LocalData->Leaderboards) {
            if (!displayedIds.contains(l->ID)) {
                if (IsServerAssetId(l->ID, true))
                    preservedModified.append(l);
                else
                    preservedLocal.append(l);
            }
        }

        _projectRepo.LocalData->Leaderboards.clear();
        for (const auto& item : workingList) {
            if (item->State != AssetState::Local)
                continue;
            item->ID = currentId++;
            _projectRepo.LocalData->Leaderboards.append(std::static_pointer_cast<RaLeaderboard>(item));
        }
        for (auto& p : preservedLocal) {
            p->ID = currentId++;
            _projectRepo.LocalData->Leaderboards.append(p);
        }
        for (auto& p : preservedModified) {
            _projectRepo.LocalData->Leaderboards.append(p);
        }
    } else {
        // Preserve any local or modified achievements not currently in the displayed filter
        QList<std::shared_ptr<RaAchievement>> preservedLocal;
        QList<std::shared_ptr<RaAchievement>> preservedModified;
        for (const auto& a : _projectRepo.LocalData->Achievements) {
            if (!displayedIds.contains(a->ID)) {
                if (IsServerAssetId(a->ID, false))
                    preservedModified.append(a);
                else
                    preservedLocal.append(a);
            }
        }

        _projectRepo.LocalData->Achievements.clear();
        for (const auto& item : workingList) {
            if (item->State != AssetState::Local)
                continue;
            item->ID = currentId++;
            _projectRepo.LocalData->Achievements.append(std::static_pointer_cast<RaAchievement>(item));
        }
        for (auto& p : preservedLocal) {
            p->ID = currentId++;
            _projectRepo.LocalData->Achievements.append(p);
        }
        for (auto& p : preservedModified) {
            _projectRepo.LocalData->Achievements.append(p);
        }
    }

    _isDirty = true;
    ReloadDataView();

    // Restore table selection to the moved items and set active cell so continuous Alt+Up/Down works
    achievementGrid->clearSelection();
    QItemSelection selection;
    int firstNewVisualRow = -1;
    for (int r = 0; r < _assetModel.rowCount(); ++r) {
        auto moved = _assetModel.GetAsset(r);
        if (moved != nullptr && objectsToSelect.contains(moved)) {
            auto left = _assetModel.index(r, 0);
            auto right = _assetModel.index(r, _assetModel.columnCount() - 1);
            selection.select(left, right);
            if (firstNewVisualRow == -1)
                firstNewVisualRow = r;
        }
    }
    achievementGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

    if (firstNewVisualRow != -1) {
        achievementGrid->selectionModel()->setCurrentIndex(_assetModel.index(firstNewVisualRow, 0), QItemSelectionModel::NoUpdate);
        achievementGrid->scrollTo(_assetModel.index(firstNewVisualRow, 0));
    }
}

void MainWindow::onRestoreOrder() {
    achievementGrid->horizontalHeader()->setSortIndicator(-1, Qt::AscendingOrder);
    ReloadDataView();
}

void MainWindow::onRenumberLocalIDs() {
    if (_projectRepo.LocalData == nullptr)
        return;
    bool hasLocal = !_projectRepo.LocalData->Achievements.isEmpty() || !_projectRepo.LocalData->Leaderboards.isEmpty();
    if (!hasLocal)
        return;

    PushMainUndo();
    int currentId = 111000001;
    for (auto& ach : _projectRepo.LocalData->Achievements) {
        if (!IsServerAssetId(ach->ID, false))
            ach->ID = currentId++;
    }
    for (auto& lb : _projectRepo.LocalData->Leaderboards) {
        if (!IsServerAssetId(lb->ID, true))
            lb->ID = currentId++;
    }

    _isDirty = true;
    ReloadDataView();
    RunGlobalValidation();
}

void MainWindow::onViewDiff() {
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.size() != 1)
        return;

    auto localItem = _assetModel.GetAsset(selectedRows.first().row());
    if (localItem != nullptr && localItem->State == AssetState::Modified && _projectRepo.ServerData != nullptr) {
        std::shared_ptr<IAssetItem> serverItem = nullptr;
        for (const auto& s : _projectRepo.ServerData->Sets) {
            if (localItem->isLeaderboard()) {
                for (const auto& lb : s.Leaderboards)
                    if (lb->ID == localItem->ID) {
                        serverItem = lb;
                        break;
                    }
            } else {
                for (const auto& ach : s.Achievements)
                    if (ach->ID == localItem->ID) {
                        serverItem = ach;
                        break;
                    }
            }
            if (serverItem != nullptr)
                break;
        }

        if (serverItem != nullptr) {
            AssetDiffDialog diff(serverItem, localItem, this);
            diff.exec();
        }
    }
}

void MainWindow::onClearDataReset() {
    auto result = QMessageBox::warning(
        this, "Clear Data / Reset",
        "This will delete all application configuration and cached data. The application will restart.\n\nAre you sure?",
        QMessageBox::Yes | QMessageBox::No, QMessageBox::No);

    if (result == QMessageBox::Yes) {
        try {
            QString appPath = QCoreApplication::applicationDirPath();
            QFile::remove(QDir(appPath).filePath("settings.ini"));

            QDir(QDir(appPath).filePath("Cache")).removeRecursively();
            QDir(QDir(appPath).filePath("Metadata")).removeRecursively();

            // Match C#'s Application.Restart(): relaunch after cleaning up.
            QProcess::startDetached(QCoreApplication::applicationFilePath(), QStringList());
            QApplication::quit();
        } catch (...) {
        }
    }
}

void MainWindow::onToggleReviewer() { ToggleReviewer(); }

void MainWindow::onOpenFile() {
    if (!ConfirmDiscardChanges())
        return;

    QString fileName =
        QFileDialog::getOpenFileName(this, "Open Project File", "", "JSON Files (*.json);;Text Files (*.txt);;All Files (*.*)");

    if (!fileName.isEmpty()) {
        txtAssetSearch->clear();
        LoadFiles(fileName);
    }
}

void MainWindow::onSaveFile() {
    if (_projectRepo.LocalData == nullptr && _projectRepo.ServerData == nullptr)
        return;
    triggerEditor->CommitActiveEditor();
    codeNoteEditor->CommitChanges();

    // Ensure local data notes reflect latest modified and batch-multiplied notes before saving
    if (_projectRepo.LocalData != nullptr) {
        _projectRepo.LocalData->Notes = codeNoteEditor->GetAllNotes();
    }

    QString savePath = _currentUserFilePath;
    if (savePath.isEmpty() && !_currentFilePath.isEmpty()) {
        QFileInfo fi(_currentFilePath);
        QString dir = fi.absolutePath();
        QString fileName = fi.completeBaseName();
        if (fileName.endsWith("-Notes", Qt::CaseInsensitive))
            fileName.chop(6);
        if (fileName.endsWith("-User", Qt::CaseInsensitive))
            fileName.chop(5);
        if (fileName.endsWith("-Rich", Qt::CaseInsensitive))
            fileName.chop(5);
        savePath = QDir(dir).filePath(QString("%1-User.txt").arg(fileName));
    }

    if (savePath.isEmpty()) {
        savePath = QFileDialog::getSaveFileName(this, "Save User File", "", "Text Files (*.txt)");
        if (savePath.isEmpty())
            return;
    }

    try {
        if (QFile::exists(savePath)) {
            QFileInfo fi(savePath);
            QString fileName = fi.fileName();
            QString gameIdStr = _projectRepo.CurrentGameId;
            if (gameIdStr.isEmpty()) {
                for (QChar c : fileName) {
                    if (c.isDigit())
                        gameIdStr.append(c);
                    else
                        break;
                }
            }
            QString backupRoot = QDir(QCoreApplication::applicationDirPath()).filePath("Backups");
            QString gameBackupDir = gameIdStr.isEmpty() ? backupRoot : QDir(backupRoot).filePath(gameIdStr);
            if (!QDir(gameBackupDir).exists())
                QDir().mkpath(gameBackupDir);

            QString timestamp = QDateTime::currentDateTime().toString("yyyyMMdd_HHmmss");
            QString backupPath = QDir(gameBackupDir).filePath(QString("%1.%2.bak").arg(fileName, timestamp));
            // C# File.Copy(..., overwrite: true)
            if (QFile::exists(backupPath))
                QFile::remove(backupPath);
            QFile::copy(savePath, backupPath);
        }
    } catch (...) {
    }

    try {
        _projectRepo.EnsureLocalDataExists();
        QString fileContent = _fileSerializer.Serialize(_projectRepo.ServerData != nullptr ? _projectRepo.ServerData->Title : "Game Title",
                                                        _projectRepo.LocalData.get(), _projectRepo.GetMergedAchievements(),
                                                        _projectRepo.GetMergedLeaderboards(), _projectRepo);

        QFile outFile(savePath);
        if (!outFile.open(QIODevice::WriteOnly | QIODevice::Text)) {
            QMessageBox::critical(this, "Error", QString("Error saving: %1").arg(outFile.errorString()));
            return;
        }

        QByteArray bytes = fileContent.toUtf8();
        if (outFile.write(bytes) != bytes.size() || !outFile.flush()) {
            QMessageBox::critical(this, "Error", QString("Error saving: %1").arg(outFile.errorString()));
            outFile.close();
            return;
        }
        outFile.close();

        _currentUserFilePath = savePath;
        _projectRepo.SaveRichPresence();
        _projectRepo.Metadata.Save();
        _projectRepo.MarkCurrentAsSaved();
        codeNoteEditor->SetSavedNotes(_projectRepo.GetSavedNotes());

        _rpDirty = false;
        _isDirty = false;
        _currentAssetDirty = false;
        UpdateSelectionUI();
        UpdateTriggerActionButtons();

        QMessageBox::information(this, "Save", "Saved to " + savePath);
    } catch (const std::exception& ex) {
        QMessageBox::critical(this, "Error", QString("Error saving: %1").arg(ex.what()));
    }
}

void MainWindow::onViewRpDiff() {
    if (_projectRepo.RichPresence == nullptr)
        return;

    auto ExtractDisplaySection = [](const QString& rpScript) -> QString {
        if (rpScript.isEmpty())
            return "";
        QString norm = rpScript;
        norm.replace("\r\n", "\n").replace('\r', '\n');
        QStringList lines = norm.split('\n');
        QString sb;
        bool inDisplay = false;
        for (const QString& line : lines) {
            QString trimmed = line.trimmed();
            if (trimmed.startsWith("Display:")) {
                inDisplay = true;
                continue;
            }
            if (inDisplay) {
                if (trimmed.startsWith("Lookup:") || trimmed.startsWith("Format:"))
                    break;
                sb += line + "\n";
            }
        }
        return sb.trimmed();
    };

    QString current = ExtractDisplaySection(_projectRepo.RichPresence->ToString());
    QString path = _projectRepo.RichPresenceFilePath;
    QString original = "";
    if (QFile::exists(path)) {
        QFile f(path);
        if (f.open(QIODevice::ReadOnly | QIODevice::Text)) {
            original = ExtractDisplaySection(QString::fromUtf8(f.readAll()));
            f.close();
        }
    }

    TextDiffDialog diff("RP Diff", original, current, this);
    diff.exec();
}

void MainWindow::onRevertRichPresence() {
    if (!_rpMetadata.CurrentBackup.has_value()) {
        QMessageBox::information(this, "Revert Rich Presence", "No backup found.");
        return;
    }

    auto result = QMessageBox::question(this, "Confirm", QString("Revert to state from %1?").arg(_rpMetadata.CurrentBackup->Timestamp),
                                        QMessageBox::Yes | QMessageBox::No);

    if (result == QMessageBox::Yes) {
        try {
            _projectRepo.RichPresence =
                std::make_shared<RichPresenceScript>(RichPresenceParser::Parse(_rpMetadata.CurrentBackup->OriginalScript));
            _rpDirty = true;
            _isDirty = true;
            QMessageBox::information(this, "Revert Rich Presence", "Reverted.");
        } catch (const std::exception& ex) {
            QMessageBox::critical(this, "Error", QString("Error: %1").arg(ex.what()));
        }
    }
}

void MainWindow::onRevertRegion() {
    QSet<QString> regions;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    QList<std::shared_ptr<IAssetItem>> selectedAssets;

    for (const auto& idx : selectedRows) {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr)
            selectedAssets.append(item);
    }

    if (selectedAssets.isEmpty())
        return;

    static const QRegularExpression regionRegex(R"(\((.*?)\))");

    for (const auto& asset : selectedAssets) {
        std::unique_ptr<IAssetStrategy> strategy;
        if (asset->isLeaderboard())
            strategy = std::make_unique<LeaderboardStrategy>();
        else
            strategy = std::make_unique<AchievementStrategy>();

        auto segments = strategy->ParseLogic(asset->Logic);
        for (auto& groupList : segments) {
            _projectRepo.Metadata.ApplyNamesToAsset(asset->ID, groupList);
            for (const auto& g : groupList) {
                auto match = regionRegex.match(g.GroupName);
                if (match.hasMatch())
                    regions.insert(match.captured(1));
            }
        }
    }

    if (regions.isEmpty()) {
        QMessageBox::information(this, "Revert Region", "No region-specific groups found.");
        return;
    }

    RevertRegionDialog form(regions.values(), this);
    if (form.exec() != QDialog::Accepted)
        return;

    QString targetRegion = form.SelectedRegion;
    PushMainUndo();
    int count = 0;

    for (const auto& item : selectedAssets) {
        EnsureAssetIsLocal(item);
        std::shared_ptr<IAssetItem> localItem = nullptr;
        if (item->isLeaderboard()) {
            for (const auto& l : _projectRepo.LocalData->Leaderboards)
                if (l->ID == item->ID) {
                    localItem = l;
                    break;
                }
        } else {
            for (const auto& a : _projectRepo.LocalData->Achievements)
                if (a->ID == item->ID) {
                    localItem = a;
                    break;
                }
        }

        if (localItem == nullptr)
            continue;

        std::unique_ptr<IAssetStrategy> strategy;
        if (localItem->isLeaderboard())
            strategy = std::make_unique<LeaderboardStrategy>();
        else
            strategy = std::make_unique<AchievementStrategy>();

        auto segments = strategy->ParseLogic(localItem->Logic);
        bool modified = false;

        for (const QString& segKey : segments.keys()) {
            auto& groups = segments[segKey];
            _projectRepo.Metadata.ApplyNamesToAsset(localItem->ID, groups);

            std::optional<AchievementConditionGroup> targetGroup;
            for (const auto& g : groups) {
                if (g.GroupName.contains(QString("(%1)").arg(targetRegion))) {
                    targetGroup = g;
                    break;
                }
            }

            if (targetGroup.has_value()) {
                while (!targetGroup->Conditions.isEmpty() && targetGroup->Conditions[0].Flag == "Pause If")
                    targetGroup->Conditions.removeFirst();

                AchievementConditionGroup newCore;
                newCore.GroupName = "Core";
                newCore.Conditions = targetGroup->Conditions;
                segments[segKey] = {newCore};
                modified = true;
                _projectRepo.Metadata.UpdateAssetGroups(localItem->ID, segments[segKey]);
            }
        }

        if (modified) {
            localItem->Logic = strategy->BuildLogic(segments);
            count++;
        }
    }

    _isDirty = true;
    ReloadDataView();
    if (_selectedAsset != nullptr)
        RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
    QMessageBox::information(this, "Revert Region", QString("Reverted %1 assets to %2.").arg(count).arg(targetRegion));
}

void MainWindow::onLogicOptimizer() {
    if (_currentDisplayList.isEmpty())
        return;

    QList<std::shared_ptr<IAssetItem>> assetsToOptimize;
    QStringList originalLogics;
    assetsToOptimize.reserve(_currentDisplayList.size());
    originalLogics.reserve(_currentDisplayList.size());

    for (const auto& item : _currentDisplayList) {
        auto clone = item->clone(false);
        clone->State = item->State;
        assetsToOptimize.append(clone);
        originalLogics.append(item->Logic);
    }

    LogicOptimizerDialog form(assetsToOptimize, this);
    form.exec();

    QList<std::shared_ptr<IAssetItem>> changedItems;
    for (int i = 0; i < assetsToOptimize.size(); ++i) {
        if (assetsToOptimize[i]->Logic != originalLogics[i]) {
            changedItems.append(assetsToOptimize[i]);
        }
    }

    if (changedItems.isEmpty())
        return;

    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    for (const auto& item : changedItems) {
        if (item->isLeaderboard()) {
            bool updatedExisting = false;
            for (auto& existing : _projectRepo.LocalData->Leaderboards) {
                if (existing->ID == item->ID) {
                    existing->Logic = item->Logic;
                    UpdateAssetState(existing);
                    updatedExisting = true;
                    break;
                }
            }
            if (!updatedExisting) {
                UpdateAssetState(item);
                _projectRepo.LocalData->Leaderboards.append(std::static_pointer_cast<RaLeaderboard>(item));
            }
        } else {
            bool updatedExisting = false;
            for (auto& existing : _projectRepo.LocalData->Achievements) {
                if (existing->ID == item->ID) {
                    existing->Logic = item->Logic;
                    UpdateAssetState(existing);
                    updatedExisting = true;
                    break;
                }
            }
            if (!updatedExisting) {
                UpdateAssetState(item);
                _projectRepo.LocalData->Achievements.append(std::static_pointer_cast<RaAchievement>(item));
            }
        }
    }

    _isDirty = true;
    ReloadDataView();
    if (_selectedAsset != nullptr)
        RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
}

void MainWindow::onMultiRegionBatch(MultiRegionDialog* dialog) {
    if (dialog == nullptr)
        return;
    auto configs = dialog->GetConfiguredRegions();
    if (configs.isEmpty())
        return;

    // Use value storage to eliminate dangling pointer risks when modifying or copying region configurations
    std::optional<RegionConfig> baseConfigOpt;
    QList<RegionConfig> targetConfigs;
    for (const auto& c : configs) {
        if (c.IsBase)
            baseConfigOpt = c;
        else
            targetConfigs.append(c);
    }

    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    if (dialog->ShouldUpdateRichPresence() && _projectRepo.RichPresence != nullptr) {
        _rpMetadata.SaveBackup(_projectRepo.RichPresence->ToString());
        auto newRp = _rpService.GenerateMultiRegion(*_projectRepo.RichPresence, configs);
        _projectRepo.RichPresence = std::make_shared<RichPresenceScript>(newRp);
        _rpDirty = true;
        QMessageBox::information(this, "RP Updated", "Rich Presence script updated in memory.");
    }

    if (dialog->UsePauseLocks()) {
        auto InvertLockLogic = [this](QList<AchievementCondition>& conditions) {
            for (auto& c : conditions) {
                c.Flag = "Pause If";
                c.Operator = InvertOperator(c.Operator);
            }
        };

        for (auto& cfg : configs) {
            InvertLockLogic(cfg.LockConditions);
        }

        baseConfigOpt = std::nullopt;
        targetConfigs.clear();
        for (const auto& c : configs) {
            if (c.IsBase)
                baseConfigOpt = c;
            else
                targetConfigs.append(c);
        }
    }

    QList<std::shared_ptr<IAssetItem>> assetsToProcess;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty()) {
        for (const auto& idx : selectedRows) {
            auto a = _assetModel.GetAsset(idx.row());
            if (a != nullptr)
                assetsToProcess.append(a);
        }
    } else {
        assetsToProcess = _currentDisplayList;
    }

    if (assetsToProcess.isEmpty())
        return;

    int count = 0;
    const RegionConfig* baseConfigPtr = baseConfigOpt.has_value() ? &baseConfigOpt.value() : nullptr;

    for (const auto& item : assetsToProcess) {
        EnsureAssetIsLocal(item);
        std::shared_ptr<IAssetItem> localItem = nullptr;
        if (item->isLeaderboard()) {
            for (const auto& l : _projectRepo.LocalData->Leaderboards)
                if (l->ID == item->ID) {
                    localItem = l;
                    break;
                }
        } else {
            for (const auto& a : _projectRepo.LocalData->Achievements)
                if (a->ID == item->ID) {
                    localItem = a;
                    break;
                }
        }

        if (localItem == nullptr)
            continue;

        _batchProcessor->ApplyMultiRegionToAsset(localItem, baseConfigPtr, targetConfigs);
        count++;
    }

    _isDirty = true;
    QMessageBox::information(this, "Batch Complete", QString("Multi-Region logic applied to %1 assets.").arg(count));
    ReloadDataView();
    if (_selectedAsset != nullptr)
        RefreshSelectedAssetAfterStoreChange();
    RunGlobalValidation();
}

bool MainWindow::IsAssetInLocalStore(const std::shared_ptr<IAssetItem>& item) {
    if (_projectRepo.LocalData == nullptr)
        return false;
    if (item->isLeaderboard()) {
        for (const auto& l : _projectRepo.LocalData->Leaderboards)
            if (l->ID == item->ID)
                return true;
        return false;
    }
    for (const auto& a : _projectRepo.LocalData->Achievements)
        if (a->ID == item->ID)
            return true;
    return false;
}

bool MainWindow::IsServerAssetId(int id, bool isLeaderboard) const {
    if (_projectRepo.ServerData == nullptr)
        return false;
    for (const auto& s : _projectRepo.ServerData->Sets) {
        if (isLeaderboard) {
            for (const auto& lb : s.Leaderboards)
                if (lb->ID == id)
                    return true;
        } else {
            for (const auto& ach : s.Achievements)
                if (ach->ID == id)
                    return true;
        }
    }
    return false;
}

} // namespace RALE
