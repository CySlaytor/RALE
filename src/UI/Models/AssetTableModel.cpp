#include "AssetTableModel.h"
#include <QColor>
#include <QPainter>
#include <algorithm>

namespace RALogicEditor {

AssetTableModel::AssetTableModel(QObject *parent)
    : QAbstractTableModel(parent)
{
}

int AssetTableModel::rowCount(const QModelIndex &parent) const
{
    if (parent.isValid()) return 0;
    return _assets.size();
}

int AssetTableModel::columnCount(const QModelIndex &parent) const
{
    if (parent.isValid()) return 0;
    return ColumnCount;
}

QVariant AssetTableModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= _assets.size())
        return QVariant();

    const auto &item = _assets[index.row()];
    bool isDark = false;

    if (role == Qt::DisplayRole)
    {
        switch (index.column())
        {
            case ColID:
            {
                QString displayId = QString::number(item->ID);
                if (item->isLeaderboard() && item->ID >= 111000001)
                    displayId = "L" + displayId;
                return displayId;
            }
            case ColTitle:
                return item->Title;
        }
    }
    else if (role == Qt::TextAlignmentRole)
    {
        return static_cast<int>(Qt::AlignLeft | Qt::AlignVCenter);
    }
    else if (role == Qt::ForegroundRole)
    {
        if (item->State == AssetState::Modified)
            return QColor(255, 140, 0); // Orange
        else if (item->State == AssetState::Local)
            return QColor(46, 139, 87);  // Sea Green
    }
    else if (role == Qt::UserRole)
    {
        return QVariant::fromValue(item.get());
    }

    return QVariant();
}

QVariant AssetTableModel::headerData(int section, Qt::Orientation orientation, int role) const
{
    if (orientation == Qt::Horizontal)
    {
        if (role == Qt::DisplayRole)
        {
            switch (section)
            {
                case ColID: return "ID";
                case ColTitle: return "Title";
            }
        }
        else if (role == Qt::TextAlignmentRole)
        {
            return static_cast<int>(Qt::AlignLeft | Qt::AlignVCenter);
        }
    }
    return QAbstractTableModel::headerData(section, orientation, role);
}

void AssetTableModel::sort(int column, Qt::SortOrder order)
{
    if (_assets.isEmpty()) return;

    beginResetModel();
    std::sort(_assets.begin(), _assets.end(), [column, order](const std::shared_ptr<IAssetItem> &a, const std::shared_ptr<IAssetItem> &b) {
        if (column == ColID)
        {
            return (order == Qt::AscendingOrder) ? (a->ID < b->ID) : (a->ID > b->ID);
        }
        else
        {
            int cmp = QString::compare(a->Title, b->Title, Qt::CaseInsensitive);
            return (order == Qt::AscendingOrder) ? (cmp < 0) : (cmp > 0);
        }
    });
    endResetModel();
}

void AssetTableModel::SetAssets(const QList<std::shared_ptr<IAssetItem>> &assets)
{
    beginResetModel();
    _assets = assets;
    endResetModel();
}

std::shared_ptr<IAssetItem> AssetTableModel::GetAsset(int row) const
{
    if (row >= 0 && row < _assets.size())
        return _assets[row];
    return nullptr;
}

void AssetTableModel::ReplaceAsset(int row, const std::shared_ptr<IAssetItem> &asset)
{
    if (row >= 0 && row < _assets.size())
    {
        _assets[row] = asset;
        emit dataChanged(index(row, 0), index(row, ColumnCount - 1));
    }
}

int AssetTableModel::FindRowByAssetId(int id) const
{
    for (int i = 0; i < _assets.size(); i++)
    {
        if (_assets[i]->ID == id) return i;
    }
    return -1;
}

void AssetTableModel::NotifyAssetChanged(const std::shared_ptr<IAssetItem> &asset)
{
    if (asset == nullptr) return;
    int r = FindRowByAssetId(asset->ID);
    if (r != -1)
    {
        emit dataChanged(index(r, 0), index(r, ColumnCount - 1), { Qt::DisplayRole, Qt::ForegroundRole });
    }
}

// --- AssetTableDelegate implementation ---
AssetTableDelegate::AssetTableDelegate(QObject *parent)
    : QStyledItemDelegate(parent)
{
}

void AssetTableDelegate::paint(QPainter *painter, const QStyleOptionViewItem &option, const QModelIndex &index) const
{
    QStyleOptionViewItem opt = option;
    initStyleOption(&opt, index);

    bool isSelected = (opt.state & QStyle::State_Selected);
    bool isDark = (opt.palette.window().color().red() < 128);

    // 1. Explicit pure white cell in light mode, dark gray in dark mode
    QColor baseCellColor = isDark ? QColor(32, 32, 34) : QColor(255, 255, 255);
    QColor bgColor = isSelected ? opt.palette.highlight().color() : baseCellColor;
    painter->fillRect(opt.rect, bgColor);

    // 2. Draw clean 1px thin gridline
    QColor gridColor = isDark ? QColor(46, 46, 50) : QColor(112, 112, 112);
    painter->save();
    painter->setPen(QPen(gridColor, 1));
    painter->drawLine(opt.rect.right(), opt.rect.top(), opt.rect.right(), opt.rect.bottom());
    painter->drawLine(opt.rect.left(), opt.rect.bottom(), opt.rect.right(), opt.rect.bottom());
    painter->restore();

    // 3. Determine text color
    QColor textColor;
    if (isSelected)
    {
        textColor = opt.palette.highlightedText().color(); // White text on selection
    }
    else
    {
        QVariant fg = index.data(Qt::ForegroundRole);
        if (fg.isValid())
            textColor = fg.value<QColor>();
        else
            textColor = opt.palette.text().color();
    }

    // 4. Draw text with 4px padding
    painter->save();
    painter->setPen(textColor);
    painter->setFont(opt.font);

    int alignFlags = Qt::AlignLeft | Qt::AlignVCenter;

    QRect textRect = opt.rect.adjusted(4, 0, -4, 0);
    painter->drawText(textRect, alignFlags, opt.text);
    painter->restore();
}

} // namespace RALogicEditor
