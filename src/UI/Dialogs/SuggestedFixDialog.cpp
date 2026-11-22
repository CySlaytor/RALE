#include "SuggestedFixDialog.h"

#include "../../Common/AppSettings.h"

#include <QHBoxLayout>
#include <QHeaderView>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

SuggestedFixDialog::SuggestedFixDialog(const QString& issueTitle, const QString& issueDescription,
                                       const AchievementConditionGroup& sourceGroup, const SuggestedLogicFix& fix, bool showDecimal,
                                       bool showAliases, bool collapseChains,
                                       const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                       const QList<ConditionFormatter::RangeEntry>* rangeCache, QWidget* parent)
    : QDialog(parent), _showDecimal(showDecimal), _showAliases(showAliases), _collapseChains(collapseChains), _notesLookup(notesLookup),
      _rangeCache(rangeCache) {
    // Include any preceding AddAddress chain context so pointer aliases resolve cleanly in the preview tables
    int contextStart = fix.StartConditionIndex;
    while (contextStart > 0 && sourceGroup.Conditions[contextStart - 1].Flag == "Add Address") {
        --contextStart;
    }

    _beforeGroup.GroupName = sourceGroup.GroupName;
    _afterGroup.GroupName = sourceGroup.GroupName;

    for (int i = contextStart; i < fix.StartConditionIndex + fix.ReplaceCount && i < sourceGroup.Conditions.size(); ++i) {
        _beforeGroup.Conditions.append(sourceGroup.Conditions[i].Clone());
    }

    for (int i = contextStart; i < fix.StartConditionIndex && i < sourceGroup.Conditions.size(); ++i) {
        _afterGroup.Conditions.append(sourceGroup.Conditions[i].Clone());
    }
    for (const auto& rep : fix.ReplacementConditions) {
        _afterGroup.Conditions.append(rep.Clone());
    }

    InitializeUI(issueTitle, issueDescription);
    RefreshTables();
    loadPositionSettings();
}

void SuggestedFixDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("SuggestedFixDialog", p, s)) {
        move(p);
        if (s.isValid()) {
            resize(std::max(s.width(), 640), std::max(s.height(), 360));
        }
    }
}

void SuggestedFixDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("SuggestedFixDialog", pos(), size()); }

void SuggestedFixDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

void SuggestedFixDialog::InitializeUI(const QString& issueTitle, const QString& issueDescription) {
    setWindowTitle(QString("Suggested Fix - %1").arg(issueTitle));
    setModal(true);
    resize(820, 440);
    setMinimumSize(640, 360);
    setWindowFlags(windowFlags() & ~Qt::WindowContextHelpButtonHint);

    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 10, 12, 10);
    mainLayout->setSpacing(8);

    auto* lblTitle = new QLabel(issueTitle, this);
    QFont tf = lblTitle->font();
    tf.setBold(true);
    tf.setPointSize(10);
    lblTitle->setFont(tf);
    mainLayout->addWidget(lblTitle);

    auto* lblDesc = new QLabel(issueDescription, this);
    lblDesc->setWordWrap(true);
    mainLayout->addWidget(lblDesc);

    auto* lblBefore = new QLabel("Current Logic:", this);
    QFont bf = lblBefore->font();
    bf.setBold(true);
    lblBefore->setFont(bf);
    mainLayout->addWidget(lblBefore);

    _beforeGrid = new QTableView(this);
    _beforeHeader = new ConditionHeaderView(Qt::Vertical, _beforeGrid);
    SetupPreviewTable(_beforeGrid, &_beforeModel, _beforeHeader);
    mainLayout->addWidget(_beforeGrid, 1);

    auto* lblAfter = new QLabel("Suggested Fix:", this);
    lblAfter->setFont(bf);
    mainLayout->addWidget(lblAfter);

    _afterGrid = new QTableView(this);
    _afterHeader = new ConditionHeaderView(Qt::Vertical, _afterGrid);
    SetupPreviewTable(_afterGrid, &_afterModel, _afterHeader);
    mainLayout->addWidget(_afterGrid, 1);

    auto* bottomRow = new QHBoxLayout();
    bottomRow->setSpacing(10);

    _chkCollapseChains = new QCheckBox("Collapse Chains", this);
    _chkCollapseChains->setChecked(_collapseChains);
    connect(_chkCollapseChains, &QCheckBox::toggled, this, [this](bool checked) {
        _collapseChains = checked;
        RefreshTables();
    });
    bottomRow->addWidget(_chkCollapseChains);

    _chkShowAliases = new QCheckBox("Show Aliases", this);
    _chkShowAliases->setChecked(_showAliases);
    connect(_chkShowAliases, &QCheckBox::toggled, this, [this](bool checked) {
        _showAliases = checked;
        RefreshTables();
    });
    bottomRow->addWidget(_chkShowAliases);

    _chkShowDecimal = new QCheckBox("Show Decimal", this);
    _chkShowDecimal->setChecked(_showDecimal);
    connect(_chkShowDecimal, &QCheckBox::toggled, this, [this](bool checked) {
        _showDecimal = checked;
        RefreshTables();
    });
    bottomRow->addWidget(_chkShowDecimal);

    bottomRow->addStretch();

    _btnApply = new QPushButton("Apply Fix", this);
    _btnApply->setMinimumWidth(85);
    _btnApply->setFixedHeight(25);
    _btnApply->setDefault(true);
    connect(_btnApply, &QPushButton::clicked, this, &QDialog::accept);
    bottomRow->addWidget(_btnApply);

    _btnCancel = new QPushButton("Cancel", this);
    _btnCancel->setMinimumWidth(75);
    _btnCancel->setFixedHeight(25);
    connect(_btnCancel, &QPushButton::clicked, this, &QDialog::reject);
    bottomRow->addWidget(_btnCancel);

    mainLayout->addLayout(bottomRow);
}

