#include "MainWindow.h"
#include "../Common/AppSettings.h"
#include "../Common/ImageCache.h"
#include "../Utilities/LogicFormatter.h"
#include "../Parsers/PointerTreeParser.h"
#include "../Parsers/RichPresenceParser.h"
#include "Dialogs/AssetDiffDialog.h"
#include "Dialogs/TextDiffDialog.h"
#include "Dialogs/MultiRegionDialog.h"
#include "Dialogs/LogicOptimizerDialog.h"
#include "Dialogs/BatchNoteMultiplierDialog.h"
#include "Dialogs/RevertRegionDialog.h"
#include <QApplication>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QMenuBar>
#include <QStatusBar>
#include <QFileDialog>
#include <QMessageBox>
#include <QCloseEvent>
#include <QShowEvent>
#include <QHeaderView>
#include <QKeyEvent>
#include <QRegularExpression>
#include <QTimer>
#include <QProcess>
#include <algorithm>

namespace RALogicEditor {

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
{
    // Load UI settings first so they are available to controls during construction
    AppSettings::Load();

    InitializeUI();

    // Initialize services that require dependencies
    _batchProcessor = std::make_unique<MultiRegionBatchProcessor>(_projectRepo.Metadata);

    ApplySettings();

    // --- Event Wiring ---

    // Trigger Editor (Logic)
    connect(triggerEditor, &TriggerEditorWidget::TriggerTextChanged, this, [this]() {
        if (_isLoading || _selectedAsset == nullptr) return;
        _projectRepo.EnsureLocalDataExists();
        EnsureAssetIsLocal();
        UpdateAssetState(_selectedAsset);
        _isDirty = true;
    });

    connect(triggerEditor, &TriggerEditorWidget::AchievementDataChanged, this, [this]() {
        if (_isLoading || _selectedAsset == nullptr) return;
        _projectRepo.EnsureLocalDataExists();

        auto selectedRows = achievementGrid->selectionModel()->selectedRows();
        if (selectedRows.size() > 1) ApplyBulkEdit(_selectedAsset);
        else
        {
            EnsureAssetIsLocal();
            UpdateAssetState(_selectedAsset);
        }

        _isDirty = true;
        _assetModel.NotifyAssetChanged(_selectedAsset);
        UpdateSelectionUI();
    });

    // The editor's Reset button targets the asset currently open in the editor only;
    // the asset-grid context-menu Reset keeps its multi-row behaviour.
    connect(triggerEditor, &TriggerEditorWidget::AssetResetRequested, this, [this]() {
        if (_selectedAsset == nullptr || _projectRepo.LocalData == nullptr) return;
        PushMainUndo();

        if (_selectedAsset->isLeaderboard())
        {
            for (int i = _projectRepo.LocalData->Leaderboards.size() - 1; i >= 0; --i)
                if (_projectRepo.LocalData->Leaderboards[i]->ID == _selectedAsset->ID)
                    _projectRepo.LocalData->Leaderboards.removeAt(i);
        }
        else
        {
            for (int i = _projectRepo.LocalData->Achievements.size() - 1; i >= 0; --i)
                if (_projectRepo.LocalData->Achievements[i]->ID == _selectedAsset->ID)
                    _projectRepo.LocalData->Achievements.removeAt(i);
        }

        _isDirty = true;
        _notesCacheDirty = true;
        ReloadDataView();
        LoadSelectedAsset();
    });

    connect(triggerEditor, &TriggerEditorWidget::AddFromCodeNoteRequested, this, [this]() {
        auto data = codeNoteEditor->GetSelectedLogic();
        if (!std::get<0>(data).isEmpty())
            triggerEditor->AddConditionFromNote(std::get<0>(data), std::get<1>(data), std::get<2>(data));
        else
            QMessageBox::information(this, "Add Note", "No valid address selected.");
    });

    connect(triggerEditor, &TriggerEditorWidget::ValidationCompleted, this, [this](const QList<ValidationIssue> &issues) {
        if (_selectedAsset == nullptr) return;
        for (int i = _globalIssues.size() - 1; i >= 0; --i)
        {
            if (_globalIssues[i].Asset != nullptr && _globalIssues[i].Asset->ID == _selectedAsset->ID)
                _globalIssues.removeAt(i);
        }
        _globalIssues.append(issues);
        if (!_isLoading) codeReviewControl->SetIssues(_globalIssues);
    });

    connect(triggerEditor, &TriggerEditorWidget::MultiRegionLogicRequested, this, &MainWindow::onMultiRegionBatch);

    connect(triggerEditor, &TriggerEditorWidget::LogicOptimizationRequested, this, &MainWindow::onLogicOptimizer);

    // Bridge Trigger Editor -> Code Note Editor (Highlighting)
    connect(triggerEditor, &TriggerEditorWidget::AddressSelected, this, [this](const QString &addr) {
        codeNoteEditor->HighlightAddress(addr);
    });

    connect(triggerEditor, &TriggerEditorWidget::PointerChainSelected, this, [this](const PointerChainArgs &e) {
        codeNoteEditor->HighlightAddress(e.BaseAddress);
        if (!e.Offsets.isEmpty()) codeNoteEditor->HighlightVariable(e.Offsets);
    });

    // Trigger Editor -> MainForm Bridge for Multiplier
    connect(triggerEditor, &TriggerEditorWidget::BatchMultiplyNotesRequested, this, &MainWindow::onBatchMultiplyNotes);

    // Code Reviewer
    connect(codeReviewControl, &CodeReviewWidget::IssueSelected, this, [this](ValidationIssue issue) {
        if (issue.Asset == nullptr) return;
        if (_selectedAsset == nullptr || _selectedAsset->ID != issue.Asset->ID) SelectAssetInGrid(issue.Asset);
        if (issue.Target.has_value())
            triggerEditor->SelectCondition(&issue.Target.value());
    });

    // Code Note Editor actions
    connect(codeNoteEditor, &CodeNoteEditorWidget::ReloadRequested, this, &MainWindow::onReload);
    connect(codeNoteEditor, &CodeNoteEditorWidget::SaveRequested, this, &MainWindow::onSaveFile);

    // Real-time grid updates & bidirectional note synchronization
    connect(codeNoteEditor, &CodeNoteEditorWidget::NoteUpdated, this, [this]() {
        if (_isLoading || _projectRepo.LocalData == nullptr) return;

        // NoteContentChanged also fires for selection changes. Only treat this as a
        // real edit when the note list actually differs; otherwise merely browsing
        // notes would mark the project dirty and prompt on close.
        const QList<CodeNote> notes = codeNoteEditor->GetAllNotes();
        if (_projectRepo.LocalData->Notes == notes) return;

        _projectRepo.LocalData->Notes = notes;
        triggerEditor->SetNotes(notes);
        _notesCacheDirty = true;
        _isDirty = true;
    });

    // Resets local note override
    connect(codeNoteEditor, &CodeNoteEditorWidget::NoteResetRequested, this, [this](const QString &address) {
        if (_projectRepo.LocalData == nullptr) return;

        PushMainUndo();
        _projectRepo.RevertNote(address);
        _isDirty = true;

        auto notes = _projectRepo.LocalData->Notes;
        codeNoteEditor->SetNotes(notes);
        triggerEditor->SetNotes(notes);
        _notesCacheDirty = true;
        RunGlobalValidation();
    });

    // Undo/Redo State Listeners
    _mainHistory.HistoryChanged = [this]() { UpdateUndoRedoState(); };
    connect(triggerEditor, &TriggerEditorWidget::HistoryChanged, this, &MainWindow::UpdateUndoRedoState);
    connect(qApp, &QApplication::focusChanged, this, [this](QWidget *, QWidget *) { UpdateUndoRedoState(); });

    // Initial Load
    _recentFiles.Load();
    UpdateRecentMenu();
    ToggleReviewer(AppSettings::Current.ShowLogicReviewer);
    UpdateUndoRedoState();
}

MainWindow::~MainWindow() = default;

void MainWindow::InitializeUI()
{
    setWindowTitle("RA Logic Editor");
    resize(1200, 700);

    auto *centralWidget = new QWidget(this);
    setCentralWidget(centralWidget);
    auto *centralLayout = new QVBoxLayout(centralWidget);
    centralLayout->setContentsMargins(0, 0, 0, 0);

    splitMain = new QSplitter(Qt::Horizontal, centralWidget);

    // Left Panel: Grid & Filters
    auto *leftPanel = new QWidget(splitMain);
    leftPanel->setMinimumWidth(260);
    auto *leftLayout = new QVBoxLayout(leftPanel);
    leftLayout->setContentsMargins(2, 2, 2, 2);
    leftLayout->setSpacing(4);

    // Top Filter Bar (Fixed 60px height)
    auto *panelGridTop = new QWidget(leftPanel);
    panelGridTop->setFixedHeight(60);
    auto *topGridVBox = new QVBoxLayout(panelGridTop);
    topGridVBox->setContentsMargins(0, 0, 0, 0);
    topGridVBox->setSpacing(2);

    auto *filterRow1 = new QHBoxLayout();
    filterRow1->setContentsMargins(0, 0, 0, 0);
    filterRow1->setSpacing(4);

    // Proportional stretch allocation and explicit minimum widths prevent text truncation (e.g. "JSON", "Unofficial")
    cmbAssetType = new QComboBox(panelGridTop);
    cmbAssetType->setMinimumWidth(100);
    ThemeManager::SetupComboBox(cmbAssetType, 14);
    cmbAssetType->addItems({ "Achievements", "Leaderboards" });
    filterRow1->addWidget(cmbAssetType, 11);

    cmbCategory = new QComboBox(panelGridTop);
    cmbCategory->setMinimumWidth(76);
    ThemeManager::SetupComboBox(cmbCategory, 14);
    cmbCategory->addItems({ "Core", "Unofficial", "Local" });
    filterRow1->addWidget(cmbCategory, 8);

    cmbSource = new QComboBox(panelGridTop);
    cmbSource->setMinimumWidth(68);
    ThemeManager::SetupComboBox(cmbSource, 14);
    cmbSource->addItems({ "Local", "JSON", "New" });
    filterRow1->addWidget(cmbSource, 7);
    topGridVBox->addLayout(filterRow1);

    auto *filterRow2 = new QHBoxLayout();
    filterRow2->setContentsMargins(0, 0, 0, 0);
    filterRow2->setSpacing(4);
    auto *lblSearch = new QLabel("Search:", panelGridTop);
    lblSearch->setFixedWidth(45);
    filterRow2->addWidget(lblSearch);
    txtAssetSearch = new QLineEdit(panelGridTop);
    txtAssetSearch->setFixedHeight(23);
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

    connect(achievementGrid->selectionModel(), &QItemSelectionModel::selectionChanged,
            this, &MainWindow::onAssetSelectionChanged);
    connect(achievementGrid, &QTableView::doubleClicked,
            this, &MainWindow::onAssetDoubleClicked);

    leftLayout->addWidget(achievementGrid, 1);

    // Bottom Button Bar (Fixed 35px height)
    auto *panelGridBottom = new QWidget(leftPanel);
    panelGridBottom->setFixedHeight(35);
    auto *bottomBtnLayout = new QHBoxLayout(panelGridBottom);
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
    splitMain->setSizes({ 260, 940 });
    splitEditor->setSizes({ 600, 340 });
    splitMiddle->setSizes({ 400, 250 });

    centralLayout->addWidget(splitMain);

    InitializeFilters();
    InitializeMainMenu();
    InitializeAssetContextMenu();
}

void MainWindow::InitializeFilters()
{
    cmbSource->clear();
    cmbSource->addItems({ "JSON", "Local", "New" });
    cmbCategory->clear();
    cmbCategory->addItems({ "Core", "Unofficial" });

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
    connect(cmbAssetType, QOverload<int>::of(&QComboBox::currentIndexChanged), this, [this]() {
        ReloadDataView();
        RunGlobalValidation();
    });
    connect(cmbSource, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &MainWindow::onFilterChanged);
    connect(cmbCategory, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &MainWindow::onFilterChanged);
    connect(txtAssetSearch, &QLineEdit::textChanged, this, &MainWindow::onSearchTextChanged);
}

void MainWindow::InitializeMainMenu()
{
    auto *menuBar = this->menuBar();

    // File Menu
    auto *fileMenu = menuBar->addMenu("&File");
    fileMenu->addAction("&Open...", this, &MainWindow::onOpenFile)->setShortcut(QKeySequence::Open);
    fileMenu->addAction("&Save User File", this, &MainWindow::onSaveFile)->setShortcut(QKeySequence::Save);
    recentMenu = fileMenu->addMenu("Recent Files");

    // Edit Menu
    auto *editMenu = menuBar->addMenu("&Edit");
    actUndo = editMenu->addAction("Undo", this, &MainWindow::onUndo);
    actUndo->setShortcut(QKeySequence::Undo);
    actRedo = editMenu->addAction("Redo", this, &MainWindow::onRedo);
    actRedo->setShortcut(QKeySequence::Redo);
    editMenu->addAction("Renumber Local IDs", this, &MainWindow::onRenumberLocalIDs);
    editMenu->addAction("Clear Data / Reset...", this, &MainWindow::onClearDataReset);
    editMenu->addSeparator();
    editMenu->addAction("Revert Rich Presence", this, &MainWindow::onRevertRichPresence);

    // View Menu
    auto *viewMenu = menuBar->addMenu("&View");
    actViewReviewer = viewMenu->addAction("Logic Reviewer", this, &MainWindow::onToggleReviewer);
    actViewReviewer->setShortcut(QKeySequence(Qt::Key_F8));
    actViewReviewer->setCheckable(true);
    viewMenu->addAction("View RP Diff", this, &MainWindow::onViewRpDiff);

    // Theme Submenu
    viewMenu->addSeparator();
    themeMenu = viewMenu->addMenu("&Theme");
    auto *themeGroup = new QActionGroup(this);

    actThemeLight = themeMenu->addAction("Light", this, [this]() { onSetTheme(ThemeManager::Theme::Light); });
    actThemeLight->setCheckable(true);
    themeGroup->addAction(actThemeLight);

    actThemeDark = themeMenu->addAction("Dark", this, [this]() { onSetTheme(ThemeManager::Theme::Dark); });
    actThemeDark->setCheckable(true);
    themeGroup->addAction(actThemeDark);

    bool isDark = (AppSettings::Current.Theme.compare("Dark", Qt::CaseInsensitive) == 0);
    actThemeDark->setChecked(isDark);
    actThemeLight->setChecked(!isDark);
}

void MainWindow::onSetTheme(ThemeManager::Theme theme)
{
    ThemeManager::ApplyTheme(theme);
    AppSettings::Current.Theme = (theme == ThemeManager::Theme::Dark) ? "Dark" : "Light";
    // Persist the live geometry along with the theme instead of writing stale values.
    SaveSettings();

    if (actThemeLight) actThemeLight->setChecked(theme == ThemeManager::Theme::Light);
    if (actThemeDark) actThemeDark->setChecked(theme == ThemeManager::Theme::Dark);
}

void MainWindow::InitializeAssetContextMenu()
{
    assetContextMenu = new QMenu(this);

    actNewAsset = assetContextMenu->addAction("New", this, &MainWindow::onNewAsset);
    actDuplicateAsset = assetContextMenu->addAction("Duplicate", this, &MainWindow::onDuplicateAsset);
    actDuplicateAsset->setShortcut(QKeySequence("Ctrl+D"));
    actDeleteAsset = assetContextMenu->addAction("Delete", this, &MainWindow::onDeleteAsset);
    actDeleteAsset->setShortcut(QKeySequence::Delete);
    actResetAsset = assetContextMenu->addAction("Reset", this, &MainWindow::onResetAsset);

    assetContextMenu->addSeparator();

    actMoveUp = assetContextMenu->addAction("Move Up", this, &MainWindow::onMoveAssetUp);
    actMoveUp->setShortcut(QKeySequence("Alt+Up"));
    actMoveDown = assetContextMenu->addAction("Move Down", this, &MainWindow::onMoveAssetDown);
    actMoveDown->setShortcut(QKeySequence("Alt+Down"));

    assetContextMenu->addSeparator();

    actRestoreOrder = assetContextMenu->addAction("Restore Default Order", this, &MainWindow::onRestoreOrder);

    assetContextMenu->addSeparator();

    actRenumberIDs = assetContextMenu->addAction("Renumber Local IDs", this, &MainWindow::onRenumberLocalIDs);
    actViewDiff = assetContextMenu->addAction("View Diff (Server/Local)", this, &MainWindow::onViewDiff);

    assetContextMenu->addSeparator();

    actRevertRegion = assetContextMenu->addAction("Revert Multi-Region...", this, &MainWindow::onRevertRegion);

    achievementGrid->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(achievementGrid, &QWidget::customContextMenuRequested, this, [this](const QPoint &pos) {
        UpdateSelectionUI();
        assetContextMenu->exec(achievementGrid->viewport()->mapToGlobal(pos));
    });
}

bool MainWindow::eventFilter(QObject *watched, QEvent *event)
{
    if (watched == achievementGrid && event->type() == QEvent::KeyPress)
    {
        auto *keyEvent = static_cast<QKeyEvent *>(event);

        if (keyEvent->key() == Qt::Key_Return || keyEvent->key() == Qt::Key_Enter)
        {
            LoadSelectedAsset();
            return true;
        }
        else if (keyEvent->modifiers() == Qt::AltModifier && keyEvent->key() == Qt::Key_Up)
        {
            if (actMoveUp && actMoveUp->isEnabled()) onMoveAssetUp();
            return true;
        }
        else if (keyEvent->modifiers() == Qt::AltModifier && keyEvent->key() == Qt::Key_Down)
        {
            if (actMoveDown && actMoveDown->isEnabled()) onMoveAssetDown();
            return true;
        }
        else if (keyEvent->key() == Qt::Key_Delete)
        {
            // C# deletes the Local subset of a mixed selection, so only require a selection.
            if (!achievementGrid->selectionModel()->selectedRows().isEmpty()) onDeleteAsset();
            return true;
        }
        else if (keyEvent->modifiers() == Qt::ControlModifier && keyEvent->key() == Qt::Key_D)
        {
            if (btnDuplicateAsset && btnDuplicateAsset->isEnabled()) onDuplicateAsset();
            return true;
        }
    }

    return QMainWindow::eventFilter(watched, event);
}

void MainWindow::showEvent(QShowEvent *event)
{
    QMainWindow::showEvent(event);
    if (_isFirstShow)
    {
        _isFirstShow = false;
        if (AppSettings::Current.MainSplitterDistance > 0)
        {
            int rightW = std::max(200, width() - AppSettings::Current.MainSplitterDistance);
            splitMain->setSizes({ AppSettings::Current.MainSplitterDistance, rightW });
        }
        if (AppSettings::Current.EditorSplitterDistance > 0)
        {
            int rightW = std::max(280, splitEditor->width() - AppSettings::Current.EditorSplitterDistance);
            splitEditor->setSizes({ AppSettings::Current.EditorSplitterDistance, rightW });
        }
        if (AppSettings::Current.VerticalSplitterDistance > 0)
        {
            int bottomH = std::max(100, splitMiddle->height() - AppSettings::Current.VerticalSplitterDistance);
            splitMiddle->setSizes({ AppSettings::Current.VerticalSplitterDistance, bottomH });
        }
    }
}

void MainWindow::ApplySettings()
{
    if (AppSettings::Current.WindowWidth > 0 && AppSettings::Current.WindowHeight > 0)
        resize(AppSettings::Current.WindowWidth, AppSettings::Current.WindowHeight);

    if (AppSettings::Current.WindowX != -1 && AppSettings::Current.WindowY != -1)
        move(AppSettings::Current.WindowX, AppSettings::Current.WindowY);

    if (AppSettings::Current.IsMaximized) setWindowState(Qt::WindowMaximized);

    triggerEditor->SetShowDecimal(AppSettings::Current.Trigger_ShowDecimal);
    triggerEditor->SetShowAliases(AppSettings::Current.Trigger_ShowAliases);
    triggerEditor->SetCollapseChains(AppSettings::Current.Trigger_CollapseChains);
    codeNoteEditor->SetShowLeafsOnly(AppSettings::Current.Notes_ShowLeafsOnly);
    codeNoteEditor->SetIndentValues(AppSettings::Current.Notes_IndentValues);
    codeNoteEditor->SetNoteIndentStyle(AppSettings::Current.NoteIndentStyle);
}

void MainWindow::SaveSettings()
{
    const bool minimized = isMinimized();

    if (!isMaximized() && !minimized)
    {
        AppSettings::Current.WindowX = pos().x();
        AppSettings::Current.WindowY = pos().y();
        AppSettings::Current.WindowWidth = width();
        AppSettings::Current.WindowHeight = height();
        AppSettings::Current.IsMaximized = false;
    }
    else
    {
        AppSettings::Current.IsMaximized = isMaximized();

        // Keep the restored (non-maximized) geometry so un-maximizing after a
        // restart does not snap back to the stale/default 1200x700 size.
        QRect normal = normalGeometry();
        if (normal.width() > 0 && normal.height() > 0)
        {
            AppSettings::Current.WindowX = normal.x();
            AppSettings::Current.WindowY = normal.y();
            AppSettings::Current.WindowWidth = normal.width();
            AppSettings::Current.WindowHeight = normal.height();
        }
    }

    // Never persist splitter sizes while minimized (Qt reports collapsed sizes).
    if (!minimized)
    {
        AppSettings::Current.MainSplitterDistance = splitMain->sizes().value(0, 260);
        AppSettings::Current.EditorSplitterDistance = splitEditor->sizes().value(0, 600);
        if (codeReviewControl != nullptr && codeReviewControl->isVisible())
        {
            AppSettings::Current.VerticalSplitterDistance = splitMiddle->sizes().value(0, 400);
        }
    }

    AppSettings::Current.LastAssetTypeIndex = cmbAssetType->currentIndex();
    AppSettings::Current.LastCategoryIndex = cmbCategory->currentIndex();
    AppSettings::Current.LastSourceIndex = cmbSource->currentIndex();

    AppSettings::Current.Trigger_ShowDecimal = triggerEditor->ShowDecimal();
    AppSettings::Current.Trigger_ShowAliases = triggerEditor->ShowAliases();
    AppSettings::Current.Trigger_CollapseChains = triggerEditor->CollapseChains();

    AppSettings::Current.Notes_ShowLeafsOnly = codeNoteEditor->ShowLeafsOnly();
    AppSettings::Current.Notes_IndentValues = codeNoteEditor->IndentValues();
    AppSettings::Current.ShowLogicReviewer = actViewReviewer->isChecked();

    AppSettings::Save();
}

void MainWindow::ToggleReviewer(std::optional<bool> forceState)
{
    bool newState = forceState.has_value() ? forceState.value() : !codeReviewControl->isVisible();
    actViewReviewer->setChecked(newState);
    codeReviewControl->setVisible(newState);

    if (newState)
    {
        int totalH = splitMiddle->height();
        int topH = (AppSettings::Current.VerticalSplitterDistance > 0 && AppSettings::Current.VerticalSplitterDistance < totalH - 60)
            ? AppSettings::Current.VerticalSplitterDistance
            : std::max(100, totalH * 60 / 100);
        int bottomH = std::max(60, totalH - topH);
        splitMiddle->setSizes({ topH, bottomH });
    }
}

void MainWindow::UpdateUndoRedoState()
{
    QWidget *focus = QApplication::focusWidget();
    bool isTriggerContext = focus && (focus == triggerEditor || triggerEditor->isAncestorOf(focus));
    bool isNoteContext = focus && (focus == codeNoteEditor || codeNoteEditor->isAncestorOf(focus));

    if (isTriggerContext)
    {
        actUndo->setEnabled(triggerEditor->CanUndo());
        actRedo->setEnabled(triggerEditor->CanRedo());
    }
    else if (isNoteContext)
    {
        actUndo->setEnabled(codeNoteEditor->CanUndo());
        actRedo->setEnabled(codeNoteEditor->CanRedo());
    }
    else
    {
        actUndo->setEnabled(_mainHistory.CanUndo());
        actRedo->setEnabled(_mainHistory.CanRedo());
    }
}

void MainWindow::UpdateRecentMenu()
{
    recentMenu->clear();
    for (const auto &entry : _recentFiles.Files)
    {
        QString text = entry.GameTitle.isEmpty() ? entry.Path : QString("%1 | %2").arg(entry.GameTitle, entry.Path);
        QString path = entry.Path;
        recentMenu->addAction(text, this, [this, path]() {
            // Defer: LoadFiles rebuilds this menu, which would delete the QAction
            // whose triggered() signal is still on the stack.
            QTimer::singleShot(0, this, [this, path]() {
                if (ConfirmDiscardChanges())
                {
                    txtAssetSearch->clear();
                    LoadFiles(path);
                }
            });
        });
    }
}

bool MainWindow::ConfirmDiscardChanges()
{
    if (!_isDirty) return true;

    auto result = QMessageBox::warning(
        this,
        "Unsaved Changes",
        "You have unsaved changes. Any unsaved local changes will be lost.\n\nContinue?",
        QMessageBox::Yes | QMessageBox::No,
        QMessageBox::No);

    return result == QMessageBox::Yes;
}

void MainWindow::closeEvent(QCloseEvent *event)
{
    if (!ConfirmDiscardChanges())
    {
        event->ignore();
        return;
    }
    SaveSettings();
    event->accept();
}

// Full Project Load Logic
void MainWindow::LoadFiles(const QString &primaryPath)
{
    _isLoading = true;

    try
    {
        ImageCache::Clear();
        ImageCache::ConfigureLocalPaths(primaryPath);

        _recentFiles.Add(primaryPath);
        UpdateRecentMenu();

        _currentFilePath = primaryPath;
        _selectedAsset = nullptr;
        _currentDisplayList.clear();
        _mainHistory.Clear();
        // Drop the old model/selection before loading; otherwise ReloadDataView can
        // carry an ID over and auto-select an unrelated asset in the new project.
        achievementGrid->clearSelection();
        _assetModel.SetAssets({});
        triggerEditor->Clear();

        try
        {
            _projectRepo.LoadFiles(primaryPath, _currentUserFilePath);
            _rpMetadata.Load(_projectRepo.CurrentGameId);
        }
        catch (const std::exception &ex)
        {
            QMessageBox::critical(this, "Load Error", QString("Error loading files: %1").arg(ex.what()));
            _isLoading = false;
            return;
        }

        if (_projectRepo.ServerData != nullptr)
            this->setWindowTitle(QString("RA Logic Editor - %1").arg(_projectRepo.CurrentTitle));
        else
            this->setWindowTitle(QString("RA Logic Editor - %1").arg(QFileInfo(primaryPath).fileName()));

        bool hasServerAssets = false;
        if (_projectRepo.ServerData != nullptr)
        {
            for (const auto &s : _projectRepo.ServerData->Sets)
            {
                if (!s.Achievements.isEmpty() || !s.Leaderboards.isEmpty())
                {
                    hasServerAssets = true;
                    break;
                }
            }
        }

        QString targetSource = hasServerAssets ? "JSON" : "New";

        if (cmbSource->currentText() != targetSource)
        {
            int idx = cmbSource->findText(targetSource);
            if (idx != -1) cmbSource->setCurrentIndex(idx);
        }

        ReloadDataView();

        auto notes = (_projectRepo.LocalData != nullptr) ? _projectRepo.LocalData->Notes : QList<CodeNote>();
        codeNoteEditor->SetNotes(notes);
        triggerEditor->SetNotes(notes);

        _notesCacheDirty = true;
        RunGlobalValidation();
        UpdateUndoRedoState();

        _isDirty = false;
    }
    catch (...) {}

    _isLoading = false;
}

void MainWindow::onFilterChanged()
{
    ReloadDataView();
}

void MainWindow::onSearchTextChanged(const QString & /*text*/)
{
    ReloadDataView();
}

void MainWindow::ReloadDataView()
{
    int savedId = -1;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty())
    {
        auto item = _assetModel.GetAsset(selectedRows.first().row());
        if (item != nullptr) savedId = item->ID;
    }

