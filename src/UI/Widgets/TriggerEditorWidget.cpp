#include "TriggerEditorWidget.h"
#include "../../Services/LogicValidator.h"
#include "../../Services/LogicGeneratorService.h"
#include "../../Parsers/AchievementParser.h"
#include "../../Parsers/PointerTreeParser.h"
#include "../../Common/ImageCache.h"
#include "../../Definitions/RaLogicSyntax.h"
#include "../../Utilities/LogicFormatter.h"
#include "../Dialogs/GeneratorConfigDialog.h"
#include "../Dialogs/MultiRegionDialog.h"
#include "../Dialogs/BatchNoteMultiplierDialog.h"
#include "../Dialogs/SnippetSelectDialog.h"
#include "../../Services/SnippetManager.h"
#include "../Styles/ThemeManager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QSplitter>
#include <QHeaderView>
#include <QScrollBar>
#include <QKeyEvent>
#include <QClipboard>
#include <QGuiApplication>
#include <QMessageBox>
#include <QInputDialog>
#include <QRegularExpression>
#include <QSignalBlocker>
#include <algorithm>

namespace RALE {

static LogicValidator staticValidator;
static LogicGeneratorService staticGenerator;

TriggerEditorWidget::TriggerEditorWidget(QWidget *parent)
    : QWidget(parent),
      _viewModel(staticValidator, staticGenerator, this)
{
    _lbFormats = {
        { "Score", "SCORE" },
        { "Time (Frames)", "TIME" },
        { "Time (Centiseconds)", "MILLISECS" },
        { "Time (Seconds)", "SECONDS" },
        { "Time (Seconds) [Legacy]", "TIMESECS" },
        { "Time (Minutes)", "MINUTES" },
        { "Time (Seconds as Minutes)", "SECS_AS_MINS" },
        { "Value", "VALUE" },
        { "Value (Unsigned)", "UNSIGNED" },
        { "Value (Tens)", "TENS" },
        { "Value (Hundreds)", "HUNDREDS" },
        { "Value (Thousands)", "THOUSANDS" },
        { "Value (Fixed1)", "FIXED1" },
        { "Value (Fixed2)", "FIXED2" },
        { "Value (Fixed3)", "FIXED3" },
        { "Value (Float1)", "FLOAT1" },
        { "Value (Float2)", "FLOAT2" },
        { "Value (Float3)", "FLOAT3" },
        { "Value (Float4)", "FLOAT4" },
        { "Value (Float5)", "FLOAT5" },
        { "Value (Float6)", "FLOAT6" }
    };

    InitializeUI();
    InitializeLeaderboardControls();
    InitializeColumnContextMenus();

    SetStatus("Ready");

    connect(&_viewModel, &TriggerViewModel::LogicChanged, this, [this]() {
        emit TriggerTextChanged();
        _isProgrammaticallyChanging = true;
        RefreshGroupsListUI();
        _isProgrammaticallyChanging = false;
        RunValidation();
    });

    connect(&_viewModel, &TriggerViewModel::SegmentsChanged, this, [this]() {
        lvLeaderboardComponents->clear();
        QStringList segments = _viewModel.AssetStrategy->GetSegmentNames();

        if (segments.size() > 1)
        {
            for (const auto &seg : segments)
                lvLeaderboardComponents->addItem(seg);

            lvLeaderboardComponents->setVisible(true);
            if (lvLeaderboardComponents->count() > 0)
            {
                bool wasLocked = _isProgrammaticallyChanging;
                _isProgrammaticallyChanging = true;

                int selectIndex = 0;
                if (!_preserveSegmentSelection.isEmpty())
                {
                    for (int i = 0; i < lvLeaderboardComponents->count(); i++)
                    {
                        if (lvLeaderboardComponents->item(i)->text().compare(_preserveSegmentSelection, Qt::CaseInsensitive) == 0)
                            selectIndex = i;
                    }
                }

                lvLeaderboardComponents->setCurrentRow(selectIndex);
                _viewModel.SelectSegment(lvLeaderboardComponents->item(selectIndex)->text());
                _isProgrammaticallyChanging = wasLocked;
            }
        }
        else
        {
            lvLeaderboardComponents->setVisible(false);
        }

        RefreshGroupsListUI();
    });

    connect(&_viewModel, &TriggerViewModel::HistoryChanged, this, &TriggerEditorWidget::HistoryChanged);

    // Asynchronously dispatch logic update and validation to prevent beginResetModel from executing inside active setData
    connect(&_conditionModel, &ConditionTableModel::ConditionDataChanged, this, [this]() {
        QMetaObject::invokeMethod(this, [this]() {
            _viewModel.UpdateLogic(true);
        }, Qt::QueuedConnection);
    });
}

QIcon TriggerEditorWidget::getThemeIcon(const QString &resourcePath) const
{
    return ThemeManager::getIcon(resourcePath, ThemeManager::CurrentTheme());
}

void TriggerEditorWidget::updateThemeIcons()
{
    const auto actionsList = findChildren<QAction*>();
    for (QAction *action : actionsList)
    {
        QVariant iconPathVar = action->property("iconPath");
        if (iconPathVar.isValid())
        {
            action->setIcon(getThemeIcon(iconPathVar.toString()));
        }
    }
}

void TriggerEditorWidget::InitializeUI()
{
    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(2);

    // Header Panel: Fixed 85px height matching WinForms panelHeader exactly
    panelHeader = new QWidget(this);
    panelHeader->setObjectName("panelHeader");
    panelHeader->setFixedHeight(90);
    auto *headerLayout = new QVBoxLayout(panelHeader);
    headerLayout->setContentsMargins(5, 4, 5, 4);
    headerLayout->setSpacing(3);

    // Row 1: Badge + Title + Description + LB Components
    auto *row1 = new QHBoxLayout();
    row1->setContentsMargins(0, 0, 0, 0);
    row1->setSpacing(6);

    pbBadge = new QLabel(panelHeader);
    pbBadge->setFixedSize(64, 64);
    pbBadge->setScaledContents(true);
    pbBadge->setStyleSheet("border: 1px solid #adadad; background-color: #e0e0e0;");
    row1->addWidget(pbBadge);

    lvLeaderboardComponents = new QListWidget(panelHeader);
    lvLeaderboardComponents->setFixedSize(88, 64);
    lvLeaderboardComponents->setVisible(false);
    connect(lvLeaderboardComponents, &QListWidget::currentRowChanged, this, &TriggerEditorWidget::onLbComponentsSelectionChanged);
    row1->addWidget(lvLeaderboardComponents);

    auto *titleDescCol = new QVBoxLayout();
    titleDescCol->setContentsMargins(0, 0, 0, 0);
    titleDescCol->setSpacing(3);

    txtTitle = new QLineEdit(panelHeader);
    txtTitle->setFixedHeight(26);
    QFont titleFont = txtTitle->font();
    titleFont.setPointSize(11);
    titleFont.setBold(true);
    txtTitle->setFont(titleFont);
    connect(txtTitle, &QLineEdit::textChanged, this, &TriggerEditorWidget::onTitleChanged);
    titleDescCol->addWidget(txtTitle);

    txtDescription = new QLineEdit(panelHeader);
    txtDescription->setFixedHeight(23);
    connect(txtDescription, &QLineEdit::textChanged, this, &TriggerEditorWidget::onDescriptionChanged);
    titleDescCol->addWidget(txtDescription);

    // Row 2: Controls & Details
    auto *row2 = new QHBoxLayout();
    row2->setContentsMargins(0, 0, 0, 0);
    row2->setSpacing(4);

    cmbPoints = new QComboBox(panelHeader);
    cmbPoints->setFixedWidth(40);
    ThemeManager::SetupComboBox(cmbPoints, 14);
    QFont pointsFont = cmbPoints->font();
    pointsFont.setBold(true);
    cmbPoints->setFont(pointsFont);
    cmbPoints->addItems({ "0", "1", "2", "3", "4", "5", "10", "25", "50", "100" });
    connect(cmbPoints, &QComboBox::currentTextChanged, this, &TriggerEditorWidget::onPointsChanged);
    row2->addWidget(cmbPoints);

    cmbType = new QComboBox(panelHeader);
    cmbType->setFixedWidth(94);
    ThemeManager::SetupComboBox(cmbType, 14);
    cmbType->addItems({ "", "Progression", "Win", "Missable" });
    connect(cmbType, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &TriggerEditorWidget::onTypeChanged);
    row2->addWidget(cmbType);

    lblFormat = new QLabel("Format:", panelHeader);
    lblFormat->setVisible(false);
    row2->addWidget(lblFormat);

    cmbFormat = new QComboBox(panelHeader);
    cmbFormat->setFixedWidth(110);
    ThemeManager::SetupComboBox(cmbFormat, 14);
    cmbFormat->setVisible(false);
    connect(cmbFormat, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &TriggerEditorWidget::onFormatChanged);
    row2->addWidget(cmbFormat);

    chkLowerIsBetter = new QCheckBox("Lower is Better", panelHeader);
    chkLowerIsBetter->setVisible(false);
    connect(chkLowerIsBetter, &QCheckBox::toggled, this, &TriggerEditorWidget::onLowerIsBetterChanged);
    row2->addWidget(chkLowerIsBetter);

    lblAuthor = new QLabel("Author: Unknown", panelHeader);
    lblAuthor->setStyleSheet("color: #707070;");
    row2->addWidget(lblAuthor);

    lblAssetState = new QLabel("Core", panelHeader);
    lblAssetState->setStyleSheet("font-style: italic; color: #707070;");
    row2->addWidget(lblAssetState);

    row2->addStretch();

    btnCopyDef = new QPushButton("Copy Def", panelHeader);
    btnCopyDef->setFixedSize(62, 24);
    connect(btnCopyDef, &QPushButton::clicked, this, &TriggerEditorWidget::onBtnCopyDefClicked);
    row2->addWidget(btnCopyDef);

    btnTools = new QPushButton("Tools ▼", panelHeader);
    btnTools->setFixedSize(54, 24);
    connect(btnTools, &QPushButton::clicked, this, &TriggerEditorWidget::onBtnToolsClicked);
    row2->addWidget(btnTools);

    btnAddNote = new QPushButton("Add Note", panelHeader);
    btnAddNote->setFixedSize(62, 24);
    connect(btnAddNote, &QPushButton::clicked, this, &TriggerEditorWidget::onBtnAddNoteClicked);
    row2->addWidget(btnAddNote);

    btnReset = new QPushButton("Reset", panelHeader);
    btnReset->setFixedSize(50, 24);
    connect(btnReset, &QPushButton::clicked, this, &TriggerEditorWidget::onBtnResetClicked);
    row2->addWidget(btnReset);

    titleDescCol->addLayout(row2);
    row1->addLayout(titleDescCol, 1);
    headerLayout->addLayout(row1);
    mainLayout->addWidget(panelHeader);

    // Splitter: Groups List + Condition Grid
    auto *splitGroups = new QSplitter(Qt::Horizontal, this);

    groupsContainer = new QWidget(splitGroups);
    groupsContainer->setFixedWidth(65);
    auto *groupsLayout = new QVBoxLayout(groupsContainer);
    groupsLayout->setContentsMargins(0, 0, 0, 0);
    groupsLayout->setSpacing(0);

    lblGroupsHeader = new QLabel("Groups", groupsContainer);
    lblGroupsHeader->setObjectName("lblGroupsHeader");
    lblGroupsHeader->setFixedHeight(24);
    lblGroupsHeader->setAlignment(Qt::AlignCenter);
    groupsLayout->addWidget(lblGroupsHeader);

    groupsList = new QListWidget(groupsContainer);
    groupsList->setObjectName("groupsList");
    connect(groupsList, &QListWidget::currentRowChanged, this, &TriggerEditorWidget::onGroupsListSelectionChanged);
    groupsLayout->addWidget(groupsList, 1);

    // Dedicated compact button container matching Screenshot 3
    btnPanel = new QWidget(groupsContainer);
    btnPanel->setFixedHeight(24);
    auto *btnLayout = new QHBoxLayout(btnPanel);
    btnLayout->setContentsMargins(3, 2, 3, 2);
    btnLayout->setSpacing(3);

    btnAddGroup = new QPushButton("+", btnPanel);
    btnAddGroup->setObjectName("btnAddGroup");
    btnAddGroup->setFixedSize(22, 19);
    connect(btnAddGroup, &QPushButton::clicked, this, &TriggerEditorWidget::onAddGroupClicked);

    btnRemoveGroup = new QPushButton("-", btnPanel);
    btnRemoveGroup->setObjectName("btnRemoveGroup");
    btnRemoveGroup->setFixedSize(22, 19);
    connect(btnRemoveGroup, &QPushButton::clicked, this, &TriggerEditorWidget::onRemoveGroupClicked);

    btnLayout->addWidget(btnAddGroup);
    btnLayout->addStretch();
    btnLayout->addWidget(btnRemoveGroup);
    groupsLayout->addWidget(btnPanel);

    splitGroups->addWidget(groupsContainer);

    conditionGrid = new QTableView(splitGroups);
    conditionGrid->setObjectName("conditionGrid");
    conditionGrid->setModel(&_conditionModel);

    _headerView = new ConditionHeaderView(Qt::Vertical, conditionGrid);
    _headerView->setConditionModel(&_conditionModel);
    _headerView->setTableView(conditionGrid);
    _headerView->setFixedWidth(25);
    conditionGrid->setVerticalHeader(_headerView);

    // Custom delegate draws the clean single 1px thin gridline
    conditionGrid->setShowGrid(false);
    conditionGrid->verticalHeader()->setDefaultSectionSize(26);
    conditionGrid->verticalHeader()->setSectionResizeMode(QHeaderView::Fixed);
    conditionGrid->horizontalHeader()->setFixedHeight(24);
    conditionGrid->horizontalHeader()->setHighlightSections(true);
    conditionGrid->horizontalHeader()->setSectionResizeMode(QHeaderView::Interactive);

    conditionGrid->setColumnWidth(ConditionTableModel::ColID, 35);
    conditionGrid->setColumnWidth(ConditionTableModel::ColFlag, 80);
    conditionGrid->setColumnWidth(ConditionTableModel::ColLType, 50);
    conditionGrid->setColumnWidth(ConditionTableModel::ColLSize, 60);
    conditionGrid->horizontalHeader()->setSectionResizeMode(ConditionTableModel::ColLValue, QHeaderView::Stretch);
    conditionGrid->setColumnWidth(ConditionTableModel::ColCmp, 40);
    conditionGrid->setColumnWidth(ConditionTableModel::ColRType, 50);
    conditionGrid->setColumnWidth(ConditionTableModel::ColRSize, 60);
    conditionGrid->horizontalHeader()->setSectionResizeMode(ConditionTableModel::ColRValue, QHeaderView::Stretch);
    conditionGrid->setColumnWidth(ConditionTableModel::ColHits, 50);

    _tableDelegate = new ConditionTableDelegate(this);
    conditionGrid->setItemDelegate(_tableDelegate);
    conditionGrid->setSelectionBehavior(QAbstractItemView::SelectRows);
    conditionGrid->setSelectionMode(QAbstractItemView::ExtendedSelection);
    conditionGrid->installEventFilter(this);

    connect(conditionGrid->selectionModel(), &QItemSelectionModel::currentChanged, this, [this]() {
        if (_headerView != nullptr) _headerView->viewport()->update();
    });

    splitGroups->addWidget(conditionGrid);
    splitGroups->setSizes({ 65, 635 });
    splitGroups->setCollapsible(0, false);
    mainLayout->addWidget(splitGroups, 1);

    // Bottom Bar: Status on Left, Checkboxes on Right (Screenshot 2 1:1)
    bottomBar = new QWidget(this);
    bottomBar->setFixedHeight(24);
    auto *bottomLayout = new QHBoxLayout(bottomBar);
    bottomLayout->setContentsMargins(6, 0, 6, 0);
    bottomLayout->setSpacing(12);

    lblEditorStatus = new QLabel("Ready", bottomBar);
    bottomLayout->addWidget(lblEditorStatus);
    bottomLayout->addStretch();

    chkCollapseChains = new QCheckBox("Collapse Chains", bottomBar);
    chkCollapseChains->setToolTip("Hides intermediate AddAddress rows to display pointer chains compactly");
    connect(chkCollapseChains, &QCheckBox::toggled, this, &TriggerEditorWidget::onCollapseChainsChanged);
    bottomLayout->addWidget(chkCollapseChains);

    chkAliasMode = new QCheckBox("Show Aliases", bottomBar);
    chkAliasMode->setToolTip("Replaces raw memory addresses with descriptions from Code Notes");
    connect(chkAliasMode, &QCheckBox::toggled, this, &TriggerEditorWidget::onAliasModeChanged);
    bottomLayout->addWidget(chkAliasMode);

    chkShowDecimal = new QCheckBox("Show Decimal", bottomBar);
    chkShowDecimal->setToolTip("Displays immediate numerical values in decimal instead of hexadecimal");
    connect(chkShowDecimal, &QCheckBox::toggled, this, &TriggerEditorWidget::onShowDecimalChanged);
    bottomLayout->addWidget(chkShowDecimal);

    mainLayout->addWidget(bottomBar);

    // Context Menus
    toolsContextMenu = new QMenu(this);
    actCreateGenerator = toolsContextMenu->addAction(getThemeIcon(":/icons/range_finder.svg"), "Create Generator...", this, &TriggerEditorWidget::onCreateGeneratorClicked);
    actCreateGenerator->setProperty("iconPath", ":/icons/range_finder.svg");

    actCommitGenerator = toolsContextMenu->addAction(getThemeIcon(":/icons/save.svg"), "Commit Generator (Bake)", this, &TriggerEditorWidget::onCommitGeneratorClicked);
    actCommitGenerator->setProperty("iconPath", ":/icons/save.svg");

    actMultiRegion = toolsContextMenu->addAction(getThemeIcon(":/icons/converter.svg"), "Multi-Region Support...", this, &TriggerEditorWidget::onMultiRegionClicked);
    actMultiRegion->setProperty("iconPath", ":/icons/converter.svg");

    actLogicOptimizer = toolsContextMenu->addAction(getThemeIcon(":/icons/hierarchy_fixer.svg"), "Logic Optimizer (Length)...", this, &TriggerEditorWidget::onLogicOptimizerClicked);
    actLogicOptimizer->setProperty("iconPath", ":/icons/hierarchy_fixer.svg");

    actBatchMultiply = toolsContextMenu->addAction(getThemeIcon(":/icons/copy.svg"), "Batch Multiply Notes...", this, &TriggerEditorWidget::onBatchMultiplyNotesClicked);
    actBatchMultiply->setProperty("iconPath", ":/icons/copy.svg");

    // Wire up dynamic enablement for Tools menu items matching WinForms ToolsContextMenu_Opening
    connect(toolsContextMenu, &QMenu::aboutToShow, this, [this]() {
        int row = groupsList->currentRow();
        bool hasActiveGroups = !_viewModel.GetActiveGroups().isEmpty();
        bool canBake = (row >= 0 && _viewModel.GeneratorRules.contains(row));

        if (actCreateGenerator) actCreateGenerator->setEnabled(hasActiveGroups);
        if (actCommitGenerator) actCommitGenerator->setEnabled(canBake);
        if (actMultiRegion) actMultiRegion->setEnabled(hasActiveGroups && !_rawNotes.isEmpty());
        if (actBatchMultiply) actBatchMultiply->setEnabled(!_rawNotes.isEmpty());
    });

    groupsContextMenu = new QMenu(this);
    actAddGroup = groupsContextMenu->addAction(getThemeIcon(":/icons/bookmark_add.svg"), "Add Group", this, &TriggerEditorWidget::onAddGroupClicked);
    actAddGroup->setProperty("iconPath", ":/icons/bookmark_add.svg");

    actRemoveGroup = groupsContextMenu->addAction(getThemeIcon(":/icons/delete.svg"), "Remove Group", this, &TriggerEditorWidget::onRemoveGroupClicked);
    actRemoveGroup->setProperty("iconPath", ":/icons/delete.svg");

    groupsContextMenu->addSeparator();

    actRemoveAllAltGroups = groupsContextMenu->addAction(getThemeIcon(":/icons/delete.svg"), "Remove All Alt Groups", this, &TriggerEditorWidget::onRemoveAllAltGroupsClicked);
    actRemoveAllAltGroups->setProperty("iconPath", ":/icons/delete.svg");

    groupsList->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(groupsList, &QWidget::customContextMenuRequested, this, [this](const QPoint &pos) {
        groupsContextMenu->exec(groupsList->mapToGlobal(pos));
    });

    // Wire up dynamic enablement for Groups menu items matching WinForms GroupsContextMenu_Opening
    connect(groupsContextMenu, &QMenu::aboutToShow, this, [this]() {
        int row = groupsList->currentRow();
        bool hasSelection = (row >= 0);
        bool isCore = (row == 0);

        if (actRemoveGroup) actRemoveGroup->setEnabled(hasSelection && !isCore);
    });

    gridContextMenu = new QMenu(this);
    auto *actMoveUp = gridContextMenu->addAction(getThemeIcon(":/icons/sort.svg"), "Move Up", this, [this]() { MoveSelected(-1); });
    actMoveUp->setShortcut(QKeySequence("Alt+Up"));
    actMoveUp->setProperty("iconPath", ":/icons/sort.svg");

    auto *actMoveDown = gridContextMenu->addAction(getThemeIcon(":/icons/sort.svg"), "Move Down", this, [this]() { MoveSelected(1); });
    actMoveDown->setShortcut(QKeySequence("Alt+Down"));
    actMoveDown->setProperty("iconPath", ":/icons/sort.svg");

    gridContextMenu->addSeparator();

    auto *actAddCond = gridContextMenu->addAction(getThemeIcon(":/icons/bookmark_add.svg"), "Add Condition", this, &TriggerEditorWidget::AddCondition);
    actAddCond->setProperty("iconPath", ":/icons/bookmark_add.svg");

    auto *actDup = gridContextMenu->addAction(getThemeIcon(":/icons/copy.svg"), "Duplicate", this, &TriggerEditorWidget::DuplicateSelected);
    actDup->setShortcut(QKeySequence("Ctrl+D"));
    actDup->setProperty("iconPath", ":/icons/copy.svg");

    auto *actDel = gridContextMenu->addAction(getThemeIcon(":/icons/delete.svg"), "Delete", this, &TriggerEditorWidget::DeleteSelected);
    actDel->setShortcut(QKeySequence::Delete);
    actDel->setProperty("iconPath", ":/icons/delete.svg");

    gridContextMenu->addSeparator();

    auto *actExtend = gridContextMenu->addAction(getThemeIcon(":/icons/range_finder.svg"), "Extend Selection by Pattern", this, &TriggerEditorWidget::ExtendSelectionByPattern);
    actExtend->setProperty("iconPath", ":/icons/range_finder.svg");

    auto *actCut = gridContextMenu->addAction(getThemeIcon(":/icons/cut.svg"), "Cut Logic", this, &TriggerEditorWidget::CutSelected);
    actCut->setShortcut(QKeySequence::Cut);
    actCut->setProperty("iconPath", ":/icons/cut.svg");

    auto *actCopyLogic = gridContextMenu->addAction(getThemeIcon(":/icons/copy_ra.svg"), "Copy Logic", this, &TriggerEditorWidget::CopySelectedLogic);
    actCopyLogic->setShortcut(QKeySequence::Copy);
    actCopyLogic->setProperty("iconPath", ":/icons/copy_ra.svg");

    auto *actCopyRow = gridContextMenu->addAction(getThemeIcon(":/icons/copy.svg"), "Copy Row", this, &TriggerEditorWidget::CopySelectedRows);
    actCopyRow->setShortcut(QKeySequence("Ctrl+Shift+C"));
    actCopyRow->setProperty("iconPath", ":/icons/copy.svg");

    auto *actPaste = gridContextMenu->addAction(getThemeIcon(":/icons/load.svg"), "Paste Logic", this, [this]() { PasteLogic(); });
    actPaste->setShortcut(QKeySequence::Paste);
    actPaste->setProperty("iconPath", ":/icons/load.svg");

    gridContextMenu->addSeparator();

    auto *actSaveSnip = gridContextMenu->addAction(getThemeIcon(":/icons/copy_note.svg"), "Save Selected Rows to Snippet Library...", this, &TriggerEditorWidget::onSaveAsSnippetClicked);
    actSaveSnip->setProperty("iconPath", ":/icons/copy_note.svg");

    auto *actInsertSnip = gridContextMenu->addAction(getThemeIcon(":/icons/load.svg"), "Insert Snippet from Library...", this, &TriggerEditorWidget::onInsertSnippetClicked);
    actInsertSnip->setProperty("iconPath", ":/icons/load.svg");

    conditionGrid->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(conditionGrid, &QWidget::customContextMenuRequested, this, [this](const QPoint &pos) {
        QModelIndex idx = conditionGrid->indexAt(pos);
        if (idx.isValid())
        {
            // Ensure the right-clicked row is selected if it is not already part of the active selection
            if (!conditionGrid->selectionModel()->isRowSelected(idx.row(), QModelIndex()))
            {
                conditionGrid->selectionModel()->select(
                    QItemSelection(_conditionModel.index(idx.row(), 0), _conditionModel.index(idx.row(), _conditionModel.columnCount() - 1)),
                    QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows
                );
                conditionGrid->selectionModel()->setCurrentIndex(idx, QItemSelectionModel::NoUpdate);
            }

            // Route right-click to column-specific context menu for bulk/in-place editing matching WinForms
            int col = idx.column();
            if (col == ConditionTableModel::ColFlag)
            {
                _flagContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
                return;
            }
            else if (col == ConditionTableModel::ColLType || col == ConditionTableModel::ColRType)
            {
                _typeContextMenu->setProperty("targetColumn", col);
                _typeContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
                return;
            }
            else if (col == ConditionTableModel::ColLSize || col == ConditionTableModel::ColRSize)
            {
                _sizeContextMenu->setProperty("targetColumn", col);
                _sizeContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
                return;
            }
            else if (col == ConditionTableModel::ColCmp)
            {
                auto *cond = _conditionModel.GetConditionAtRow(idx.row());
                QString flag = (cond != nullptr) ? cond->Flag : "";
                if (RaLogicSyntax::ArithmeticFlags.contains(flag))
                    _cmpArithmeticContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
                else if (RaLogicSyntax::StrictComparisonFlags.contains(flag))
                    _cmpStrictContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
                else
                    _cmpContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
                return;
            }
        }
        gridContextMenu->exec(conditionGrid->viewport()->mapToGlobal(pos));
    });
}

void TriggerEditorWidget::InitializeLeaderboardControls()
{
    cmbFormat->clear();
    for (const auto &fmt : _lbFormats)
        cmbFormat->addItem(fmt.Label, fmt.Value);
}

void TriggerEditorWidget::InitializeColumnContextMenus()
{
    auto CreateMenu = [this](QMenu *menu, const QStringList &items, int column = -1) {
        for (const auto &item : items)
        {
            auto *act = menu->addAction(item, this, [this, menu, column, item]() {
                int col = (column != -1) ? column : menu->property("targetColumn").toInt();
                OnColumnContextMenuTriggered(col, item);
            });
            act->setData(item);
        }
    };

    _flagContextMenu = new QMenu(this);
    CreateMenu(_flagContextMenu, RaLogicSyntax::Flags, ConditionTableModel::ColFlag);

    _typeContextMenu = new QMenu(this);
    CreateMenu(_typeContextMenu, RaLogicSyntax::OperandTypes);

    _sizeContextMenu = new QMenu(this);
    CreateMenu(_sizeContextMenu, RaLogicSyntax::MemorySizes);

    _cmpContextMenu = new QMenu(this);
    CreateMenu(_cmpContextMenu, RaLogicSyntax::ComparisonOperators, ConditionTableModel::ColCmp);

    _cmpArithmeticContextMenu = new QMenu(this);
    CreateMenu(_cmpArithmeticContextMenu, RaLogicSyntax::ArithmeticOperators, ConditionTableModel::ColCmp);

    _cmpStrictContextMenu = new QMenu(this);
    QStringList strictOps;
    for (const auto &op : RaLogicSyntax::ComparisonOperators)
    {
        if (op != RaLogicSyntax::NO_OPERATOR_TEXT)
            strictOps.append(op);
    }
    CreateMenu(_cmpStrictContextMenu, strictOps, ConditionTableModel::ColCmp);
}

void TriggerEditorWidget::OnColumnContextMenuTriggered(int column, const QString &val)
{
    if (IsGridReadOnly()) return;

    QString cleanVal = (val == RaLogicSyntax::NO_FLAG_TEXT || val == RaLogicSyntax::NO_OPERATOR_TEXT) ? "" : val;

    QList<int> selectedVisualRows = GetSelectedVisualRows();
    if (selectedVisualRows.isEmpty()) return;

    _conditionModel.BulkUpdateConditions(selectedVisualRows, column, cleanVal);
    SetStatus("Updated condition property.");
}

bool TriggerEditorWidget::ShowDecimal() const { return _showDecimal; }
void TriggerEditorWidget::SetShowDecimal(bool value) { chkShowDecimal->setChecked(value); }

bool TriggerEditorWidget::ShowAliases() const { return _showAliases; }
void TriggerEditorWidget::SetShowAliases(bool value) { chkAliasMode->setChecked(value); }

bool TriggerEditorWidget::CollapseChains() const { return _collapseChains; }
void TriggerEditorWidget::SetCollapseChains(bool value) { chkCollapseChains->setChecked(value); }

QString TriggerEditorWidget::GetTriggerText() const
{
    const_cast<TriggerEditorWidget*>(this)->CommitActiveEditor();
    return _viewModel.BoundAsset != nullptr ? _viewModel.BoundAsset->Logic : "";
}

void TriggerEditorWidget::SetTriggerText(const QString &value)
{
    if (_viewModel.BoundAsset != nullptr && _viewModel.BoundAsset->Logic != value)
    {
        _viewModel.BoundAsset->Logic = value;
        _viewModel.LoadAsset(_viewModel.BoundAsset);
    }
}

void TriggerEditorWidget::SetStatus(const QString &msg)
{
    if (lblEditorStatus != nullptr)
        lblEditorStatus->setText(msg);
    emit StatusUpdateRequested(msg);
}

void TriggerEditorWidget::SetNotes(const QList<CodeNote> &notes)
{
    _rawNotes = notes;
    _notesLookup.clear();
    _rangeCache.clear();

    PointerTreeParser parser;
    QSet<int64_t> rangedStarts;

    // Pass 1: Parse all real notes
    for (const auto &note : notes)
    {
        QString key = LogicFormatter::NormalizeAddress(note.Address);
        auto nodes = parser.ParseNoteText(note.Note);
        _notesLookup.insert(key, nodes);

        if (!nodes.isEmpty())
        {
            auto root = nodes[0];
            if (root->Size > 1)
            {
                bool ok = false;
                QString clean = key;
                clean.remove("0x", Qt::CaseInsensitive);
                int64_t startAddr = clean.toLongLong(&ok, 16);
                if (ok && !rangedStarts.contains(startAddr))
                {
                    rangedStarts.insert(startAddr);
                    _rangeCache.append(ConditionFormatter::RangeEntry{ startAddr, startAddr + root->Size, root });
                }
            }
        }
    }

    // Pass 2: Handle Redirects for Aliasing (Refer to $0x...)
    // If a note is empty but refers to another note, clone the tree so the Grid can display aliases
    static const QRegularExpression redirectRegex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression structRegex(
        QString::fromUtf8(R"(^[.\+\s\x{2500}-\x{257F}])"),
        QRegularExpression::MultilineOption);

    for (const auto &note : notes)
    {
        bool hasStructure = structRegex.match(note.Note).hasMatch();
        if (!hasStructure)
        {
            auto match = redirectRegex.match(note.Note);
            if (match.hasMatch())
            {
                QString targetAddr = LogicFormatter::NormalizeAddress(match.captured(1));
                QString sourceKey = LogicFormatter::NormalizeAddress(note.Address);

                if (_notesLookup.contains(targetAddr))
                {
                    const auto targetNodes = _notesLookup.value(targetAddr);
                    _notesLookup.insert(sourceKey, targetNodes);
                    if (!targetNodes.isEmpty() && targetNodes[0]->Size > 1)
                    {
                        bool ok = false;
                        QString clean = sourceKey;
                        clean.remove("0x", Qt::CaseInsensitive);
                        int64_t startAddr = clean.toLongLong(&ok, 16);
                        if (ok && !rangedStarts.contains(startAddr))
                        {
                            rangedStarts.insert(startAddr);
                            _rangeCache.append(ConditionFormatter::RangeEntry{ startAddr, startAddr + targetNodes[0]->Size, targetNodes[0] });
                        }
                    }
                }
            }
        }
    }

    _viewModel.NotesLookup = &_notesLookup;
    _viewModel.RangeCache = &_rangeCache;

    RefreshCurrentGridDisplay();
}

void TriggerEditorWidget::BindAchievement(std::shared_ptr<IAssetItem> asset, const QString &setType)
{
    const QList<int> savedVisualRows = GetSelectedVisualRows();
    const int savedCurrentVisualRow = conditionGrid->currentIndex().row();
    const int savedCurrentCol = conditionGrid->currentIndex().column();
    const int savedScroll = conditionGrid->verticalScrollBar()->value();

    _preserveSegmentSelection = "";
    if (lvLeaderboardComponents->isVisible() && lvLeaderboardComponents->currentItem() != nullptr)
    {
        _preserveSegmentSelection = lvLeaderboardComponents->currentItem()->text();
    }

    _isProgrammaticallyChanging = true;
    _viewModel.LoadAsset(asset);

    txtTitle->setText(asset != nullptr ? asset->Title : "");
    txtDescription->setText(asset != nullptr ? asset->Description : "");

    if (asset != nullptr)
    {
        if (!asset->isLeaderboard())
        {
            const auto &cheevo = static_cast<const RaAchievement &>(*asset);
            SetupAchievementUI(cheevo);
            pbBadge->setPixmap(ImageCache::GetBadge(cheevo.BadgeName, [this, currentBadgeName = cheevo.BadgeName](const QPixmap &pix) {
                if (_viewModel.BoundAsset != nullptr && !_viewModel.BoundAsset->isLeaderboard())
                {
                    const auto &curAch = static_cast<const RaAchievement &>(*_viewModel.BoundAsset);
                    if (curAch.BadgeName == currentBadgeName)
                        pbBadge->setPixmap(pix);
                }
            }, this));
        }
        else
        {
            const auto &lb = static_cast<const RaLeaderboard &>(*asset);
            SetupLeaderboardUI(lb);
        }

        if (asset->State == AssetState::Local)
        {
            lblAssetState->setText("Local");
            lblAssetState->setStyleSheet("color: #2e8b57; font-style: italic;");
        }
        else
        {
            lblAssetState->setText(setType);
            lblAssetState->setStyleSheet("color: #707070; font-style: italic;");
        }

        btnReset->setVisible(asset->State != AssetState::Local);
    }

    SetStatus("Asset loaded.");
    RunValidation();

    int newRowCount = _conditionModel.rowCount();
    QItemSelection restoredSelection;
    QList<int> validRestoredRows;
    for (int vRow : savedVisualRows)
    {
        if (vRow >= 0 && vRow < newRowCount)
        {
            auto left = _conditionModel.index(vRow, 0);
            auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
            restoredSelection.select(left, right);
            validRestoredRows.append(vRow);
        }
    }

    if (!restoredSelection.isEmpty())
    {
        conditionGrid->selectionModel()->select(
            restoredSelection,
            QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

        int focusRow = (savedCurrentVisualRow >= 0 && savedCurrentVisualRow < newRowCount)
            ? savedCurrentVisualRow
            : validRestoredRows.first();
        int focusCol = (savedCurrentCol >= 0 && savedCurrentCol < _conditionModel.columnCount())
            ? savedCurrentCol
            : 0;
        conditionGrid->selectionModel()->setCurrentIndex(
            _conditionModel.index(focusRow, focusCol),
            QItemSelectionModel::NoUpdate);
    }
    else
    {
        // Clear grid selection on load so row 0 is not highlighted in blue by default
        conditionGrid->clearSelection();
        conditionGrid->selectionModel()->setCurrentIndex(QModelIndex(), QItemSelectionModel::NoUpdate);
    }

    conditionGrid->verticalScrollBar()->setValue(savedScroll);
    if (_headerView != nullptr)
        _headerView->viewport()->update();

    _preserveSegmentSelection = "";
    _isProgrammaticallyChanging = false;
}

void TriggerEditorWidget::SetupAchievementUI(const RaAchievement &cheevo)
{
    cmbPoints->setVisible(true);
    cmbType->setVisible(true);
    lblAuthor->setVisible(true);
    lblAssetState->setVisible(true);
    pbBadge->setVisible(true);

    lblFormat->setVisible(false);
    cmbFormat->setVisible(false);
    chkLowerIsBetter->setVisible(false);

    cmbPoints->setCurrentText(QString::number(cheevo.Points));
    lblAuthor->setText(QString("Author: %1").arg(cheevo.Author));

    QString displayType = "";
    if (cheevo.Type == "progression") displayType = "Progression";
    else if (cheevo.Type == "win_condition") displayType = "Win";
    else if (cheevo.Type == "missable") displayType = "Missable";

    int idx = cmbType->findText(displayType);
    if (idx != -1) cmbType->setCurrentIndex(idx);
    else cmbType->setCurrentText(displayType);
}

void TriggerEditorWidget::SetupLeaderboardUI(const RaLeaderboard &lb)
{
    cmbPoints->setVisible(false);
    cmbType->setVisible(false);
    pbBadge->setVisible(false);
    lblAuthor->setVisible(true);
    lblAssetState->setVisible(true);

    lblFormat->setVisible(true);
    cmbFormat->setVisible(true);
    chkLowerIsBetter->setVisible(true);

    lblAuthor->setText("Author: Unknown");

    int fIdx = cmbFormat->findData(lb.Format);
    if (fIdx != -1) cmbFormat->setCurrentIndex(fIdx);
    else cmbFormat->setCurrentText(lb.Format);

    chkLowerIsBetter->setChecked(lb.LowerIsBetter);
}

void TriggerEditorWidget::Clear()
{
    _isProgrammaticallyChanging = true;
    _viewModel.LoadAsset(std::make_shared<RaAchievement>());
    txtTitle->clear();
    txtDescription->clear();
    cmbPoints->setCurrentIndex(-1);
    cmbType->setCurrentIndex(-1);
    cmbFormat->setCurrentIndex(-1);
    chkLowerIsBetter->setChecked(false);
    lblAuthor->clear();
    pbBadge->clear();
    groupsList->clear();
    conditionGrid->clearSelection();
    conditionGrid->selectionModel()->setCurrentIndex(QModelIndex(), QItemSelectionModel::NoUpdate);
    if (_headerView != nullptr)
        _headerView->viewport()->update();
    _currentIssues.clear();

    cmbPoints->setVisible(true);
    cmbType->setVisible(true);
    lblAuthor->setVisible(true);
    lblAssetState->setVisible(false);
    pbBadge->setVisible(true);
    lblFormat->setVisible(false);
    cmbFormat->setVisible(false);
    chkLowerIsBetter->setVisible(false);

    btnReset->setVisible(false);
    SetStatus("Ready");
    emit ValidationCompleted({});
    _isProgrammaticallyChanging = false;
}

void TriggerEditorWidget::Undo() { _viewModel.Undo(); SetStatus("Undone."); }
void TriggerEditorWidget::Redo() { _viewModel.Redo(); SetStatus("Redone."); }

void TriggerEditorWidget::RefreshGroupsListUI()
{
    int savedIndex = groupsList->currentRow();
    const auto &activeGroups = _viewModel.GetActiveGroups();
    const auto &generatorRules = _viewModel.GeneratorRules;

    // Suppress the transient currentRowChanged(-1) emitted by clear(): it would
    // repaint the grid with a null group and wipe the selection/current cell.
    bool wasChanging = _isProgrammaticallyChanging;
    _isProgrammaticallyChanging = true;

    groupsList->clear();

    QSet<int> generatedIndices;
    for (auto it = generatorRules.cbegin(); it != generatorRules.cend(); ++it)
    {
        if (!it.value().IsArray)
        {
            for (int i = 1; i <= it.value().Copies; i++)
                generatedIndices.insert(it.key() + i);
        }
    }

    for (int i = 0; i < activeGroups.size(); i++)
    {
        QString text = activeGroups[i].GroupName;
        auto *item = new QListWidgetItem(text);

        if (generatedIndices.contains(i))
        {
            item->setForeground(Qt::gray);
            QFont f = item->font();
            f.setItalic(true);
            item->setFont(f);
            item->setText(text + " (Auto)");
        }
        else if (generatorRules.contains(i))
        {
            item->setForeground(QColor(0, 120, 215));
            QFont f = item->font();
            f.setBold(true);
            item->setFont(f);
            item->setText(text + " *");

            if (generatorRules[i].IsArray)
                item->setText(item->text() + " [Array]");
        }
        groupsList->addItem(item);
    }

    if (groupsList->count() > 0)
    {
        int targetIndex = (savedIndex >= 0 && savedIndex < groupsList->count()) ? savedIndex : 0;
        groupsList->setCurrentRow(targetIndex);
    }

    RefreshCurrentGridDisplay();

    _isProgrammaticallyChanging = wasChanging;
}

void TriggerEditorWidget::RefreshCurrentGridDisplay()
{
    int savedScroll = conditionGrid->verticalScrollBar()->value();
    QList<int> selectedModelRows;
    for (int vRow : GetSelectedVisualRows())
    {
        int mIdx = _conditionModel.GetModelIndexForVisualRow(vRow);
        if (mIdx != -1 && !selectedModelRows.contains(mIdx))
            selectedModelRows.append(mIdx);
    }
    int currentVisualRow = conditionGrid->currentIndex().row();
    int currentModelRow = _conditionModel.GetModelIndexForVisualRow(currentVisualRow);
    int currentColumn = conditionGrid->currentIndex().column();

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    AchievementConditionGroup *group = (row >= 0 && row < groups.size()) ? &groups[row] : nullptr;

    bool isGenerated = false;
    for (auto it = _viewModel.GeneratorRules.cbegin(); it != _viewModel.GeneratorRules.cend(); ++it)
    {
        if (!it.value().IsArray && row > it.key() && row <= it.key() + it.value().Copies)
        {
            isGenerated = true;
            break;
        }
    }

    _conditionModel.SetGroup(
        group,
        isGenerated,
        _showDecimal,
        _showAliases,
        _collapseChains,
        &_notesLookup,
        &_rangeCache,
        &_currentIssues);

    if (group != nullptr && !selectedModelRows.isEmpty())
    {
        QItemSelection selection;
        for (int mIdx : selectedModelRows)
        {
            if (mIdx >= 0 && mIdx < group->Conditions.size())
            {
                int vRow = _conditionModel.GetVisualRowForCondition(&group->Conditions[mIdx]);
                if (vRow != -1)
                {
                    auto left = _conditionModel.index(vRow, 0);
                    auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
                    selection.select(left, right);
                }
            }
        }
        conditionGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);
    }
    else
    {
        conditionGrid->clearSelection();
    }

    // Use QItemSelectionModel::NoUpdate to set focus cell without wiping multi-row selection
    if (group != nullptr && currentModelRow != -1 && currentModelRow < group->Conditions.size())
    {
        int vRow = _conditionModel.GetVisualRowForCondition(&group->Conditions[currentModelRow]);
        if (vRow != -1)
        {
            int col = (currentColumn >= 0 && currentColumn < _conditionModel.columnCount()) ? currentColumn : 0;
            conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, col), QItemSelectionModel::NoUpdate);
        }
    }

    if (_headerView != nullptr)
        _headerView->viewport()->update();

    conditionGrid->verticalScrollBar()->setValue(savedScroll);
}

