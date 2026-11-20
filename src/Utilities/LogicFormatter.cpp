#include "LogicFormatter.h"

#include "../Definitions/RaLogicSyntax.h"

#include <QStringBuilder>
#include <QtEndian>

#include <cstring>

namespace RALE {

namespace {

// Matches .NET's long.ToString("xN"): negative values are rendered as their
// two's-complement hex representation instead of putting a '-' after "0x".
QString ToHexString(int64_t value, int padding) {
    QString hex = QString::number(static_cast<quint64>(value), 16);
    if (hex.length() < padding)
        hex = hex.rightJustified(padding, '0');
    return hex;
}

} // namespace

QString LogicFormatter::ConditionToString(const AchievementCondition& cond) {
    QString sb;
    if (!cond.Flag.isEmpty() && RaLogicSyntax::ReverseFlagMap.contains(cond.Flag)) {
        sb += RaLogicSyntax::ReverseFlagMap.value(cond.Flag);
        sb += ':';
    }
    sb += OperandToString(cond.LeftOperand);
    sb += cond.Operator;
    sb += OperandToString(cond.RightOperand);
    if (cond.RequiredHits > 0) {
        sb += '.';
        sb += QString::number(cond.RequiredHits);
        sb += '.';
    }
    return sb;
}

QString LogicFormatter::OperandToString(const Operand& op) {
    if (op.Type.isEmpty())
        return "";
    if (op.Type.compare("Recall", Qt::CaseInsensitive) == 0)
        return "{recall}";

    QString sb;
    QString core = op.Type;
    bool pfx = false;

    if (op.Type == "Delta") {
        sb += 'd';
        pfx = true;
    } else if (op.Type == "Prior") {
        sb += 'p';
        pfx = true;
    } else if (op.Type == "BCD") {
        sb += 'b';
        pfx = true;
    } else if (op.Type == "Inverted") {
        sb += '~';
        pfx = true;
    } else if (op.Type == "Float") {
        // Fix: Ensure float values always have a decimal point for RA integration (e.g. 81 -> f81.0)
        QString fVal = op.Value;
        if (!fVal.contains('.'))
            fVal += ".0";
        return "f" + fVal;
    }

    if (pfx)
        core = "Mem";

    if (core == "Mem") {
        if (RaLogicSyntax::ReverseFloatSizeMap.contains(op.Size)) {
            sb += "f";
            sb += RaLogicSyntax::ReverseFloatSizeMap.value(op.Size);
            QString cleanVal = op.Value;
            cleanVal.remove("0x", Qt::CaseInsensitive);
            sb += cleanVal;
        } else {
            sb += "0x";
            if (RaLogicSyntax::ReverseSizeMap.contains(op.Size)) {
                QChar s = RaLogicSyntax::ReverseSizeMap.value(op.Size);
                if (s != ' ')
                    sb += s.toUpper();
            }
            QString cleanVal = op.Value;
            cleanVal.remove("0x", Qt::CaseInsensitive);
            sb += cleanVal;
        }
    } else if (core == "Value") {
        // Robustly parse value as Hex (even if missing 0x prefix) and convert to Decimal for RA logic string.
        // This prevents issues where "ffffffff" is interpreted as Float by RAIntegration because it starts with 'f'.
        QString cleanVal = op.Value.trimmed();
        if (cleanVal.startsWith("0x", Qt::CaseInsensitive))
            cleanVal = cleanVal.mid(2);

        bool ok = false;
        int64_t v = cleanVal.toLongLong(&ok, 16);
        if (ok)
            return QString::number(v);

        return op.Value;
    } else {
        sb += op.Value;
    }

    return sb;
}

QString LogicFormatter::FormatDisplayValue(const Operand& operand, bool showDecimal, const QString& sizeReference) {
    if (operand.Value.isEmpty())
        return "";

    QString cleanHex = operand.Value;
    cleanHex.remove("0x", Qt::CaseInsensitive);
    bool ok = false;
    int64_t val = cleanHex.toLongLong(&ok, 16);

    if (ok) {
        if (operand.Type == "Value") {
            if (showDecimal)
                return QString::number(val);
            int padding = GetPaddingForSize(sizeReference);
            int64_t mask = GetMaskForSize(sizeReference);

            // Apply mask only if valid mask (not -1)
            int64_t displayVal = (mask != -1) ? (val & mask) : val;

            return "0x" + ToHexString(displayVal, padding);
        } else if (operand.Type != "Float") {
            return "0x" + ToHexString(val, 8);
        }
    }

    if (operand.Type == "Float") {
        QString normalized = operand.Value;
        normalized.replace(',', '.');
        bool floatOk = false;
        double decVal = normalized.toDouble(&floatOk);
        if (floatOk && (decVal == static_cast<int64_t>(decVal)) && !normalized.contains('.')) {
            return normalized + ".0";
        }
        return normalized;
    }

    return operand.Value;
}

QString LogicFormatter::ParseAndFormatValue(const QString& input, const QString& operandType, const QString& size) {
    if (input.trimmed().isEmpty())
        return "";

    if (operandType == "Float")
        return QString(input).replace(',', '.');

    if (operandType == "Mem" && RaLogicSyntax::ReverseFloatSizeMap.contains(size)) {
        // Only treat as floating point literal if it explicitly contains a decimal separator
        if (input.contains('.') || input.contains(',')) {
            QString safeInput = input;
            safeInput.replace(',', '.');
            bool floatOk = false;
            float floatVal = safeInput.toFloat(&floatOk);
            if (floatOk) {
                uint32_t uintVal = 0;
                std::memcpy(&uintVal, &floatVal, sizeof(float));
                if (size.endsWith("BE", Qt::CaseInsensitive)) {
                    uintVal = qbswap(uintVal);
                }
                return QString("0x%1").arg(uintVal, 8, 16, QChar('0'));
            }
        }

        // Standard memory addresses: parse hex without float corruption
        QString cleanHex = input.trimmed();
        if (cleanHex.startsWith("0x", Qt::CaseInsensitive))
            cleanHex = cleanHex.mid(2);

        bool hexOk = false;
        uint32_t hexVal = cleanHex.toUInt(&hexOk, 16);
        if (hexOk)
            return QString("0x%1").arg(hexVal, 8, 16, QChar('0'));

        return input;
    }

    int64_t val = 0;
    int padding = 8;

    if (operandType == "Mem" || operandType == "Delta" || operandType == "Prior" || operandType == "BCD" || operandType == "Inverted") {
        QString hexInput = input.startsWith("0x", Qt::CaseInsensitive) ? input.mid(2) : input;
        // .NET's NumberStyles.HexNumber rejects a leading sign; keep the raw input in that case.
        if (hexInput.contains('-'))
            return input;
        bool ok = false;
        val = hexInput.toLongLong(&ok, 16);
        if (!ok)
            return input;
        padding = 8;
    } else if (operandType == "Value") {
        if (input.startsWith("0x", Qt::CaseInsensitive)) {
            bool ok = false;
            val = input.mid(2).toLongLong(&ok, 16);
            if (!ok)
                return input;
        } else {
            bool ok = false;
            val = input.toLongLong(&ok, 10);
            if (!ok) {
                // Fallback: Try parsing as Hex without 0x prefix (e.g. "ffffffff")
                // This fixes inputs that look like floats ("f...") from breaking RA parsing
                val = input.toLongLong(&ok, 16);
                if (ok) {
                    // Success parsing as hex
                } else {
                    return input;
                }
            }
        }
        padding = GetPaddingForSize(size);
    } else {
        return input;
    }

    return "0x" + ToHexString(val, padding);
}

QString LogicFormatter::GetRightOperandSizeReference(const AchievementCondition& cond) {
    if (RaLogicSyntax::ArithmeticOperators.contains(cond.Operator) && cond.Operator != RaLogicSyntax::NO_OPERATOR_TEXT)
        return "32-bit";
    return cond.LeftOperand.Size;
}

int LogicFormatter::GetPaddingForSize(const QString& size) {
    if (size.contains("8-bit") || size.contains("Bit") || size.contains("4"))
        return 2;
    if (size.contains("16-bit"))
        return 4;
    if (size.contains("24-bit"))
        return 6;
    return 8;
}

int64_t LogicFormatter::GetMaskForSize(const QString& size) {
    if (size.contains("8-bit") || size.contains("Bit") || size.contains("4"))
        return 0xFF;
    if (size.contains("16-bit"))
        return 0xFFFF;
    if (size.contains("24-bit"))
        return 0xFFFFFF;
    return -1;
}

// New helper for consistent dictionary keys (e.g. "0x001234" -> "0x1234")
QString LogicFormatter::NormalizeAddress(const QString& address) {
    if (address.trimmed().isEmpty())
        return "";
    QString clean = address.trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2);

    bool ok = false;
    int64_t val = clean.toLongLong(&ok, 16);
    if (ok) {
        return QString("0x%1").arg(val, 0, 16);
    }
    // Fallback if it's not valid hex (shouldn't happen often in this context)
    return "0x" + clean.toLower();
}

} // namespace RALE
