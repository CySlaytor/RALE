#include "ConditionHeaderView.h"
#include "../Models/ConditionTableModel.h"
#include <QPainter>
#include <QPaintEvent>
#include <QStyleOptionHeader>

namespace RALE {

ConditionHeaderView::ConditionHeaderView(Qt::Orientation orientation, QWidget *parent)
    : QHeaderView(orientation, parent)
{
    setDefaultSectionSize(26);
}

void ConditionHeaderView::setConditionModel(ConditionTableModel *model)
{
    _conditionModel = model;
}

void ConditionHeaderView::setTableView(QTableView *view)
{
    _tableView = view;
}

void ConditionHeaderView::paintEvent(QPaintEvent *event)
{
    QHeaderView::paintEvent(event);

    if (_conditionModel == nullptr || _tableView == nullptr) return;

    int totalRows = _conditionModel->rowCount();
    int lastY = 0;
    if (totalRows > 0)
    {
        lastY = sectionViewportPosition(totalRows - 1) + sectionSize(totalRows - 1);
    }

    // Blend the empty space below the last row into the table workspace background
    if (lastY < viewport()->height())
    {
        QPainter painter(viewport());
        bool isDark = (palette().window().color().red() < 128);
        QColor workspaceColor = isDark ? QColor(32, 32, 34) : QColor(140, 140, 140);
        painter.fillRect(QRect(0, lastY, viewport()->width(), viewport()->height() - lastY), workspaceColor);
    }
}

void ConditionHeaderView::paintSection(QPainter *painter, const QRect &rect, int logicalIndex) const
{
    // Check if this row is the currently active/focused row from the actual TableView
    bool isCurrentRow = false;
    if (_tableView != nullptr && _tableView->selectionModel() != nullptr)
    {
        isCurrentRow = (_tableView->selectionModel()->currentIndex().row() == logicalIndex);
    }

    bool isDark = (palette().window().color().red() < 128);
    QColor borderColor = isDark ? QColor(46, 46, 50) : QColor(112, 112, 112);

    // Active row header highlights with soft blue (#cce4f7) just like WinForms DataGridView; inactive rows are neutral #f0f0f0
    QColor sectionBg;
    if (isDark)
    {
        sectionBg = isCurrentRow ? QColor(45, 45, 48) : QColor(32, 32, 34);
    }
    else
    {
        sectionBg = isCurrentRow ? QColor(204, 228, 247) : QColor(240, 240, 240);
    }

    painter->fillRect(rect, sectionBg);

    // Draw crisp right and bottom 1px borders matching WinForms DataGridView
    painter->save();
    painter->setPen(borderColor);
    painter->drawLine(rect.right(), rect.top(), rect.right(), rect.bottom());
    painter->drawLine(rect.left(), rect.bottom(), rect.right(), rect.bottom());
    painter->restore();

    if (_conditionModel == nullptr || logicalIndex < 0 || logicalIndex >= _conditionModel->rowCount())
        return;

    const auto *cond = _conditionModel->GetConditionAtRow(logicalIndex);
    if (cond == nullptr) return;

    // Draw active row indicator triangle (►) matching Windows Forms DataGridView exactly
    if (isCurrentRow)
    {
        painter->save();
        painter->setRenderHint(QPainter::Antialiasing, true);
        QColor arrowColor = isDark ? QColor(240, 240, 240) : QColor(20, 20, 20);
        painter->setBrush(arrowColor);
        painter->setPen(Qt::NoPen);

        int midY = rect.top() + (rect.height() / 2);
        int arrowLeft = rect.left() + 4;
        QPolygon arrow;
        arrow << QPoint(arrowLeft, midY - 4)
              << QPoint(arrowLeft + 5, midY)
              << QPoint(arrowLeft, midY + 4);
        painter->drawPolygon(arrow);
        painter->restore();
    }

    // Determine colors for incoming (Up) and outgoing (Down) connector lines
    std::optional<QColor> upColor = std::nullopt;
    std::optional<QColor> downColor = std::nullopt;

    if (logicalIndex > 0)
    {
        const auto *prevCond = _conditionModel->GetConditionAtRow(logicalIndex - 1);
        if (prevCond != nullptr)
        {
            upColor = GetChainColor(prevCond->Flag);
        }
    }

    downColor = GetChainColor(cond->Flag);

    // Draw continuous 3px vertical grouping lines centered with breathing room from arrow
    if (upColor.has_value() || downColor.has_value())
    {
        painter->save();
        painter->setRenderHint(QPainter::Antialiasing, false);

        int x = rect.left() + (rect.width() / 2);
        int yTop = rect.top();
        int yMid = rect.top() + (rect.height() / 2);
        int yBottom = rect.bottom() + 1; // Overlap by 1px to prevent seam gaps between rows

        QPen pen;
        pen.setWidth(3);
        pen.setCapStyle(Qt::FlatCap);

        // Top Half (Connection from previous row)
        if (upColor.has_value())
        {
            pen.setColor(upColor.value());
            painter->setPen(pen);
            painter->drawLine(x, yTop, x, yMid);
        }

        // Bottom Half (Connection to next row)
        if (downColor.has_value())
        {
            pen.setColor(downColor.value());
            painter->setPen(pen);
            painter->drawLine(x, yMid, x, yBottom);
        }

        painter->restore();
    }
}

std::optional<QColor> ConditionHeaderView::GetChainColor(const QString &flag) const
{
    if (flag == "Add Address")
        return QColor(255, 140, 0); // Orange
    else if (flag == "Add Source" || flag == "Sub Source")
        return QColor(50, 205, 50); // LimeGreen
    else if (flag == "AndNext" || flag == "OrNext")
        return QColor(0, 191, 255); // DeepSkyBlue
    else if (flag == "ResetNextIf")
        return QColor(139, 0, 0); // DarkRed
    else if (flag == "Add Hits" || flag == "Sub Hits")
        return QColor(147, 112, 219); // MediumPurple
    else
        return std::nullopt;
}

} // namespace RALE