void TriggerEditorWidget::RunValidation()
{
    _currentIssues = _viewModel.ValidateCurrentLogic();
    emit ValidationCompleted(_currentIssues);

    bool hasErrors = false;
    bool hasWarnings = false;
    for (const auto &i : _currentIssues)
    {
        if (i.Severity >= FeedbackSeverity::Error) hasErrors = true;
        else if (i.Severity >= FeedbackSeverity::Warning) hasWarnings = true;
    }

    if (hasErrors) SetStatus(QString::fromUtf8("❌ Error(s) found in logic."));
    else if (hasWarnings) SetStatus(QString::fromUtf8("⚠️ Warning(s) found in logic."));
    else
    {
        if (lblEditorStatus->text().startsWith(QString::fromUtf8("❌")) || lblEditorStatus->text().startsWith(QString::fromUtf8("⚠️")))
            SetStatus("Ready");
    }

    _conditionModel.SetIssues(&_currentIssues);
}

void TriggerEditorWidget::CommitActiveEditor()
{
    if (conditionGrid != nullptr && !conditionGrid->viewport()->findChildren<QWidget*>().isEmpty())
    {
        auto curIdx = conditionGrid->currentIndex();
        conditionGrid->setCurrentIndex(QModelIndex());
        if (curIdx.isValid())
        {
            conditionGrid->setCurrentIndex(curIdx);
        }
    }
}

