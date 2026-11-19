#pragma once

#include <QDialog>
#include <QTableWidget>
#include <QTabWidget>
#include <QCheckBox>
#include <QPushButton>
#include <QMenu>
#include <QMap>
#include <QList>
#include "../../Models/LogicModels.h"
#include "../../Models/RegionConfig.h"

namespace RALE {

class MultiRegionDialog : public QDialog
{
    Q_OBJECT

public:
    // Property exposed for MainForm to check before applying RP logic
    bool ShouldUpdateRichPresence() const;

    // Property exposed for MainForm to determine whether to apply PauseLocks
    bool UsePauseLocks() const;

    explicit MultiRegionDialog(const QList<CodeNote> &notes, QWidget *parent = nullptr);

    QList<RegionConfig> GetConfiguredRegions();

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

    QTableWidget *dgvMapping = nullptr;
    QTabWidget *tabControlRegions = nullptr;
    QCheckBox *chkUpdateRP = nullptr;
    QCheckBox *chkUsePauseLocks = nullptr;
    QPushButton *btnHelp = nullptr;
    QPushButton *btnOK = nullptr;
    QPushButton *btnCancel = nullptr;
    QMenu *ctxGridActions = nullptr;

    void InitializeUI();
    void InitializeDynamicRegions();
    void CreateRegionTab(const QString &regionName, bool isBase);
    void ScanForMappings(const QString &baseRegion);
    QList<AchievementCondition> ExtractLogicFromGrid(QTableWidget *grid);
    QTableWidget *GetActiveGrid();
    AchievementCondition *ManualParseCondition(const QString &raw);
    void ParseOperand(const QString &raw, Operand &op);
};

} // namespace RALE
