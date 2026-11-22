#pragma once

#include "../../Common/ConditionFormatter.h"
#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../../Models/ValidationModels.h"

#include <QAbstractTableModel>
#include <QList>
#include <QMap>

#include <memory>
#include <optional>

namespace RALE {

class ConditionTableModel : public QAbstractTableModel {
    Q_OBJECT

  public:
    enum Column {
        ColID = 0,
        ColFlag = 1,
        ColLType = 2,
        ColLSize = 3,
        ColLValue = 4,
        ColCmp = 5,
        ColRType = 6,
        ColRSize = 7,
        ColRValue = 8,
        ColHits = 9,
        ColumnCount = 10
    };

    explicit ConditionTableModel(QObject* parent = nullptr);

    int rowCount(const QModelIndex& parent = QModelIndex()) const override;
    int columnCount(const QModelIndex& parent = QModelIndex()) const override;
    QVariant data(const QModelIndex& index, int role = Qt::DisplayRole) const override;
    bool setData(const QModelIndex& index, const QVariant& value, int role = Qt::EditRole) override;
    QVariant headerData(int section, Qt::Orientation orientation, int role = Qt::DisplayRole) const override;
    Qt::ItemFlags flags(const QModelIndex& index) const override;

    void SetGroup(AchievementConditionGroup* group, bool readOnly, bool showDecimal, bool showAliases, bool collapseChains,
                  const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                  const QList<ConditionFormatter::RangeEntry>* rangeCache, const QList<ValidationIssue>* issues = nullptr,
                  const QString& segmentName = "", int groupIndex = -1);

    void SetIssues(const QList<ValidationIssue>* issues, const QString& segmentName = "", int groupIndex = -1);

    AchievementCondition* GetConditionAtRow(int visualRow);
    int GetModelIndexForVisualRow(int visualRow) const;
    int GetVisualRowForCondition(const AchievementCondition* cond) const;

    // Helper to support bulk column context menu updates across selected visual rows
    void BulkUpdateConditions(const QList<int>& visualRows, int column, const QString& value);

  signals:
    void BeforeConditionDataChanged();
    void ConditionDataChanged();

  private:
    AchievementConditionGroup* _group = nullptr;
    bool _readOnly = false;
    bool _showDecimal = false;
    bool _showAliases = false;
    bool _collapseChains = false;
    QString _segmentName = "";
    int _groupIndex = -1;

    const QMap<QString, QList<std::shared_ptr<NoteNode>>>* _notesLookup = nullptr;
    const QList<ConditionFormatter::RangeEntry>* _rangeCache = nullptr;
    const QList<ValidationIssue>* _issues = nullptr;

    QList<int> _visualToModelMap; // Maps visual row to group->Conditions index

    void RebuildVisualMap();
    std::optional<ValidationIssue> GetIssueForCondition(const AchievementCondition& cond) const;
    QList<ValidationIssue> GetIssuesForCondition(const AchievementCondition& cond) const;

    void HandleFlagChange(AchievementCondition& cond, const QString& oldFlag);
    void HandleOperatorChange(AchievementCondition& cond, const QString& oldOp);
    void HandleTypeChange(Operand& op, const QString& newType, const QString& refSize, const Operand* copyFrom = nullptr);
    void ApplyConditionLogicRules(AchievementCondition& cond);
};

} // namespace RALE
