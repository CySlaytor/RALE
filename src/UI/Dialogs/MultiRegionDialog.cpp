#include "MultiRegionDialog.h"

#include "../../Common/AppSettings.h"
#include "../../Definitions/RegionDefinitions.h"
#include "../../Parsers/AchievementParser.h"
#include "../../Utilities/LogicFormatter.h"
#include "../Styles/ThemeManager.h"

#include <QClipboard>
#include <QGroupBox>
#include <QGuiApplication>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QLabel>
#include <QMessageBox>
#include <QPainter>
#include <QRegularExpression>
#include <QSet>
#include <QStyledItemDelegate>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

// Delegate ensuring crisp 1px thin borders, pure white background, and standard Windows selection highlight
class MultiRegionTableDelegate : public QStyledItemDelegate {
  public:
    using QStyledItemDelegate::QStyledItemDelegate;

    void paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const override {
        QStyleOptionViewItem opt = option;
        initStyleOption(&opt, index);

        bool isSelected = (opt.state & QStyle::State_Selected);
        bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);

        QColor baseColor = isDark ? QColor(26, 26, 28) : QColor(255, 255, 255);
        QColor bgColor = isSelected ? opt.palette.highlight().color() : baseColor;

        painter->fillRect(opt.rect, bgColor);

        // Clean 1px single thin border matching DataGridView
        QColor gridColor = isDark ? QColor(56, 56, 60) : QColor(208, 208, 208);
        painter->save();
        painter->setPen(QPen(gridColor, 1));
        painter->drawLine(opt.rect.right(), opt.rect.top(), opt.rect.right(), opt.rect.bottom());
        painter->drawLine(opt.rect.left(), opt.rect.bottom(), opt.rect.right(), opt.rect.bottom());
        painter->restore();

        QColor textColor = isSelected ? opt.palette.highlightedText().color() : opt.palette.text().color();
        painter->save();
        painter->setPen(textColor);
        painter->setFont(opt.font);
        QRect textRect = opt.rect.adjusted(4, 0, -4, 0);
        painter->drawText(textRect, Qt::AlignLeft | Qt::AlignVCenter, opt.text);
        painter->restore();
    }
};

MultiRegionDialog::MultiRegionDialog(const QList<CodeNote>& notes, QWidget* parent) : QDialog(parent), _allNotes(notes) {
    InitializeUI();
    InitializeDynamicRegions();
    loadPositionSettings();
}

void MultiRegionDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("MultiRegionDialog", p, s)) {
        move(p);
        if (s.isValid()) {
            resize(std::max(s.width(), 550), std::max(s.height(), 400));
        }
    }
}

void MultiRegionDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("MultiRegionDialog", pos(), size()); }

void MultiRegionDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

bool MultiRegionDialog::ShouldUpdateRichPresence() const { return chkUpdateRP->isChecked(); }
bool MultiRegionDialog::UsePauseLocks() const { return chkUsePauseLocks->isChecked(); }

