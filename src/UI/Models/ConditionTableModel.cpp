#include "ConditionTableModel.h"
#include "../../Definitions/RaLogicSyntax.h"
#include "../../Utilities/LogicFormatter.h"
#include "../Styles/ThemeManager.h"
#include <QColor>
#include <QRegularExpression>
#include <cstring>

namespace RALogicEditor {

ConditionTableModel::ConditionTableModel(QObject *parent)
    : QAbstractTableModel(parent)
{
}

void ConditionTableModel::RebuildVisualMap()
{
    _visualToModelMap.clear();
    if (_group == nullptr) return;

    for (int i = 0; i < _group->Conditions.size(); i++)
    {
        // Renumber conditions sequentially so every condition has a unique ID, matching WinForms
        _group->Conditions[i].ID = i + 1;

        // C# zeroes hit counts for flags that cannot carry them while rendering; mirror
        // it so the stale hits disappear from the emitted logic too.
        if (RaLogicSyntax::FlagsWithoutHits.contains(_group->Conditions.at(i).Flag))
            _group->Conditions[i].RequiredHits = 0;

        if (_collapseChains && _group->Conditions.at(i).Flag == "Add Address")
            continue;

        _visualToModelMap.append(i);
    }
}

void ConditionTableModel::SetGroup(
    AchievementConditionGroup *group,
    bool readOnly,
    bool showDecimal,
    bool showAliases,
    bool collapseChains,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>> *notesLookup,
    const QList<ConditionFormatter::RangeEntry> *rangeCache,
    const QList<ValidationIssue> *issues)
{
    beginResetModel();
    _group = group;
    _readOnly = readOnly;
    _showDecimal = showDecimal;
    _showAliases = showAliases;
    _collapseChains = collapseChains;
    _notesLookup = notesLookup;
    _rangeCache = rangeCache;
    _issues = issues;

    RebuildVisualMap();
    endResetModel();
}

void ConditionTableModel::SetIssues(const QList<ValidationIssue> *issues)
{
    _issues = issues;
    if (_group != nullptr && !_visualToModelMap.isEmpty())
    {
        emit dataChanged(index(0, ColID), index(_visualToModelMap.size() - 1, ColID));
    }
}

int ConditionTableModel::rowCount(const QModelIndex &parent) const
{
    if (parent.isValid() || _group == nullptr) return 0;
    return _visualToModelMap.size();
}

int ConditionTableModel::columnCount(const QModelIndex &parent) const
{
    if (parent.isValid()) return 0;
    return ColumnCount;
}

AchievementCondition* ConditionTableModel::GetConditionAtRow(int visualRow)
{
    if (_group == nullptr || visualRow < 0 || visualRow >= _visualToModelMap.size())
        return nullptr;
    int modelIdx = _visualToModelMap[visualRow];
    if (modelIdx >= 0 && modelIdx < _group->Conditions.size())
        return &_group->Conditions[modelIdx];
    return nullptr;
}

int ConditionTableModel::GetModelIndexForVisualRow(int visualRow) const
{
    if (visualRow >= 0 && visualRow < _visualToModelMap.size())
        return _visualToModelMap[visualRow];
    return -1;
}

int ConditionTableModel::GetVisualRowForCondition(const AchievementCondition *cond) const
{
    if (_group == nullptr || cond == nullptr) return -1;
    for (int v = 0; v < _visualToModelMap.size(); ++v)
    {
        int m = _visualToModelMap[v];
        if (m >= 0 && m < _group->Conditions.size())
        {
            if (&_group->Conditions.at(m) == cond || _group->Conditions.at(m) == *cond)
                return v;
        }
    }
    return -1;
}

std::optional<ValidationIssue> ConditionTableModel::GetIssueForCondition(const AchievementCondition &cond) const
{
    if (_issues == nullptr) return std::nullopt;

    std::optional<ValidationIssue> best = std::nullopt;
    for (const auto &iss : *_issues)
    {
        if (iss.Target.has_value() && iss.Target.value() == cond)
        {
            if (!best.has_value() || iss.Severity > best->Severity)
                best = iss;
        }
    }
    return best;
}

QVariant ConditionTableModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || _group == nullptr || index.row() < 0 || index.row() >= _visualToModelMap.size())
        return QVariant();

    int modelIdx = _visualToModelMap[index.row()];
    if (modelIdx < 0 || modelIdx >= _group->Conditions.size())
        return QVariant();

    const auto &cond = _group->Conditions.at(modelIdx);

    if (role == Qt::DisplayRole)
    {
        switch (index.column())
        {
            case ColID: return modelIdx + 1;
            case ColFlag: return cond.Flag;
            case ColLType: return cond.LeftOperand.Type;
            case ColLSize: return cond.LeftOperand.Size;
            case ColLValue:
                return ConditionFormatter::FormatOperandDisplay(
                    cond.LeftOperand, _showDecimal, _showAliases, _notesLookup, _rangeCache, _group, modelIdx);
            case ColCmp: return cond.Operator;
            case ColRType: return cond.RightOperand.Type;
            case ColRSize: return cond.RightOperand.Size;
            case ColRValue:
            {
                QString rightSizeRef = LogicFormatter::GetRightOperandSizeReference(cond);
                return ConditionFormatter::FormatOperandDisplay(
                    cond.RightOperand, _showDecimal, _showAliases, _notesLookup, _rangeCache, _group, modelIdx, rightSizeRef);
            }
            case ColHits:
                if (RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag)) return "";
                return (cond.RequiredHits > 0) ? QString::number(cond.RequiredHits) : "";
        }
    }
    else if (role == Qt::EditRole)
    {
        switch (index.column())
        {
            case ColID: return modelIdx + 1;
            case ColFlag: return cond.Flag.isEmpty() ? RaLogicSyntax::NO_FLAG_TEXT : cond.Flag;
            case ColLType: return cond.LeftOperand.Type;
            case ColLSize: return cond.LeftOperand.Size;
            case ColLValue:
                if (cond.LeftOperand.Type == "Value")
                {
                    return ConditionFormatter::FormatOperandDisplay(
                        cond.LeftOperand, _showDecimal, false, nullptr, nullptr, nullptr, -1);
                }
                return cond.LeftOperand.Value;
            case ColCmp: return cond.Operator.isEmpty() ? RaLogicSyntax::NO_OPERATOR_TEXT : cond.Operator;
            case ColRType: return cond.RightOperand.Type;
            case ColRSize: return cond.RightOperand.Size;
            case ColRValue:
                if (cond.RightOperand.Type == "Value")
                {
                    QString rightSizeRef = LogicFormatter::GetRightOperandSizeReference(cond);
                    return ConditionFormatter::FormatOperandDisplay(
                        cond.RightOperand, _showDecimal, false, nullptr, nullptr, nullptr, -1, rightSizeRef);
                }
                return cond.RightOperand.Value;
            case ColHits:
                if (RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag)) return "";
                return (cond.RequiredHits > 0) ? QString::number(cond.RequiredHits) : "";
        }
    }
    else if (role == Qt::TextAlignmentRole)
    {
        if (index.column() == ColCmp || index.column() == ColHits)
            return static_cast<int>(Qt::AlignCenter);
        return static_cast<int>(Qt::AlignLeft | Qt::AlignVCenter);
    }
    else if (role == Qt::BackgroundRole)
    {
        bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
        QColor staticColor = isDark ? QColor(45, 45, 48) : QColor(227, 227, 227); // #2d2d30 in dark mode, #E3E3E3 in light mode

        // 1. Issue Highlighting for ID column
        if (index.column() == ColID)
        {
            auto issue = GetIssueForCondition(cond);
            if (issue.has_value())
            {
                if (issue->Severity == FeedbackSeverity::Error)
                    return isDark ? QColor(90, 30, 30) : QColor(255, 220, 220); // Soft red
                else if (issue->Severity == FeedbackSeverity::Warning)
                    return isDark ? QColor(90, 80, 30) : QColor(255, 245, 200); // Soft yellow
            }
            return staticColor; // SystemColors.ControlLight
        }

        // 2. Implicit size warning for Size columns (LemonChiffon)
        if (index.column() == ColLSize && cond.LeftOperand.Size.isEmpty() &&
            RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type))
        {
            return _readOnly ? (isDark ? QColor(60, 60, 45) : QColor(240, 240, 220))
                             : (isDark ? QColor(80, 80, 40) : QColor(255, 250, 205)); // LemonChiffon
        }
        if (index.column() == ColRSize && cond.RightOperand.Size.isEmpty() &&
            RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type))
        {
            return _readOnly ? (isDark ? QColor(60, 60, 45) : QColor(240, 240, 220))
                             : (isDark ? QColor(80, 80, 40) : QColor(255, 250, 205));
        }

        // 3. Static/disabled cells (SystemColors.ControlLight)
        if (index.column() == ColLSize && (cond.LeftOperand.Type == "Float" || cond.LeftOperand.Type == "Value" || cond.LeftOperand.Type == "Recall"))
            return staticColor;

        if (index.column() == ColLValue && cond.LeftOperand.Type == "Recall")
            return staticColor;

        if (cond.Operator.isEmpty() && (index.column() == ColCmp || index.column() == ColRType || index.column() == ColRSize || index.column() == ColRValue))
            return staticColor;

        if (index.column() == ColRSize && (cond.RightOperand.Type == "Float" || cond.RightOperand.Type == "Value" || cond.RightOperand.Type == "Recall"))
            return staticColor;

        if (index.column() == ColRValue && cond.RightOperand.Type == "Recall")
            return staticColor;

        if (index.column() == ColHits && RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag))
            return staticColor;

        return QVariant();
    }
    else if (role == Qt::ToolTipRole)
    {
        if (index.column() == ColLSize && cond.LeftOperand.Size.isEmpty() &&
            RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type))
        {
            return "Implicit 32-bit";
        }
        if (index.column() == ColRSize && cond.RightOperand.Size.isEmpty() &&
            RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type))
        {
            return "Implicit 32-bit";
        }

        // Code-note description tooltip for memory cells (C# Grid_CellToolTipTextNeeded).
        if ((index.column() == ColLValue || index.column() == ColRValue) && _notesLookup != nullptr)
        {
            const Operand &op = (index.column() == ColLValue) ? cond.LeftOperand : cond.RightOperand;
            if (RaLogicSyntax::MemoryTypes.contains(op.Type))
            {
                static const QRegularExpression tooltipTagRegex(R"(\[.*?\])");
                auto CleanDescription = [](const QString &desc) {
                    QString clean = desc;
                    clean.remove(tooltipTagRegex);
                    return clean.trimmed();
                };

                QString addr = op.Value.trimmed();
                if (!addr.startsWith("0x", Qt::CaseInsensitive)) addr = "0x" + addr;
                QString key = LogicFormatter::NormalizeAddress(addr);

                if (_notesLookup->contains(key))
                {
                    const auto &nodes = _notesLookup->value(key);
                    std::shared_ptr<NoteNode> root = nullptr;
                    for (const auto &n : nodes)
                    {
                        if (n->IndentLevel == -2) { root = n; break; }
                    }
                    if (root == nullptr && !nodes.isEmpty()) root = nodes.first();
                    if (root != nullptr)
                    {
                        QString desc = root->Description;
                        if (desc.isEmpty())
                        {
                            QString norm = root->Content;
                            norm.replace("\r\n", "\n").replace('\r', '\n');
                            desc = norm.split('\n', Qt::SkipEmptyParts).value(0, "");
                        }
                        desc = CleanDescription(desc);
                        if (!desc.isEmpty()) return desc;
                    }
                }
                else if (_rangeCache != nullptr)
                {
                    QString clean = key;
                    clean.remove("0x", Qt::CaseInsensitive);
                    bool ok = false;
                    int64_t targetVal = clean.toLongLong(&ok, 16);
                    if (ok)
                    {
                        for (const auto &range : *_rangeCache)
                        {
                            if (targetVal < range.Start || targetVal >= range.End || range.Node == nullptr)
                                continue;
                            QString desc = range.Node->Description;
                            if (desc.isEmpty())
                            {
                                QString norm = range.Node->Content;
                                norm.replace("\r\n", "\n").replace('\r', '\n');
                                desc = norm.split('\n', Qt::SkipEmptyParts).value(0, "");
                            }
                            desc = CleanDescription(desc);
                            int64_t delta = targetVal - range.Start;
                            if (delta > 0) desc += QString(" +0x%1").arg(QString::number(delta, 16).toUpper());
                            if (!desc.isEmpty()) return desc;
                            break;
                        }
                    }
                }
            }
        }
    }

    return QVariant();
}

