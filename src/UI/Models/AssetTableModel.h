#pragma once

#include "../../Models/AssetModels.h"

#include <QAbstractTableModel>
#include <QList>
#include <QStyledItemDelegate>

#include <memory>

namespace RALE {

class AssetTableModel : public QAbstractTableModel {
    Q_OBJECT

  public:
    enum Column { ColID = 0, ColTitle = 1, ColumnCount = 2 };

    explicit AssetTableModel(QObject* parent = nullptr);

    int rowCount(const QModelIndex& parent = QModelIndex()) const override;
    int columnCount(const QModelIndex& parent = QModelIndex()) const override;
    QVariant data(const QModelIndex& index, int role = Qt::DisplayRole) const override;
    QVariant headerData(int section, Qt::Orientation orientation, int role = Qt::DisplayRole) const override;
    void sort(int column, Qt::SortOrder order = Qt::AscendingOrder) override;

    void SetAssets(const QList<std::shared_ptr<IAssetItem>>& assets);
    std::shared_ptr<IAssetItem> GetAsset(int row) const;
    void ReplaceAsset(int row, const std::shared_ptr<IAssetItem>& asset);
    int FindRowByAssetId(int id) const;
    void NotifyAssetChanged(const std::shared_ptr<IAssetItem>& asset);

  private:
    QList<std::shared_ptr<IAssetItem>> _assets;
};

// Delegate to ensure crisp 1:1 Windows Forms row selection, 4px padding, and 1px gridline borders
class AssetTableDelegate : public QStyledItemDelegate {
    Q_OBJECT
  public:
    explicit AssetTableDelegate(QObject* parent = nullptr);
    void paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const override;
};

} // namespace RALE