void MultiRegionDialog::InitializeUI() {
    setWindowTitle("Multi-Region Logic Generator");
    resize(800, 600);

    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 9, 12, 10);
    mainLayout->setSpacing(6);

    auto* lblInstruction = new QLabel("Detects regions from notes. Define specific locks for each region in the tabs below.", this);
    lblInstruction->setObjectName("lblInstruction");
    mainLayout->addWidget(lblInstruction);

    auto* lblMapping = new QLabel("Detected Address Mappings (from Code Notes)", this);
    QFont hf = lblMapping->font();
    hf.setBold(true);
    lblMapping->setFont(hf);
    mainLayout->addWidget(lblMapping);

    dgvMapping = new QTableWidget(this);
    dgvMapping->setObjectName("dgvMapping");
    dgvMapping->setItemDelegate(new MultiRegionTableDelegate(dgvMapping));
    dgvMapping->setColumnCount(4);
    dgvMapping->setHorizontalHeaderLabels({"Base Addr (Active)", "Mapped Addr", "Region", "Note Description"});
    dgvMapping->verticalHeader()->setVisible(false);
    dgvMapping->verticalHeader()->setDefaultSectionSize(22);
    dgvMapping->horizontalHeader()->setFixedHeight(24);
    dgvMapping->setShowGrid(false);
    dgvMapping->setSelectionBehavior(QAbstractItemView::SelectRows);
    dgvMapping->setEditTriggers(QAbstractItemView::NoEditTriggers);
    dgvMapping->setColumnWidth(0, 120);
    dgvMapping->setColumnWidth(1, 120);
    dgvMapping->setColumnWidth(2, 80);
    dgvMapping->horizontalHeader()->setSectionResizeMode(3, QHeaderView::Stretch);
    mainLayout->addWidget(dgvMapping, 1);

    tabControlRegions = new QTabWidget(this);
    mainLayout->addWidget(tabControlRegions, 1);

    // Bottom Bar
    auto* bottomRow = new QHBoxLayout();
    btnHelp = new QPushButton("Help", this);
    btnHelp->setFixedSize(75, 23);
    connect(btnHelp, &QPushButton::clicked, this, &MultiRegionDialog::onBtnHelp);
    bottomRow->addWidget(btnHelp);

    chkUsePauseLocks = new QCheckBox("Generate as PauseLocks", this);
    chkUsePauseLocks->setChecked(AppSettings::Current.MultiRegion_UsePauseLocks);
    connect(chkUsePauseLocks, &QCheckBox::toggled, this, [](bool checked) {
        AppSettings::Current.MultiRegion_UsePauseLocks = checked;
        AppSettings::Save();
    });
    bottomRow->addWidget(chkUsePauseLocks);

    bottomRow->addStretch();

    chkUpdateRP = new QCheckBox("Update Rich Presence", this);
    chkUpdateRP->setChecked(AppSettings::Current.MultiRegion_UpdateRP);
    connect(chkUpdateRP, &QCheckBox::toggled, this, [](bool checked) {
        AppSettings::Current.MultiRegion_UpdateRP = checked;
        AppSettings::Save();
    });
    bottomRow->addWidget(chkUpdateRP);

    btnOK = new QPushButton("Apply", this);
    btnOK->setFixedSize(75, 23);
    connect(btnOK, &QPushButton::clicked, this, &MultiRegionDialog::onBtnOK);
    bottomRow->addWidget(btnOK);

    btnCancel = new QPushButton("Cancel", this);
    btnCancel->setFixedSize(75, 23);
    connect(btnCancel, &QPushButton::clicked, this, &MultiRegionDialog::onBtnCancel);
    bottomRow->addWidget(btnCancel);

    mainLayout->addLayout(bottomRow);

    // Context Menu for Condition Grids
    ctxGridActions = new QMenu(this);
    auto* actPaste = ctxGridActions->addAction(ThemeManager::getIcon(":/icons/load.svg", ThemeManager::CurrentTheme()), "Paste Logic", this,
                                               &MultiRegionDialog::onPasteToGrid);
    actPaste->setShortcut(QKeySequence::Paste);
    auto* actCopy = ctxGridActions->addAction(ThemeManager::getIcon(":/icons/copy.svg", ThemeManager::CurrentTheme()), "Copy", this,
                                              &MultiRegionDialog::onCopyFromGrid);
    actCopy->setShortcut(QKeySequence::Copy);
    auto* actDelete = ctxGridActions->addAction(ThemeManager::getIcon(":/icons/delete.svg", ThemeManager::CurrentTheme()), "Delete Row",
                                                this, &MultiRegionDialog::onDeleteFromGrid);
    actDelete->setShortcut(QKeySequence::Delete);
}

void MultiRegionDialog::InitializeDynamicRegions() {
    QSet<QString> detectedRegions;
    bool hasUnknownNotes = false;

    for (const auto& note : _allNotes) {
        QString region = RegionDefinitions::DetectRegion(note.Note);
        if (region != "Unknown")
            detectedRegions.insert(region);
        else
            hasUnknownNotes = true;
    }

    if (hasUnknownNotes && !detectedRegions.contains("USA"))
        detectedRegions.insert("USA");
    if (detectedRegions.isEmpty())
        detectedRegions.insert("USA");

    QString baseRegion = detectedRegions.contains("USA") ? "USA" : *detectedRegions.begin();

    tabControlRegions->clear();
    _regionGrids.clear();
    _regionOrder.clear();

    QStringList sortedRegions = detectedRegions.values();
    std::sort(sortedRegions.begin(), sortedRegions.end(), [baseRegion](const QString& a, const QString& b) {
        if (a == baseRegion)
            return true;
        if (b == baseRegion)
            return false;
        return a < b;
    });

    for (const auto& region : sortedRegions) {
        bool isBase = (region == baseRegion);
        CreateRegionTab(region, isBase);
    }

    ScanForMappings(baseRegion);
}

