#include "RaLogicSyntax.h"

#include <QRegularExpression>

namespace RALE {

const QString RaLogicSyntax::NO_FLAG_TEXT = "(No Flag)";
const QString RaLogicSyntax::NO_OPERATOR_TEXT = "(None)";

const QStringList RaLogicSyntax::Flags = {NO_FLAG_TEXT, "Reset If",    "Pause If",    "Add Source", "Sub Source", "Add Hits",
                                          "Sub Hits",   "Add Address", "AndNext",     "OrNext",     "Measured",   "MeasuredIf",
                                          "Measured%",  "Trigger",     "ResetNextIf", "Remember"};

const QStringList RaLogicSyntax::OperandTypes = {"Mem", "Value", "Delta", "Prior", "BCD", "Inverted", "Float", "Recall"};

const QStringList RaLogicSyntax::MemorySizes = {"",         "8-bit",       "16-bit",    "24-bit",    "32-bit",    "Bit0",  "Bit1",
                                                "Bit2",     "Bit3",        "Bit4",      "Bit5",      "Bit6",      "Bit7",  "Lower4",
                                                "Upper4",   "BitCount",    "16-bit BE", "24-bit BE", "32-bit BE", "Float", "Float BE",
                                                "Double32", "Double32 BE", "MBF32",     "MBF32 LE"};

const QStringList RaLogicSyntax::ComparisonOperators = {NO_OPERATOR_TEXT, "=", "!=", "<", "<=", ">", ">="};

const QStringList RaLogicSyntax::ArithmeticOperators = {NO_OPERATOR_TEXT, "&", "^", "+", "-", "*", "/", "%"};

const QSet<QString> RaLogicSyntax::ArithmeticFlags = {"Add Source", "Sub Source", "Add Address", "Remember"};

const QSet<QString> RaLogicSyntax::StrictComparisonFlags = {"Reset If", "Pause If",   "Add Hits", "Sub Hits",   "AndNext",
                                                            "OrNext",   "MeasuredIf", "Trigger",  "ResetNextIf"};

// Removed AndNext and OrNext to allow Hit Counts on these flags
const QSet<QString> RaLogicSyntax::FlagsWithoutHits = {"Add Source", "Sub Source", "Add Address"};

const QSet<QString> RaLogicSyntax::MemoryTypes = {"Mem", "Delta", "Prior", "BCD", "Inverted"};

const QMap<QString, QChar> RaLogicSyntax::ReverseFlagMap = {
    {"Reset If", 'R'},   {"Pause If", 'P'},    {"Add Source", 'A'}, {"Sub Source", 'B'},  {"Add Hits", 'C'},
    {"Sub Hits", 'D'},   {"Add Address", 'I'}, {"AndNext", 'N'},    {"OrNext", 'O'},      {"Measured", 'M'},
    {"MeasuredIf", 'Q'}, {"Measured%", 'G'},   {"Trigger", 'T'},    {"ResetNextIf", 'Z'}, {"Remember", 'K'}};

const QMap<QString, QChar> RaLogicSyntax::ReverseSizeMap = {
    {"", ' '},       {"16-bit", ' '},   {"8-bit", 'H'},     {"32-bit", 'X'},    {"24-bit", 'W'},   {"Bit0", 'M'}, {"Bit1", 'N'},
    {"Bit2", 'O'},   {"Bit3", 'P'},     {"Bit4", 'Q'},      {"Bit5", 'R'},      {"Bit6", 'S'},     {"Bit7", 'T'}, {"Lower4", 'L'},
    {"Upper4", 'U'}, {"BitCount", 'K'}, {"32-bit BE", 'G'}, {"16-bit BE", 'I'}, {"24-bit BE", 'J'}};

const QMap<QString, QChar> RaLogicSyntax::ReverseFloatSizeMap = {{"Float", 'F'},       {"Float BE", 'B'}, {"Double32", 'H'},
                                                                 {"Double32 BE", 'I'}, {"MBF32", 'M'},    {"MBF32 LE", 'L'}};

const QMap<QChar, QString> RaLogicSyntax::FloatSizeMap = {{'F', "Float"},       {'B', "Float BE"}, {'H', "Double32"},
                                                          {'I', "Double32 BE"}, {'M', "MBF32"},    {'L', "MBF32 LE"}};

QString RaLogicSyntax::GetPlatformPointerTemplate(int consoleId, const QString& noteHeader) {
    // If the header already explicitly declares a pointer structure tag, preserve it verbatim
    if (!noteHeader.isEmpty()) {
        static const QRegularExpression ptrTagRx(R"(\[([^\]]*pointer[^\]]*)\])", QRegularExpression::CaseInsensitiveOption);
        auto match = ptrTagRx.match(noteHeader);
        if (match.hasMatch()) {
            return QString("[%1]").arg(match.captured(1).trimmed());
        }
    }

    switch (consoleId) {
    case 16: // GameCube
    case 19: // Wii
        return "[32-bit BE & 0x1FFFFFFF] Pointer";
    case 2: // Nintendo 64
        return "[32-bit BE] Pointer";
    case 21: // PlayStation 2
        return "[32-bit & 0x1FFFFFFF] Pointer";
    case 41: // PlayStation Portable (PSP)
        return "[32-bit & 0x1FFFFFF] Pointer";
    case 12: // PlayStation (PS1)
    case 18: // Nintendo DS
    case 78: // Nintendo DSi
    case 40: // Dreamcast
        return "[24-bit] Pointer";
    case 5: // Game Boy Advance
    default:
        return "[32-bit] Pointer";
    }
}

QString RaLogicSyntax::GetPlatformPointerPrefix(int consoleId) {
    switch (consoleId) {
    case 16: // GameCube
    case 19: // Wii
    case 2:  // N64
        return "G";
    case 12: // PlayStation
    case 18: // NDS
    case 78: // DSi
    case 40: // Dreamcast
        return "W";
    case 5: // GBA
        return "O";
    case 21: // PS2
    case 41: // PSP
    default:
        return "X";
    }
}

QString RaLogicSyntax::GetPlatformPointerMask(int consoleId) {
    switch (consoleId) {
    case 16:                 // GameCube
    case 19:                 // Wii
        return "&536870911"; // 0x1FFFFFFF
    case 21:                 // PlayStation 2
        return "&536870911"; // 0x1FFFFFFF
    case 41:                 // PSP
        return "&33554431";  // 0x01FFFFFF
    default:
        return "";
    }
}

} // namespace RALE
