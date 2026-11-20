#include "LogicOptimizerDialog.h"

#include "../../Common/AppSettings.h"
#include "../Styles/ThemeManager.h"

#include <QHBoxLayout>
#include <QHeaderView>
#include <QMessageBox>
#include <QMouseEvent>
#include <QPainter>
#include <QStyleOptionButton>
#include <QStyleOptionViewItem>
#include <QStyledItemDelegate>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

// Delegate ensuring clean white cell backgrounds, 1px thin gridlines, and standard Windows Forms blue highlight
class OptimizerTableDelegate : public QStyledItemDelegate {
  public:
    using QStyledItemDelegate::QStyledItemDelegate;

    bool editorEvent(QEvent* event, QAbstractItemModel* model, const QStyleOptionViewItem& option, const QModelIndex& index) override {
        if (index.column() == 0 && (event->type() == QEvent::MouseButtonRelease || event->type() == QEvent::MouseButtonDblClick)) {
            auto* mouseEvent = static_cast<QMouseEvent*>(event);
            if (mouseEvent->button() == Qt::LeftButton) {
                if (option.rect.contains(mouseEvent->pos())) {
                    Qt::CheckState current = static_cast<Qt::CheckState>(index.data(Qt::CheckStateRole).toInt());
                    Qt::CheckState next = (current == Qt::Checked) ? Qt::Unchecked : Qt::Checked;
                    model->setData(index, next, Qt::CheckStateRole);
                    return true;
                }
            }
        }
        return QStyledItemDelegate::editorEvent(event, model, option, index);
    }

    void paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const override {
        QStyleOptionViewItem opt = option;
        initStyleOption(&opt, index);

        bool isSelected = (opt.state & QStyle::State_Selected);
        bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);

        // 1. Draw Cell Background
        QColor baseColor = isDark ? QColor(26, 26, 28) : QColor(255, 255, 255);
        QColor bgColor = isSelected ? opt.palette.highlight().color() : baseColor;
        painter->fillRect(opt.rect, bgColor);

        // 2. Draw 1px Thin Gridline
        QColor gridColor = isDark ? QColor(56, 56, 60) : QColor(208, 208, 208);
        painter->save();
        painter->setPen(QPen(gridColor, 1));
        painter->drawLine(opt.rect.right(), opt.rect.top(), opt.rect.right(), opt.rect.bottom());
        painter->drawLine(opt.rect.left(), opt.rect.bottom(), opt.rect.right(), opt.rect.bottom());
        painter->restore();

        // 3. Draw Checkbox in Col 0 or Text in other columns
        if (index.column() == 0) {
            // Center and route checkbox indicator through the unified style system
            QStyleOptionViewItem checkOpt = opt;
            int checkSize = 13;
            checkOpt.rect = QRect(opt.rect.x() + (opt.rect.width() - checkSize) / 2, opt.rect.y() + (opt.rect.height() - checkSize) / 2,
                                  checkSize, checkSize);
            checkOpt.state = opt.state & ~QStyle::State_Selected;
            bool isChecked = (index.data(Qt::CheckStateRole).toInt() == Qt::Checked);
            checkOpt.checkState = isChecked ? Qt::Checked : Qt::Unchecked;
            checkOpt.state &= ~(QStyle::State_On | QStyle::State_Off);
            checkOpt.state |= (isChecked ? QStyle::State_On : QStyle::State_Off);

            if (opt.widget != nullptr) {
                opt.widget->style()->drawPrimitive(QStyle::PE_IndicatorItemViewItemCheck, &checkOpt, painter, opt.widget);
            }
        } else {
            QColor textColor;
            if (isSelected) {
                textColor = opt.palette.highlightedText().color();
            } else {
                QVariant fg = index.data(Qt::ForegroundRole);
                textColor = fg.isValid() ? fg.value<QColor>() : opt.palette.text().color();
            }

            painter->save();
            painter->setPen(textColor);
            painter->setFont(opt.font);
            QRect textRect = opt.rect.adjusted(4, 0, -4, 0);
            painter->drawText(textRect, Qt::AlignLeft | Qt::AlignVCenter, opt.text);
            painter->restore();
        }
    }
};

