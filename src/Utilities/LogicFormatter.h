#pragma once

#include <QString>
#include "../Models/LogicModels.h"

namespace RALE {

class LogicFormatter
{
public:
    static QString ConditionToString(const AchievementCondition &cond);
    static QString OperandToString(const Operand &op);
    static QString FormatDisplayValue(const Operand &operand, bool showDecimal, const QString &sizeReference = "");
    static QString ParseAndFormatValue(const QString &input, const QString &operandType, const QString &size);
    static QString GetRightOperandSizeReference(const AchievementCondition &cond);
    static int GetPaddingForSize(const QString &size);
    static int64_t GetMaskForSize(const QString &size);

    // New helper for consistent dictionary keys (e.g. "0x001234" -> "0x1234")
    static QString NormalizeAddress(const QString &address);
};

} // namespace RALE
