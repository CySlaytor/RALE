#pragma once

#include "../../Models/LogicModels.h"
#include "../../Models/RegionConfig.h"

#include <QCheckBox>
#include <QDialog>
#include <QList>
#include <QMap>
#include <QMenu>
#include <QPushButton>
#include <QTabWidget>
#include <QTableWidget>

#include <optional>

namespace RALE {

class MultiRegionDialog : public QDialog {
    Q_OBJECT

  public:
    // Property exposed for MainForm to check before applying RP logic
    bool ShouldUpdateRichPresence() const;

    // Property exposed for MainForm to determine whether to apply PauseLocks
    bool UsePauseLocks() const;

    explicit MultiRegionDialog(const QList<CodeNote>& notes, QWidget* parent = nullptr);

    QList<RegionConfig> GetConfiguredRegions();

  protected:
    void done(int r) override;

  private slots:
    void onBtnOK();
    void onBtnCancel();
    void onBtnHelp();
    void onPasteToGrid();
    void onCopyFromGrid();
    void onDeleteFromGrid();

  private:
    QList<CodeNote> _allNotes;
    QMap<QString, QTableWidget*> _regionGrids;
    QStringList _regionOrder; // tab creation order (base first), matching C#

    QTableWidget* dgvMapping = nullptr;
    QTabWidget* tabControlRegions = nullptr;
    QCheckBox* chkUpdateRP = nullptr;
    QCheckBox* chkUsePauseLocks = nullptr;
    QPushButton* btnHelp = nullptr;
    QPushButton* btnOK = nullptr;
    QPushButton* btnCancel = nullptr;
    QMenu* ctxGridActions = nullptr;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
    void InitializeDynamicRegions();
    void CreateRegionTab(const QString& regionName, bool isBase);
    void ScanForMappings(const QString& baseRegion);
    QList<AchievementCondition> ExtractLogicFromGrid(QTableWidget* grid);
    QTableWidget* GetActiveGrid();
    std::optional<AchievementCondition> ManualParseCondition(const QString& raw);
};

} // namespace RALE