LogicOptimizerDialog::LogicOptimizerDialog(const QList<std::shared_ptr<IAssetItem>>& assets, QWidget* parent)
    : QDialog(parent), _assets(assets) {
    InitializeUI();
    PopulateGrid();
    UpdateSelectAllButtonState();
    loadPositionSettings();
}

void LogicOptimizerDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("LogicOptimizerDialog", p, s)) {
        move(p);
        if (s.isValid()) {
            resize(std::max(s.width(), 700), std::max(s.height(), 400));
        }
    }
}

void LogicOptimizerDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("LogicOptimizerDialog", pos(), size()); }

void LogicOptimizerDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

void LogicOptimizerDialog::InitializeUI() {
    setWindowTitle("Logic Optimizer");
    resize(784, 461);
    setMinimumSize(700, 400);

    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    dgvAssets = new QTableWidget(this);
    dgvAssets->setObjectName("dgvAssets");
    dgvAssets->setItemDelegate(new OptimizerTableDelegate(dgvAssets));
    dgvAssets->setColumnCount(6);
    dgvAssets->setHorizontalHeaderLabels({"", "ID", "Title", "Length", "Status", "Stripped?"});
    dgvAssets->verticalHeader()->setVisible(false);
    dgvAssets->verticalHeader()->setDefaultSectionSize(22);
    dgvAssets->horizontalHeader()->setFixedHeight(24);
    dgvAssets->setShowGrid(false);
    dgvAssets->setSelectionBehavior(QAbstractItemView::SelectRows);

    dgvAssets->setColumnWidth(0, 30);
    dgvAssets->setColumnWidth(1, 80);
    dgvAssets->horizontalHeader()->setSectionResizeMode(2, QHeaderView::Stretch);
    dgvAssets->setColumnWidth(3, 70);
    dgvAssets->setColumnWidth(4, 80);
    dgvAssets->setColumnWidth(5, 70);

    connect(dgvAssets, &QTableWidget::cellChanged, this, &LogicOptimizerDialog::onCellChanged);
    mainLayout->addWidget(dgvAssets, 1);

    auto* panelBottomWidget = new QWidget(this);
    panelBottomWidget->setFixedHeight(50);
    auto* panelBottom = new QHBoxLayout(panelBottomWidget);
    panelBottom->setContentsMargins(12, 12, 12, 12);
    panelBottom->setSpacing(6);

    btnSelectAll = new QPushButton("Select All", panelBottomWidget);
    btnSelectAll->setFixedSize(75, 26);
    connect(btnSelectAll, &QPushButton::clicked, this, &LogicOptimizerDialog::onSelectAllClicked);
    panelBottom->addWidget(btnSelectAll);

    btnSelectWarning = new QPushButton("Select Warnings", panelBottomWidget);
    btnSelectWarning->setFixedSize(120, 26);
    btnSelectWarning->setToolTip("Deselects all, then selects only items exceeding length limits");
    connect(btnSelectWarning, &QPushButton::clicked, this, &LogicOptimizerDialog::onSelectWarningClicked);
    panelBottom->addWidget(btnSelectWarning);

    lblInfo = new QLabel("Strip 0x00... to save chars", panelBottomWidget);
    lblInfo->setObjectName("lblInfo");
    panelBottom->addWidget(lblInfo);

    panelBottom->addStretch();

    btnHelp = new QPushButton("?", panelBottomWidget);
    btnHelp->setFixedSize(30, 26);
    btnHelp->setToolTip("View Instructions");
    connect(btnHelp, &QPushButton::clicked, this, &LogicOptimizerDialog::onHelpClicked);
    panelBottom->addWidget(btnHelp);

    btnPad = new QPushButton("Reset / Pad", panelBottomWidget);
    btnPad->setFixedSize(95, 26);
    btnPad->setToolTip("Pads hex values to 8 characters (0x00001234)");
    connect(btnPad, &QPushButton::clicked, this, &LogicOptimizerDialog::onPadClicked);
    panelBottom->addWidget(btnPad);

    btnOptimize = new QPushButton("Optimize Selected", panelBottomWidget);
    btnOptimize->setFixedSize(140, 26);
    QFont bFont = btnOptimize->font();
    bFont.setBold(true);
    btnOptimize->setFont(bFont);
    btnOptimize->setToolTip("Removes leading zeros from addresses and values to save space");
    connect(btnOptimize, &QPushButton::clicked, this, &LogicOptimizerDialog::onOptimizeClicked);
    panelBottom->addWidget(btnOptimize);

    btnClose = new QPushButton("Close", panelBottomWidget);
    btnClose->setFixedSize(75, 26);
    connect(btnClose, &QPushButton::clicked, this, &LogicOptimizerDialog::onCloseClicked);
    panelBottom->addWidget(btnClose);

    mainLayout->addWidget(panelBottomWidget);
}