void ConditionTableModel::HandleFlagChange(AchievementCondition &cond, const QString &oldFlag)
{
    bool wasA = RaLogicSyntax::ArithmeticFlags.contains(oldFlag);
    bool isA = RaLogicSyntax::ArithmeticFlags.contains(cond.Flag);
    if (wasA && !isA)
    {
        cond.Operator = "=";
        cond.RightOperand = Operand{ "Value", "", "0x1" };
    }
    else if (!wasA && isA)
    {
        cond.Operator = "";
        cond.RightOperand = Operand();
    }

    if (RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag))
    {
        cond.RequiredHits = 0;
    }
}

void ConditionTableModel::HandleOperatorChange(AchievementCondition &cond, const QString &oldOp)
{
    bool wasE = oldOp.isEmpty();
    bool isE = cond.Operator.isEmpty();
    if (wasE && !isE)
    {
        cond.RightOperand = Operand{ "Value", "", "0x1" };
    }
    else if (!wasE && isE)
    {
        cond.RightOperand = Operand();
    }
}

void ConditionTableModel::HandleTypeChange(Operand &op, const QString &newType, const QString &refSize, const Operand *copyFrom)
{
    QString oldType = op.Type;
    if (oldType == newType) return;

    if (copyFrom != nullptr && (newType == "Delta" || newType == "Prior"))
    {
        op.Value = copyFrom->Value;
        op.Size = copyFrom->Size;
        op.Type = newType;
        return;
    }

    if (newType == "Recall")
    {
        op.Value = "";
        op.Type = newType;
        return;
    }

    if (op.Value.isEmpty() || oldType == "Recall")
    {
        if (newType == "Float") op.Value = "1.0";
        else op.Value = QString("0x%1").arg(1, LogicFormatter::GetPaddingForSize(refSize), 16, QChar('0'));
    }
    else
    {
        static const QSet<QString> integerLike = { "Mem", "Value", "Delta", "Prior", "BCD", "Inverted" };
        if (integerLike.contains(oldType) && newType == "Float")
        {
            QString clean = op.Value;
            clean.remove("0x", Qt::CaseInsensitive);
            bool ok = false;
            uint32_t hexVal = clean.toUInt(&ok, 16);
            if (ok)
            {
                float f = 0.0f;
                std::memcpy(&f, &hexVal, sizeof(float));
                op.Value = QString::number(f, 'g', 9);
            }
        }
        else if (oldType == "Float" && integerLike.contains(newType))
        {
            QString safeVal = op.Value;
            safeVal.replace(',', '.');
            bool ok = false;
            float floatVal = safeVal.toFloat(&ok);
            if (floatVal != 0.0f || ok)
            {
                uint32_t uintVal = 0;
                std::memcpy(&uintVal, &floatVal, sizeof(float));
                op.Value = QString("0x%1").arg(uintVal, 8, 16, QChar('0'));
            }
        }
    }
    op.Type = newType;
}