void MultiRegionDialog::CreateRegionTab(const QString& regionName, bool isBase) {
    QString title = isBase ? QString("%1 (Base)").arg(regionName) : regionName;
    auto* page = new QWidget();
    auto* pageLayout = new QVBoxLayout(page);
    pageLayout->setContentsMargins(4, 4, 4, 4);

    auto* grp = new QGroupBox(QString("%1 Lock Logic (Paste logic here)").arg(regionName), page);
    auto* grpLayout = new QVBoxLayout(grp);
    grpLayout->setContentsMargins(4, 8, 4, 4);

    auto* grid = new QTableWidget(grp);
    grid->setObjectName("gridLockLogic");
    grid->setItemDelegate(new MultiRegionTableDelegate(grid));
    grid->setColumnCount(7);
    grid->setHorizontalHeaderLabels({"Type", "Size", "Memory", "Cmp", "Type", "Size", "Val/Mem"});
    grid->verticalHeader()->setVisible(false);
    grid->verticalHeader()->setDefaultSectionSize(22);
    grid->horizontalHeader()->setFixedHeight(24);
    grid->setShowGrid(false);
    grid->setSelectionBehavior(QAbstractItemView::SelectRows);
    grid->setEditTriggers(QAbstractItemView::NoEditTriggers);
    grid->setColumnWidth(0, 50);
    grid->setColumnWidth(1, 60);
    grid->horizontalHeader()->setSectionResizeMode(2, QHeaderView::Stretch);
    grid->setColumnWidth(3, 40);
    grid->setColumnWidth(4, 50);
    grid->setColumnWidth(5, 60);
    grid->horizontalHeader()->setSectionResizeMode(6, QHeaderView::Stretch);

    grid->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(grid, &QWidget::customContextMenuRequested, this,
            [this, grid](const QPoint& pos) { ctxGridActions->exec(grid->mapToGlobal(pos)); });

    // Register the menu actions on the table so their shortcuts work without
    // opening the context menu (C# handled these in Grid_KeyDown).
    if (ctxGridActions != nullptr) {
        for (QAction* action : ctxGridActions->actions())
            grid->addAction(action);
    }

    grpLayout->addWidget(grid);
    pageLayout->addWidget(grp);

    tabControlRegions->addTab(page, title);
    _regionGrids.insert(regionName, grid);
    _regionOrder.append(regionName);
}

void MultiRegionDialog::ScanForMappings(const QString& baseRegion) {
    dgvMapping->setRowCount(0);
    static const QRegularExpression regex(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);

    for (const auto& note : _allNotes) {
        auto match = regex.match(note.Note);
        if (match.hasMatch()) {
            QString targetAddrHex = match.captured(1);
            QString normalizedTarget = LogicFormatter::NormalizeAddress(targetAddrHex);
            QString sourceAddrHex = note.Address;
            sourceAddrHex.remove("0x", Qt::CaseInsensitive);

            QString sourceRegion = RegionDefinitions::DetectRegion(note.Note);

            const CodeNote* targetNote = nullptr;
            for (const auto& n : _allNotes) {
                if (LogicFormatter::NormalizeAddress(n.Address) == normalizedTarget) {
                    targetNote = &n;
                    break;
                }
            }

            QString targetRegion = (targetNote != nullptr) ? RegionDefinitions::DetectRegion(targetNote->Note) : "Unknown";

            bool sourceIsBaseCandidate = (sourceRegion == "Unknown" || sourceRegion == baseRegion);
            bool targetIsBaseCandidate = (targetRegion == "Unknown" || targetRegion == baseRegion);

            QString baseAddr = "";
            QString mappedAddr = "";
            QString mappedRegion = "";
            QString description = note.Note.split('\n', Qt::SkipEmptyParts).value(0, "");

            if (!sourceIsBaseCandidate && targetIsBaseCandidate) {
                baseAddr = targetAddrHex;
                mappedAddr = sourceAddrHex;
                mappedRegion = sourceRegion;
                if (targetNote != nullptr)
                    description = targetNote->Note.split('\n', Qt::SkipEmptyParts).value(0, "");
            } else if (sourceIsBaseCandidate && !targetIsBaseCandidate) {
                baseAddr = sourceAddrHex;
                mappedAddr = targetAddrHex;
                mappedRegion = targetRegion;
            }

            if (!baseAddr.isEmpty()) {
                int r = dgvMapping->rowCount();
                dgvMapping->insertRow(r);
                dgvMapping->setItem(r, 0, new QTableWidgetItem(baseAddr));
                dgvMapping->setItem(r, 1, new QTableWidgetItem(mappedAddr));
                dgvMapping->setItem(r, 2, new QTableWidgetItem(mappedRegion));
                dgvMapping->setItem(r, 3, new QTableWidgetItem(description));
            }
        }
    }
}

