#pragma once

#include <QColor>
#include <QHeaderView>
#include <QTableView>

#include <optional>

namespace RALE {

class ConditionTableModel;

// 1:1 Port of DoubleBufferedDataGridView row header rendering matching WinForms and PointerFinder2
class ConditionHeaderView : public QHeaderView {
    Q_OBJECT

  public:
    explicit ConditionHeaderView(Qt::Orientation orientation, QWidget* parent = nullptr);

    void setConditionModel(ConditionTableModel* model);
    void setTableView(QTableView* view);

  protected:
    void paintEvent(QPaintEvent* event) override;
    void paintSection(QPainter* painter, const QRect& rect, int logicalIndex) const override;

  private:
    ConditionTableModel* _conditionModel = nullptr;
    QTableView* _tableView = nullptr;

    std::optional<QColor> GetChainColor(const QString& flag) const;
};

} // namespace RALE
