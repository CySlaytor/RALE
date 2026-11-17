#include "MultiRegionDialog.h"
#include "../../Definitions/RegionDefinitions.h"
#include "../../Parsers/AchievementParser.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGroupBox>
#include <QHeaderView>
#include <QLabel>
#include <QMessageBox>
#include <QClipboard>
#include <QGuiApplication>
#include <QRegularExpression>
#include <QSet>
#include <QStyledItemDelegate>
#include <QPainter>

namespace RALogicEditor {

// Delegate ensuring crisp 1px thin borders, pure white background, and standard Windows selection highlight
class MultiRegionTableDelegate : public QStyledItemDelegate
{
public:
    using QStyledItemDelegate::QStyledItemDelegate;

    void paint(QPainter *painter, const QStyleOptionViewItem &option, const QModelIndex &index) const override
    {
        QStyleOptionViewItem opt = option;
        initStyleOption(&opt, index);

        bool isSelected = (opt.state & QStyle::State_Selected);
        bool isDark = (opt.palette.window().color().red() < 128);

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

MultiRegionDialog::MultiRegionDialog(const QList<CodeNote> &notes, QWidget *parent)
    : QDialog(parent), _allNotes(notes)
{
    InitializeUI();
    InitializeDynamicRegions();
}

bool MultiRegionDialog::ShouldUpdateRichPresence() const { return chkUpdateRP->isChecked(); }
bool MultiRegionDialog::UsePauseLocks() const { return chkUsePauseLocks->isChecked(); }

void MultiRegionDialog::InitializeUI()
{
    setWindowTitle("Multi-Region Logic Generator");
    resize(800, 600);

    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 9, 12, 10);
    mainLayout->setSpacing(6);

    auto *lblInstruction = new QLabel("Detects regions from notes. Define specific locks for each region in the tabs below.", this);
    lblInstruction->setObjectName("lblInstruction");
    mainLayout->addWidget(lblInstruction);

    auto *lblMapping = new QLabel("Detected Address Mappings (from Code Notes)", this);
    QFont hf = lblMapping->font();
    hf.setBold(true);
    lblMapping->setFont(hf);
    mainLayout->addWidget(lblMapping);

    dgvMapping = new QTableWidget(this);
    dgvMapping->setObjectName("dgvMapping");
    dgvMapping->setItemDelegate(new MultiRegionTableDelegate(dgvMapping));
    dgvMapping->setColumnCount(4);
    dgvMapping->setHorizontalHeaderLabels({ "Base Addr (Active)", "Mapped Addr", "Region", "Note Description" });
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
    auto *bottomRow = new QHBoxLayout();
    btnHelp = new QPushButton("Help", this);
    btnHelp->setFixedSize(75, 23);
    connect(btnHelp, &QPushButton::clicked, this, &MultiRegionDialog::onBtnHelp);
    bottomRow->addWidget(btnHelp);

    chkUsePauseLocks = new QCheckBox("Generate as PauseLocks", this);
    chkUsePauseLocks->setChecked(true);
    bottomRow->addWidget(chkUsePauseLocks);

    bottomRow->addStretch();

    chkUpdateRP = new QCheckBox("Update Rich Presence", this);
    chkUpdateRP->setChecked(true);
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
    auto *actPaste = ctxGridActions->addAction("Paste Logic", this, &MultiRegionDialog::onPasteToGrid);
    actPaste->setShortcut(QKeySequence::Paste);
    auto *actCopy = ctxGridActions->addAction("Copy", this, &MultiRegionDialog::onCopyFromGrid);
    actCopy->setShortcut(QKeySequence::Copy);
    auto *actDelete = ctxGridActions->addAction("Delete Row", this, &MultiRegionDialog::onDeleteFromGrid);
    actDelete->setShortcut(QKeySequence::Delete);
}

void MultiRegionDialog::InitializeDynamicRegions()
{
    QSet<QString> detectedRegions;
    bool hasUnknownNotes = false;

    for (const auto &note : _allNotes)
    {
        QString region = RegionDefinitions::DetectRegion(note.Note);
        if (region != "Unknown") detectedRegions.insert(region);
        else hasUnknownNotes = true;
    }

    if (hasUnknownNotes && !detectedRegions.contains("USA")) detectedRegions.insert("USA");
    if (detectedRegions.isEmpty()) detectedRegions.insert("USA");

    QString baseRegion = detectedRegions.contains("USA") ? "USA" : *detectedRegions.begin();

    tabControlRegions->clear();
    _regionGrids.clear();
    _regionOrder.clear();

    QStringList sortedRegions = detectedRegions.values();
    std::sort(sortedRegions.begin(), sortedRegions.end(), [baseRegion](const QString &a, const QString &b) {
        if (a == baseRegion) return true;
        if (b == baseRegion) return false;
        return a < b;
    });

    for (const auto &region : sortedRegions)
    {
        bool isBase = (region == baseRegion);
        CreateRegionTab(region, isBase);
    }

    ScanForMappings(baseRegion);
}

void MultiRegionDialog::CreateRegionTab(const QString &regionName, bool isBase)
{
    QString title = isBase ? QString("%1 (Base)").arg(regionName) : regionName;
    auto *page = new QWidget();
    auto *pageLayout = new QVBoxLayout(page);
    pageLayout->setContentsMargins(4, 4, 4, 4);

    auto *grp = new QGroupBox(QString("%1 Lock Logic (Paste logic here)").arg(regionName), page);
    auto *grpLayout = new QVBoxLayout(grp);
    grpLayout->setContentsMargins(4, 8, 4, 4);

    auto *grid = new QTableWidget(grp);
    grid->setObjectName("gridLockLogic");
    grid->setItemDelegate(new MultiRegionTableDelegate(grid));
    grid->setColumnCount(7);
    grid->setHorizontalHeaderLabels({ "Type", "Size", "Memory", "Cmp", "Type", "Size", "Val/Mem" });
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
    connect(grid, &QWidget::customContextMenuRequested, this, [this, grid](const QPoint &pos) {
        ctxGridActions->exec(grid->mapToGlobal(pos));
    });

    // Register the menu actions on the table so their shortcuts work without
    // opening the context menu (C# handled these in Grid_KeyDown).
    if (ctxGridActions != nullptr)
    {
        for (QAction *action : ctxGridActions->actions())
            grid->addAction(action);
    }

    grpLayout->addWidget(grid);
    pageLayout->addWidget(grp);

    tabControlRegions->addTab(page, title);
    _regionGrids.insert(regionName, grid);
    _regionOrder.append(regionName);
}

void MultiRegionDialog::ScanForMappings(const QString &baseRegion)
{
    dgvMapping->setRowCount(0);
    static const QRegularExpression regex(R"(refer to \$0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);

    for (const auto &note : _allNotes)
    {
        auto match = regex.match(note.Note);
        if (match.hasMatch())
        {
            QString targetAddrHex = match.captured(1);
            QString sourceAddrHex = note.Address;
            sourceAddrHex.remove("0x", Qt::CaseInsensitive);

            QString sourceRegion = RegionDefinitions::DetectRegion(note.Note);

            const CodeNote *targetNote = nullptr;
            for (const auto &n : _allNotes)
            {
                if (n.Address.endsWith(targetAddrHex, Qt::CaseInsensitive))
                {
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

            if (!sourceIsBaseCandidate && targetIsBaseCandidate)
            {
                baseAddr = targetAddrHex;
                mappedAddr = sourceAddrHex;
                mappedRegion = sourceRegion;
                if (targetNote != nullptr) description = targetNote->Note.split('\n', Qt::SkipEmptyParts).value(0, "");
            }
            else if (sourceIsBaseCandidate && !targetIsBaseCandidate)
            {
                baseAddr = sourceAddrHex;
                mappedAddr = targetAddrHex;
                mappedRegion = targetRegion;
            }

            if (!baseAddr.isEmpty())
            {
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

QList<RegionConfig> MultiRegionDialog::GetConfiguredRegions()
{
    QList<RegionConfig> results;
    QMap<QString, QMap<QString, QString>> simpleMappings;
    QMap<QString, QList<AddressRangeMap>> rangeMappings;

    for (int r = 0; r < dgvMapping->rowCount(); r++)
    {
        QString baseAddrStr = dgvMapping->item(r, 0) != nullptr ? dgvMapping->item(r, 0)->text().trimmed() : "";
        QString targetAddrStr = dgvMapping->item(r, 1) != nullptr ? dgvMapping->item(r, 1)->text().trimmed() : "";
        QString region = dgvMapping->item(r, 2) != nullptr ? dgvMapping->item(r, 2)->text().trimmed() : "";
        QString description = dgvMapping->item(r, 3) != nullptr ? dgvMapping->item(r, 3)->text().trimmed() : "";

        if (!region.isEmpty() && !baseAddrStr.isEmpty() && !targetAddrStr.isEmpty())
        {
            if (baseAddrStr.startsWith("0x", Qt::CaseInsensitive)) baseAddrStr = baseAddrStr.mid(2);
            if (targetAddrStr.startsWith("0x", Qt::CaseInsensitive)) targetAddrStr = targetAddrStr.mid(2);

            bool okB = false, okT = false;
            int64_t bVal = baseAddrStr.toLongLong(&okB, 16);
            int64_t tVal = targetAddrStr.toLongLong(&okT, 16);

            if (okB && okT)
            {
                simpleMappings[region][QString::number(bVal, 16)] = QString::number(tVal, 16);

                int64_t size = 1;
                static const QRegularExpression sizeMatchRegex(R"(\[.*?((?:0x)?[0-9a-fA-F]+)\s*[-]?\s*bytes?.*?\])", QRegularExpression::CaseInsensitiveOption);
                auto sizeMatch = sizeMatchRegex.match(description);

                if (sizeMatch.hasMatch())
                {
                    QString sizeStr = sizeMatch.captured(1);
                    if (sizeStr.startsWith("0x", Qt::CaseInsensitive))
                        size = sizeStr.mid(2).toLongLong(nullptr, 16);
                    else
                        size = sizeStr.toLongLong();
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

    for (const QString &regionName : _regionOrder)
    {
        QTableWidget *grid = _regionGrids.value(regionName, nullptr);
        if (grid == nullptr) continue;
        bool isBase = !simpleMappings.contains(regionName);

        for (int i = 0; i < tabControlRegions->count(); i++)
        {
            if (tabControlRegions->tabText(i).startsWith(regionName) && tabControlRegions->tabText(i).contains("(Base)"))
            {
                isBase = true;
                break;
            }
        }

        RegionConfig config;
        config.RegionName = regionName;
        config.IsBase = isBase;
        config.LockConditions = ExtractLogicFromGrid(grid);

        if (!isBase)
        {
            if (simpleMappings.contains(regionName)) config.AddressMap = simpleMappings.value(regionName);
            if (rangeMappings.contains(regionName)) config.AddressRanges = rangeMappings.value(regionName);
        }

        results.append(config);
    }

    return results;
}

QList<AchievementCondition> MultiRegionDialog::ExtractLogicFromGrid(QTableWidget *grid)
{
    QList<AchievementCondition> list;
    if (grid == nullptr) return list;

    for (int r = 0; r < grid->rowCount(); r++)
    {
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

QTableWidget *MultiRegionDialog::GetActiveGrid()
{
    int idx = tabControlRegions->currentIndex();
    if (idx >= 0)
    {
        auto *page = tabControlRegions->widget(idx);
        return page->findChild<QTableWidget*>();
    }
    return nullptr;
}

void MultiRegionDialog::onPasteToGrid()
{
    auto *grid = GetActiveGrid();
    if (grid == nullptr) return;

    QString text = QGuiApplication::clipboard()->text().trimmed().remove('"');
    if (text.isEmpty()) return;

    QList<AchievementCondition> conds;
    try
    {
        auto groups = AchievementParser::ParseAchievementTrigger(text, 'S', false);
        if (!groups.isEmpty() && !groups[0].Conditions.isEmpty())
            conds.append(groups[0].Conditions);
    }
    catch (...) {}

    if (conds.isEmpty() && (text.contains('=') || text.contains('>') || text.contains('<')))
    {
        QStringList parts = text.split(QRegularExpression(R"([_\r\n]+)"), Qt::SkipEmptyParts);
        for (const QString &part : parts)
        {
            QString cleanPart = part.trimmed();
            if (cleanPart.isEmpty()) continue;
            auto *c = ManualParseCondition(cleanPart);
            if (c != nullptr)
            {
                conds.append(*c);
                delete c;
            }
        }
    }

    if (!conds.isEmpty())
    {
        for (const auto &c : conds)
        {
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
    }
    else
    {
        QStringList lines = text.split('\n', Qt::SkipEmptyParts);
        bool added = false;
        for (const QString &line : lines)
        {
            QStringList cells = line.split('\t');
            if (cells.size() >= 3)
            {
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

void MultiRegionDialog::onCopyFromGrid()
{
    auto *grid = GetActiveGrid();
    if (grid == nullptr) return;

    QString sb;
    for (int r = 0; r < grid->rowCount(); r++)
    {
        if (grid->item(r, 0) != nullptr && grid->item(r, 0)->isSelected())
        {
            QStringList rowParts;
            for (int c = 0; c < grid->columnCount(); c++)
                rowParts.append(grid->item(r, c) != nullptr ? grid->item(r, c)->text() : "");
            sb += rowParts.join('\t') + "\n";
        }
    }
    QGuiApplication::clipboard()->setText(sb);
}

void MultiRegionDialog::onDeleteFromGrid()
{
    auto *grid = GetActiveGrid();
    if (grid == nullptr) return;

    auto selectedRows = grid->selectionModel()->selectedRows();
    QList<int> rows;
    for (const auto &idx : selectedRows) rows.append(idx.row());
    std::sort(rows.begin(), rows.end(), std::greater<int>());

    for (int r : rows)
        grid->removeRow(r);
}

AchievementCondition *MultiRegionDialog::ManualParseCondition(const QString &raw)
{
    static const QRegularExpression regex(R"(^([^=<>!]+)([=<>!]+)([^=<>!]+)$)");
    auto match = regex.match(raw);
    if (!match.hasMatch()) return nullptr;

    auto *cond = new AchievementCondition();
    cond->Operator = match.captured(2);
    ParseOperand(match.captured(1), cond->LeftOperand);
    ParseOperand(match.captured(3), cond->RightOperand);
    return cond;
}

void MultiRegionDialog::ParseOperand(const QString &raw, Operand &op)
{
    QString trimmed = raw.trimmed();
    if (trimmed.startsWith("0x", Qt::CaseInsensitive))
    {
        QString body = trimmed.mid(2);
        if (!body.isEmpty())
        {
            QChar typeChar = body[0].toUpper();
            if (QString("HXW ").contains(typeChar) || typeChar.isDigit())
            {
                op.Type = "Mem";
                if (typeChar == 'H') { op.Size = "8-bit"; body = body.mid(1); }
                else if (typeChar == ' ') { op.Size = "16-bit"; body = body.mid(1); }
                else if (typeChar == 'W') { op.Size = "24-bit"; body = body.mid(1); }
                else if (typeChar == 'X') { op.Size = "32-bit"; body = body.mid(1); }
                else { op.Size = "16-bit"; }
                op.Value = "0x" + body;
                return;
            }
        }
    }
    op.Type = "Value";
    op.Size = "";
    op.Value = trimmed;
}

void MultiRegionDialog::onBtnOK() { accept(); }
void MultiRegionDialog::onBtnCancel() { reject(); }
void MultiRegionDialog::onBtnHelp()
{
    QMessageBox::information(this, "Help",
        "Maps regions based on notes. Ensure base notes have size (e.g. [0x20 bytes]) for range mapping.\n\nOptionally check 'Generate as PauseLocks' to automatically apply PauseIf and invert the operators for memory checks.");
}

} // namespace RALogicEditor