QList<RegionConfig> MultiRegionDialog::GetConfiguredRegions() {
    QList<RegionConfig> results;
    QMap<QString, QMap<QString, QString>> simpleMappings;
    QMap<QString, QList<AddressRangeMap>> rangeMappings;

    for (int r = 0; r < dgvMapping->rowCount(); r++) {
        QString baseAddrStr = dgvMapping->item(r, 0) != nullptr ? dgvMapping->item(r, 0)->text().trimmed() : "";
        QString targetAddrStr = dgvMapping->item(r, 1) != nullptr ? dgvMapping->item(r, 1)->text().trimmed() : "";
        QString region = dgvMapping->item(r, 2) != nullptr ? dgvMapping->item(r, 2)->text().trimmed() : "";
        QString description = dgvMapping->item(r, 3) != nullptr ? dgvMapping->item(r, 3)->text().trimmed() : "";

        if (!region.isEmpty() && !baseAddrStr.isEmpty() && !targetAddrStr.isEmpty()) {
            if (baseAddrStr.startsWith("0x", Qt::CaseInsensitive))
                baseAddrStr = baseAddrStr.mid(2);
            if (targetAddrStr.startsWith("0x", Qt::CaseInsensitive))
                targetAddrStr = targetAddrStr.mid(2);

            bool okB = false, okT = false;
            int64_t bVal = baseAddrStr.toLongLong(&okB, 16);
            int64_t tVal = targetAddrStr.toLongLong(&okT, 16);

            if (okB && okT) {
                simpleMappings[region][QString::number(bVal, 16)] = QString::number(tVal, 16);

                int64_t size = 1;
                static const QRegularExpression sizeMatchRegex(
                    R"(\[[^\]]*?\b(?:(\d+)\s*x\s*(\d+)|(0x[0-9A-Fa-f]+)|(\d+))[\s\-]*bytes?\b[^\]]*\])",
                    QRegularExpression::CaseInsensitiveOption);
                auto sizeMatch = sizeMatchRegex.match(description);

                if (sizeMatch.hasMatch()) {
                    if (!sizeMatch.captured(1).isEmpty() && !sizeMatch.captured(2).isEmpty()) {
                        bool ok1 = false, ok2 = false;
                        int64_t w = sizeMatch.captured(1).toLongLong(&ok1);
                        int64_t h = sizeMatch.captured(2).toLongLong(&ok2);
                        if (ok1 && ok2 && w > 0 && h > 0)
                            size = w * h;
                    } else if (!sizeMatch.captured(3).isEmpty()) {
                        bool okHex = false;
                        int64_t bSize = sizeMatch.captured(3).mid(2).toLongLong(&okHex, 16);
                        if (okHex && bSize > 0)
                            size = bSize;
                    } else if (!sizeMatch.captured(4).isEmpty()) {
                        bool okDec = false;
                        int64_t bSize = sizeMatch.captured(4).toLongLong(&okDec);
                        if (okDec && bSize > 0)
                            size = bSize;
                    }
                } else {
                    static const QRegularExpression bits64Rx(R"(\[[^\]]*\b(?:64[\s\-]*bits?|Double64)\b[^\]]*\])",
                                                             QRegularExpression::CaseInsensitiveOption);
                    static const QRegularExpression bits32Rx(
                        R"(\[[^\]]*\b(?:32[\s\-]*bits?|Float(?:32)?|MBF32(?:\s+LE)?|Double32(?:\s+BE)?)\b[^\]]*\])",
                        QRegularExpression::CaseInsensitiveOption);
                    static const QRegularExpression bits24Rx(R"(\[[^\]]*\b24[\s\-]*bits?\b[^\]]*\])",
                                                             QRegularExpression::CaseInsensitiveOption);
                    static const QRegularExpression bits16Rx(R"(\[[^\]]*\b16[\s\-]*bits?\b[^\]]*\])",
                                                             QRegularExpression::CaseInsensitiveOption);

                    if (bits64Rx.match(description).hasMatch())
                        size = 8;
                    else if (bits32Rx.match(description).hasMatch())
                        size = 4;
                    else if (bits24Rx.match(description).hasMatch())
                        size = 3;
                    else if (bits16Rx.match(description).hasMatch())
                        size = 2;
                }

                AddressRangeMap arm;
                arm.SourceBase = bVal;
                arm.TargetBase = tVal;
                arm.Size = size;
                arm.Region = region;
                rangeMappings[region].append(arm);
            }
        }
    }

    for (const QString& regionName : _regionOrder) {
        QTableWidget* grid = _regionGrids.value(regionName, nullptr);
        if (grid == nullptr)
            continue;
        bool isBase = false;

        for (int i = 0; i < tabControlRegions->count(); i++) {
            if (tabControlRegions->tabText(i).startsWith(regionName) && tabControlRegions->tabText(i).contains("(Base)")) {
                isBase = true;
                break;
            }
        }

        RegionConfig config;
        config.RegionName = regionName;
        config.IsBase = isBase;
        config.LockConditions = ExtractLogicFromGrid(grid);

        if (!isBase) {
            if (simpleMappings.contains(regionName))
                config.AddressMap = simpleMappings.value(regionName);
            if (rangeMappings.contains(regionName))
                config.AddressRanges = rangeMappings.value(regionName);
        }

        results.append(config);
    }

    return results;
}

