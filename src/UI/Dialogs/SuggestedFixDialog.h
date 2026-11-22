#pragma once

#include "../../Common/ConditionFormatter.h"
#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../../Models/ValidationModels.h"
#include "../Models/ConditionTableModel.h"
#include "../Widgets/ConditionHeaderView.h"
#include "../Widgets/ConditionTableDelegate.h"

#include <QCheckBox>
#include <QDialog>
#include <QLabel>
#include <QMap>
#include <QPushButton>
#include <QTableView>

#include <memory>

namespace RALE {

class SuggestedFixDialog : public QDialog {
    Q_OBJECT

  public:
    SuggestedFixDialog(const QString& issueTitle, const QString& issueDescription, const AchievementConditionGroup& sourceGroup,
                       const SuggestedLogicFix& fix, bool showDecimal, bool showAliases, bool collapseChains,
                       const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                       const QList<ConditionFormatter::RangeEntry>* rangeCache, QWidget* parent = nullptr);

  protected:
    void done(int r) override;

  private:
    AchievementConditionGroup _beforeGroup;
    AchievementConditionGroup _afterGroup;

    ConditionTableModel _beforeModel;
    ConditionTableModel _afterModel;

    bool _showDecimal = false;
    bool _showAliases = false;
    bool _collapseChains = false;

    const QMap<QString, QList<std::shared_ptr<NoteNode>>>* _notesLookup = nullptr;
    const QList<ConditionFormatter::RangeEntry>* _rangeCache = nullptr;

    QTableView* _beforeGrid = nullptr;
    QTableView* _afterGrid = nullptr;
    ConditionHeaderView* _beforeHeader = nullptr;
    ConditionHeaderView* _afterHeader = nullptr;

    QCheckBox* _chkCollapseChains = nullptr;
    QCheckBox* _chkShowAliases = nullptr;
    QCheckBox* _chkShowDecimal = nullptr;
    QPushButton* _btnApply = nullptr;
    QPushButton* _btnCancel = nullptr;

    void InitializeUI(const QString& issueTitle, const QString& issueDescription);
    void SetupPreviewTable(QTableView* table, ConditionTableModel* model, ConditionHeaderView* header);
    void RefreshTables();
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