void TriggerEditorWidget::onTitleChanged(const QString &text)
{
    if (_isProgrammaticallyChanging || _viewModel.BoundAsset == nullptr) return;
    _viewModel.BoundAsset->Title = text;
    emit AchievementDataChanged();
}

void TriggerEditorWidget::onDescriptionChanged(const QString &text)
{
    if (_isProgrammaticallyChanging || _viewModel.BoundAsset == nullptr) return;
    _viewModel.BoundAsset->Description = text;
    emit AchievementDataChanged();
}

void TriggerEditorWidget::onPointsChanged()
{
    if (_isProgrammaticallyChanging || _viewModel.BoundAsset == nullptr) return;
    if (!_viewModel.BoundAsset->isLeaderboard())
    {
        auto &ach = static_cast<RaAchievement &>(*_viewModel.BoundAsset);
        bool ok = false;
        int p = cmbPoints->currentText().toInt(&ok);
        if (ok)
        {
            ach.Points = p;
            emit AchievementDataChanged();
        }
    }
}

void TriggerEditorWidget::onTypeChanged(int /*index*/)
{
    if (_isProgrammaticallyChanging || _viewModel.BoundAsset == nullptr) return;
    if (!_viewModel.BoundAsset->isLeaderboard())
    {
        auto &ach = static_cast<RaAchievement &>(*_viewModel.BoundAsset);
        QString t = cmbType->currentText();
        if (t == "Progression") ach.Type = "progression";
        else if (t == "Win") ach.Type = "win_condition";
        else if (t == "Missable") ach.Type = "missable";
        else ach.Type = "";
        emit AchievementDataChanged();
    }
}