QList<AchievementCondition> MultiRegionDialog::ExtractLogicFromGrid(QTableWidget* grid) {
    QList<AchievementCondition> list;
    if (grid == nullptr)
        return list;

    for (int r = 0; r < grid->rowCount(); r++) {
        AchievementCondition cond;
        cond.LeftOperand.Type = grid->item(r, 0) != nullptr ? grid->item(r, 0)->text() : "Mem";
        cond.LeftOperand.Size = grid->item(r, 1) != nullptr ? grid->item(r, 1)->text() : "32-bit";
        cond.LeftOperand.Value = grid->item(r, 2) != nullptr ? grid->item(r, 2)->text() : "0";
        cond.Operator = grid->item(r, 3) != nullptr ? grid->item(r, 3)->text() : "=";
        cond.RightOperand.Type = grid->item(r, 4) != nullptr ? grid->item(r, 4)->text() : "Value";
        cond.RightOperand.Size = grid->item(r, 5) != nullptr ? grid->item(r, 5)->text() : "";
        cond.RightOperand.Value = grid->item(r, 6) != nullptr ? grid->item(r, 6)->text() : "0";
        list.append(cond);
    }
    return list;
}

QTableWidget* MultiRegionDialog::GetActiveGrid() {
    int idx = tabControlRegions->currentIndex();
    if (idx >= 0) {
        auto* page = tabControlRegions->widget(idx);
        return page->findChild<QTableWidget*>();
    }
    return nullptr;
}

