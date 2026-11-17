#include "RaLogicSyntax.h"

namespace RALogicEditor {

const QString RaLogicSyntax::NO_FLAG_TEXT = "(No Flag)";
const QString RaLogicSyntax::NO_OPERATOR_TEXT = "(None)";

const QStringList RaLogicSyntax::Flags = {
    NO_FLAG_TEXT, "Reset If", "Pause If", "Add Source", "Sub Source", "Add Hits", "Sub Hits",
    "Add Address", "AndNext", "OrNext", "Measured", "MeasuredIf", "Measured%", "Trigger",
    "ResetNextIf", "Remember"
};

const QStringList RaLogicSyntax::OperandTypes = {
    "Mem", "Value", "Delta", "Prior", "BCD", "Inverted", "Float", "Recall"
};

const QStringList RaLogicSyntax::MemorySizes = {
    "", "8-bit", "16-bit", "24-bit", "32-bit", "Bit0", "Bit1", "Bit2", "Bit3", "Bit4",
    "Bit5", "Bit6", "Bit7", "Lower4", "Upper4", "BitCount", "16-bit BE", "24-bit BE",
    "32-bit BE", "Float", "Float BE", "Double32", "Double32 BE", "MBF32", "MBF32 LE"
};

const QStringList RaLogicSyntax::ComparisonOperators = {
    NO_OPERATOR_TEXT, "=", "!=", "<", "<=", ">", ">="
};

const QStringList RaLogicSyntax::ArithmeticOperators = {
    NO_OPERATOR_TEXT, "&", "^", "+", "-", "*", "/", "%"
};

const QSet<QString> RaLogicSyntax::ArithmeticFlags = {
    "Add Source", "Sub Source", "Add Address", "Remember"
};

const QSet<QString> RaLogicSyntax::StrictComparisonFlags = {
    "Reset If", "Pause If", "Add Hits", "Sub Hits", "AndNext", "OrNext",
    "MeasuredIf", "Trigger", "ResetNextIf"
};

// Removed AndNext and OrNext to allow Hit Counts on these flags
const QSet<QString> RaLogicSyntax::FlagsWithoutHits = {
    "Add Source", "Sub Source", "Add Address"
};

const QSet<QString> RaLogicSyntax::MemoryTypes = {
    "Mem", "Delta", "Prior", "BCD", "Inverted"
};

const QMap<QString, QChar> RaLogicSyntax::ReverseFlagMap = {
    { "Reset If", 'R' }, { "Pause If", 'P' }, { "Add Source", 'A' }, { "Sub Source", 'B' },
    { "Add Hits", 'C' }, { "Sub Hits", 'D' }, { "Add Address", 'I' }, { "AndNext", 'N' },
    { "OrNext", 'O' }, { "Measured", 'M' }, { "MeasuredIf", 'Q' }, { "Measured%", 'G' },
    { "Trigger", 'T' }, { "ResetNextIf", 'Z' }, { "Remember", 'K' }
};

const QMap<QString, QChar> RaLogicSyntax::ReverseSizeMap = {
    { "", ' ' }, { "16-bit", ' ' }, { "8-bit", 'H' }, { "32-bit", 'X' }, { "24-bit", 'W' },
    { "Bit0", 'M' }, { "Bit1", 'N' }, { "Bit2", 'O' }, { "Bit3", 'P' }, { "Bit4", 'Q' },
    { "Bit5", 'R' }, { "Bit6", 'S' }, { "Bit7", 'T' }, { "Lower4", 'L' }, { "Upper4", 'U' },
    { "BitCount", 'K' }, { "32-bit BE", 'G' }, { "16-bit BE", 'I' }, { "24-bit BE", 'J' }
};

const QMap<QString, QChar> RaLogicSyntax::ReverseFloatSizeMap = {
    { "Float", 'F' }, { "Float BE", 'B' }, { "Double32", 'H' }, { "Double32 BE", 'I' },
    { "MBF32", 'M' }, { "MBF32 LE", 'L' }
};

const QMap<QChar, QString> RaLogicSyntax::FloatSizeMap = {
    { 'F', "Float" }, { 'B', "Float BE" }, { 'H', "Double32" }, { 'I', "Double32 BE" },
    { 'M', "MBF32" }, { 'L', "MBF32 LE" }
};

} // namespace RALogicEditor