void ConditionTableModel::ApplyConditionLogicRules(AchievementCondition &cond)
{
    if (cond.LeftOperand.Type == "Value" || cond.LeftOperand.Type == "Float" || cond.LeftOperand.Type == "Recall")
        cond.LeftOperand.Size = "";
    else if (RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type) && cond.LeftOperand.Size.isEmpty())
        cond.LeftOperand.Size = "32-bit";

    if (cond.RightOperand.Type == "Value" || cond.RightOperand.Type == "Float" || cond.RightOperand.Type == "Recall")
        cond.RightOperand.Size = "";
    else if (RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type) && cond.RightOperand.Size.isEmpty())
        cond.RightOperand.Size = "32-bit";

    if (cond.RightOperand.Type == "Value")
        cond.RightOperand.Size = "";

    if (RaLogicSyntax::StrictComparisonFlags.contains(cond.Flag))
    {
        if (!RaLogicSyntax::ComparisonOperators.contains(cond.Operator) || cond.Operator.isEmpty())
        {
            cond.Operator = "=";
            cond.RightOperand = Operand{ "Value", "", "0x1" };
        }
    }
    else if (RaLogicSyntax::ArithmeticFlags.contains(cond.Flag))
    {
        if (RaLogicSyntax::ComparisonOperators.contains(cond.Operator))
        {
            cond.Operator = "";
            cond.RightOperand = Operand();
        }
    }

    if (RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag))
    {
        cond.RequiredHits = 0;
    }
}

