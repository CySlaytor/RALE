#pragma once

#include "../../Models/AssetModels.h"
#include "../../Services/LogicOptimizer.h"

#include <QDialog>
#include <QLabel>
#include <QList>
#include <QPushButton>
#include <QTableWidget>

#include <memory>

namespace RALE {

class LogicOptimizerDialog : public QDialog {
    Q_OBJECT

  public:
    explicit LogicOptimizerDialog(const QList<std::shared_ptr<IAssetItem>>& assets, QWidget* parent = nullptr);

  protected:
    void done(int r) override;

  private slots:
    void onSelectAllClicked();
    void onSelectWarningClicked();
    void onOptimizeClicked();
    void onPadClicked();
    void onHelpClicked();
    void onCloseClicked();
    void onCellChanged(int row, int column);

  private:
    QList<std::shared_ptr<IAssetItem>> _assets;
    LogicOptimizer _optimizer;

    QTableWidget* dgvAssets = nullptr;
    QPushButton* btnSelectAll = nullptr;
    QPushButton* btnSelectWarning = nullptr;
    QPushButton* btnPad = nullptr;
    QPushButton* btnOptimize = nullptr;
    QPushButton* btnHelp = nullptr;
    QPushButton* btnClose = nullptr;
    QLabel* lblInfo = nullptr;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
    void PopulateGrid();
    void UpdateRowForAsset(const std::shared_ptr<IAssetItem>& asset);
    void UpdateSelectAllButtonState();
    bool AreAllRowsSelected();
    void ProcessSelectedAssets(std::function<int(std::shared_ptr<IAssetItem>)> action, const QString& actionName);
};

} // namespace RALE
