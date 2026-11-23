#include "SuggestedFixDialog.h"

#include "../../Common/AppSettings.h"
#include "../../Definitions/RaLogicSyntax.h"
#include "../../Services/AssetStrategies.h"
#include "../../Utilities/LogicFormatter.h"
#include "../../Utilities/TitleCaseAnalyzer.h"
#include "../Styles/ThemeManager.h"

#include <QGroupBox>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QListWidget>
#include <QPainter>
#include <QStyledItemDelegate>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

namespace {

bool IsCombiningFlag(const QString& flag) {
    return flag == "Add Source" || flag == "Sub Source" || flag == "Add Address" || flag == "AndNext" || flag == "OrNext";
}

bool IsTerminatingFlag(const QString& flag) { return !IsCombiningFlag(flag); }

bool IsComparisonOp(const QString& op) {
    return op == "=" || op == "!=" || op == ">" || op == ">=" || op == "<" || op == "<=";
}

QString InvertComparisonOp(const QString& op) {
    if (op == "=")
        return "!=";
    if (op == "!=")
        return "=";
    if (op == ">")
        return "<=";
    if (op == "<")
        return ">=";
    if (op == ">=")
        return "<";
    if (op == "<=")
        return ">";
    return op;
}

class SuggestedFixTableDelegate : public QStyledItemDelegate {
  public:
    using QStyledItemDelegate::QStyledItemDelegate;

    void paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const override {
        QStyleOptionViewItem opt = option;
        initStyleOption(&opt, index);

        const bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
        QColor bgColor = isDark ? QColor(32, 32, 34) : QColor(255, 255, 255);
        const QVariant bgVar = index.data(Qt::BackgroundRole);
        if (bgVar.isValid())
            bgColor = bgVar.value<QColor>();

        painter->fillRect(opt.rect, bgColor);

        const QColor gridColor = isDark ? QColor(46, 46, 50) : QColor(200, 200, 200);
        painter->save();
        painter->setPen(QPen(gridColor, 1));
        painter->drawLine(opt.rect.right(), opt.rect.top(), opt.rect.right(), opt.rect.bottom());
        painter->drawLine(opt.rect.left(), opt.rect.bottom(), opt.rect.right(), opt.rect.bottom());
        painter->restore();

        QColor textColor = opt.palette.text().color();
        const QVariant fgVar = index.data(Qt::ForegroundRole);
        if (fgVar.isValid())
            textColor = fgVar.value<QColor>();

        painter->save();
        painter->setPen(textColor);
        painter->setFont(opt.font);
        int alignFlags = Qt::AlignVCenter | Qt::TextSingleLine;
        if (index.column() == 5 || index.column() == 9)
            alignFlags |= Qt::AlignCenter;
        else
            alignFlags |= Qt::AlignLeft;
        painter->setClipRect(opt.rect.adjusted(1, 1, -1, -1));
        painter->drawText(opt.rect.adjusted(4, 0, -4, 0), alignFlags, opt.text);
        painter->restore();
    }
};

} // namespace

SuggestedFixDialog::SuggestedFixDialog(const ValidationIssue& issue, const std::shared_ptr<IAssetItem>& asset,
                                       const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                       const QList<ConditionFormatter::RangeEntry>* rangeCache, bool showDecimal, bool showAliases,
                                       QWidget* parent)
    : QDialog(parent), _issue(issue), _asset(asset), _notesLookup(notesLookup), _rangeCache(rangeCache), _showDecimal(showDecimal),
      _showAliases(showAliases) {
    setWindowTitle(QString("Suggested Fix - %1").arg(issue.Title));
    setModal(true);
    setWindowFlags(windowFlags() & ~Qt::WindowContextHelpButtonHint);
    resize(860, 440);
    setMinimumSize(640, 320);

    BuildFixPreview();
    InitializeUI();
    loadPositionSettings();
}

void SuggestedFixDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("SuggestedFixDialog", p, s)) {
        move(p);
        if (s.isValid()) {
            resize(std::max(s.width(), 640), std::max(s.height(), 320));
        }
    }
}

void SuggestedFixDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("SuggestedFixDialog", pos(), size()); }

void SuggestedFixDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