bool ConditionTableModel::setData(const QModelIndex &index, const QVariant &value, int role)
{
    if (!index.isValid() || _group == nullptr || role != Qt::EditRole || _readOnly)
        return false;

    int modelIdx = _visualToModelMap[index.row()];
    auto &cond = _group->Conditions[modelIdx];
    QString str = value.toString();

    switch (index.column())
    {
        case ColFlag:
        {
            QString oldFlag = cond.Flag;
            cond.Flag = (str == RaLogicSyntax::NO_FLAG_TEXT) ? "" : str;
            HandleFlagChange(cond, oldFlag);
            ApplyConditionLogicRules(cond);
            emit dataChanged(this->index(index.row(), 0), this->index(index.row(), ColumnCount - 1));
            emit ConditionDataChanged();
            return true;
        }
        case ColLType:
        {
            HandleTypeChange(cond.LeftOperand, str, cond.LeftOperand.Size);
            ApplyConditionLogicRules(cond);
            emit dataChanged(this->index(index.row(), 0), this->index(index.row(), ColumnCount - 1));
            emit ConditionDataChanged();
            return true;
        }
        case ColLSize:
            cond.LeftOperand.Size = str;
            ApplyConditionLogicRules(cond);
            break;
        case ColLValue:
            cond.LeftOperand.Value = LogicFormatter::ParseAndFormatValue(str, cond.LeftOperand.Type, cond.LeftOperand.Size);
            // Materialises an implicit memory size and re-renders the value column
            // with the (possibly corrected) size reference, like C# CellEndEdit.
            ApplyConditionLogicRules(cond);
            break;
        case ColCmp:
        {
            QString oldOp = cond.Operator;
            cond.Operator = (str == RaLogicSyntax::NO_OPERATOR_TEXT) ? "" : str;
            HandleOperatorChange(cond, oldOp);
            ApplyConditionLogicRules(cond);
            emit dataChanged(this->index(index.row(), 0), this->index(index.row(), ColumnCount - 1));
            emit ConditionDataChanged();
            return true;
        }
        case ColRType:
        {
            HandleTypeChange(cond.RightOperand, str, cond.LeftOperand.Size, &cond.LeftOperand);
            ApplyConditionLogicRules(cond);
            emit dataChanged(this->index(index.row(), 0), this->index(index.row(), ColumnCount - 1));
            emit ConditionDataChanged();
            return true;
        }
        case ColRSize:
            cond.RightOperand.Size = str;
            ApplyConditionLogicRules(cond);
            break;
        case ColRValue:
            cond.RightOperand.Value = LogicFormatter::ParseAndFormatValue(str, cond.RightOperand.Type, LogicFormatter::GetRightOperandSizeReference(cond));
            ApplyConditionLogicRules(cond);
            break;
        case ColHits:
        {
            static const QRegularExpression hitDigits(R"(\d+)");
            auto match = hitDigits.match(str);
            cond.RequiredHits = match.hasMatch() ? match.captured(0).toUInt() : 0;
            break;
        }
        default:
            return false;
    }

    // Re-render the whole row: value display depends on size, and logic rules may
    // have changed several operands at once.
    emit dataChanged(this->index(index.row(), 0), this->index(index.row(), ColumnCount - 1));
    emit ConditionDataChanged();
    return true;
}