void SuggestedFixDialog::SetupPreviewTable(QTableView* table, ConditionTableModel* model, ConditionHeaderView* header) {
    table->setModel(model);
    header->setConditionModel(model);
    header->setTableView(table);
    header->setFixedWidth(25);
    table->setVerticalHeader(header);

    table->setShowGrid(false);
    table->verticalHeader()->setDefaultSectionSize(26);
    table->verticalHeader()->setSectionResizeMode(QHeaderView::Fixed);
    table->horizontalHeader()->setFixedHeight(24);
    table->horizontalHeader()->setSectionResizeMode(QHeaderView::Interactive);

    table->setColumnWidth(ConditionTableModel::ColID, 35);
    table->setColumnWidth(ConditionTableModel::ColFlag, 80);
    table->setColumnWidth(ConditionTableModel::ColLType, 50);
    table->setColumnWidth(ConditionTableModel::ColLSize, 60);
    table->horizontalHeader()->setSectionResizeMode(ConditionTableModel::ColLValue, QHeaderView::Stretch);
    table->setColumnWidth(ConditionTableModel::ColCmp, 40);
    table->setColumnWidth(ConditionTableModel::ColRType, 50);
    table->setColumnWidth(ConditionTableModel::ColRSize, 60);
    table->horizontalHeader()->setSectionResizeMode(ConditionTableModel::ColRValue, QHeaderView::Stretch);
    table->setColumnWidth(ConditionTableModel::ColHits, 50);

    table->setItemDelegate(new ConditionTableDelegate(table));
    table->setSelectionBehavior(QAbstractItemView::SelectRows);
    table->setSelectionMode(QAbstractItemView::NoSelection);
    table->setFocusPolicy(Qt::NoFocus);
}

void SuggestedFixDialog::RefreshTables() {
    _beforeModel.SetGroup(&_beforeGroup, true, _showDecimal, _showAliases, _collapseChains, _notesLookup, _rangeCache, nullptr);
    _afterModel.SetGroup(&_afterGroup, true, _showDecimal, _showAliases, _collapseChains, _notesLookup, _rangeCache, nullptr);

    if (_beforeHeader != nullptr)
        _beforeHeader->viewport()->update();
    if (_afterHeader != nullptr)
        _afterHeader->viewport()->update();
}

} // namespace RALE