void MultiRegionDialog::onPasteToGrid() {
    auto* grid = GetActiveGrid();
    if (grid == nullptr)
        return;

    QString text = QGuiApplication::clipboard()->text().trimmed().remove('"');
    if (text.isEmpty())
        return;

    QList<AchievementCondition> conds;
    try {
        auto groups = AchievementParser::ParseAchievementTrigger(text, 'S', false);
        if (!groups.isEmpty() && !groups[0].Conditions.isEmpty())
            conds.append(groups[0].Conditions);
    } catch (...) {
    }

    if (conds.isEmpty() && (text.contains('=') || text.contains('>') || text.contains('<'))) {
        QStringList parts = text.split(QRegularExpression(R"([_\r\n]+)"), Qt::SkipEmptyParts);
        for (const QString& part : parts) {
            QString cleanPart = part.trimmed();
            if (cleanPart.isEmpty())
                continue;
            auto c = ManualParseCondition(cleanPart);
            if (c.has_value()) {
                conds.append(c.value());
            }
        }
    }

    if (!conds.isEmpty()) {
        for (const auto& c : conds) {
            int r = grid->rowCount();
            grid->insertRow(r);
            grid->setItem(r, 0, new QTableWidgetItem(c.LeftOperand.Type));
            grid->setItem(r, 1, new QTableWidgetItem(c.LeftOperand.Size));
            grid->setItem(r, 2, new QTableWidgetItem(c.LeftOperand.Value));
            grid->setItem(r, 3, new QTableWidgetItem(c.Operator));
            grid->setItem(r, 4, new QTableWidgetItem(c.RightOperand.Type));
            grid->setItem(r, 5, new QTableWidgetItem(c.RightOperand.Size));
            grid->setItem(r, 6, new QTableWidgetItem(c.RightOperand.Value));
        }
    } else {
        QStringList lines = text.split('\n', Qt::SkipEmptyParts);
        bool added = false;
        for (const QString& line : lines) {
            QStringList cells = line.split('\t');
            if (cells.size() >= 3) {
                int r = grid->rowCount();
                grid->insertRow(r);
                for (int c = 0; c < cells.size() && c < grid->columnCount(); c++)
                    grid->setItem(r, c, new QTableWidgetItem(cells[c].trimmed()));
                added = true;
            }
        }
        if (!added)
            QMessageBox::warning(this, "Paste Failed", "Could not parse clipboard content.");
    }
}

void MultiRegionDialog::onCopyFromGrid() {
    auto* grid = GetActiveGrid();
    if (grid == nullptr)
        return;

    QString sb;
    for (int r = 0; r < grid->rowCount(); r++) {
        if (grid->item(r, 0) != nullptr && grid->item(r, 0)->isSelected()) {
            QStringList rowParts;
            for (int c = 0; c < grid->columnCount(); c++)
                rowParts.append(grid->item(r, c) != nullptr ? grid->item(r, c)->text() : "");
            sb += rowParts.join('\t') + "\n";
        }
    }
    QGuiApplication::clipboard()->setText(sb);
}

void MultiRegionDialog::onDeleteFromGrid() {
    auto* grid = GetActiveGrid();
    if (grid == nullptr)
        return;

    auto selectedRows = grid->selectionModel()->selectedRows();
    QList<int> rows;
    for (const auto& idx : selectedRows)
        rows.append(idx.row());
    std::sort(rows.begin(), rows.end(), std::greater<int>());

    for (int r : rows)
        grid->removeRow(r);
}

std::optional<AchievementCondition> MultiRegionDialog::ManualParseCondition(const QString& raw) {
    static const QRegularExpression regex(R"(^([^=<>!]+)([=<>!]+)([^=<>!]+)$)");
    auto match = regex.match(raw);
    if (!match.hasMatch())
        return std::nullopt;

    AchievementCondition cond;
    cond.Operator = match.captured(2);
    cond.LeftOperand = AchievementParser::ParseOperand(match.captured(1).trimmed());
    cond.RightOperand = AchievementParser::ParseOperand(match.captured(3).trimmed());
    return cond;
}

void MultiRegionDialog::onBtnOK() { accept(); }
void MultiRegionDialog::onBtnCancel() { reject(); }
void MultiRegionDialog::onBtnHelp() {
    QMessageBox::information(
        this, "Help",
        "Maps regions based on notes. Ensure base notes have size (e.g. [0x20 bytes]) for range mapping.\n\nOptionally check 'Generate as "
        "PauseLocks' to automatically apply PauseIf and invert the operators for memory checks.");
}

} // namespace RALE