QString SuggestedFixDialog::ResultingTitle() const {
    if (_issue.FixType == SuggestedFixType::ApplyTitleCase ||
        (_issue.FixType == SuggestedFixType::NormalizeSmartQuotes && _issue.TargetField == "title")) {
        return _txtAfterText != nullptr ? _txtAfterText->text() : _afterText;
    }
    return _asset != nullptr ? _asset->Title : "";
}

QString SuggestedFixDialog::ResultingDescription() const {
    if (_issue.FixType == SuggestedFixType::NormalizeSmartQuotes && _issue.TargetField == "desc") {
        return _txtAfterText != nullptr ? _txtAfterText->text() : _afterText;
    }
    return _asset != nullptr ? _asset->Description : "";
}

void SuggestedFixDialog::BuildFixPreview() {
    _hasValidFix = false;
    if (_asset == nullptr)
        return;

    if (_issue.FixType == SuggestedFixType::ApplyTitleCase) {
        _beforeText = _asset->Title;
        const auto analysis = TitleCaseAnalyzer::Analyze(_asset->Title);
        _afterText = !analysis.Changes.isEmpty() ? analysis.Suggestion : _issue.FixPreviewText;
        _hasValidFix = (!_afterText.isEmpty() && _afterText != _beforeText);
        return;
    }

    if (_issue.FixType == SuggestedFixType::NormalizeSmartQuotes) {
        _beforeText = (_issue.TargetField == "desc") ? _asset->Description : _asset->Title;
        _afterText = _beforeText;
        _afterText.replace(QChar(0x2018), '\'').replace(QChar(0x2019), '\'').replace(QChar(0x201C), '"').replace(QChar(0x201D), '"');
        _hasValidFix = (_afterText != _beforeText);
        return;
    }

    // Logic fixes
    if (!_issue.Target.has_value())
        return;

    std::unique_ptr<IAssetStrategy> strategy;
    if (_asset->isLeaderboard())
        strategy = std::make_unique<LeaderboardStrategy>();
    else
        strategy = std::make_unique<AchievementStrategy>();

    auto segments = strategy->ParseLogic(_asset->Logic);

    QString targetSeg = _issue.SegmentName;
    if (targetSeg.isEmpty() || !segments.contains(targetSeg))
        targetSeg = segments.keys().value(0, "Core");

    if (!segments.contains(targetSeg))
        return;

    auto& groups = segments[targetSeg];
    int targetGroupIdx = _issue.GroupIndex;
    int targetCondIdx = -1;

    const auto& targetCond = _issue.Target.value();

    auto findCondInGroup = [&targetCond](const AchievementConditionGroup& grp) -> int {
        for (int i = 0; i < grp.Conditions.size(); ++i) {
            if (grp.Conditions[i] == targetCond)
                return i;
        }
        for (int i = 0; i < grp.Conditions.size(); ++i) {
            const auto& c = grp.Conditions[i];
            if (c.ID == targetCond.ID && c.Flag == targetCond.Flag && c.LeftOperand.Value == targetCond.LeftOperand.Value)
                return i;
        }
        return -1;
    };

    if (targetGroupIdx >= 0 && targetGroupIdx < groups.size()) {
        targetCondIdx = findCondInGroup(groups[targetGroupIdx]);
    }
    if (targetCondIdx == -1) {
        for (int gi = 0; gi < groups.size(); ++gi) {
            targetCondIdx = findCondInGroup(groups[gi]);
            if (targetCondIdx != -1) {
                targetGroupIdx = gi;
                break;
            }
        }
    }

    if (targetGroupIdx < 0 || targetGroupIdx >= groups.size() || targetCondIdx < 0)
        return;

    AchievementConditionGroup& group = groups[targetGroupIdx];
    _beforeGroup = group;
    _targetLocationLabel = _asset->isLeaderboard() ? QString("%1 / %2").arg(targetSeg, group.GroupName) : group.GroupName;

    if (_issue.FixType == SuggestedFixType::InvertChain) {
        int startIdx = targetCondIdx;
        while (startIdx > 0 && !IsTerminatingFlag(group.Conditions[startIdx - 1].Flag)) {
            startIdx--;
        }
        _chainStartIndex = startIdx;
        _chainEndIndex = targetCondIdx;

        auto& target = group.Conditions[targetCondIdx];
        target.Flag = "";
        target.RequiredHits = 0;
        if (IsComparisonOp(target.Operator))
            target.Operator = InvertComparisonOp(target.Operator);

        for (int i = startIdx; i < targetCondIdx; ++i) {
            auto& c = group.Conditions[i];
            if (c.Flag == "AndNext") {
                c.Flag = "OrNext";
                if (IsComparisonOp(c.Operator))
                    c.Operator = InvertComparisonOp(c.Operator);
            } else if (c.Flag == "OrNext") {
                c.Flag = "AndNext";
                if (IsComparisonOp(c.Operator))
                    c.Operator = InvertComparisonOp(c.Operator);
            }
        }

        _afterGroup = group;
        _resultingLogic = strategy->BuildLogic(segments);
        _hasValidFix = (_resultingLogic != _asset->Logic);
    } else if (_issue.FixType == SuggestedFixType::ClearResetHitCount) {
        _chainStartIndex = targetCondIdx;
        _chainEndIndex = targetCondIdx;

        group.Conditions[targetCondIdx].RequiredHits = 0;

        _afterGroup = group;
        _resultingLogic = strategy->BuildLogic(segments);
        _hasValidFix = (_resultingLogic != _asset->Logic);
    } else if (_issue.FixType == SuggestedFixType::RemoveUselessAndNext) {
        int startIdx = targetCondIdx;
        while (startIdx > 0 && !IsTerminatingFlag(group.Conditions[startIdx - 1].Flag)) {
            startIdx--;
        }
        int endIdx = targetCondIdx;
        while (endIdx < group.Conditions.size() - 1 && !IsTerminatingFlag(group.Conditions[endIdx].Flag)) {
            endIdx++;
        }

        _chainStartIndex = startIdx;
        _chainEndIndex = endIdx;

        for (int i = startIdx; i <= endIdx; ++i) {
            if (group.Conditions[i].Flag == "AndNext")
                group.Conditions[i].Flag = "";
        }

        _afterGroup = group;
        _resultingLogic = strategy->BuildLogic(segments);
        _hasValidFix = (_resultingLogic != _asset->Logic);
    }
}