void LogicOptimizerDialog::PopulateGrid() {
    dgvAssets->blockSignals(true);
    dgvAssets->setRowCount(0);
    for (const auto& asset : _assets) {
        UpdateRowForAsset(asset);
    }
    dgvAssets->blockSignals(false);
}

void LogicOptimizerDialog::UpdateRowForAsset(const std::shared_ptr<IAssetItem>& asset) {
    int len = asset->Logic.length();
    QString status = "OK";
    QColor statusColor(0, 128, 0); // Clean Green matching WinForms

    if (len > 65535) {
        status = "EXCEEDED";
        statusColor = QColor(255, 0, 0); // Red
    } else if (len > 60000) {
        status = "Warning";
        statusColor = QColor(255, 140, 0); // Orange
    }

    bool isStripped = _optimizer.IsFullyOptimized(asset);
    QString strippedStatus = isStripped ? "Yes" : "No";
    QColor strippedColor = isStripped ? QColor(0, 128, 0) : QColor(140, 140, 140);

    int rowIdx = -1;
    for (int r = 0; r < dgvAssets->rowCount(); r++) {
        if (dgvAssets->item(r, 1) != nullptr && dgvAssets->item(r, 1)->data(Qt::UserRole).toInt() == asset->ID) {
            rowIdx = r;
            break;
        }
    }

    if (rowIdx == -1) {
        rowIdx = dgvAssets->rowCount();
        dgvAssets->insertRow(rowIdx);

        auto* chkItem = new QTableWidgetItem();
        chkItem->setFlags(chkItem->flags() | Qt::ItemIsUserCheckable);
        chkItem->setData(Qt::CheckStateRole, Qt::Unchecked);
        dgvAssets->setItem(rowIdx, 0, chkItem);

        auto* idItem = new QTableWidgetItem(QString::number(asset->ID));
        idItem->setData(Qt::UserRole, asset->ID);
        idItem->setFlags(Qt::ItemIsEnabled | Qt::ItemIsSelectable);
        dgvAssets->setItem(rowIdx, 1, idItem);

        auto* titleItem = new QTableWidgetItem(asset->Title);
        titleItem->setFlags(Qt::ItemIsEnabled | Qt::ItemIsSelectable);
        dgvAssets->setItem(rowIdx, 2, titleItem);
    }

    auto* lenItem = new QTableWidgetItem(QString::number(len));
    lenItem->setFlags(Qt::ItemIsEnabled | Qt::ItemIsSelectable);
    dgvAssets->setItem(rowIdx, 3, lenItem);

    auto* statusItem = new QTableWidgetItem(status);
    statusItem->setForeground(statusColor);
    statusItem->setFlags(Qt::ItemIsEnabled | Qt::ItemIsSelectable);
    dgvAssets->setItem(rowIdx, 4, statusItem);

    auto* strippedItem = new QTableWidgetItem(strippedStatus);
    strippedItem->setForeground(strippedColor);
    strippedItem->setFlags(Qt::ItemIsEnabled | Qt::ItemIsSelectable);
    dgvAssets->setItem(rowIdx, 5, strippedItem);
}

void LogicOptimizerDialog::onSelectAllClicked() {
    bool allSelected = AreAllRowsSelected();
    bool newState = !allSelected;

    dgvAssets->blockSignals(true);
    for (int r = 0; r < dgvAssets->rowCount(); r++) {
        if (dgvAssets->item(r, 0) != nullptr)
            dgvAssets->item(r, 0)->setData(Qt::CheckStateRole, newState ? Qt::Checked : Qt::Unchecked);
    }
    dgvAssets->blockSignals(false);
    dgvAssets->viewport()->update();
    UpdateSelectAllButtonState();
}

void LogicOptimizerDialog::UpdateSelectAllButtonState() {
    if (AreAllRowsSelected())
        btnSelectAll->setText("Deselect All");
    else
        btnSelectAll->setText("Select All");
}