    PopulateList();

    if (savedId != -1)
    {
        int row = _assetModel.FindRowByAssetId(savedId);
        if (row != -1)
        {
            achievementGrid->selectRow(row);
        }
    }
}

void MainWindow::PopulateList()
{
    _currentDisplayList.clear();

    bool showLeaderboards = (cmbAssetType->currentIndex() == 1);
    QString sourceMode = cmbSource->currentText();
    QString categoryFilter = cmbCategory->currentText();
    QString filterText = txtAssetSearch->text().trimmed();

    auto mergedList = _projectRepo.GetMergedAssets(showLeaderboards);

    for (const auto &item : mergedList)
    {
        UpdateAssetState(item);

        if (sourceMode == "New") { if (item->State != AssetState::Local) continue; }
        else if (sourceMode == "JSON") { if (item->State == AssetState::Local) continue; }
        else { if (item->State == AssetState::Synced) continue; }

        if (sourceMode != "New")
        {
            QString itemCat = item->Category.isEmpty() ? "Local" : item->Category;
            if (itemCat.compare(categoryFilter, Qt::CaseInsensitive) != 0) continue;
        }

        if (!filterText.isEmpty())
        {
            bool matchesId = QString::number(item->ID).contains(filterText);
            bool matchesTitle = item->Title.contains(filterText, Qt::CaseInsensitive);
            if (!matchesId && !matchesTitle) continue;
        }

        _currentDisplayList.append(item);
    }

    _assetModel.SetAssets(_currentDisplayList);
    UpdateSelectionUI();
}