void ConditionTableModel::BulkUpdateConditions(const QList<int> &visualRows, int column, const QString &value)
{
    if (_group == nullptr || visualRows.isEmpty() || _readOnly) return;

    for (int vRow : visualRows)
    {
        if (vRow < 0 || vRow >= _visualToModelMap.size()) continue;
        int mIdx = _visualToModelMap[vRow];
        auto &cond = _group->Conditions[mIdx];

        if (column == ColFlag)
        {
            QString oldFlag = cond.Flag;
            cond.Flag = value;
            HandleFlagChange(cond, oldFlag);
        }
        else if (column == ColLType)
        {
            HandleTypeChange(cond.LeftOperand, value, cond.LeftOperand.Size);
        }
        else if (column == ColLSize)
        {
            cond.LeftOperand.Size = value;
        }
        else if (column == ColCmp)
        {
            QString oldOp = cond.Operator;
            cond.Operator = value;
            HandleOperatorChange(cond, oldOp);
        }
        else if (column == ColRType)
        {
            HandleTypeChange(cond.RightOperand, value, cond.LeftOperand.Size, &cond.LeftOperand);
        }
        else if (column == ColRSize)
        {
            cond.RightOperand.Size = value;
        }

        ApplyConditionLogicRules(cond);
    }

    emit dataChanged(index(0, 0), index(rowCount() - 1, columnCount() - 1));
    emit ConditionDataChanged();
}