bool LogicOptimizerDialog::AreAllRowsSelected() {
    if (dgvAssets->rowCount() == 0)
        return false;
    for (int r = 0; r < dgvAssets->rowCount(); r++) {
        if (dgvAssets->item(r, 0) == nullptr || dgvAssets->item(r, 0)->data(Qt::CheckStateRole).toInt() != Qt::Checked)
            return false;
    }
    return true;
}

void LogicOptimizerDialog::onCellChanged(int /*row*/, int column) {
    if (column == 0) {
        UpdateSelectAllButtonState();
    }
}

void LogicOptimizerDialog::onSelectWarningClicked() {
    dgvAssets->blockSignals(true);
    for (int r = 0; r < dgvAssets->rowCount(); r++) {
        if (dgvAssets->item(r, 0) != nullptr)
            dgvAssets->item(r, 0)->setData(Qt::CheckStateRole, Qt::Unchecked);
    }

    int count = 0;
    for (int r = 0; r < dgvAssets->rowCount(); r++) {
        QString s = dgvAssets->item(r, 4) != nullptr ? dgvAssets->item(r, 4)->text() : "";
        if (s == "EXCEEDED" || s == "Warning") {
            if (dgvAssets->item(r, 0) != nullptr)
                dgvAssets->item(r, 0)->setData(Qt::CheckStateRole, Qt::Checked);
            count++;
        }
    }
    dgvAssets->blockSignals(false);
    dgvAssets->viewport()->update();
    UpdateSelectAllButtonState();
    QMessageBox::information(this, "Select Warnings", QString("Selected %1 items with warnings.").arg(count));
}

void LogicOptimizerDialog::onOptimizeClicked() {
    ProcessSelectedAssets([this](std::shared_ptr<IAssetItem> a) { return _optimizer.OptimizeAsset(a); }, "Optimization");
}

void LogicOptimizerDialog::onPadClicked() {
    ProcessSelectedAssets([this](std::shared_ptr<IAssetItem> a) { return _optimizer.PadAsset(a); }, "Padding");
}

void LogicOptimizerDialog::ProcessSelectedAssets(std::function<int(std::shared_ptr<IAssetItem>)> action, const QString& actionName) {
    int totalChanges = 0;
    int assetsChanged = 0;

    for (int r = 0; r < dgvAssets->rowCount(); r++) {
        if (dgvAssets->item(r, 0) != nullptr && dgvAssets->item(r, 0)->data(Qt::CheckStateRole).toInt() == Qt::Checked) {
            int assetId = dgvAssets->item(r, 1)->data(Qt::UserRole).toInt();
            for (const auto& asset : _assets) {
                if (asset->ID == assetId) {
                    int changes = action(asset);
                    if (changes > 0) {
                        totalChanges += changes;
                        assetsChanged++;
                        UpdateRowForAsset(asset);
                    }
                    break;
                }
            }
        }
    }

    QMessageBox::information(this, "Complete",
                             QString("%1 applied to %2 assets (%3 modifications).").arg(actionName).arg(assetsChanged).arg(totalChanges));
}

void LogicOptimizerDialog::onHelpClicked() {
    QString msg = "LOGIC OPTIMIZER INSTRUCTIONS:\n\n"
                  "1. LENGTH REDUCTION:\n"
                  "   The database has a limit of 65,535 characters. Large scripts may fail to upload.\n"
                  "   Click 'Optimize Selected' to remove leading zeros from addresses and values.\n"
                  "   Example: 0x00001234 -> 0x1234 (Saves 4 chars).\n\n"
                  "2. RESET / PAD:\n"
                  "   If you want to standardize format or fix readability, click 'Reset / Pad'.\n"
                  "   This pads all hex values to 8 characters (lowercase).\n"
                  "   Example: 0x1234 -> 0x00001234.\n\n"
                  "3. STATUS COLUMNS:\n"
                  "   - Length: Shows current character count. Red means over limit.\n"
                  "   - Stripped?: 'Yes' means all values are minimal. 'No' means leading zeros exist.";

    QMessageBox::information(this, "Logic Optimizer Help", msg);
}

void LogicOptimizerDialog::onCloseClicked() { close(); }

} // namespace RALE