void TriggerEditorWidget::onFormatChanged(int /*index*/)
{
    if (_isProgrammaticallyChanging || _viewModel.BoundAsset == nullptr) return;
    if (_viewModel.BoundAsset->isLeaderboard())
    {
        auto &lb = static_cast<RaLeaderboard &>(*_viewModel.BoundAsset);
        lb.Format = cmbFormat->currentData().toString();
        emit AchievementDataChanged();
    }
}

void TriggerEditorWidget::onLowerIsBetterChanged(bool checked)
{
    if (_isProgrammaticallyChanging || _viewModel.BoundAsset == nullptr) return;
    if (_viewModel.BoundAsset->isLeaderboard())
    {
        auto &lb = static_cast<RaLeaderboard &>(*_viewModel.BoundAsset);
        lb.LowerIsBetter = checked;
        emit AchievementDataChanged();
    }
}

void TriggerEditorWidget::onShowDecimalChanged(bool checked)
{
    _showDecimal = checked;
    RefreshCurrentGridDisplay();
}

void TriggerEditorWidget::onAliasModeChanged(bool checked)
{
    _showAliases = checked;
    RefreshCurrentGridDisplay();
}

void TriggerEditorWidget::onCollapseChainsChanged(bool checked)
{
    _collapseChains = checked;
    RefreshCurrentGridDisplay();
}

void TriggerEditorWidget::onGroupsListSelectionChanged()
{
    if (_isProgrammaticallyChanging) return;
    RefreshCurrentGridDisplay();
}

void TriggerEditorWidget::onLbComponentsSelectionChanged()
{
    if (_isProgrammaticallyChanging || lvLeaderboardComponents->currentItem() == nullptr) return;
    QString seg = lvLeaderboardComponents->currentItem()->text();
    _viewModel.SelectSegment(seg);
    RefreshGroupsListUI();
}

void TriggerEditorWidget::onBtnResetClicked() { emit AssetResetRequested(); }
void TriggerEditorWidget::onBtnAddNoteClicked() { emit AddFromCodeNoteRequested(); }
void TriggerEditorWidget::onBtnCopyDefClicked()
{
    CommitActiveEditor();
    if (_viewModel.BoundAsset != nullptr)
    {
        QGuiApplication::clipboard()->setText(_viewModel.BoundAsset->Logic);
        SetStatus("Full definition copied.");
    }
}

void TriggerEditorWidget::onBtnToolsClicked()
{
    toolsContextMenu->exec(btnTools->mapToGlobal(QPoint(0, btnTools->height())));
}

