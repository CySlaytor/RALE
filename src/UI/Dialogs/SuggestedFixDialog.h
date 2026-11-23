#pragma once

#include "../../Common/ConditionFormatter.h"
#include "../../Models/AssetModels.h"
#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../../Models/ValidationModels.h"

#include <QCheckBox>
#include <QDialog>
#include <QLabel>
#include <QLineEdit>
#include <QList>
#include <QMap>
#include <QPushButton>
#include <QTableWidget>

#include <memory>

namespace RALE {

class SuggestedFixDialog : public QDialog {
    Q_OBJECT

  public:
    SuggestedFixDialog(const ValidationIssue& issue, const std::shared_ptr<IAssetItem>& asset,
                       const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                       const QList<ConditionFormatter::RangeEntry>* rangeCache, bool showDecimal, bool showAliases,
                       QWidget* parent = nullptr);

    bool HasValidFix() const { return _hasValidFix; }
    SuggestedFixType FixType() const { return _issue.FixType; }
    QString TargetField() const { return _issue.TargetField; }

    QString ResultingLogic() const { return _resultingLogic; }
    QString ResultingTitle() const;
    QString ResultingDescription() const;

  protected:
    void done(int r) override;

  private slots:
    void onDisplayOptionChanged();
    void onApplyClicked();

  private:
    ValidationIssue _issue;
    std::shared_ptr<IAssetItem> _asset;
    const QMap<QString, QList<std::shared_ptr<NoteNode>>>* _notesLookup = nullptr;
    const QList<ConditionFormatter::RangeEntry>* _rangeCache = nullptr;

    bool _showDecimal = false;
    bool _showAliases = false;
    bool _hasValidFix = false;

    QString _resultingLogic;
    QString _beforeText;
    QString _afterText;

    // Logic preview slices (for InvertChain, ClearResetHitCount, RemoveUselessAndNext)
    AchievementConditionGroup _beforeGroup;
    AchievementConditionGroup _afterGroup;
    int _chainStartIndex = -1;
    int _chainEndIndex = -1;
    QString _targetLocationLabel;

    QLabel* _lblSummary = nullptr;
    QLabel* _lblExplanation = nullptr;
    QTableWidget* _tblBefore = nullptr;
    QTableWidget* _tblAfter = nullptr;
    QCheckBox* _chkAliases = nullptr;
    QCheckBox* _chkDecimal = nullptr;
    QLineEdit* _txtBeforeText = nullptr;
    QLineEdit* _txtAfterText = nullptr;
    QPushButton* _btnApply = nullptr;
    QPushButton* _btnCancel = nullptr;

    void InitializeUI();
    void BuildFixPreview();
    void PopulateLogicTables();
    void PopulateSingleLogicTable(QTableWidget* table, const AchievementConditionGroup& group,
                                  const AchievementConditionGroup& compareGroup) const;
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