void MainWindow::LoadSelectedAsset()
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty())
    {
        int rowIdx = selectedRows.first().row();
        auto asset = _assetModel.GetAsset(rowIdx);
        if (asset != nullptr)
        {
            _isLoading = true;
            if (asset->State == AssetState::Synced)
            {
                auto clone = asset->clone(false);
                _selectedAsset = clone;
                int dispIdx = _currentDisplayList.indexOf(asset);
                if (dispIdx != -1) _currentDisplayList[dispIdx] = clone;
                _assetModel.ReplaceAsset(rowIdx, clone);
            }
            else
            {
                _selectedAsset = asset;
            }

            std::unique_ptr<IAssetStrategy> strategy;
            if (_selectedAsset->isLeaderboard()) strategy = std::make_unique<LeaderboardStrategy>();
            else strategy = std::make_unique<AchievementStrategy>();

            auto segments = strategy->ParseLogic(_selectedAsset->Logic);
            for (auto &groupList : segments)
                _projectRepo.Metadata.ApplyNamesToAsset(_selectedAsset->ID, groupList);

            QString displayCat = _selectedAsset->Category.isEmpty() ? "Core"
                : _selectedAsset->Category.left(1).toUpper() + _selectedAsset->Category.mid(1);

            triggerEditor->BindAchievement(_selectedAsset, displayCat);
            _isLoading = false;
        }
    }
    UpdateSelectionUI();
}