void TriggerEditorWidget::onAddGroupClicked()
{
    _viewModel.AddGroup();
    RefreshGroupsListUI();
    if (groupsList->count() > 0)
        groupsList->setCurrentRow(groupsList->count() - 1);
}

void TriggerEditorWidget::onRemoveGroupClicked()
{
    int row = groupsList->currentRow();
    if (row == 0)
    {
        QMessageBox::warning(this, "Operation Not Allowed", "The Core group cannot be deleted.");
        return;
    }
    _viewModel.RemoveGroup(row);
    RefreshGroupsListUI();
}

void TriggerEditorWidget::onRemoveAllAltGroupsClicked()
{
    if (QMessageBox::question(this, "Confirm", "Remove all Alt groups?") == QMessageBox::Yes)
    {
        _viewModel.RemoveAllAltGroups();
        RefreshGroupsListUI();
    }
}

bool TriggerEditorWidget::eventFilter(QObject *watched, QEvent *event)
{
    if (watched == conditionGrid && event->type() == QEvent::KeyPress)
    {
        auto *keyEvent = static_cast<QKeyEvent *>(event);
        bool isEditing = !conditionGrid->viewport()->findChildren<QWidget*>().isEmpty();

        // Generated ("Auto") groups are read-only: swallow mutating shortcuts.
        if (!isEditing && IsGridReadOnly())
        {
            if (keyEvent->key() == Qt::Key_Delete ||
                keyEvent->matches(QKeySequence::Cut) ||
                keyEvent->matches(QKeySequence::Paste) ||
                (keyEvent->modifiers() == Qt::ControlModifier && keyEvent->key() == Qt::Key_D))
            {
                return true;
            }
        }

        // Only handle navigation and grid operations when not typing in an in-place cell editor
        if (!isEditing)
        {
            if (keyEvent->key() == Qt::Key_Return || keyEvent->key() == Qt::Key_Enter)
            {
                auto current = conditionGrid->currentIndex();
                if (current.isValid() && (current.column() == ConditionTableModel::ColLValue || current.column() == ConditionTableModel::ColRValue))
                {
                    HandleAddressSelection(current.row(), current.column());
                    return true;
                }
            }
            else if (keyEvent->modifiers() == Qt::AltModifier && keyEvent->key() == Qt::Key_Up)
            {
                MoveSelected(-1);
                return true;
            }
            else if (keyEvent->modifiers() == Qt::AltModifier && keyEvent->key() == Qt::Key_Down)
            {
                MoveSelected(1);
                return true;
            }
            else if (keyEvent->matches(QKeySequence::SelectAll))
            {
                conditionGrid->selectAll();
                return true;
            }
            else if (keyEvent->modifiers() == Qt::ControlModifier && keyEvent->key() == Qt::Key_C)
            {
                CopySelectedLogic();
                return true;
            }
            else if (keyEvent->modifiers() == (Qt::ControlModifier | Qt::ShiftModifier) && keyEvent->key() == Qt::Key_C)
            {
                CopySelectedRows();
                return true;
            }
            else if (keyEvent->matches(QKeySequence::Cut))
            {
                CutSelected();
                return true;
            }
            else if (keyEvent->matches(QKeySequence::Paste))
            {
                PasteLogic();
                return true;
            }
            else if (keyEvent->key() == Qt::Key_Delete)
            {
                DeleteSelected();
                return true;
            }
            else if (keyEvent->modifiers() == Qt::ControlModifier && keyEvent->key() == Qt::Key_D)
            {
                DuplicateSelected();
                return true;
            }
        }
    }

    return QWidget::eventFilter(watched, event);
}

QList<int> TriggerEditorWidget::GetSelectedVisualRows() const
{
    QSet<int> visualRowsSet;
    auto selectedRows = conditionGrid->selectionModel()->selectedRows();
    for (const auto &idx : selectedRows)
        visualRowsSet.insert(idx.row());

    if (visualRowsSet.isEmpty() && conditionGrid->selectionModel()->hasSelection())
    {
        for (const auto &idx : conditionGrid->selectionModel()->selectedIndexes())
            visualRowsSet.insert(idx.row());
    }

    // No current-index fallback: when the user clicks empty space below the rows the
    // selection is empty, and destructive actions must not silently target a stale current row.

    QList<int> result = visualRowsSet.values();
    std::sort(result.begin(), result.end());
    return result;
}

QList<int> TriggerEditorWidget::GetEffectiveSelectionIndices()
{
    int groupRow = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (groupRow < 0 || groupRow >= groups.size()) return {};

    auto &group = groups[groupRow];

    QList<int> selectedIndices;
    for (int vRow : GetSelectedVisualRows())
    {
        int modelIdx = _conditionModel.GetModelIndexForVisualRow(vRow);
        if (modelIdx >= 0 && modelIdx < group.Conditions.size() && !selectedIndices.contains(modelIdx))
            selectedIndices.append(modelIdx);
    }
    std::sort(selectedIndices.begin(), selectedIndices.end());

    if (!_collapseChains) return selectedIndices;

    // Expand each visible leaf back to its hidden Add Address chain. Identity is
    // tracked by model index (value copies made pointer-based de-duplication fail).
    QList<int> expanded;
    QSet<int> processed;
    for (int modelIdx : selectedIndices)
    {
        QList<int> chain;
        int scan = modelIdx - 1;
        while (scan >= 0)
        {
            if (group.Conditions[scan].Flag == "Add Address")
            {
                chain.prepend(scan);
                scan--;
            }
            else break;
        }

        for (int chainIdx : chain)
        {
            if (!processed.contains(chainIdx))
            {
                processed.insert(chainIdx);
                expanded.append(chainIdx);
            }
        }
        if (!processed.contains(modelIdx))
        {
            processed.insert(modelIdx);
            expanded.append(modelIdx);
        }
    }

    return expanded;
}

QList<AchievementCondition> TriggerEditorWidget::GetEffectiveSelection()
{
    QList<AchievementCondition> result;
    int groupRow = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (groupRow < 0 || groupRow >= groups.size()) return result;

    const auto &group = groups[groupRow];
    for (int modelIdx : GetEffectiveSelectionIndices())
    {
        if (modelIdx >= 0 && modelIdx < group.Conditions.size())
            result.append(group.Conditions[modelIdx]);
    }
    return result;
}

bool TriggerEditorWidget::IsGridReadOnly() const
{
    int row = groupsList->currentRow();
    for (auto it = _viewModel.GeneratorRules.cbegin(); it != _viewModel.GeneratorRules.cend(); ++it)
    {
        if (!it.value().IsArray && row > it.key() && row <= it.key() + it.value().Copies)
            return true;
    }
    return false;
}

void TriggerEditorWidget::CopySelectedRows()
{
    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;
    const auto &group = groups[row];

    // Copy Row mirrors what is visible on screen: never expand hidden Add Address
    // chains (C# uses the selected grid rows directly here).
    QList<AchievementCondition> conditionsToCopy;
    for (int vRow : GetSelectedVisualRows())
    {
        auto *c = _conditionModel.GetConditionAtRow(vRow);
        if (c != nullptr) conditionsToCopy.append(*c);
    }
    if (conditionsToCopy.isEmpty()) return;

    int colCount = _conditionModel.columnCount();
    QList<QStringList> formattedRows;
    QVector<int> colWidths(colCount, 0);

    for (const auto &cond : conditionsToCopy)
    {
        int modelIdx = group.Conditions.indexOf(cond);
        QStringList rowCells;
        rowCells.reserve(colCount);

        for (int c = 0; c < colCount; c++)
        {
            QString txt = "";
            switch (c)
            {
                case ConditionTableModel::ColID:
                    txt = QString::number(modelIdx != -1 ? modelIdx + 1 : cond.ID);
                    break;
                case ConditionTableModel::ColFlag:
                    txt = cond.Flag;
                    break;
                case ConditionTableModel::ColLType:
                    txt = cond.LeftOperand.Type;
                    break;
                case ConditionTableModel::ColLSize:
                    txt = cond.LeftOperand.Size;
                    break;
                case ConditionTableModel::ColLValue:
                    txt = ConditionFormatter::FormatOperandDisplay(
                        cond.LeftOperand, _showDecimal, _showAliases, &_notesLookup, &_rangeCache, &group, modelIdx);
                    break;
                case ConditionTableModel::ColCmp:
                    txt = cond.Operator;
                    break;
                case ConditionTableModel::ColRType:
                    txt = cond.RightOperand.Type;
                    break;
                case ConditionTableModel::ColRSize:
                    txt = cond.RightOperand.Size;
                    break;
                case ConditionTableModel::ColRValue:
                {
                    QString rightSizeRef = LogicFormatter::GetRightOperandSizeReference(cond);
                    txt = ConditionFormatter::FormatOperandDisplay(
                        cond.RightOperand, _showDecimal, _showAliases, &_notesLookup, &_rangeCache, &group, modelIdx, rightSizeRef);
                    break;
                }
                case ConditionTableModel::ColHits:
                    txt = (!RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag) && cond.RequiredHits > 0)
                        ? QString::number(cond.RequiredHits) : "";
                    break;
            }

            if (txt.length() > colWidths[c]) colWidths[c] = txt.length();
            rowCells.append(txt);
        }
        formattedRows.append(rowCells);
    }

    QString sb;
    for (const auto &rowCells : formattedRows)
    {
        for (int c = 0; c < colCount; c++)
        {
            const QString &txt = rowCells[c];
            if (c < colCount - 1)
                sb += txt.leftJustified(colWidths[c] + 2, ' ');
            else
                sb += txt;
        }
        sb += '\n';
    }

    QGuiApplication::clipboard()->setText(sb);
    SetStatus("Rows copied to clipboard (aligned).");
}

QString TriggerEditorWidget::GetSelectedLogicString()
{
    auto conditionsToCopy = GetEffectiveSelection();
    if (conditionsToCopy.isEmpty()) return "";

    QString sb;
    for (int i = 0; i < conditionsToCopy.size(); i++)
    {
        if (i > 0) sb += '_';
        sb += LogicFormatter::ConditionToString(conditionsToCopy[i]);
    }
    return sb;
}

void TriggerEditorWidget::CopySelectedLogic()
{
    QString logic = GetSelectedLogicString();
    if (!logic.isEmpty())
    {
        QGuiApplication::clipboard()->setText(logic);
        SetStatus("Logic string copied to clipboard.");
    }
}

void TriggerEditorWidget::CutSelected()
{
    CopySelectedLogic();
    DeleteSelected();
    SetStatus("Logic cut to clipboard.");
}

void TriggerEditorWidget::AddCondition()
{
    if (IsGridReadOnly()) return;

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;

    _viewModel.PushUndoState();
    auto &group = groups[row];

    AchievementCondition newCond;
    newCond.ID = group.Conditions.size() + 1;
    newCond.LeftOperand.Type = "Value";
    newCond.LeftOperand.Value = "0";
    newCond.Operator = "=";
    newCond.RightOperand.Type = "Value";
    newCond.RightOperand.Value = "1";

    int insertIdx = group.Conditions.size();
    auto selectedRows = conditionGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty())
    {
        int maxVisualRow = -1;
        for (const auto &idx : selectedRows)
            if (idx.row() > maxVisualRow) maxVisualRow = idx.row();

        int mIdx = _conditionModel.GetModelIndexForVisualRow(maxVisualRow);
        if (mIdx != -1) insertIdx = mIdx + 1;
    }

    group.Conditions.insert(insertIdx, newCond);
    _viewModel.UpdateLogic(false);
    RefreshCurrentGridDisplay();

    // UpdateLogic may apply generator rules that insert/reallocate groups, so
    // re-fetch the group instead of using a possibly dangling reference.
    auto &groupsAfter = _viewModel.GetActiveGroups();
    if (row >= groupsAfter.size()) return;
    auto &groupAfter = groupsAfter[row];
    if (insertIdx < 0 || insertIdx >= groupAfter.Conditions.size()) return;

    int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[insertIdx]);
    if (vRow != -1)
    {
        auto left = _conditionModel.index(vRow, 0);
        auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
        conditionGrid->selectionModel()->select(QItemSelection(left, right), QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);
        conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, 0), QItemSelectionModel::NoUpdate);
        if (_headerView != nullptr) _headerView->viewport()->update();
    }

    SetStatus("Condition added.");
}