void SuggestedFixDialog::InitializeUI() {
    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 12, 12, 12);
    mainLayout->setSpacing(10);

    const QString assetLabel = _asset != nullptr
                                   ? (_asset->isLeaderboard() ? QString("L%1 - %2").arg(_asset->ID).arg(_asset->Title)
                                                              : QString("%1 - %2").arg(_asset->ID).arg(_asset->Title))
                                   : "Asset";
    const QString locSuffix = _targetLocationLabel.isEmpty() ? QString() : QString(" [%1]").arg(_targetLocationLabel);

    _lblSummary = new QLabel(QString("<b>%1</b> &mdash; %2%3").arg(_issue.Title.toHtmlEscaped(), assetLabel.toHtmlEscaped(), locSuffix.toHtmlEscaped()), this);
    mainLayout->addWidget(_lblSummary);

    QString explanation;
    switch (_issue.FixType) {
    case SuggestedFixType::InvertChain:
        explanation = "Converts the PauseIf/ResetIf condition into an active positive requirement by removing the flag and inverting the "
                      "comparison operator (and swapping any chained AndNext/OrNext flags).";
        break;
    case SuggestedFixType::ClearResetHitCount:
        explanation = "Removes the redundant hitcount of 1 from the ResetIf requirement.";
        break;
    case SuggestedFixType::RemoveUselessAndNext:
        explanation = "Removes redundant AndNext flags from the requirement chain (AND is the default behavior).";
        break;
    case SuggestedFixType::ApplyTitleCase:
        explanation = "Applies Chicago Manual of Style (CMOS) title capitalization to the asset title.";
        break;
    case SuggestedFixType::NormalizeSmartQuotes:
        explanation = QString("Replaces typographic smart quotes in the %1 with standard ASCII quotes.")
                          .arg(_issue.TargetField == "desc" ? "description" : "title");
        break;
    default:
        explanation = "No automated fix is available for this issue.";
        break;
    }

    _lblExplanation = new QLabel(explanation, this);
    _lblExplanation->setWordWrap(true);
    _lblExplanation->setObjectName("subTextDescLabel");
    mainLayout->addWidget(_lblExplanation);

    const bool isLogicFix = (_issue.FixType == SuggestedFixType::InvertChain || _issue.FixType == SuggestedFixType::ClearResetHitCount ||
                             _issue.FixType == SuggestedFixType::RemoveUselessAndNext);

    if (isLogicFix) {
        auto configureTable = [this](QTableWidget* tbl) {
            tbl->setColumnCount(10);
            tbl->setHorizontalHeaderLabels({"ID", "Flag", "Type", "Size", "Memory", "Cmp", "Type", "Size", "Mem/Val", "Hits"});
            tbl->setItemDelegate(new SuggestedFixTableDelegate(tbl));
            tbl->verticalHeader()->setVisible(false);
            tbl->verticalHeader()->setDefaultSectionSize(24);
            tbl->horizontalHeader()->setFixedHeight(24);
            tbl->setShowGrid(false);
            tbl->setSelectionMode(QAbstractItemView::NoSelection);
            tbl->setEditTriggers(QAbstractItemView::NoEditTriggers);
            tbl->setFocusPolicy(Qt::NoFocus);

            tbl->setColumnWidth(0, 35);
            tbl->setColumnWidth(1, 85);
            tbl->setColumnWidth(2, 50);
            tbl->setColumnWidth(3, 60);
            tbl->horizontalHeader()->setSectionResizeMode(4, QHeaderView::Stretch);
            tbl->setColumnWidth(5, 40);
            tbl->setColumnWidth(6, 50);
            tbl->setColumnWidth(7, 60);
            tbl->horizontalHeader()->setSectionResizeMode(8, QHeaderView::Stretch);
            tbl->setColumnWidth(9, 45);
        };

        auto* grpBefore = new QGroupBox("Before (Current Logic)", this);
        auto* beforeLayout = new QVBoxLayout(grpBefore);
        beforeLayout->setContentsMargins(6, 10, 6, 6);
        _tblBefore = new QTableWidget(grpBefore);
        configureTable(_tblBefore);
        beforeLayout->addWidget(_tblBefore);
        mainLayout->addWidget(grpBefore, 1);

        auto* grpAfter = new QGroupBox("After (Suggested Fix)", this);
        auto* afterLayout = new QVBoxLayout(grpAfter);
        afterLayout->setContentsMargins(6, 10, 6, 6);
        _tblAfter = new QTableWidget(grpAfter);
        configureTable(_tblAfter);
        afterLayout->addWidget(_tblAfter);
        mainLayout->addWidget(grpAfter, 1);

        PopulateLogicTables();
    } else if (_issue.FixType == SuggestedFixType::ApplyTitleCase || _issue.FixType == SuggestedFixType::NormalizeSmartQuotes) {
        auto* grpBefore = new QGroupBox("Before", this);
        auto* beforeLayout = new QVBoxLayout(grpBefore);
        _txtBeforeText = new QLineEdit(_beforeText, grpBefore);
        _txtBeforeText->setReadOnly(true);
        beforeLayout->addWidget(_txtBeforeText);
        mainLayout->addWidget(grpBefore);

        auto* grpAfter = new QGroupBox("After (Editable)", this);
        auto* afterLayout = new QVBoxLayout(grpAfter);
        _txtAfterText = new QLineEdit(_afterText, grpAfter);
        afterLayout->addWidget(_txtAfterText);
        mainLayout->addWidget(grpAfter);

        if (!_issue.FixDetails.isEmpty()) {
            auto* grpChanges = new QGroupBox("Word Capitalization Details", this);
            auto* changesLayout = new QVBoxLayout(grpChanges);
            auto* listChanges = new QListWidget(grpChanges);
            for (const QString& detail : _issue.FixDetails)
                listChanges->addItem(detail);
            changesLayout->addWidget(listChanges);
            mainLayout->addWidget(grpChanges, 1);
        } else {
            mainLayout->addStretch(1);
        }
    } else {
        mainLayout->addStretch(1);
    }

    auto* bottomRow = new QHBoxLayout();
    bottomRow->setSpacing(8);

    if (isLogicFix) {
        _chkAliases = new QCheckBox("Show Aliases", this);
        _chkAliases->setChecked(_showAliases);
        connect(_chkAliases, &QCheckBox::toggled, this, &SuggestedFixDialog::onDisplayOptionChanged);
        bottomRow->addWidget(_chkAliases);

        _chkDecimal = new QCheckBox("Show Decimal", this);
        _chkDecimal->setChecked(_showDecimal);
        connect(_chkDecimal, &QCheckBox::toggled, this, &SuggestedFixDialog::onDisplayOptionChanged);
        bottomRow->addWidget(_chkDecimal);
    }

    bottomRow->addStretch();

    _btnApply = new QPushButton("Apply Fix", this);
    _btnApply->setMinimumWidth(95);
    _btnApply->setFixedHeight(26);
    _btnApply->setDefault(true);
    _btnApply->setEnabled(_hasValidFix);
    connect(_btnApply, &QPushButton::clicked, this, &SuggestedFixDialog::onApplyClicked);
    bottomRow->addWidget(_btnApply);

    _btnCancel = new QPushButton("Cancel", this);
    _btnCancel->setObjectName("cancelButton");
    _btnCancel->setMinimumWidth(75);
    _btnCancel->setFixedHeight(26);
    connect(_btnCancel, &QPushButton::clicked, this, &QDialog::reject);
    bottomRow->addWidget(_btnCancel);

    mainLayout->addLayout(bottomRow);
}