void MainWindow::UpdateAssetState(const std::shared_ptr<IAssetItem> &item)
{
    if (item == nullptr) return;
    if (_projectRepo.ServerData == nullptr) { item->State = AssetState::Local; return; }

    std::shared_ptr<IAssetItem> serverItem = nullptr;
    for (const auto &s : _projectRepo.ServerData->Sets)
    {
        if (item->isLeaderboard())
        {
            for (const auto &lb : s.Leaderboards)
                if (lb->ID == item->ID) { serverItem = lb; break; }
        }
        else
        {
            for (const auto &ach : s.Achievements)
                if (ach->ID == item->ID) { serverItem = ach; break; }
        }
        if (serverItem != nullptr) break;
    }

    if (serverItem == nullptr)
    {
        item->State = AssetState::Local;
    }
    else
    {
        bool equal = (item->Logic == serverItem->Logic && item->Title == serverItem->Title && item->Description == serverItem->Description);
        if (!item->isLeaderboard() && !serverItem->isLeaderboard())
        {
            auto a = std::static_pointer_cast<RaAchievement>(item);
            auto b = std::static_pointer_cast<RaAchievement>(serverItem);
            if (a->Points != b->Points || a->Type != b->Type) equal = false;
        }
        else if (item->isLeaderboard() && serverItem->isLeaderboard())
        {
            auto la = std::static_pointer_cast<RaLeaderboard>(item);
            auto lb = std::static_pointer_cast<RaLeaderboard>(serverItem);
            if (la->Format != lb->Format || la->LowerIsBetter != lb->LowerIsBetter) equal = false;
        }
        item->State = equal ? AssetState::Synced : AssetState::Modified;
    }

    // Immediately notify the asset model so modified rows update color in real time
    _assetModel.NotifyAssetChanged(item);
}

void MainWindow::UpdateSelectionUI()
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    bool hasSelection = !selectedRows.isEmpty();
    bool allLocal = true;
    bool anyModified = false;

    for (const auto &idx : selectedRows)
    {
        auto asset = _assetModel.GetAsset(idx.row());
        if (asset != nullptr)
        {
            if (asset->State != AssetState::Local) allLocal = false;
            if (asset->State == AssetState::Modified) anyModified = true;
        }
    }

    btnDuplicateAsset->setEnabled(hasSelection);
    btnDeleteAsset->setEnabled(hasSelection && allLocal);

    if (actDuplicateAsset) actDuplicateAsset->setEnabled(hasSelection);
    if (actDeleteAsset) actDeleteAsset->setEnabled(hasSelection && allLocal);
    if (actResetAsset) actResetAsset->setEnabled(hasSelection && anyModified);
    if (actMoveUp) actMoveUp->setEnabled(hasSelection && allLocal);
    if (actMoveDown) actMoveDown->setEnabled(hasSelection && allLocal);
    if (actViewDiff) actViewDiff->setEnabled(selectedRows.size() == 1 && anyModified);
    if (actRenumberIDs) actRenumberIDs->setEnabled(_projectRepo.LocalData != nullptr && (!_projectRepo.LocalData->Achievements.isEmpty() || !_projectRepo.LocalData->Leaderboards.isEmpty()));
}

void MainWindow::onAssetSelectionChanged()
{
    UpdateSelectionUI();
}

void MainWindow::onAssetDoubleClicked(const QModelIndex & /*index*/)
{
    LoadSelectedAsset();
}

void MainWindow::SelectAssetInGrid(const std::shared_ptr<IAssetItem> &asset)
{
    if (asset == nullptr) return;

    int typeIndex = asset->isLeaderboard() ? 1 : 0;
    if (cmbAssetType->currentIndex() != typeIndex)
        cmbAssetType->setCurrentIndex(typeIndex);

    QString targetCat = asset->Category.isEmpty() ? "Local" : asset->Category;
    int catIdx = cmbCategory->findText(targetCat, Qt::MatchFixedString);
    if (catIdx != -1 && cmbCategory->currentIndex() != catIdx)
        cmbCategory->setCurrentIndex(catIdx);

    int sourceIndex = cmbSource->currentIndex();
    switch (asset->State)
    {
        case AssetState::Local: sourceIndex = 2; break;
        case AssetState::Modified: sourceIndex = 1; break;
        case AssetState::Synced: sourceIndex = 0; break;
    }
    if (cmbSource->currentIndex() != sourceIndex)
        cmbSource->setCurrentIndex(sourceIndex);

    ReloadDataView();

    int row = _assetModel.FindRowByAssetId(asset->ID);
    if (row != -1)
    {
        achievementGrid->selectRow(row);
        LoadSelectedAsset();
    }
}