QVariant ConditionTableModel::headerData(int section, Qt::Orientation orientation, int role) const
{
    if (orientation == Qt::Horizontal)
    {
        if (role == Qt::DisplayRole)
        {
            switch (section)
            {
                case ColID: return "ID";
                case ColFlag: return "Flag";
                case ColLType: return "Type";
                case ColLSize: return "Size";
                case ColLValue: return "Memory";
                case ColCmp: return "Cmp";
                case ColRType: return "Type";
                case ColRSize: return "Size";
                case ColRValue: return "Mem/Val";
                case ColHits: return "Hits";
            }
        }
        else if (role == Qt::TextAlignmentRole)
        {
            return static_cast<int>(Qt::AlignLeft | Qt::AlignVCenter);
        }
    }
    return QAbstractTableModel::headerData(section, orientation, role);
}

Qt::ItemFlags ConditionTableModel::flags(const QModelIndex &index) const
{
    if (!index.isValid() || _group == nullptr || index.row() < 0 || index.row() >= _visualToModelMap.size())
        return Qt::NoItemFlags;

    int modelIdx = _visualToModelMap[index.row()];
    if (modelIdx < 0 || modelIdx >= _group->Conditions.size())
        return Qt::NoItemFlags;

    if (_readOnly) return Qt::ItemIsEnabled | Qt::ItemIsSelectable;

    const auto &cond = _group->Conditions.at(modelIdx);

    if (index.column() == ColLSize)
    {
        if (cond.LeftOperand.Type == "Value" || cond.LeftOperand.Type == "Float" || cond.LeftOperand.Type == "Recall")
            return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColLValue)
    {
        if (cond.LeftOperand.Type == "Recall") return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        if (_showAliases && RaLogicSyntax::MemoryTypes.contains(cond.LeftOperand.Type))
            return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColCmp)
    {
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColRType)
    {
        if (cond.Operator.isEmpty()) return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColRSize)
    {
        if (cond.Operator.isEmpty() || cond.RightOperand.Type == "Value" || cond.RightOperand.Type == "Float" || cond.RightOperand.Type == "Recall")
            return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColRValue)
    {
        if (cond.Operator.isEmpty() || cond.RightOperand.Type == "Recall")
            return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        if (_showAliases && RaLogicSyntax::MemoryTypes.contains(cond.RightOperand.Type))
            return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColHits)
    {
        if (RaLogicSyntax::FlagsWithoutHits.contains(cond.Flag))
            return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    if (index.column() == ColFlag || index.column() == ColLType)
    {
        return Qt::ItemIsEnabled | Qt::ItemIsSelectable | Qt::ItemIsEditable;
    }

    return Qt::ItemIsEnabled | Qt::ItemIsSelectable;
}

} // namespace RALogicEditor