void SuggestedFixDialog::onDisplayOptionChanged() {
    if (_chkAliases != nullptr)
        _showAliases = _chkAliases->isChecked();
    if (_chkDecimal != nullptr)
        _showDecimal = _chkDecimal->isChecked();
    PopulateLogicTables();
}

void SuggestedFixDialog::PopulateLogicTables() {
    if (_tblBefore == nullptr || _tblAfter == nullptr)
        return;
    PopulateSingleLogicTable(_tblBefore, _beforeGroup, _afterGroup);
    PopulateSingleLogicTable(_tblAfter, _afterGroup, _beforeGroup);
}

void SuggestedFixDialog::PopulateSingleLogicTable(QTableWidget* table, const AchievementConditionGroup& group,
                                                  const AchievementConditionGroup& compareGroup) const {
    table->setRowCount(0);
    if (_chainStartIndex < 0 || _chainEndIndex < _chainStartIndex || _chainEndIndex >= group.Conditions.size())
        return;

    const bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
    const bool isBeforeTable = (table == _tblBefore);
    const QColor diffBg = isBeforeTable ? (isDark ? QColor(90, 35, 35) : QColor(255, 220, 220))
                                        : (isDark ? QColor(30, 80, 40) : QColor(215, 250, 215));
    const QColor mutedFg = isDark ? QColor(145, 145, 150) : QColor(120, 120, 120);

    auto cellsFor = [this](const AchievementConditionGroup& grp, int modelIdx) -> QStringList {
        const auto& cond = grp.Conditions.at(modelIdx);
        const QString rightSizeRef = LogicFormatter::GetRightOperandSizeReference(cond);
        return {
            QString::number(modelIdx + 1),
            cond.Flag,
            cond.LeftOperand.Type,
            cond.LeftOperand.Size,
            ConditionFormatter::FormatOperandDisplay(cond.LeftOperand, _showDecimal, _showAliases, _notesLookup, _rangeCache, &grp,
                                                     modelIdx),
            cond.Operator,
            cond.RightOperand.Type,
            cond.RightOperand.Size,
            ConditionFormatter::FormatOperandDisplay(cond.RightOperand, _showDecimal, _showAliases, _notesLookup, _rangeCache, &grp,
                                                     modelIdx, rightSizeRef),
            (!RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag) && cond.RequiredHits > 0) ? QString::number(cond.RequiredHits) : ""};
    };

    for (int idx = _chainStartIndex; idx <= _chainEndIndex; ++idx) {
        const int row = table->rowCount();
        table->insertRow(row);

        const QStringList vals = cellsFor(group, idx);
        const QStringList otherVals = (idx < compareGroup.Conditions.size()) ? cellsFor(compareGroup, idx) : vals;
        const bool isAddAddress = (group.Conditions[idx].Flag == "Add Address");

        for (int c = 0; c < 10; ++c) {
            QString displayVal = vals[c];
            const bool changed = (vals[c] != otherVals[c]);
            if (c == 1 && displayVal.isEmpty() && changed)
                displayVal = "(No Flag)";

            auto* item = new QTableWidgetItem(displayVal);
            if (changed) {
                item->setBackground(diffBg);
                QFont f = item->font();
                f.setBold(true);
                item->setFont(f);
            } else if (isAddAddress) {
                item->setForeground(mutedFg);
            }
            table->setItem(row, c, item);
        }
    }
}

void SuggestedFixDialog::onApplyClicked() { accept(); }

} // namespace RALE