void MainWindow::RebuildNotesCache()
{
    _cachedNotesLookup.clear();
    _cachedRangeCache.clear();
    PointerTreeParser parser;

    if (_projectRepo.LocalData != nullptr)
    {
        // Pass 1: Parse all real notes
        for (const auto &n : _projectRepo.LocalData->Notes)
        {
            QString key = LogicFormatter::NormalizeAddress(n.Address);
            auto nodes = parser.ParseNoteText(n.Note);
            if (!_cachedNotesLookup.contains(key)) _cachedNotesLookup.insert(key, nodes);

            if (!nodes.isEmpty() && nodes[0]->Size > 1)
            {
                bool ok = false;
                QString clean = key;
                clean.remove("0x", Qt::CaseInsensitive);
                int64_t startAddr = clean.toLongLong(&ok, 16);
                if (ok)
                {
                    _cachedRangeCache.append(ConditionFormatter::RangeEntry{ startAddr, startAddr + nodes[0]->Size, nodes[0] });
                }
            }
        }

        // Pass 2: Resolve redirects (refer to $0x...) to eliminate false-positive validation warnings
        static const QRegularExpression redirectRegex(R"(refer to \$0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
        static const QRegularExpression structRegex(
            QString::fromUtf8(R"(^[.\+\s\x{2500}-\x{257F}])"),
            QRegularExpression::MultilineOption);

        for (const auto &note : _projectRepo.LocalData->Notes)
        {
            bool hasStructure = structRegex.match(note.Note).hasMatch();
            if (!hasStructure)
            {
                auto match = redirectRegex.match(note.Note);
                if (match.hasMatch())
                {
                    QString targetAddr = LogicFormatter::NormalizeAddress(match.captured(1));
                    QString sourceKey = LogicFormatter::NormalizeAddress(note.Address);

                    if (_cachedNotesLookup.contains(targetAddr))
                    {
                        _cachedNotesLookup.insert(sourceKey, _cachedNotesLookup.value(targetAddr));
                    }
                }
            }
        }
    }
    _notesCacheDirty = false;
}

void MainWindow::RunGlobalValidation()
{
    _globalIssues.clear();
    bool validateLeaderboards = (cmbAssetType->currentIndex() == 1);
    auto allAssets = _projectRepo.GetMergedAssets(validateLeaderboards);

    if (_notesCacheDirty)
    {
        RebuildNotesCache();
    }

    for (const auto &asset : allAssets)
    {
        std::unique_ptr<IAssetStrategy> strategy;
        if (asset->isLeaderboard()) strategy = std::make_unique<LeaderboardStrategy>();
        else strategy = std::make_unique<AchievementStrategy>();

        auto segments = strategy->ParseLogic(asset->Logic);
        QList<AchievementConditionGroup> allGroups;
        // Preserve segment insertion order (Start/Cancel/Submit/Value) like the C#
        // dictionary; QMap iteration is alphabetical and would make Cancel "Core".
        for (const QString &segmentName : strategy->GetSegmentNames())
        {
            auto it = segments.constFind(segmentName);
            if (it != segments.constEnd())
                allGroups.append(it.value());
        }
        // Include any segment the strategy does not name (defensive).
        for (auto it = segments.cbegin(); it != segments.cend(); ++it)
        {
            if (strategy->GetSegmentNames().contains(it.key()))
                continue;
            allGroups.append(it.value());
        }

        auto issues = _validator.ValidateAsset(allGroups, asset, &_cachedNotesLookup, &_cachedRangeCache);
        _globalIssues.append(issues);
    }

    codeReviewControl->SetIssues(_globalIssues);
}

void MainWindow::PushMainUndo()
{
    UserTextParser::UserData snapshot;
    if (_projectRepo.LocalData != nullptr)
    {
        for (const auto &ach : _projectRepo.LocalData->Achievements)
            snapshot.Achievements.append(ach->cloneAchievement(false));
        for (const auto &lb : _projectRepo.LocalData->Leaderboards)
            snapshot.Leaderboards.append(lb->cloneLeaderboard(false));
        for (const auto &n : _projectRepo.LocalData->Notes)
            snapshot.Notes.append(CodeNote{ n.User, n.Address, n.Note });
    }
    _mainHistory.PushState(snapshot);
}

void MainWindow::UndoMain()
{
    if (_projectRepo.LocalData == nullptr) return;
    auto previousState = _mainHistory.Undo(*_projectRepo.LocalData);
    _projectRepo.LocalData->Achievements = previousState.Achievements;
    _projectRepo.LocalData->Leaderboards = previousState.Leaderboards;
    _projectRepo.LocalData->Notes = previousState.Notes;
    _isDirty = true;
    _notesCacheDirty = true;
    // Reload the note editors too; otherwise the next note selection emits
    // NoteUpdated and writes the pre-undo note list straight back into LocalData.
    codeNoteEditor->SetNotes(_projectRepo.LocalData->Notes);
    triggerEditor->SetNotes(_projectRepo.LocalData->Notes);
    ReloadDataView();
    LoadSelectedAsset();
}

void MainWindow::RedoMain()
{
    if (_projectRepo.LocalData == nullptr) return;
    auto nextState = _mainHistory.Redo(*_projectRepo.LocalData);
    _projectRepo.LocalData->Achievements = nextState.Achievements;
    _projectRepo.LocalData->Leaderboards = nextState.Leaderboards;
    _projectRepo.LocalData->Notes = nextState.Notes;
    _isDirty = true;
    _notesCacheDirty = true;
    codeNoteEditor->SetNotes(_projectRepo.LocalData->Notes);
    triggerEditor->SetNotes(_projectRepo.LocalData->Notes);
    ReloadDataView();
    LoadSelectedAsset();
}

void MainWindow::onUndo()
{
    QWidget *focus = QApplication::focusWidget();
    bool isTriggerContext = focus && (focus == triggerEditor || triggerEditor->isAncestorOf(focus));
    bool isNoteContext = focus && (focus == codeNoteEditor || codeNoteEditor->isAncestorOf(focus));

    if (isTriggerContext) triggerEditor->Undo();
    else if (isNoteContext) codeNoteEditor->Undo();
    else UndoMain();
}

void MainWindow::onRedo()
{
    QWidget *focus = QApplication::focusWidget();
    bool isTriggerContext = focus && (focus == triggerEditor || triggerEditor->isAncestorOf(focus));
    bool isNoteContext = focus && (focus == codeNoteEditor || codeNoteEditor->isAncestorOf(focus));

    if (isTriggerContext) triggerEditor->Redo();
    else if (isNoteContext) codeNoteEditor->Redo();
    else RedoMain();
}

void MainWindow::onReload()
{
    if (_currentFilePath.isEmpty()) return;
    if (!ConfirmDiscardChanges()) return;

    int selectedId = -1;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty())
    {
        auto item = _assetModel.GetAsset(selectedRows.first().row());
        if (item != nullptr) selectedId = item->ID;
    }

    int savedSource = cmbSource->currentIndex();
    int savedCategory = cmbCategory->currentIndex();
    int savedType = cmbAssetType->currentIndex();

    LoadFiles(_currentFilePath);

    cmbAssetType->setCurrentIndex(savedType);
    cmbCategory->setCurrentIndex(savedCategory);
    cmbSource->setCurrentIndex(savedSource);

    if (selectedId != -1)
    {
        int row = _assetModel.FindRowByAssetId(selectedId);
        if (row != -1)
        {
            achievementGrid->selectRow(row);
            LoadSelectedAsset();
        }
    }
}

void MainWindow::EnsureAssetIsLocal(const std::shared_ptr<IAssetItem> &asset)
{
    if (asset == nullptr || _projectRepo.LocalData == nullptr) return;
    if (asset->State == AssetState::Local || asset->State == AssetState::Modified) return;

    if (!asset->isLeaderboard())
    {
        auto ra = std::static_pointer_cast<RaAchievement>(asset);
        bool exists = false;
        for (const auto &a : _projectRepo.LocalData->Achievements)
            if (a->ID == ra->ID) { exists = true; break; }

        if (!exists)
        {
            if (asset == _selectedAsset)
                _projectRepo.LocalData->Achievements.append(ra);
            else
                _projectRepo.LocalData->Achievements.append(ra->cloneAchievement(false));
        }
    }
    else
    {
        auto rl = std::static_pointer_cast<RaLeaderboard>(asset);
        bool exists = false;
        for (const auto &l : _projectRepo.LocalData->Leaderboards)
            if (l->ID == rl->ID) { exists = true; break; }

        if (!exists)
        {
            if (asset == _selectedAsset)
                _projectRepo.LocalData->Leaderboards.append(rl);
            else
                _projectRepo.LocalData->Leaderboards.append(rl->cloneLeaderboard(false));
        }
    }
}

void MainWindow::EnsureAssetIsLocal()
{
    EnsureAssetIsLocal(_selectedAsset);
}

QString MainWindow::InvertOperator(const QString &op)
{
    if (op == "=") return "!=";
    if (op == "!=") return "=";
    if (op == "<") return ">=";
    if (op == "<=") return ">";
    if (op == ">") return "<=";
    if (op == ">=") return "<";
    return op;
}

void MainWindow::ApplyBulkEdit(const std::shared_ptr<IAssetItem> &sourceTemplate)
{
    if (sourceTemplate == nullptr || sourceTemplate->isLeaderboard()) return;
    auto srcAch = std::static_pointer_cast<RaAchievement>(sourceTemplate);

    for (const auto &idx : achievementGrid->selectionModel()->selectedRows())
    {
        auto target = _assetModel.GetAsset(idx.row());
        if (target != nullptr && target != sourceTemplate && !target->isLeaderboard())
        {
            auto targetAch = std::static_pointer_cast<RaAchievement>(target);
            EnsureAssetIsLocal(targetAch);

            std::shared_ptr<RaAchievement> localAch = nullptr;
            for (const auto &a : _projectRepo.LocalData->Achievements)
            {
                if (a->ID == targetAch->ID) { localAch = a; break; }
            }

            if (localAch != nullptr)
            {
                localAch->Points = srcAch->Points;
                localAch->Type = srcAch->Type;
                UpdateAssetState(localAch);
                int dispIdx = _currentDisplayList.indexOf(target);
                if (dispIdx != -1) _currentDisplayList[dispIdx] = localAch;
                _assetModel.ReplaceAsset(idx.row(), localAch);
            }
        }
    }
}