void TriggerEditorWidget::DuplicateSelected()
{
    if (IsGridReadOnly()) return;

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;

    QList<int> selectedIndices = GetEffectiveSelectionIndices();
    if (selectedIndices.isEmpty()) return;

    _viewModel.PushUndoState();
    auto &group = groups[row];

    int insertIdx = group.Conditions.size();
    int lastModelIdx = selectedIndices.last();
    if (lastModelIdx != -1) insertIdx = lastModelIdx + 1;

    QList<AchievementCondition> clones;
    for (int modelIdx : selectedIndices)
    {
        auto clone = group.Conditions[modelIdx].Clone();
        clone.ID = group.Conditions.size() + clones.size() + 1;
        clones.append(clone);
    }

    for (int i = 0; i < clones.size(); ++i)
        group.Conditions.insert(insertIdx + i, clones[i]);

    _viewModel.UpdateLogic(false);
    RefreshCurrentGridDisplay();

    auto &groupsAfter = _viewModel.GetActiveGroups();
    if (row >= groupsAfter.size()) return;
    auto &groupAfter = groupsAfter[row];

    QItemSelection selection;
    for (int i = 0; i < clones.size(); ++i)
    {
        int idx = insertIdx + i;
        if (idx < 0 || idx >= groupAfter.Conditions.size()) continue;
        int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[idx]);
        if (vRow != -1)
        {
            auto left = _conditionModel.index(vRow, 0);
            auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
            selection.select(left, right);
        }
    }
    conditionGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

    if (!clones.isEmpty() && insertIdx >= 0 && insertIdx < groupAfter.Conditions.size())
    {
        int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[insertIdx]);
        if (vRow != -1)
        {
            conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, 0), QItemSelectionModel::NoUpdate);
            conditionGrid->scrollTo(_conditionModel.index(vRow, 0));
        }
    }

    if (_headerView != nullptr)
        _headerView->viewport()->update();

    SetStatus(QString("Conditions duplicated (%1 lines).").arg(clones.size()));
}

void TriggerEditorWidget::DeleteSelected()
{
    if (IsGridReadOnly()) return;

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;

    QList<int> toDelete = GetEffectiveSelectionIndices();
    if (toDelete.isEmpty()) return;

    _viewModel.PushUndoState();
    auto &group = groups[row];

    int firstDeleteModelIdx = toDelete.first();

    // Remove from the highest index down so earlier indices stay valid.
    std::sort(toDelete.begin(), toDelete.end(), [](int a, int b) { return a > b; });
    for (int modelIdx : toDelete)
    {
        if (modelIdx >= 0 && modelIdx < group.Conditions.size())
            group.Conditions.removeAt(modelIdx);
    }

    _viewModel.UpdateLogic(false);
    RefreshCurrentGridDisplay();

    auto &groupsAfter = _viewModel.GetActiveGroups();
    if (row >= groupsAfter.size()) return;
    auto &groupAfter = groupsAfter[row];

    if (firstDeleteModelIdx != -1 && !groupAfter.Conditions.isEmpty())
    {
        if (firstDeleteModelIdx >= groupAfter.Conditions.size())
            firstDeleteModelIdx = groupAfter.Conditions.size() - 1;

        int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[firstDeleteModelIdx]);
        if (vRow != -1)
        {
            auto left = _conditionModel.index(vRow, 0);
            auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
            conditionGrid->selectionModel()->select(QItemSelection(left, right), QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);
            conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, 0), QItemSelectionModel::NoUpdate);
        }
    }

    if (_headerView != nullptr)
        _headerView->viewport()->update();

    SetStatus("Conditions deleted.");
}

void TriggerEditorWidget::MoveSelected(int direction)
{
    if (IsGridReadOnly()) return;

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size() || direction == 0) return;

    auto &group = groups[row];
    auto selectedRows = conditionGrid->selectionModel()->selectedRows();
    if (selectedRows.isEmpty()) return;

    int savedCurrentCol = conditionGrid->currentIndex().column();
    if (savedCurrentCol < 0 || savedCurrentCol >= _conditionModel.columnCount())
        savedCurrentCol = 0;

    QList<int> selectedModelIndices = GetEffectiveSelectionIndices();
    if (selectedModelIndices.isEmpty()) return;

    QSet<int> selectedSet;
    for (int idx : selectedModelIndices) selectedSet.insert(idx);

    // Units are model-index lists; comparing indices avoids value-equality bugs
    // when two conditions in the same group are identical.
    QList<QList<int>> units;

    if (_collapseChains)
    {
        QList<int> currentUnit;
        for (int i = 0; i < group.Conditions.size(); ++i)
        {
            currentUnit.append(i);
            if (group.Conditions[i].Flag != "Add Address")
            {
                units.append(currentUnit);
                currentUnit.clear();
            }
        }
        if (!currentUnit.isEmpty()) units.append(currentUnit);
    }
    else
    {
        for (int i = 0; i < group.Conditions.size(); ++i)
            units.append({ i });
    }

    QList<int> selectedUnitIndices;
    for (int i = 0; i < units.size(); i++)
    {
        bool unitSelected = false;
        for (int modelIdx : units[i])
        {
            if (selectedSet.contains(modelIdx))
            {
                unitSelected = true;
                break;
            }
        }
        if (unitSelected) selectedUnitIndices.append(i);
    }

    if (selectedUnitIndices.isEmpty()) return;
    std::sort(selectedUnitIndices.begin(), selectedUnitIndices.end());

    if (direction < 0 && selectedUnitIndices.first() == 0) return;
    if (direction > 0 && selectedUnitIndices.last() >= units.size() - 1) return;

    _viewModel.PushUndoState();

    if (direction < 0)
    {
        for (int idx : selectedUnitIndices)
            units.swapItemsAt(idx, idx - 1);
    }
    else
    {
        for (int i = selectedUnitIndices.size() - 1; i >= 0; --i)
        {
            int idx = selectedUnitIndices[i];
            units.swapItemsAt(idx, idx + 1);
        }
    }

    // Track the new unit indices that were moved
    QSet<int> newMovedUnitIndices;
    if (direction < 0)
    {
        for (int idx : selectedUnitIndices)
            newMovedUnitIndices.insert(idx - 1);
    }
    else
    {
        for (int idx : selectedUnitIndices)
            newMovedUnitIndices.insert(idx + 1);
    }

    // Determine the exact condition indices in group.Conditions that should be selected
    QSet<int> modelIndicesToSelect;
    int condOffset = 0;
    for (int u = 0; u < units.size(); ++u)
    {
        int unitSize = units[u].size();
        if (newMovedUnitIndices.contains(u))
        {
            for (int k = 0; k < unitSize; ++k)
                modelIndicesToSelect.insert(condOffset + k);
        }
        condOffset += unitSize;
    }

    const QList<AchievementCondition> originalConditions = group.Conditions;
    group.Conditions.clear();
    for (const auto &unit : units)
    {
        for (int modelIdx : unit)
        {
            if (modelIdx >= 0 && modelIdx < originalConditions.size())
                group.Conditions.append(originalConditions[modelIdx]);
        }
    }

    _viewModel.UpdateLogic(false);
    RefreshCurrentGridDisplay();

    auto &groupsAfter = _viewModel.GetActiveGroups();
    if (row >= groupsAfter.size()) return;
    auto &groupAfter = groupsAfter[row];

    QList<int> visualRowsToSelect;
    for (int mIdx : modelIndicesToSelect)
    {
        if (mIdx >= 0 && mIdx < groupAfter.Conditions.size())
        {
            int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[mIdx]);
            if (vRow != -1 && !visualRowsToSelect.contains(vRow))
                visualRowsToSelect.append(vRow);
        }
    }

    std::sort(visualRowsToSelect.begin(), visualRowsToSelect.end());

    QItemSelection selection;
    for (int vRow : visualRowsToSelect)
    {
        auto left = _conditionModel.index(vRow, 0);
        auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
        selection.select(left, right);
    }
    conditionGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

    if (!visualRowsToSelect.isEmpty())
    {
        int focusRow = (direction > 0) ? visualRowsToSelect.last() : visualRowsToSelect.first();
        conditionGrid->selectionModel()->setCurrentIndex(
            _conditionModel.index(focusRow, savedCurrentCol),
            QItemSelectionModel::NoUpdate
        );
        conditionGrid->scrollTo(_conditionModel.index(focusRow, savedCurrentCol));
    }

    if (_headerView != nullptr)
        _headerView->viewport()->update();

    SetStatus("Conditions moved.");
}

void TriggerEditorWidget::ExtendSelectionByPattern()
{
    // Order selected rows by visual row index ascending to establish correct sequential pattern
    auto selectedVisualRows = GetSelectedVisualRows();
    if (selectedVisualRows.size() < 2)
    {
        QMessageBox::information(this, "Extend Selection", "Please select at least two sequential rows to establish a pattern.");
        return;
    }

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;
    const auto &group = groups[row];

    int v1 = selectedVisualRows[selectedVisualRows.size() - 2];
    int v2 = selectedVisualRows.last();
    int r1Idx = _conditionModel.GetModelIndexForVisualRow(v1);
    int r2Idx = _conditionModel.GetModelIndexForVisualRow(v2);
    if (r1Idx == -1 || r2Idx == -1) return;

    const auto &c1 = group.Conditions[r1Idx];
    const auto &c2 = group.Conditions[r2Idx];

    auto ParseNumeric = [](const Operand &op) -> int64_t {
        if (op.Value.trimmed().isEmpty()) return 0;
        QString val = op.Value.trimmed();
        bool ok = false;
        if (val.startsWith("0x", Qt::CaseInsensitive))
            return val.mid(2).toLongLong(&ok, 16);
        return val.toLongLong(&ok, 10);
    };

    auto IsFloatOrRecall = [](const Operand &op) {
        return op.Type == "Float" || op.Type == "Recall";
    };

    int64_t leftDelta = ParseNumeric(c2.LeftOperand) - ParseNumeric(c1.LeftOperand);
    int64_t rightDelta = ParseNumeric(c2.RightOperand) - ParseNumeric(c1.RightOperand);
    int64_t hitsDelta = static_cast<int64_t>(c2.RequiredHits) - c1.RequiredHits;

    // Walk the VISIBLE rows (C# semantics): with collapsed chains, model indices
    // contain hidden Add Address gaps and must not be used as the stride.
    int visualStride = v2 - v1;
    if (visualStride <= 0) return;

    int currentVisual = v2;
    AchievementCondition cPrev = c2;
    int selectedCount = 0;

    while (true)
    {
        int nextVisual = currentVisual + visualStride;
        if (nextVisual < 0 || nextVisual >= _conditionModel.rowCount()) break;

        auto *cNextPtr = _conditionModel.GetConditionAtRow(nextVisual);
        if (cNextPtr == nullptr) break;
        const auto &cNext = *cNextPtr;

        if (cNext.Flag != cPrev.Flag || cNext.Operator != cPrev.Operator ||
            cNext.LeftOperand.Type != cPrev.LeftOperand.Type || cNext.LeftOperand.Size != cPrev.LeftOperand.Size ||
            cNext.RightOperand.Type != cPrev.RightOperand.Type || cNext.RightOperand.Size != cPrev.RightOperand.Size)
            break;

        // Float/Recall values cannot be stepped numerically; require an exact match.
        bool leftOk = IsFloatOrRecall(cPrev.LeftOperand)
            ? (cNext.LeftOperand.Value == cPrev.LeftOperand.Value)
            : (ParseNumeric(cNext.LeftOperand) == ParseNumeric(cPrev.LeftOperand) + leftDelta);
        if (!leftOk) break;

        bool rightOk = IsFloatOrRecall(cPrev.RightOperand)
            ? (cNext.RightOperand.Value == cPrev.RightOperand.Value)
            : (ParseNumeric(cNext.RightOperand) == ParseNumeric(cPrev.RightOperand) + rightDelta);
        if (!rightOk) break;

        if (static_cast<int64_t>(cNext.RequiredHits) != static_cast<int64_t>(cPrev.RequiredHits) + hitsDelta) break;

        auto left = _conditionModel.index(nextVisual, 0);
        auto right = _conditionModel.index(nextVisual, _conditionModel.columnCount() - 1);
        conditionGrid->selectionModel()->select(QItemSelection(left, right), QItemSelectionModel::Select | QItemSelectionModel::Rows);

        currentVisual = nextVisual;
        cPrev = cNext;
        selectedCount++;
    }

    if (selectedCount > 0)
    {
        int lastVisualRow = -1;
        auto currentSelected = conditionGrid->selectionModel()->selectedRows();
        for (const auto &idx : currentSelected)
        {
            if (idx.row() > lastVisualRow) lastVisualRow = idx.row();
        }
        if (lastVisualRow != -1)
        {
            conditionGrid->selectionModel()->setCurrentIndex(
                _conditionModel.index(lastVisualRow, 0),
                QItemSelectionModel::NoUpdate
            );
            conditionGrid->scrollTo(_conditionModel.index(lastVisualRow, 0));
        }
        SetStatus(QString("Extended selection by %1 row(s).").arg(selectedCount));
    }
    else
    {
        SetStatus("No matching pattern found to extend.");
    }
}