void MainWindow::onNewAsset()
{
    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    int newId = 111000001;
    if (_projectRepo.LocalData != nullptr)
    {
        QList<int> localIds;
        for (const auto &a : _projectRepo.LocalData->Achievements) localIds.append(a->ID);
        for (const auto &l : _projectRepo.LocalData->Leaderboards) localIds.append(l->ID);

        if (!localIds.isEmpty())
        {
            int maxId = *std::max_element(localIds.begin(), localIds.end());
            newId = (maxId < 111000000) ? 111000001 : maxId + 1;
        }
    }

    bool isLeaderboard = (cmbAssetType->currentIndex() == 1);
    if (isLeaderboard)
    {
        auto lb = std::make_shared<RaLeaderboard>();
        lb->ID = newId;
        lb->Title = "New Leaderboard";
        lb->State = AssetState::Local;
        lb->Category = "Local";
        _projectRepo.LocalData->Leaderboards.append(lb);
    }
    else
    {
        auto ach = std::make_shared<RaAchievement>();
        ach->ID = newId;
        ach->Title = "New Achievement";
        ach->State = AssetState::Local;
        ach->Category = "Local";
        _projectRepo.LocalData->Achievements.append(ach);
    }

    if (cmbSource->currentText() != "New")
    {
        int idx = cmbSource->findText("New");
        if (idx != -1) cmbSource->setCurrentIndex(idx);
    }

    _isDirty = true;
    ReloadDataView();

    int row = _assetModel.FindRowByAssetId(newId);
    if (row != -1)
    {
        achievementGrid->selectRow(row);
        LoadSelectedAsset();
    }
}

void MainWindow::onDuplicateAsset()
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty()) return;

    std::sort(selectedRows.begin(), selectedRows.end(), [](const QModelIndex &a, const QModelIndex &b) {
        return a.row() < b.row();
    });

    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    int nextId = 111000001;
    if (_projectRepo.LocalData != nullptr)
    {
        QList<int> localIds;
        for (const auto &a : _projectRepo.LocalData->Achievements) localIds.append(a->ID);
        for (const auto &l : _projectRepo.LocalData->Leaderboards) localIds.append(l->ID);

        if (!localIds.isEmpty())
        {
            int maxId = *std::max_element(localIds.begin(), localIds.end());
            nextId = (maxId >= 111000000) ? maxId + 1 : 111000001;
        }
    }

    QList<int> newIds;
    for (const auto &idx : selectedRows)
    {
        auto source = _assetModel.GetAsset(idx.row());
        if (source != nullptr)
        {
            if (!source->isLeaderboard())
            {
                auto c = std::static_pointer_cast<RaAchievement>(source->clone(false));
                c->Title += " (Copy)";
                c->ID = nextId++;
                c->State = AssetState::Local;
                c->Category = "Local";
                _projectRepo.LocalData->Achievements.append(c);
                newIds.append(c->ID);
            }
            else
            {
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
    if (sIdx != -1) cmbSource->setCurrentIndex(sIdx);

    _isDirty = true;
    ReloadDataView();

    achievementGrid->clearSelection();
    QItemSelection newSelection;
    int firstNewRow = -1;
    for (int id : newIds)
    {
        int r = _assetModel.FindRowByAssetId(id);
        if (r == -1) continue;
        newSelection.select(_assetModel.index(r, 0), _assetModel.index(r, _assetModel.columnCount() - 1));
        if (firstNewRow == -1 || r < firstNewRow) firstNewRow = r;
    }
    if (!newSelection.isEmpty())
    {
        achievementGrid->selectionModel()->select(
            newSelection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);
        if (firstNewRow != -1)
        {
            achievementGrid->selectionModel()->setCurrentIndex(
                _assetModel.index(firstNewRow, 0), QItemSelectionModel::NoUpdate);
            achievementGrid->scrollTo(_assetModel.index(firstNewRow, 0));
        }
    }
}

void MainWindow::onDeleteAsset()
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr) return;

    PushMainUndo();

    for (const auto &idx : selectedRows)
    {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr && item->State == AssetState::Local)
        {
            if (!item->isLeaderboard())
            {
                for (int i = _projectRepo.LocalData->Achievements.size() - 1; i >= 0; --i)
                {
                    if (_projectRepo.LocalData->Achievements[i]->ID == item->ID)
                        _projectRepo.LocalData->Achievements.removeAt(i);
                }
            }
            else
            {
                for (int i = _projectRepo.LocalData->Leaderboards.size() - 1; i >= 0; --i)
                {
                    if (_projectRepo.LocalData->Leaderboards[i]->ID == item->ID)
                        _projectRepo.LocalData->Leaderboards.removeAt(i);
                }
            }
        }
    }

    _isDirty = true;
    ReloadDataView();
}

void MainWindow::onResetAsset()
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr) return;

    PushMainUndo();

    for (const auto &idx : selectedRows)
    {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr && item->State == AssetState::Modified)
        {
            if (!item->isLeaderboard())
            {
                for (int i = _projectRepo.LocalData->Achievements.size() - 1; i >= 0; --i)
                {
                    if (_projectRepo.LocalData->Achievements[i]->ID == item->ID)
                        _projectRepo.LocalData->Achievements.removeAt(i);
                }
            }
            else
            {
                for (int i = _projectRepo.LocalData->Leaderboards.size() - 1; i >= 0; --i)
                {
                    if (_projectRepo.LocalData->Leaderboards[i]->ID == item->ID)
                        _projectRepo.LocalData->Leaderboards.removeAt(i);
                }
            }
        }
    }

    _isDirty = true;
    ReloadDataView();
    LoadSelectedAsset();
}

void MainWindow::onMoveAssetUp() { MoveAsset(-1); }
void MainWindow::onMoveAssetDown() { MoveAsset(1); }

void MainWindow::MoveAsset(int direction)
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty() || _projectRepo.LocalData == nullptr) return;

    QList<int> selectedIndices;
    for (const auto &idx : selectedRows) selectedIndices.append(idx.row());
    std::sort(selectedIndices.begin(), selectedIndices.end());

    // Read the working order from the model, not _currentDisplayList: the model can
    // be sorted by a header click while the display list stays in natural order.
    QList<std::shared_ptr<IAssetItem>> workingList;
    for (int r = 0; r < _assetModel.rowCount(); ++r)
        workingList.append(_assetModel.GetAsset(r));

    if (workingList.isEmpty()) return;
    if (direction < 0 && selectedIndices.first() == 0) return;
    if (direction > 0 && selectedIndices.last() >= workingList.size() - 1) return;

    for (int idx : selectedIndices)
    {
        if (workingList[idx] == nullptr || workingList[idx]->State != AssetState::Local) return;
    }

    // Capture object pointers before moving to restore selection accurately after reordering
    QSet<std::shared_ptr<IAssetItem>> objectsToSelect;
    for (int idx : selectedIndices)
    {
        objectsToSelect.insert(workingList[idx]);
    }

    // Clear active sort column so Qt does not automatically re-sort manual moves back
    achievementGrid->horizontalHeader()->setSortIndicator(-1, Qt::AscendingOrder);

    PushMainUndo();

    if (direction < 0)
    {
        for (int idx : selectedIndices)
            workingList.swapItemsAt(idx, idx - 1);
    }
    else
    {
        for (int i = selectedIndices.size() - 1; i >= 0; --i)
        {
            int idx = selectedIndices[i];
            workingList.swapItemsAt(idx, idx + 1);
        }
    }

    bool isLeaderboardView = (cmbAssetType->currentIndex() == 1);
    QSet<int> displayedIds;
    for (const auto &it : workingList)
        if (it != nullptr) displayedIds.insert(it->ID);

    int currentId = 111000001;

    if (isLeaderboardView)
    {
        // Preserve any local leaderboards not currently in the displayed filter
        QList<std::shared_ptr<RaLeaderboard>> preserved;
        for (const auto &l : _projectRepo.LocalData->Leaderboards)
        {
            if (!displayedIds.contains(l->ID)) preserved.append(l);
        }

        _projectRepo.LocalData->Leaderboards.clear();
        for (const auto &item : workingList)
        {
            if (item->State != AssetState::Local) continue;
            item->ID = currentId++;
            _projectRepo.LocalData->Leaderboards.append(std::static_pointer_cast<RaLeaderboard>(item));
        }
        for (auto &p : preserved)
        {
            p->ID = currentId++;
            _projectRepo.LocalData->Leaderboards.append(p);
        }
    }
    else
    {
        // Preserve any local achievements not currently in the displayed filter
        QList<std::shared_ptr<RaAchievement>> preserved;
        for (const auto &a : _projectRepo.LocalData->Achievements)
        {
            if (!displayedIds.contains(a->ID)) preserved.append(a);
        }

        _projectRepo.LocalData->Achievements.clear();
        for (const auto &item : workingList)
        {
            if (item->State != AssetState::Local) continue;
            item->ID = currentId++;
            _projectRepo.LocalData->Achievements.append(std::static_pointer_cast<RaAchievement>(item));
        }
        for (auto &p : preserved)
        {
            p->ID = currentId++;
            _projectRepo.LocalData->Achievements.append(p);
        }
    }

    _isDirty = true;
    ReloadDataView();

    // Restore table selection to the moved items and set active cell so continuous Alt+Up/Down works
    achievementGrid->clearSelection();
    QItemSelection selection;
    int firstNewVisualRow = -1;
    for (int r = 0; r < _assetModel.rowCount(); ++r)
    {
        auto moved = _assetModel.GetAsset(r);
        if (moved != nullptr && objectsToSelect.contains(moved))
        {
            auto left = _assetModel.index(r, 0);
            auto right = _assetModel.index(r, _assetModel.columnCount() - 1);
            selection.select(left, right);
            if (firstNewVisualRow == -1) firstNewVisualRow = r;
        }
    }
    achievementGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

    if (firstNewVisualRow != -1)
    {
        achievementGrid->selectionModel()->setCurrentIndex(
            _assetModel.index(firstNewVisualRow, 0),
            QItemSelectionModel::NoUpdate
        );
        achievementGrid->scrollTo(_assetModel.index(firstNewVisualRow, 0));
    }
}

void MainWindow::onRestoreOrder()
{
    achievementGrid->horizontalHeader()->setSortIndicator(-1, Qt::AscendingOrder);
    ReloadDataView();
}

void MainWindow::onRenumberLocalIDs()
{
    if (_projectRepo.LocalData == nullptr) return;
    bool hasLocal = !_projectRepo.LocalData->Achievements.isEmpty() || !_projectRepo.LocalData->Leaderboards.isEmpty();
    if (!hasLocal) return;

    PushMainUndo();
    int currentId = 111000001;
    for (auto &ach : _projectRepo.LocalData->Achievements) ach->ID = currentId++;
    for (auto &lb : _projectRepo.LocalData->Leaderboards) lb->ID = currentId++;

    _isDirty = true;
    ReloadDataView();
}

void MainWindow::onViewDiff()
{
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (selectedRows.size() != 1) return;

    auto localItem = _assetModel.GetAsset(selectedRows.first().row());
    if (localItem != nullptr && localItem->State == AssetState::Modified && _projectRepo.ServerData != nullptr)
    {
        std::shared_ptr<IAssetItem> serverItem = nullptr;
        for (const auto &s : _projectRepo.ServerData->Sets)
        {
            if (localItem->isLeaderboard())
            {
                for (const auto &lb : s.Leaderboards)
                    if (lb->ID == localItem->ID) { serverItem = lb; break; }
            }
            else
            {
                for (const auto &ach : s.Achievements)
                    if (ach->ID == localItem->ID) { serverItem = ach; break; }
            }
            if (serverItem != nullptr) break;
        }

        if (serverItem != nullptr)
        {
            AssetDiffDialog diff(serverItem, localItem, this);
            diff.exec();
        }
    }
}

void MainWindow::onClearDataReset()
{
    auto result = QMessageBox::warning(
        this,
        "Clear Data / Reset",
        "This will delete all application configuration and cached data. The application will restart.\n\nAre you sure?",
        QMessageBox::Yes | QMessageBox::No,
        QMessageBox::No);

    if (result == QMessageBox::Yes)
    {
        try
        {
            QString appPath = QCoreApplication::applicationDirPath();
            QFile::remove(QDir(appPath).filePath("settings.json"));
            QFile::remove(QDir(appPath).filePath("recent.json"));
            QFile::remove(QDir(appPath).filePath("snippets.json"));

            QDir(QDir(appPath).filePath("Cache")).removeRecursively();
            QDir(QDir(appPath).filePath("Metadata")).removeRecursively();

            // Match C#'s Application.Restart(): relaunch after cleaning up.
            QProcess::startDetached(QCoreApplication::applicationFilePath(), QStringList());
            QApplication::quit();
        }
        catch (...) {}
    }
}

void MainWindow::onToggleReviewer()
{
    ToggleReviewer();
}

void MainWindow::onOpenFile()
{
    if (!ConfirmDiscardChanges()) return;

    QString fileName = QFileDialog::getOpenFileName(
        this,
        "Open Project File",
        "",
        "JSON Files (*.json);;Text Files (*.txt);;All Files (*.*)");

    if (!fileName.isEmpty())
    {
        txtAssetSearch->clear();
        LoadFiles(fileName);
    }
}

void MainWindow::onSaveFile()
{
    if (_projectRepo.LocalData == nullptr && _projectRepo.ServerData == nullptr) return;
    triggerEditor->CommitActiveEditor();
    codeNoteEditor->CommitChanges();

    // Ensure local data notes reflect latest modified and batch-multiplied notes before saving
    if (_projectRepo.LocalData != nullptr)
    {
        _projectRepo.LocalData->Notes = codeNoteEditor->GetAllNotes();
    }

    QString savePath = _currentUserFilePath;
    if (savePath.isEmpty() && !_currentFilePath.isEmpty())
    {
        QFileInfo fi(_currentFilePath);
        QString dir = fi.absolutePath();
        QString fileName = fi.completeBaseName();
        if (fileName.endsWith("-Notes", Qt::CaseInsensitive))
            fileName.chop(6);
        if (fileName.endsWith("-User", Qt::CaseInsensitive))
            fileName.chop(5);
        savePath = QDir(dir).filePath(QString("%1-User.txt").arg(fileName));
    }

    if (savePath.isEmpty())
    {
        savePath = QFileDialog::getSaveFileName(this, "Save User File", "", "Text Files (*.txt)");
        if (savePath.isEmpty()) return;
    }

    try
    {
        if (QFile::exists(savePath))
        {
            QFileInfo fi(savePath);
            QString fileName = fi.fileName();
            QString gameIdStr = _projectRepo.CurrentGameId;
            if (gameIdStr.isEmpty())
            {
                for (QChar c : fileName)
                {
                    if (c.isDigit()) gameIdStr.append(c);
                    else break;
                }
            }
            QString backupRoot = QDir(QCoreApplication::applicationDirPath()).filePath("Backups");
            QString gameBackupDir = gameIdStr.isEmpty() ? backupRoot : QDir(backupRoot).filePath(gameIdStr);
            if (!QDir(gameBackupDir).exists()) QDir().mkpath(gameBackupDir);

            QString timestamp = QDateTime::currentDateTime().toString("yyyyMMdd_HHmmss");
            QString backupPath = QDir(gameBackupDir).filePath(QString("%1.%2.bak").arg(fileName, timestamp));
            // C# File.Copy(..., overwrite: true)
            if (QFile::exists(backupPath)) QFile::remove(backupPath);
            QFile::copy(savePath, backupPath);
        }
    }
    catch (...) {}

    try
    {
        _projectRepo.EnsureLocalDataExists();
        QString fileContent = _fileSerializer.Serialize(
            _projectRepo.ServerData != nullptr ? _projectRepo.ServerData->Title : "Game Title",
            _projectRepo.LocalData.get(),
            _projectRepo.GetMergedAchievements(),
            _projectRepo.GetMergedLeaderboards(),
            _projectRepo
        );

        QFile outFile(savePath);
        if (!outFile.open(QIODevice::WriteOnly | QIODevice::Text))
        {
            QMessageBox::critical(this, "Error",
                QString("Error saving: %1").arg(outFile.errorString()));
            return;
        }

        QByteArray bytes = fileContent.toUtf8();
        if (outFile.write(bytes) != bytes.size() || !outFile.flush())
        {
            QMessageBox::critical(this, "Error",
                QString("Error saving: %1").arg(outFile.errorString()));
            outFile.close();
            return;
        }
        outFile.close();

        _currentUserFilePath = savePath;
        _projectRepo.SaveRichPresence();
        _projectRepo.Metadata.Save();

        _isDirty = false;

        QMessageBox::information(this, "Save", "Saved to " + savePath);
    }
    catch (const std::exception &ex)
    {
        QMessageBox::critical(this, "Error", QString("Error saving: %1").arg(ex.what()));
    }
}

void MainWindow::onViewRpDiff()
{
    if (_projectRepo.RichPresence == nullptr) return;

    auto ExtractDisplaySection = [](const QString &rpScript) -> QString {
        if (rpScript.isEmpty()) return "";
        QString norm = rpScript;
        norm.replace("\r\n", "\n").replace('\r', '\n');
        QStringList lines = norm.split('\n');
        QString sb;
        bool inDisplay = false;
        for (const QString &line : lines)
        {
            QString trimmed = line.trimmed();
            if (trimmed.startsWith("Display:")) { inDisplay = true; continue; }
            if (inDisplay)
            {
                if (trimmed.startsWith("Lookup:") || trimmed.startsWith("Format:")) break;
                sb += line + "\n";
            }
        }
        return sb.trimmed();
    };

    QString current = ExtractDisplaySection(_projectRepo.RichPresence->ToString());
    QString path = _projectRepo.RichPresenceFilePath;
    QString original = "";
    if (QFile::exists(path))
    {
        QFile f(path);
        if (f.open(QIODevice::ReadOnly | QIODevice::Text))
        {
            original = ExtractDisplaySection(QString::fromUtf8(f.readAll()));
            f.close();
        }
    }

    TextDiffDialog diff("RP Diff", original, current, this);
    diff.exec();
}

void MainWindow::onRevertRichPresence()
{
    if (!_rpMetadata.CurrentBackup.has_value())
    {
        QMessageBox::information(this, "Revert Rich Presence", "No backup found.");
        return;
    }

    auto result = QMessageBox::question(
        this,
        "Confirm",
        QString("Revert to state from %1?").arg(_rpMetadata.CurrentBackup->Timestamp),
        QMessageBox::Yes | QMessageBox::No);

    if (result == QMessageBox::Yes)
    {
        try
        {
            _projectRepo.RichPresence = std::make_shared<RichPresenceScript>(RichPresenceParser::Parse(_rpMetadata.CurrentBackup->OriginalScript));
            _isDirty = true;
            QMessageBox::information(this, "Revert Rich Presence", "Reverted.");
        }
        catch (const std::exception &ex)
        {
            QMessageBox::critical(this, "Error", QString("Error: %1").arg(ex.what()));
        }
    }
}