void TriggerEditorWidget::PasteLogic(const QString &logicToPaste)
{
    if (IsGridReadOnly()) return;

    QString txt = !logicToPaste.isEmpty() ? logicToPaste : QGuiApplication::clipboard()->text();
    txt = txt.trimmed().remove('\r').remove('\n');
    if (txt.isEmpty()) return;

    if (txt[0].isDigit() && txt.contains(":\"") && txt.count(':') > 5)
    {
        if (QMessageBox::question(this, "Confirm", "Apply Smart Paste?") == QMessageBox::Yes)
        {
            ParseAndApplySmartPaste(txt);
            return;
        }
    }

    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;

    _viewModel.PushUndoState();
    auto &group = groups[row];

    int insertIdx = group.Conditions.size();
    auto selectedRows = conditionGrid->selectionModel()->selectedRows();
    if (!selectedRows.isEmpty())
    {
        int maxVisualRow = -1;
        for (const auto &idx : selectedRows)
            if (idx.row() > maxVisualRow) maxVisualRow = idx.row();

        int mIdx = _conditionModel.GetModelIndexForVisualRow(maxVisualRow);
        if (mIdx != -1) insertIdx = mIdx + 1;
    }

    auto parsedGroups = AchievementParser::ParseAchievementTrigger(txt, 'S', false);
    QList<AchievementCondition> newConditions;
    for (const auto &g : parsedGroups)
    {
        for (const auto &c : g.Conditions)
            newConditions.append(c);
    }

    for (int i = 0; i < newConditions.size(); ++i)
        group.Conditions.insert(insertIdx + i, newConditions[i]);

    _viewModel.UpdateLogic(false);
    RefreshCurrentGridDisplay();

    auto &groupsAfter = _viewModel.GetActiveGroups();
    if (row >= groupsAfter.size()) return;
    auto &groupAfter = groupsAfter[row];

    QItemSelection selection;
    for (int i = 0; i < newConditions.size(); ++i)
    {
        int idx = insertIdx + i;
        if (idx < 0 || idx >= groupAfter.Conditions.size()) continue;
        int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[idx]);
        if (vRow != -1)
        {
            auto left = _conditionModel.index(vRow, 0);
            auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
            selection.select(left, right);
        }
    }
    conditionGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

    if (!newConditions.isEmpty() && insertIdx >= 0 && insertIdx < groupAfter.Conditions.size())
    {
        int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[insertIdx]);
        if (vRow != -1)
        {
            conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, 0), QItemSelectionModel::NoUpdate);
            conditionGrid->scrollTo(_conditionModel.index(vRow, 0));
        }
    }

    if (_headerView != nullptr)
        _headerView->viewport()->update();

    SetStatus(QString("Logic pasted (%1 lines).").arg(newConditions.size()));
}

void TriggerEditorWidget::ParseAndApplySmartPaste(const QString &raw)
{
    int firstColon = raw.indexOf(':');
    if (firstColon == -1) return;

    QString content = raw.mid(firstColon + 1);
    QStringList parts;
    bool inQuote = false;
    int start = 0;
    for (int i = 0; i < content.length(); i++)
    {
        if (content[i] == '"') inQuote = !inQuote;
        else if (content[i] == ':' && !inQuote)
        {
            QString val = content.mid(start, i - start);
            if (val.startsWith('"') && val.endsWith('"')) val = val.mid(1, val.length() - 2).replace("\\\"", "\"");
            parts.append(val);
            start = i + 1;
        }
    }
    if (start <= content.length())
    {
        QString val = content.mid(start);
        if (val.startsWith('"') && val.endsWith('"')) val = val.mid(1, val.length() - 2).replace("\\\"", "\"");
        parts.append(val);
    }

    if (parts.size() >= 3)
    {
        SetTriggerText(parts[0]);
        txtTitle->setText(parts[1]);
        txtDescription->setText(parts[2]);

        if (parts.size() > 5 && !parts[5].isEmpty())
        {
            QString t = parts[5].toLower();
            if (t.contains("prog")) cmbType->setCurrentText("Progression");
            else if (t.contains("win")) cmbType->setCurrentText("Win");
            else if (t.contains("miss")) cmbType->setCurrentText("Missable");
        }

        if (parts.size() > 7)
        {
            bool ok = false;
            int pts = parts[7].toInt(&ok);
            if (ok) cmbPoints->setCurrentText(QString::number(pts));
        }

        SetStatus("Smart Paste applied successfully.");
    }
}

void TriggerEditorWidget::HandleAddressSelection(int row, int col)
{
    if (col != ConditionTableModel::ColLValue && col != ConditionTableModel::ColRValue) return;

    auto *cond = _conditionModel.GetConditionAtRow(row);
    if (cond == nullptr) return;

    QString val = (col == ConditionTableModel::ColLValue) ? cond->LeftOperand.Value : cond->RightOperand.Value;
    if (!val.startsWith("0x", Qt::CaseInsensitive)) return;

    int gRow = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (gRow < 0 || gRow >= groups.size()) return;
    const auto &group = groups[gRow];

    int modelIdx = _conditionModel.GetModelIndexForVisualRow(row);
    if (modelIdx < 0) return;

    QList<AchievementCondition> collected;
    int scanIndex = modelIdx - 1;
    while (scanIndex >= 0)
    {
        const auto &prev = group.Conditions[scanIndex];
        if (prev.Flag != "Add Address") break;
        collected.prepend(prev);
        scanIndex--;
    }

    if (!collected.isEmpty())
    {
        auto ParseHex = [](const QString &hex) -> int64_t {
            QString clean = hex;
            clean.remove("0x", Qt::CaseInsensitive);
            bool ok = false;
            int64_t v = clean.toLongLong(&ok, 16);
            return ok ? v : -1;
        };

        while (!collected.isEmpty())
        {
            int64_t baseAddrNum = ParseHex(collected[0].LeftOperand.Value);
            if (!RaLogicSyntax::MemoryTypes.contains(collected[0].LeftOperand.Type) ||
                ConditionFormatter::GetEffectiveNote(_notesLookup, baseAddrNum) == nullptr)
            {
                collected.removeFirst();
            }
            else break;
        }

        if (!collected.isEmpty())
        {
            int64_t baseAddrLong = ParseHex(collected[0].LeftOperand.Value);
            QString baseAddress = QString("0x%1").arg(baseAddrLong, 0, 16);
            QList<int64_t> offsets;
            for (const auto &c : collected)
                offsets.append(ParseHex(c.LeftOperand.Value));

            bool ok = false;
            int64_t currentOffset = val.mid(2).toLongLong(&ok, 16);
            if (ok) offsets.append(currentOffset);

            emit PointerChainSelected(PointerChainArgs(baseAddress, offsets));
            return;
        }
    }

    emit AddressSelected(val);
}

void TriggerEditorWidget::AddConditionFromNote(const QString &logicString, const QString &type, const QString &size)
{
    if (logicString.isEmpty()) return;

    _viewModel.PushUndoState();
    auto parsed = AchievementParser::ParseAchievementTrigger(logicString, 'S', false);

    if (!parsed.isEmpty() && !parsed[0].Conditions.isEmpty())
    {
        auto conditions = parsed[0].Conditions;

        for (auto &c : conditions)
        {
            if (c.Flag == "Add Address" && c.LeftOperand.Size.isEmpty())
                c.LeftOperand.Size = "32-bit";
        }

        auto &lastCond = conditions.last();
        if (lastCond.LeftOperand.Type == "Mem")
        {
            lastCond.LeftOperand.Type = type;
            lastCond.LeftOperand.Size = ValidateMemorySize(size);
        }

        if (lastCond.Operator.isEmpty())
        {
            lastCond.Operator = "=";
            lastCond.RightOperand = Operand{ "Value", "", type.compare("Float", Qt::CaseInsensitive) == 0 ? "0.0" : "0" };
        }

        int gRow = groupsList->currentRow();
        auto &groups = _viewModel.GetActiveGroups();
        if (gRow >= 0 && gRow < groups.size())
        {
            auto &group = groups[gRow];

            // Insert after the last selected row (C# behaviour), not always at the end.
            int insertIdx = group.Conditions.size();
            auto selectedRows = conditionGrid->selectionModel()->selectedRows();
            if (!selectedRows.isEmpty())
            {
                int maxVisualRow = -1;
                for (const auto &idx : selectedRows)
                    if (idx.row() > maxVisualRow) maxVisualRow = idx.row();
                int mIdx = _conditionModel.GetModelIndexForVisualRow(maxVisualRow);
                if (mIdx != -1) insertIdx = mIdx + 1;
            }

            for (int i = 0; i < conditions.size(); ++i)
                group.Conditions.insert(insertIdx + i, conditions[i]);

            _viewModel.UpdateLogic(false);
            RefreshCurrentGridDisplay();

            auto &groupsAfter = _viewModel.GetActiveGroups();
            if (gRow >= groupsAfter.size()) return;
            auto &groupAfter = groupsAfter[gRow];

            // Select and focus the inserted chain so the user can see where it landed.
            QItemSelection selection;
            for (int i = 0; i < conditions.size(); ++i)
            {
                int idx = insertIdx + i;
                if (idx < 0 || idx >= groupAfter.Conditions.size()) continue;
                int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[idx]);
                if (vRow != -1)
                {
                    selection.select(_conditionModel.index(vRow, 0),
                                     _conditionModel.index(vRow, _conditionModel.columnCount() - 1));
                }
            }
            conditionGrid->selectionModel()->select(selection, QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);

            for (int i = 0; i < conditions.size(); ++i)
            {
                int idx = insertIdx + i;
                if (idx < 0 || idx >= groupAfter.Conditions.size()) continue;
                int vRow = _conditionModel.GetVisualRowForCondition(&groupAfter.Conditions[idx]);
                if (vRow != -1)
                {
                    conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, 0), QItemSelectionModel::NoUpdate);
                    conditionGrid->scrollTo(_conditionModel.index(vRow, 0));
                    break;
                }
            }

            if (_headerView != nullptr)
                _headerView->viewport()->update();
        }
    }
}

QString TriggerEditorWidget::ValidateMemorySize(const QString &inputSize)
{
    if (inputSize.isEmpty()) return "8-bit";
    if (inputSize.compare("Float", Qt::CaseInsensitive) == 0) return "Float";
    return RaLogicSyntax::MemorySizes.contains(inputSize) ? inputSize : "8-bit";
}

void TriggerEditorWidget::SelectCondition(const AchievementCondition *target)
{
    if (target == nullptr) return;

    const auto &groups = _viewModel.GetActiveGroups();
    int groupIndex = -1;
    const AchievementCondition *actualCond = nullptr;

    for (int i = 0; i < groups.size(); i++)
    {
        for (const auto &c : groups[i].Conditions)
        {
            if (c == *target) {
                groupIndex = i;
                actualCond = &c;
                break;
            }
        }
        if (groupIndex != -1) break;
    }

    // Fallback matching the C# reviewer: the validation snapshot may be stale after
    // the user edits the row, so match on ID + flag + memory address.
    if (groupIndex == -1)
    {
        for (int i = 0; i < groups.size(); i++)
        {
            for (const auto &c : groups[i].Conditions)
            {
                if (c.ID == target->ID && c.Flag == target->Flag &&
                    c.LeftOperand.Value == target->LeftOperand.Value)
                {
                    groupIndex = i;
                    actualCond = &c;
                    break;
                }
            }
            if (groupIndex != -1) break;
        }
    }

    if (groupIndex != -1)
    {
        groupsList->setCurrentRow(groupIndex);
        int vRow = _conditionModel.GetVisualRowForCondition(actualCond != nullptr ? actualCond : target);
        if (vRow != -1)
        {
            auto left = _conditionModel.index(vRow, 0);
            auto right = _conditionModel.index(vRow, _conditionModel.columnCount() - 1);
            conditionGrid->selectionModel()->select(QItemSelection(left, right), QItemSelectionModel::ClearAndSelect | QItemSelectionModel::Rows);
            conditionGrid->selectionModel()->setCurrentIndex(_conditionModel.index(vRow, 0), QItemSelectionModel::NoUpdate);
            conditionGrid->scrollTo(_conditionModel.index(vRow, 0));
        }
    }
}