void MainWindow::onRevertRegion()
{
    QSet<QString> regions;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    QList<std::shared_ptr<IAssetItem>> selectedAssets;

    for (const auto &idx : selectedRows)
    {
        auto item = _assetModel.GetAsset(idx.row());
        if (item != nullptr) selectedAssets.append(item);
    }

    if (selectedAssets.isEmpty()) return;

    static const QRegularExpression regionRegex(R"(\((.*?)\))");

    for (const auto &asset : selectedAssets)
    {
        std::unique_ptr<IAssetStrategy> strategy;
        if (asset->isLeaderboard()) strategy = std::make_unique<LeaderboardStrategy>();
        else strategy = std::make_unique<AchievementStrategy>();

        auto segments = strategy->ParseLogic(asset->Logic);
        for (auto &groupList : segments)
        {
            _projectRepo.Metadata.ApplyNamesToAsset(asset->ID, groupList);
            for (const auto &g : groupList)
            {
                auto match = regionRegex.match(g.GroupName);
                if (match.hasMatch()) regions.insert(match.captured(1));
            }
        }
    }

    if (regions.isEmpty())
    {
        QMessageBox::information(this, "Revert Region", "No region-specific groups found.");
        return;
    }

    RevertRegionDialog form(regions.values(), this);
    if (form.exec() != QDialog::Accepted) return;

    QString targetRegion = form.SelectedRegion;
    PushMainUndo();
    int count = 0;

    for (const auto &item : selectedAssets)
    {
        EnsureAssetIsLocal(item);
        std::shared_ptr<IAssetItem> localItem = nullptr;
        if (item->isLeaderboard())
        {
            for (const auto &l : _projectRepo.LocalData->Leaderboards)
                if (l->ID == item->ID) { localItem = l; break; }
        }
        else
        {
            for (const auto &a : _projectRepo.LocalData->Achievements)
                if (a->ID == item->ID) { localItem = a; break; }
        }

        if (localItem == nullptr) continue;

        std::unique_ptr<IAssetStrategy> strategy;
        if (localItem->isLeaderboard()) strategy = std::make_unique<LeaderboardStrategy>();
        else strategy = std::make_unique<AchievementStrategy>();

        auto segments = strategy->ParseLogic(localItem->Logic);
        bool modified = false;

        for (const QString &segKey : segments.keys())
        {
            auto &groups = segments[segKey];
            _projectRepo.Metadata.ApplyNamesToAsset(localItem->ID, groups);

            std::optional<AchievementConditionGroup> targetGroup;
            for (const auto &g : groups)
            {
                if (g.GroupName.contains(QString("(%1)").arg(targetRegion)))
                {
                    targetGroup = g;
                    break;
                }
            }

            if (targetGroup.has_value())
            {
                while (!targetGroup->Conditions.isEmpty() && targetGroup->Conditions[0].Flag == "Pause If")
                    targetGroup->Conditions.removeFirst();

                AchievementConditionGroup newCore;
                newCore.GroupName = "Core";
                newCore.Conditions = targetGroup->Conditions;
                segments[segKey] = { newCore };
                modified = true;
                _projectRepo.Metadata.UpdateAssetGroups(localItem->ID, segments[segKey]);
            }
        }

        if (modified)
        {
            localItem->Logic = strategy->BuildLogic(segments);
            count++;
        }
    }

    _isDirty = true;
    ReloadDataView();
    if (_selectedAsset != nullptr) LoadSelectedAsset();
    QMessageBox::information(this, "Revert Region", QString("Reverted %1 assets to %2.").arg(count).arg(targetRegion));
}

void MainWindow::onLogicOptimizer()
{
    if (_currentDisplayList.isEmpty()) return;

    QList<std::shared_ptr<IAssetItem>> assetsToOptimize;
    for (const auto &item : _currentDisplayList)
    {
        if (item->State == AssetState::Synced)
        {
            auto clone = item->clone(false);
            clone->State = AssetState::Synced;
            assetsToOptimize.append(clone);
        }
        else
        {
            assetsToOptimize.append(item);
        }
    }

    LogicOptimizerDialog form(assetsToOptimize, this);
    form.exec();

    _projectRepo.EnsureLocalDataExists();
    bool anyChanges = false;
    for (const auto &item : assetsToOptimize)
    {
        if (item->State == AssetState::Modified)
        {
            if (!IsAssetInLocalStore(item))
            {
                if (item->isLeaderboard())
                    _projectRepo.LocalData->Leaderboards.append(std::static_pointer_cast<RaLeaderboard>(item));
                else
                    _projectRepo.LocalData->Achievements.append(std::static_pointer_cast<RaAchievement>(item));
                anyChanges = true;
            }
            else
            {
                anyChanges = true;
            }
        }
    }

    if (anyChanges)
    {
        _isDirty = true;
        ReloadDataView();
        if (_selectedAsset != nullptr) SelectAssetInGrid(_selectedAsset);
    }
}

void MainWindow::onMultiRegionBatch(MultiRegionDialog *dialog)
{
    if (dialog == nullptr) return;
    auto configs = dialog->GetConfiguredRegions();
    if (configs.isEmpty()) return;

    // Use value storage to eliminate dangling pointer risks when modifying or copying region configurations
    std::optional<RegionConfig> baseConfigOpt;
    QList<RegionConfig> targetConfigs;
    for (const auto &c : configs)
    {
        if (c.IsBase) baseConfigOpt = c;
        else targetConfigs.append(c);
    }

    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    if (dialog->ShouldUpdateRichPresence() && _projectRepo.RichPresence != nullptr)
    {
        _rpMetadata.SaveBackup(_projectRepo.RichPresence->ToString());
        auto newRp = _rpService.GenerateMultiRegion(*_projectRepo.RichPresence, configs);
        _projectRepo.RichPresence = std::make_shared<RichPresenceScript>(newRp);
        QMessageBox::information(this, "RP Updated", "Rich Presence script updated in memory.");
    }

    if (dialog->UsePauseLocks())
    {
        auto InvertLockLogic = [this](QList<AchievementCondition> &conditions) {
            for (auto &c : conditions)
            {
                c.Flag = "Pause If";
                c.Operator = InvertOperator(c.Operator);
            }
        };

        for (auto &cfg : configs)
        {
            InvertLockLogic(cfg.LockConditions);
        }

        baseConfigOpt = std::nullopt;
        targetConfigs.clear();
        for (const auto &c : configs)
        {
            if (c.IsBase) baseConfigOpt = c;
            else targetConfigs.append(c);
        }
    }

    QList<std::shared_ptr<IAssetItem>> assetsToProcess;
    auto selectedRows = achievementGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty())
    {
        for (const auto &idx : selectedRows)
        {
            auto a = _assetModel.GetAsset(idx.row());
            if (a != nullptr) assetsToProcess.append(a);
        }
    }
    else
    {
        assetsToProcess = _currentDisplayList;
    }

    if (assetsToProcess.isEmpty()) return;

    int count = 0;
    const RegionConfig *baseConfigPtr = baseConfigOpt.has_value() ? &baseConfigOpt.value() : nullptr;

    for (const auto &item : assetsToProcess)
    {
        EnsureAssetIsLocal(item);
        std::shared_ptr<IAssetItem> localItem = nullptr;
        if (item->isLeaderboard())
        {
            for (const auto &l : _projectRepo.LocalData->Leaderboards)
                if (l->ID == item->ID) { localItem = l; break; }
        }
        else
        {
            for (const auto &a : _projectRepo.LocalData->Achievements)
                if (a->ID == item->ID) { localItem = a; break; }
        }

        if (localItem == nullptr) continue;

        _batchProcessor->ApplyMultiRegionToAsset(localItem, baseConfigPtr, targetConfigs);
        count++;
    }

    _isDirty = true;
    QMessageBox::information(this, "Batch Complete", QString("Multi-Region logic applied to %1 assets.").arg(count));
    ReloadDataView();
    if (_selectedAsset != nullptr) SelectAssetInGrid(_selectedAsset);
}

void MainWindow::onBatchMultiplyNotes(BatchNoteMultiplierDialog *dialog)
{
    if (dialog == nullptr) return;
    _projectRepo.EnsureLocalDataExists();
    PushMainUndo();

    codeNoteEditor->ExecuteBatchMultiply(
        dialog->SelectedNotes,
        dialog->SearchKeyword,
        dialog->ReplacePattern,
        dialog->Copies,
        dialog->Stride,
        dialog->StartNumber);

    _isDirty = true;

    // Immediately copy newly multiplied notes to LocalData so they persist across project saves
    _projectRepo.LocalData->Notes = codeNoteEditor->GetAllNotes();
    triggerEditor->SetNotes(_projectRepo.LocalData->Notes);
    _notesCacheDirty = true;
    RunGlobalValidation();
}

bool MainWindow::IsAssetInLocalStore(const std::shared_ptr<IAssetItem> &item)
{
    if (_projectRepo.LocalData == nullptr) return false;
    if (item->isLeaderboard())
    {
        for (const auto &l : _projectRepo.LocalData->Leaderboards)
            if (l->ID == item->ID) return true;
        return false;
    }
    for (const auto &a : _projectRepo.LocalData->Achievements)
        if (a->ID == item->ID) return true;
    return false;
}

} // namespace RALogicEditor