void TriggerEditorWidget::onCreateGeneratorClicked()
{
    int row = groupsList->currentRow();
    auto &groups = _viewModel.GetActiveGroups();
    if (row < 0 || row >= groups.size()) return;
    auto *group = &groups[row];

    int groupIndex = row;

    QList<std::shared_ptr<GeneratorConfigDialog::GeneratorItemViewModel>> rootItems;
    rootItems.reserve(128);
    int autoStride = 0;
    int detectedCopies = 1;
    QMap<int64_t, int> sizeCounts;

    std::shared_ptr<GeneratorConfigDialog::GeneratorItemViewModel> activeChainParent = nullptr;
    std::shared_ptr<NoteNode> activeNoteNode = nullptr;

    auto GetOrCreateNode = [&](const QString &addr, bool isPointerBase,
                               std::shared_ptr<GeneratorConfigDialog::GeneratorItemViewModel> parent,
                               std::shared_ptr<NoteNode> contextNode)
        -> std::pair<std::shared_ptr<GeneratorConfigDialog::GeneratorItemViewModel>, std::shared_ptr<NoteNode>>
    {
        QString normAddr = LogicFormatter::NormalizeAddress(addr);
        QString desc = "";
        std::shared_ptr<NoteNode> currentNote = nullptr;
        bool foundAsOffset = false;

        if (contextNode != nullptr)
        {
            bool ok = false;
            QString clean = normAddr;
            clean.remove("0x", Qt::CaseInsensitive);
            int64_t offsetVal = clean.toLongLong(&ok, 16);
            if (ok)
            {
                for (const auto &n : contextNode->Children)
                {
                    QString nClean = n->Offset.trimmed();
                    nClean.remove("0x", Qt::CaseInsensitive);
                    nClean.remove('+');
                    nClean.remove('-');
                    nClean = nClean.trimmed();
                    int64_t nodeOff = nClean.toLongLong(&ok, 16);
                    if (n->Offset.contains('-')) nodeOff = -nodeOff;

                    if (ok && offsetVal >= nodeOff && offsetVal < nodeOff + n->Size)
                    {
                        desc = n->Description;
                        currentNote = n;
                        foundAsOffset = true;
                        break;
                    }
                }
            }
        }

        if (!foundAsOffset && (isPointerBase || parent == nullptr))
        {
            if (_notesLookup.contains(normAddr))
            {
                const auto &nodes = _notesLookup.value(normAddr);
                auto root = (!nodes.isEmpty()) ? nodes[0] : nullptr;
                if (root != nullptr)
                {
                    desc = root->Description;
                    if (desc.isEmpty() || desc.compare("Full Note", Qt::CaseInsensitive) == 0)
                    {
                        QString norm = root->Content;
                        norm.replace("\r\n", "\n").replace('\r', '\n');
                        QStringList parts = norm.split('\n', Qt::SkipEmptyParts);
                        if (!parts.isEmpty()) desc = parts.first();
                    }

                    if (autoStride == 0 && root->Size > 1) autoStride = static_cast<int>(root->Size);
                    if (root->Size > 1) sizeCounts[root->Size]++;

                    auto syntheticContext = std::make_shared<NoteNode>();
                    for (const auto &n : nodes)
                    {
                        if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                            syntheticContext->Children.append(n);
                    }
                    currentNote = syntheticContext;
                }
            }
            else
            {
                bool ok = false;
                QString clean = normAddr;
                clean.remove("0x", Qt::CaseInsensitive);
                int64_t targetVal = clean.toLongLong(&ok, 16);
                if (ok)
                {
                    for (const auto &match : _rangeCache)
                    {
                        if (targetVal >= match.Start && targetVal < match.End && match.Node != nullptr)
                        {
                            desc = match.Node->Description;
                            if (desc.isEmpty() || desc.compare("Full Note", Qt::CaseInsensitive) == 0)
                            {
                                QString norm = match.Node->Content;
                                norm.replace("\r\n", "\n").replace('\r', '\n');
                                QStringList parts = norm.split('\n', Qt::SkipEmptyParts);
                                if (!parts.isEmpty()) desc = parts.first();
                            }

                            if (autoStride == 0 && match.Node->Size > 1) autoStride = static_cast<int>(match.Node->Size);
                            if (match.Node->Size > 1) sizeCounts[match.Node->Size]++;
                            break;
                        }
                    }
                }
            }
        }

        // Check if node already exists under parent or rootItems to deduplicate
        auto &targetList = (parent != nullptr) ? parent->Children : rootItems;
        for (const auto &existing : targetList)
        {
            if (existing != nullptr && LogicFormatter::NormalizeAddress(existing->Address) == normAddr)
            {
                return { existing, currentNote };
            }
        }

        auto item = std::make_shared<GeneratorConfigDialog::GeneratorItemViewModel>();
        item->Address = addr.startsWith("0x", Qt::CaseInsensitive) ? addr : "0x" + addr;
        item->Description = desc;

        if (parent != nullptr)
        {
            parent->Children.append(item);
        }
        else
        {
            rootItems.append(item);
        }

        return { item, currentNote };
    };

    for (const auto &cond : group->Conditions)
    {
        if (cond.Flag == "Add Address")
        {
            if (RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type))
            {
                auto val = cond.LeftOperand.Value;
                auto result = GetOrCreateNode(val, true, activeChainParent, activeNoteNode);
                // Advance activeChainParent so each level in the pointer chain nests under the previous level
                activeChainParent = result.first;
                activeNoteNode = result.second;
            }
        }
        else
        {
            if (RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type))
                GetOrCreateNode(cond.LeftOperand.Value, false, activeChainParent, activeNoteNode);
            if (RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type))
                GetOrCreateNode(cond.RightOperand.Value, false, nullptr, nullptr);

            activeChainParent = nullptr;
            activeNoteNode = nullptr;
        }
    }

    ApplySmartPadding(rootItems);

    // Stride Heuristic
    if (autoStride > 0)
    {
        int count = 0;
        for (const auto &match : _rangeCache)
        {
            if (match.Node != nullptr && match.Node->Size == autoStride)
                count++;
        }
        if (count > 1) detectedCopies = count - 1;
    }

    if (rootItems.isEmpty())
    {
        QMessageBox::information(this, "Generator", "No memory addresses found in this group to generate from.");
        return;
    }

    GeneratorDefinition existingDef;
    bool hasExistingDef = false;
    if (_viewModel.GeneratorRules.contains(groupIndex))
    {
        existingDef = _viewModel.GeneratorRules.value(groupIndex);
        hasExistingDef = true;
    }

    if (!hasExistingDef && detectedCopies > 1)
    {
        existingDef.Copies = detectedCopies;
        hasExistingDef = true;
    }

    QSet<QString> preSelectedAddresses;
    // C# preselects from the visible grid rows, not the expanded hidden chains.
    QList<AchievementCondition> effectiveSelected;
    for (int vRow : GetSelectedVisualRows())
    {
        auto *c = _conditionModel.GetConditionAtRow(vRow);
        if (c != nullptr) effectiveSelected.append(*c);
    }
    for (const auto &cond : effectiveSelected)
    {
        if (RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type) && !cond.LeftOperand.Value.trimmed().isEmpty())
            preSelectedAddresses.insert(LogicFormatter::NormalizeAddress(cond.LeftOperand.Value));
        if (RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type) && !cond.RightOperand.Value.trimmed().isEmpty())
            preSelectedAddresses.insert(LogicFormatter::NormalizeAddress(cond.RightOperand.Value));
    }

    GeneratorConfigDialog form(rootItems, autoStride, hasExistingDef ? &existingDef : nullptr, &preSelectedAddresses, this);

    if (form.exec() == QDialog::Accepted)
    {
        _viewModel.PushUndoState();

        if (!form.IsArray && groupIndex < _viewModel.GetActiveGroups().size() - 1)
        {
            auto result = QMessageBox::warning(
                this,
                "Overwrite Confirmation",
                "There are existing Alt groups after this group. Do you want to delete them to make room for the generator?",
                QMessageBox::Yes | QMessageBox::No | QMessageBox::Cancel);

            if (result == QMessageBox::Cancel) return;

            if (result == QMessageBox::Yes)
            {
                auto &activeGroups = _viewModel.GetActiveGroups();
                while (activeGroups.size() > groupIndex + 1)
                {
                    activeGroups.removeLast();
                }

                for (auto it = _viewModel.GeneratorRules.begin(); it != _viewModel.GeneratorRules.end(); )
                {
                    if (it.key() > groupIndex)
                        it = _viewModel.GeneratorRules.erase(it);
                    else
                        ++it;
                }
            }
        }

        GeneratorDefinition def;
        def.Copies = form.Copies;
        def.IsArray = form.IsArray;

        if (def.IsArray)
        {
            def.TemplateConditionCount = group->Conditions.size();
        }

        for (const auto &addr : form.SelectedAddresses)
        {
            GeneratorRule rule;
            rule.SourceAddress = addr;
            rule.Stride = form.Stride;
            def.Rules.append(rule);
        }

        _viewModel.GeneratorRules[groupIndex] = def;
        _viewModel.UpdateLogic(false);
        RefreshGroupsListUI();
    }
}

void TriggerEditorWidget::ApplySmartPadding(QList<std::shared_ptr<GeneratorConfigDialog::GeneratorItemViewModel>> &roots)
{
    for (auto &root : roots)
    {
        if (root == nullptr || root->Children.isEmpty()) continue;
        int maxLen = 0;
        for (const auto &child : root->Children)
        {
            if (child == nullptr) continue;
            QString hex = child->Address.startsWith("0x", Qt::CaseInsensitive) ? child->Address.mid(2) : child->Address;
            if (hex.length() > maxLen) maxLen = hex.length();
        }
        if (maxLen > 1)
        {
            for (auto &child : root->Children)
            {
                if (child == nullptr) continue;
                QString hex = child->Address.startsWith("0x", Qt::CaseInsensitive) ? child->Address.mid(2) : child->Address;
                child->Address = "0x" + hex.rightJustified(maxLen, '0');
            }
        }
        ApplySmartPadding(root->Children);
    }
}

void TriggerEditorWidget::onCommitGeneratorClicked()
{
    int row = groupsList->currentRow();
    if (_viewModel.GeneratorRules.contains(row))
    {
        _viewModel.PushUndoState();
        _viewModel.GeneratorRules.remove(row);
        if (!_viewModel.AssetStrategy->isLeaderboard())
        {
            auto &groups = _viewModel.GetActiveGroups();
            for (int i = 1; i < groups.size(); i++)
                groups[i].GroupName = QString("Alt %1").arg(i);
        }
        _viewModel.UpdateLogic(false);
        SetStatus("Generator baked.");
    }
}

void TriggerEditorWidget::onMultiRegionClicked()
{
    MultiRegionDialog form(_rawNotes, this);
    if (form.exec() == QDialog::Accepted)
    {
        emit MultiRegionLogicRequested(&form);
    }
}

void TriggerEditorWidget::onLogicOptimizerClicked()
{
    emit LogicOptimizationRequested();
}

void TriggerEditorWidget::onBatchMultiplyNotesClicked()
{
    if (_rawNotes.isEmpty()) return;
    BatchNoteMultiplierDialog form(_rawNotes, this);
    if (form.exec() == QDialog::Accepted)
    {
        emit BatchMultiplyNotesRequested(&form);
    }
}

void TriggerEditorWidget::onSaveAsSnippetClicked()
{
    QString logic = GetSelectedLogicString();
    if (logic.isEmpty())
    {
        QMessageBox::information(this, "No Selection", "Please select at least one row to save as a snippet.");
        return;
    }

    bool ok = false;
    QString name = QInputDialog::getText(this, "Save Snippet", "Snippet Name:", QLineEdit::Normal, "", &ok);
    if (ok && !name.trimmed().isEmpty())
    {
        SnippetManager::SaveSnippet(name.trimmed(), logic);
        SetStatus(QString("Snippet '%1' saved.").arg(name.trimmed()));
    }
}

void TriggerEditorWidget::onInsertSnippetClicked()
{
    SnippetSelectDialog form(this);
    if (form.exec() == QDialog::Accepted && form.SelectedSnippet.has_value())
    {
        PasteLogic(form.SelectedSnippet->Logic);
    }
}

} // namespace RALE
