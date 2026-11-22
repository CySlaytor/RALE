#include "LogicValidator.h"

#include "AssetStrategies.h"
#include "../Definitions/ConsoleRegions.h"
#include "../Definitions/RaLogicSyntax.h"
#include "../Utilities/LogicFormatter.h"
#include "../Utilities/TitleCaseAnalyzer.h"

#include <QMap>
#include <QRegularExpression>
#include <QSet>

#include <algorithm>
#include <cmath>
#include <functional>
#include <limits>

namespace RALE {

namespace {

enum class AddrType { Value = 0, Current = 1, Past = 2 };

AddrType GetAddrType(const Operand& op) {
    if (op.Type == "Mem" || op.Type == "BCD" || op.Type == "Inverted")
        return AddrType::Current;
    if (op.Type == "Delta" || op.Type == "Prior")
        return AddrType::Past;
    return AddrType::Value;
}

bool IsMemAddr(const Operand& op) { return GetAddrType(op) != AddrType::Value; }

int GetCanonicalPrio(const Operand& op) {
    if (op.Type == "Mem")
        return 10;
    if (op.Type == "Delta" || op.Type == "Prior")
        return 11;
    if (op.Type == "BCD" || op.Type == "Inverted")
        return 12;
    if (op.Type == "Recall")
        return 20;
    return 21; // Value, Float
}

bool IsComparisonOp(const QString& op) {
    return op == "=" || op == "!=" || op == ">" || op == ">=" || op == "<" || op == "<=";
}

QString ReverseCmpOp(const QString& op) {
    if (op == "=")
        return "!=";
    if (op == "!=")
        return "=";
    if (op == ">")
        return "<=";
    if (op == "<")
        return ">=";
    if (op == ">=")
        return "<";
    if (op == "<=")
        return ">";
    return op;
}

bool IsCombiningModifier(const QString& flag) {
    return flag == "Add Source" || flag == "Sub Source" || flag == "Add Address" || flag == "AndNext" || flag == "OrNext";
}

bool IsTerminatingFlag(const QString& flag) { return !IsCombiningModifier(flag); }

bool IsPauseOrResetFlag(const QString& flag) {
    return flag == "Reset If" || flag == "ResetNextIf" || flag == "Pause If";
}

bool IsPartialAccessSize(const QString& size) {
    return size == "Bit0" || size == "Bit1" || size == "Bit2" || size == "Bit3" || size == "Bit4" || size == "Bit5" ||
           size == "Bit6" || size == "Bit7" || size == "BitCount" || size == "Lower4" || size == "Upper4";
}

bool TryParseAddress(const Operand& op, uint64_t& outAddr) {
    if (!IsMemAddr(op))
        return false;
    QString clean = op.Value.trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2);
    bool ok = false;
    outAddr = clean.toULongLong(&ok, 16);
    return ok;
}

bool TryParseAddressString(const QString& addrStr, int64_t& outAddr) {
    QString clean = addrStr.trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2);
    bool ok = false;
    uint64_t u = clean.toULongLong(&ok, 16);
    if (!ok)
        return false;
    outAddr = static_cast<int64_t>(u);
    return true;
}

bool TryParseValueInt(const Operand& op, int64_t& outVal) {
    if (op.Type != "Value")
        return false;
    QString clean = op.Value.trimmed();
    if (clean.isEmpty())
        return false;
    bool ok = false;
    if (clean.startsWith("0x", Qt::CaseInsensitive)) {
        uint64_t u = clean.mid(2).toULongLong(&ok, 16);
        outVal = static_cast<int64_t>(u);
        return ok;
    }
    outVal = clean.toLongLong(&ok, 10);
    if (!ok) {
        uint64_t u = clean.toULongLong(&ok, 16);
        outVal = static_cast<int64_t>(u);
    }
    return ok;
}

bool TryParseFloatVal(const Operand& op, double& outVal) {
    if (op.Type != "Float")
        return false;
    QString clean = op.Value.trimmed();
    clean.replace(',', '.');
    bool ok = false;
    outVal = clean.toDouble(&ok);
    return ok;
}

bool OperandsSameValue(const Operand& a, const Operand& b) {
    if (a.Type.isEmpty() || b.Type.isEmpty())
        return a.Type.isEmpty() && b.Type.isEmpty();
    if (a.Size != b.Size)
        return false;
    if (IsMemAddr(a) && IsMemAddr(b)) {
        uint64_t va = 0, vb = 0;
        return TryParseAddress(a, va) && TryParseAddress(b, vb) && va == vb;
    }
    if (a.Type == "Value" && b.Type == "Value") {
        int64_t va = 0, vb = 0;
        return TryParseValueInt(a, va) && TryParseValueInt(b, vb) && va == vb;
    }
    if (a.Type == "Float" && b.Type == "Float") {
        double da = 0.0, db = 0.0;
        return TryParseFloatVal(a, da) && TryParseFloatVal(b, db) && da == db;
    }
    if (a.Type == "Recall" && b.Type == "Recall")
        return true;
    return a.Value == b.Value;
}

bool OperandsEqual(const Operand& a, const Operand& b) { return OperandsSameValue(a, b) && a.Type == b.Type; }

QString OperandToValueString(const Operand& op) {
    if (IsMemAddr(op)) {
        uint64_t addr = 0;
        if (TryParseAddress(op, addr))
            return QString("0x%1").arg(addr, 8, 16, QChar('0'));
        return op.Value;
    }
    if (op.Type == "Recall")
        return "{recall}";
    if (op.Type == "Value") {
        int64_t val = 0;
        if (TryParseValueInt(op, val))
            return QString::number(val);
    }
    return op.Value;
}

AchievementCondition CanonicalizeCondition(const AchievementCondition& cond) {
    AchievementCondition res = cond.Clone();
    if (!res.RightOperand.Type.isEmpty() && IsComparisonOp(res.Operator)) {
        if (GetCanonicalPrio(res.LeftOperand) > GetCanonicalPrio(res.RightOperand)) {
            std::swap(res.LeftOperand, res.RightOperand);
            res.Operator = ReverseCmpOp(res.Operator);
        }
    }
    return res;
}

bool IsConditionAlwaysFalse(const AchievementCondition& cond) {
    return cond.Operator == "=" && !cond.LeftOperand.Type.isEmpty() && !IsMemAddr(cond.LeftOperand) &&
           !cond.RightOperand.Type.isEmpty() && !IsMemAddr(cond.RightOperand) && !OperandsEqual(cond.LeftOperand, cond.RightOperand);
}

QString ToDisplayHex(int64_t addr) {
    return QString("0x%1").arg(static_cast<quint64>(addr), 8, 16, QChar('0'));
}

SuggestedLogicFix BuildInvertedChainFix(const AchievementConditionGroup& group, int ri) {
    auto invertReq = [](const AchievementCondition& c) {
        AchievementCondition copy = c.Clone();
        if (copy.Flag == "AndNext")
            copy.Flag = "OrNext";
        else if (copy.Flag == "OrNext")
            copy.Flag = "AndNext";
        if (IsComparisonOp(copy.Operator))
            copy.Operator = ReverseCmpOp(copy.Operator);
        return copy;
    };

    int chainStart = ri;
    for (int i = ri - 1; i >= 0; --i) {
        if (IsTerminatingFlag(group.Conditions[i].Flag))
            break;
        chainStart = i;
    }

    SuggestedLogicFix fix;
    fix.StartConditionIndex = chainStart;
    fix.ReplaceCount = ri - chainStart + 1;
    for (int i = chainStart; i < ri; ++i) {
        fix.ReplacementConditions.append(invertReq(group.Conditions[i]));
    }

    AchievementCondition target = invertReq(group.Conditions[ri]);
    target.Flag = "";
    target.RequiredHits = 0;
    fix.ReplacementConditions.append(target);
    return fix;
}

const LogicValidator::ParsedCodeNote* FindNoteContainingAddr(const QList<LogicValidator::ParsedCodeNote>& parsedNotes, int64_t addr) {
    for (int i = static_cast<int>(parsedNotes.size()) - 1; i >= 0; --i) {
        if (parsedNotes[i].Contains(addr))
            return &parsedNotes[i];
    }
    return nullptr;
}

const LogicValidator::ParsedCodeNote* GetEffectiveParsedNote(const QList<LogicValidator::ParsedCodeNote>& parsedNotes, int64_t addr) {
    const LogicValidator::ParsedCodeNote* note = FindNoteContainingAddr(parsedNotes, addr);
    int redirects = 0;
    static const QRegularExpression referRx(R"(refer to \$0x([0-9a-fA-F]+))", QRegularExpression::CaseInsensitiveOption);

    while (note != nullptr && redirects < 5) {
        auto m = referRx.match(note->Note);
        if (!m.hasMatch())
            break;
        bool ok = false;
        int64_t targetAddr = static_cast<int64_t>(m.captured(1).toULongLong(&ok, 16));
        if (!ok)
            break;
        const LogicValidator::ParsedCodeNote* targetNote = FindNoteContainingAddr(parsedNotes, targetAddr);
        if (targetNote == nullptr)
            break;
        note = targetNote;
        redirects++;
    }
    return note;
}

bool TryParseIntPrefix(const QString& literal, int radix, double& outVal) {
    QString s = literal.trimmed();
    if (radix == 16 && s.startsWith("0x", Qt::CaseInsensitive))
        s = s.mid(2);

    int pos = 0;
    if (pos < s.length() && (s[pos] == '+' || s[pos] == '-'))
        pos++;

    int digits = 0;
    while (pos < s.length()) {
        QChar ch = s[pos].toLower();
        bool validDigit = (radix == 16) ? (ch.isDigit() || (ch >= 'a' && ch <= 'f')) : ch.isDigit();
        if (!validDigit)
            break;
        digits++;
        pos++;
    }

    if (digits == 0)
        return false;

    bool ok = false;
    int64_t v = s.left(pos).toLongLong(&ok, radix);
    if (!ok) {
        uint64_t uv = s.left(pos).toULongLong(&ok, radix);
        if (!ok)
            return false;
        outVal = static_cast<double>(uv);
        return true;
    }
    outVal = static_cast<double>(v);
    return true;
}

bool ContainsEmojiPresentation(const QString& text) {
    const QList<uint> ucs4 = text.toUcs4();
    for (uint cp : ucs4) {
        if ((cp >= 0x231A && cp <= 0x231B) || (cp >= 0x23E9 && cp <= 0x23EC) || cp == 0x23F0 || cp == 0x23F3 ||
            (cp >= 0x25FD && cp <= 0x25FE) || (cp >= 0x2614 && cp <= 0x2615) || (cp >= 0x2648 && cp <= 0x2653) ||
            cp == 0x267F || cp == 0x2693 || cp == 0x26A1 || (cp >= 0x26AA && cp <= 0x26AB) || (cp >= 0x26BD && cp <= 0x26BE) ||
            (cp >= 0x26C4 && cp <= 0x26C5) || cp == 0x26CE || cp == 0x26D4 || cp == 0x26EA || (cp >= 0x26F2 && cp <= 0x26F3) ||
            cp == 0x26F5 || cp == 0x26FA || cp == 0x26FD || cp == 0x2705 || (cp >= 0x270A && cp <= 0x270B) || cp == 0x2728 ||
            cp == 0x274C || cp == 0x274E || (cp >= 0x2753 && cp <= 0x2755) || cp == 0x2757 || (cp >= 0x2795 && cp <= 0x2797) ||
            cp == 0x27B0 || cp == 0x27BF || (cp >= 0x2B1B && cp <= 0x2B1C) || cp == 0x2B50 || cp == 0x2B55 || cp == 0x1F004 ||
            cp == 0x1F0CF || cp == 0x1F18E || (cp >= 0x1F191 && cp <= 0x1F19A) || (cp >= 0x1F1E6 && cp <= 0x1F1FF) ||
            cp == 0x1F201 || cp == 0x1F21A || cp == 0x1F22F || (cp >= 0x1F232 && cp <= 0x1F236) ||
            (cp >= 0x1F238 && cp <= 0x1F23A) || (cp >= 0x1F250 && cp <= 0x1F251) || (cp >= 0x1F300 && cp <= 0x1F320) ||
            (cp >= 0x1F32D && cp <= 0x1F335) || (cp >= 0x1F337 && cp <= 0x1F37C) || (cp >= 0x1F37E && cp <= 0x1F393) ||
            (cp >= 0x1F3A0 && cp <= 0x1F3CA) || (cp >= 0x1F3CF && cp <= 0x1F3D3) || (cp >= 0x1F3E0 && cp <= 0x1F3F0) ||
            cp == 0x1F3F4 || (cp >= 0x1F3F8 && cp <= 0x1F43E) || cp == 0x1F440 || (cp >= 0x1F442 && cp <= 0x1F4FC) ||
            (cp >= 0x1F4FF && cp <= 0x1F53D) || (cp >= 0x1F54B && cp <= 0x1F54E) || (cp >= 0x1F550 && cp <= 0x1F567) ||
            cp == 0x1F57A || (cp >= 0x1F595 && cp <= 0x1F596) || cp == 0x1F5A4 || (cp >= 0x1F5FB && cp <= 0x1F64F) ||
            (cp >= 0x1F680 && cp <= 0x1F6C5) || cp == 0x1F6CC || (cp >= 0x1F6D0 && cp <= 0x1F6D2) ||
            (cp >= 0x1F6D5 && cp <= 0x1F6D7) || (cp >= 0x1F6DC && cp <= 0x1F6DF) || (cp >= 0x1F6EB && cp <= 0x1F6EC) ||
            (cp >= 0x1F6F4 && cp <= 0x1F6FC) || (cp >= 0x1F7E0 && cp <= 0x1F7EB) || cp == 0x1F7F0 ||
            (cp >= 0x1F90C && cp <= 0x1F93A) || (cp >= 0x1F93C && cp <= 0x1F945) || (cp >= 0x1F947 && cp <= 0x1F9FF) ||
            (cp >= 0x1FA70 && cp <= 0x1FA7C) || (cp >= 0x1FA80 && cp <= 0x1FA89) || (cp >= 0x1FA8F && cp <= 0x1FAC6) ||
            (cp >= 0x1FACE && cp <= 0x1FADC) || (cp >= 0x1FADF && cp <= 0x1FAE9) || (cp >= 0x1FAF0 && cp <= 0x1FAF8)) {
            return true;
        }
    }
    return false;
}

void GetMemSizeDetails(const QString& memSizeName, uint64_t& outMaxVal, bool& outInfinite) {
    outInfinite = false;
    outMaxVal = 0;
    if (memSizeName == "8-bit" || memSizeName == "Lower4" || memSizeName == "Upper4") {
        outMaxVal = 0xFFULL;
    } else if (memSizeName == "16-bit" || memSizeName == "16-bit BE") {
        outMaxVal = 0xFFFFULL;
    } else if (memSizeName == "24-bit" || memSizeName == "24-bit BE") {
        outMaxVal = 0xFFFFFFULL;
    } else if (memSizeName == "32-bit" || memSizeName == "32-bit BE") {
        outMaxVal = 0xFFFFFFFFULL;
    } else if (memSizeName == "Float" || memSizeName == "Float BE" || memSizeName == "Double32" ||
               memSizeName == "Double32 BE" || memSizeName == "MBF32" || memSizeName == "MBF32 LE") {
        outInfinite = true;
    } else if (memSizeName.startsWith("Bit")) {
        outMaxVal = (memSizeName == "BitCount") ? 8ULL : 1ULL;
    }
}

void ParseNoteSize(const QString& addressNote, QString& outTypeName, int64_t& outBytes, uint64_t& outMaxVal, bool& outInfinite) {
    outBytes = 1;
    outTypeName.clear();
    outMaxVal = 0;
    outInfinite = false;

    QString norm = addressNote;
    norm.replace("\r\n", "\n").replace('\r', '\n');
    const int nl = norm.indexOf('\n');
    const QString sNote = ((nl >= 0) ? norm.left(nl) : norm).toLower();
    if (sNote.length() < 4)
        return;

    bool bytesFromBits = false;
    bool foundSize = false;
    bool prevWordIsSize = false;
    bool prevWordIsNumber = false;
    QString prevWord;

    static const QRegularExpression tokenRe(R"(((\d+)|([a-z]+)|.))", QRegularExpression::CaseInsensitiveOption);
    auto it = tokenRe.globalMatch(sNote);
    while (it.hasNext()) {
        const auto m = it.next();
        const QString word = m.captured(1);
        const bool wordIsNumber = !m.captured(2).isEmpty();
        bool wordIsSize = false;

        if (wordIsNumber) {
            const int64_t num = word.toLongLong();
            if (prevWord == "mbf") {
                if (num == 32 || num == 40) {
                    outBytes = num / 8;
                    outTypeName = "MBF32";
                    wordIsSize = true;
                    foundSize = true;
                }
            } else if (prevWord == "double" && num == 32) {
                outBytes = num / 8;
                outTypeName = "Double32";
                wordIsSize = true;
                foundSize = true;
            } else if (num == 4 && (prevWord == "lower" || prevWord == "upper")) {
                outBytes = 1;
                outTypeName = "8-bit";
                wordIsSize = true;
                foundSize = true;
            }
        } else if (prevWordIsSize) {
            if (word == "float") {
                if (outTypeName == "32-bit") {
                    outTypeName = "Float";
                    wordIsSize = true;
                }
            } else if (word == "double") {
                if (outTypeName == "32-bit" || outBytes == 8) {
                    outTypeName = "Double32";
                    wordIsSize = true;
                }
            } else if (word == "be" || word == "bigendian") {
                if (outTypeName == "16-bit")
                    outTypeName = "16-bit BE";
                else if (outTypeName == "24-bit")
                    outTypeName = "24-bit BE";
                else if (outTypeName == "32-bit")
                    outTypeName = "32-bit BE";
                else if (outTypeName == "Float")
                    outTypeName = "Float BE";
                else if (outTypeName == "Double32")
                    outTypeName = "Double32 BE";
            } else if (word == "le") {
                if (outTypeName == "MBF32")
                    outTypeName = "MBF32 LE";
            } else if (word == "mbf") {
                if (outBytes == 4 || outBytes == 5)
                    outTypeName = "MBF32";
            }
        } else if (prevWordIsNumber) {
            const int64_t num = prevWord.toLongLong();
            if (word == "bit" || word == "bits") {
                if (!foundSize) {
                    outBytes = (num + 7) / 8;
                    outTypeName.clear();
                    bytesFromBits = true;
                    wordIsSize = true;
                    foundSize = true;
                }
            } else if (word == "byte" || word == "bytes") {
                if (!foundSize || bytesFromBits) {
                    outBytes = num;
                    outTypeName.clear();
                    bytesFromBits = false;
                    wordIsSize = true;
                    foundSize = true;
                }
            }

            if (wordIsSize) {
                switch (outBytes) {
                case 0:
                    outBytes = 1;
                    break;
                case 1:
                    outTypeName = "8-bit";
                    break;
                case 2:
                    outTypeName = "16-bit";
                    break;
                case 3:
                    outTypeName = "24-bit";
                    break;
                case 4:
                    outTypeName = "32-bit";
                    break;
                default:
                    outTypeName.clear();
                    break;
                }
            }
        } else if (word == "float") {
            if (!foundSize) {
                outBytes = 4;
                outTypeName = (prevWord == "be" || prevWord == "bigendian") ? "Float BE" : "Float";
                wordIsSize = true;
            }
        } else if (word == "double") {
            if (!foundSize) {
                outBytes = 8;
                outTypeName = (prevWord == "be" || prevWord == "bigendian") ? "Double32 BE" : "Double32";
                wordIsSize = true;
            }
        } else if (word.startsWith("bitflag") || word.startsWith("bitfield")) {
            if (!foundSize) {
                outTypeName = "8-bit";
                wordIsSize = true;
            }
        }

        if (word != " " && word != "-") {
            prevWordIsSize = wordIsSize;
            prevWordIsNumber = wordIsNumber;
            prevWord = word;
        }
    }

    if (!outTypeName.isEmpty())
        GetMemSizeDetails(outTypeName, outMaxVal, outInfinite);
}

QList<LogicValidator::ParsedCodeNote::EnumEntry> ParseNoteEnumerations(const QString& addressNote) {
    static const QRegularExpression enumLineRe(
        QString::fromUtf8(R"(^[.+\s\x{2500}-\x{257F}]*((?:(?:0x)?[0-9a-f]+|[-+]?[0-9]*\.?[0-9]+)+)\s*([:=|-])\s*(.+)$)"),
        QRegularExpression::CaseInsensitiveOption);

    QString norm = addressNote;
    norm.replace("\r\n", "\n").replace('\r', '\n');
    const QStringList lines = norm.split('\n');

    QList<LogicValidator::ParsedCodeNote::EnumEntry> enumerations;
    bool isHex = false;

    for (int i = 1; i < lines.size(); ++i) {
        const auto m = enumLineRe.match(lines[i]);
        if (!m.hasMatch())
            continue;

        const QString literal = m.captured(1);
        const QString meaning = m.captured(3).trimmed();

        double value = 0.0;
        bool ok = false;
        bool isInteger = true;

        if (literal.startsWith("0x", Qt::CaseInsensitive)) {
            ok = TryParseIntPrefix(literal, 16, value);
            isHex = true;
        } else if (literal.contains('.')) {
            value = literal.toDouble(&ok);
            isInteger = false;
        } else {
            ok = TryParseIntPrefix(literal, isHex ? 16 : 10, value);
        }

        if (ok && !std::isnan(value)) {
            enumerations.append({literal, value, isInteger, meaning});
        }
    }

    return (enumerations.size() >= 2) ? enumerations : QList<LogicValidator::ParsedCodeNote::EnumEntry>{};
}

} // namespace

QString LogicValidator::ParsedCodeNote::Header() const {
    QString norm = Note;
    norm.replace("\r\n", "\n").replace('\r', '\n');
    const int nl = norm.indexOf('\n');
    return (nl >= 0) ? norm.left(nl) : norm;
}

LogicValidator::ParsedCodeNote LogicValidator::ParseCodeNote(const CodeNote& note) {
    ParsedCodeNote parsed;
    TryParseAddressString(note.Address, parsed.Addr);
    parsed.AddressHex = ToDisplayHex(parsed.Addr >= 0 ? parsed.Addr : 0);
    parsed.Note = note.Note;

    static const QRegularExpression generalNotesHeaderRe(R"(^[ \t]*\[general game notes\])",
                                                       QRegularExpression::CaseInsensitiveOption | QRegularExpression::MultilineOption);

    if (parsed.Addr == 0) {
        const auto m = generalNotesHeaderRe.match(note.Note);
        if (m.hasMatch()) {
            parsed.AddressNote = note.Note.left(static_cast<int>(m.capturedStart(0)));
            parsed.HasGeneralGameNotes = true;
        } else {
            parsed.AddressNote = note.Note;
        }
    } else {
        parsed.AddressNote = note.Note;
    }

    ParseNoteSize(parsed.AddressNote, parsed.TypeName, parsed.Size, parsed.TypeMaxValue, parsed.HasInfiniteMaxValue);

    QString normLower = parsed.AddressNote;
    normLower.replace("\r\n", "\n").replace('\r', '\n');
    normLower = normLower.toLower();
    const QStringList lines = normLower.split('\n');
    if (!lines.isEmpty()) {
        if (lines[0].contains("ptr") || lines[0].contains("pointer")) {
            parsed.ProbablePointer = true;
        } else {
            static const QRegularExpression ptrLineRe(QString::fromUtf8(R"(^[.+\s\x{2500}-\x{257F}]*\+)"));
            int plusLines = 0;
            for (int i = 1; i < lines.size(); ++i) {
                if (ptrLineRe.match(lines[i]).hasMatch())
                    plusLines++;
            }
            parsed.ProbablePointer = (plusLines >= 2);
        }
    }

    if (!parsed.ProbablePointer) {
        parsed.Enumerations = ParseNoteEnumerations(parsed.AddressNote);
    }

    return parsed;
}

QList<LogicValidator::ParsedCodeNote> LogicValidator::BuildParsedNotes(const QList<CodeNote>& rawNotes) {
    QList<ParsedCodeNote> parsed;
    parsed.reserve(rawNotes.size());
    for (const auto& n : rawNotes) {
        if (n.Note.isEmpty())
            continue;
        parsed.append(ParseCodeNote(n));
    }
    std::sort(parsed.begin(), parsed.end(), [](const ParsedCodeNote& a, const ParsedCodeNote& b) { return a.Addr < b.Addr; });
    return parsed;
}

bool LogicValidator::NeedsFeedback(const IAssetItem* asset) {
    if (asset == nullptr)
        return false;
    if (asset->Title.contains("[VOID]", Qt::CaseInsensitive))
        return false;
    if (asset->ID >= 101000000 && asset->ID < 111000000)
        return false; // emulator warnings
    if (asset->isLeaderboard()) {
        const auto* lb = static_cast<const RaLeaderboard*>(asset);
        if (lb->Hidden)
            return false;
    }
    return true;
}

// Validates the entire asset (all groups) for cross-group logic like Delta checks
QList<ValidationIssue> LogicValidator::ValidateAsset(const QList<AchievementConditionGroup>& groups, std::shared_ptr<IAssetItem> asset,
                                                     const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                                     const QList<ConditionFormatter::RangeEntry>* rangeCache,
                                                     const QList<CodeNote>* rawNotes) {
    QList<ParsedCodeNote> parsedNotes;
    if (rawNotes != nullptr) {
        parsedNotes = BuildParsedNotes(*rawNotes);
    }
    if (asset != nullptr && asset->isLeaderboard()) {
        LeaderboardStrategy lbStrategy;
        auto segments = lbStrategy.ParseLogic(asset->Logic);
        return ValidateLeaderboardWithParsedNotes(segments, asset, notesLookup, rangeCache, parsedNotes);
    }
    return ValidateAchievementWithParsedNotes(groups, asset, notesLookup, rangeCache, parsedNotes);
}

QList<ValidationIssue> LogicValidator::ValidateLeaderboard(const QMap<QString, QList<AchievementConditionGroup>>& segments,
                                                           std::shared_ptr<IAssetItem> asset,
                                                           const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                                           const QList<ConditionFormatter::RangeEntry>* rangeCache,
                                                           const QList<CodeNote>* rawNotes) {
    QList<ParsedCodeNote> parsedNotes;
    if (rawNotes != nullptr) {
        parsedNotes = BuildParsedNotes(*rawNotes);
    }
    return ValidateLeaderboardWithParsedNotes(segments, asset, notesLookup, rangeCache, parsedNotes);
}

QList<ValidationIssue> LogicValidator::ValidateAchievementWithParsedNotes(
    const QList<AchievementConditionGroup>& groups, std::shared_ptr<IAssetItem> asset,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup, const QList<ConditionFormatter::RangeEntry>* rangeCache,
    const QList<ParsedCodeNote>& parsedNotes) {
    QList<ValidationIssue> allIssues;
    if (asset != nullptr && !NeedsFeedback(asset.get()))
        return allIssues;

    allIssues.append(ValidateLogicBlock(groups, false, true, asset, "Core", notesLookup, rangeCache, parsedNotes));

    if (asset != nullptr)
        CheckPresentation(asset, allIssues);

    return allIssues;
}

QList<ValidationIssue> LogicValidator::ValidateLeaderboardWithParsedNotes(
    const QMap<QString, QList<AchievementConditionGroup>>& segments, std::shared_ptr<IAssetItem> asset,
    const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup, const QList<ConditionFormatter::RangeEntry>* rangeCache,
    const QList<ParsedCodeNote>& parsedNotes) {
    QList<ValidationIssue> allIssues;
    if (asset != nullptr && !NeedsFeedback(asset.get()))
        return allIssues;

    struct SegmentSpec {
        QString Name;
        bool RunFullLogic;
        bool IsValue;
    };
    const QList<SegmentSpec> specs = {
        {"Start", true, false},
        {"Cancel", false, false},
        {"Submit", false, false},
        {"Value", false, true},
    };

    for (const auto& spec : specs) {
        auto it = segments.constFind(spec.Name);
        if (it == segments.constEnd())
            continue;
        allIssues.append(
            ValidateLogicBlock(it.value(), spec.IsValue, spec.RunFullLogic, asset, spec.Name, notesLookup, rangeCache, parsedNotes));
    }

    if (asset != nullptr)
        CheckPresentation(asset, allIssues);

    return allIssues;
}

QList<ValidationIssue> LogicValidator::ValidateLogicBlock(
    const QList<AchievementConditionGroup>& groups, bool isValue, bool runFullLogicTests, std::shared_ptr<IAssetItem> asset,
    const QString& segmentName, const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
    const QList<ConditionFormatter::RangeEntry>* rangeCache, const QList<ParsedCodeNote>& parsedNotes) {
    QList<ValidationIssue> issues;
    if (groups.isEmpty())
        return issues;

    if (runFullLogicTests) {
        CheckDeltas(groups, issues, asset, segmentName);
        CheckOneCondition(groups, isValue, issues, asset, segmentName);
    }

    const bool hasNotes = (notesLookup != nullptr && !notesLookup->isEmpty()) || !parsedNotes.isEmpty();

    for (int gi = 0; gi < groups.size(); ++gi) {
        const auto& group = groups[gi];
        if (group.Conditions.isEmpty())
            continue;

        if (hasNotes) {
            static const QMap<QString, QList<std::shared_ptr<NoteNode>>> emptyLookup;
            const auto& lookupRef = (notesLookup != nullptr) ? *notesLookup : emptyLookup;
            CheckMissingNotes(group, gi, issues, asset, segmentName, lookupRef, rangeCache, parsedNotes);
            CheckMismatchNotes(group, gi, issues, asset, segmentName, parsedNotes);
        }

        CheckPointers(group, gi, issues, asset, segmentName, parsedNotes);
        CheckValidOffsets(group, gi, issues, asset, segmentName);
        CheckBadChains(group, gi, issues, asset, segmentName);
        CheckPriors(group, gi, issues, asset, segmentName);
        CheckStaleAddAddress(group, gi, issues, asset, segmentName);
        CheckUselessAndNext(group, gi, issues, asset, segmentName);
        CheckResetWithHits(group, gi, issues, asset, segmentName);
        CheckUselessResetNextIf(group, gi, isValue, issues, asset, segmentName);
        CheckUselessAddHits(group, gi, isValue, issues, asset, segmentName);
    }

    CheckUnclearedHits(groups, isValue, issues, asset, segmentName);
    CheckPauseLocks(groups, issues, asset, segmentName);
    CheckUselessPause(groups, isValue, issues, asset, segmentName);
    CheckUselessReset(groups, isValue, issues, asset, segmentName);

    return issues;
}

void LogicValidator::CheckBadChains(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                    std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    if (group.Conditions.isEmpty())
        return;
    const auto& last = group.Conditions.last();
    if (IsChainFlag(last.Flag)) {
        issues.append(ValidationIssue(
            FeedbackSeverity::Error, "Bad Chain",
            QString("The last requirement of a group cannot have a chaining flag (%1 in '%2').").arg(last.Flag, group.GroupName), last, "",
            asset, "Logic", segmentName, groupIndex));
    }
}

void LogicValidator::CheckPauseLocks(const QList<AchievementConditionGroup>& groups, QList<ValidationIssue>& issues,
                                     std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    QSet<int> groupsWithReset;
    for (int gi = 0; gi < groups.size(); ++gi) {
        for (const auto& c : groups[gi].Conditions) {
            if (c.Flag == "Reset If") {
                groupsWithReset.insert(gi);
                break;
            }
        }
    }

    for (int gi = 0; gi < groups.size(); ++gi) {
        const auto& g = groups[gi];
        for (int ri = 0; ri < g.Conditions.size(); ++ri) {
            const auto& req = g.Conditions[ri];
            // this is a pauselock
            if (req.RequiredHits > 0 && req.Flag == "Pause If") {
                bool foundRni = false;
                for (int i = ri - 1; i >= 0; --i) {
                    if (g.Conditions[i].Flag == "ResetNextIf") {
                        foundRni = true;
                        break;
                    }
                    if (IsTerminatingFlag(g.Conditions[i].Flag)) {
                        break;
                    }
                }
                if (foundRni)
                    continue;

                // no RNI found, so if there isn't an alt reset, that's a problem
                bool otherGroupHasReset = false;
                for (int resetGi : groupsWithReset) {
                    if (resetGi != gi) {
                        otherGroupHasReset = true;
                        break;
                    }
                }

                if (!otherGroupHasReset) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Fail, "PauseLock Without Reset",
                        "PauseLocks require a reset, either via ResetNextIf, or a ResetIf in another group.", req,
                        "https://docs.retroachievements.org/developer-docs/flags/pauseif.html#pauseif-with-hit-counts", asset, "Logic",
                        segmentName, gi));
                }
            }
        }
    }
}

void LogicValidator::CheckUnclearedHits(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                                        std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    // value groups don't require resets
    if (isValue)
        return;

    bool hasResetIf = false;
    for (const auto& g : groups) {
        for (const auto& req : g.Conditions) {
            if (req.Flag == "Reset If") {
                hasResetIf = true;
                break;
            }
        }
        if (hasResetIf)
            break;
    }

    for (int gi = 0; gi < groups.size(); ++gi) {
        const auto& g = groups[gi];
        for (int ri = 0; ri < g.Conditions.size(); ++ri) {
            const auto& req = g.Conditions[ri];
            // a reset with hits is still a reset, and a pause with hits is a pauselock
            if (req.RequiredHits > 0 && req.Flag != "Reset If" && req.Flag != "Pause If") {
                bool foundRni = false;
                for (int i = ri - 1; i >= 0; --i) {
                    if (g.Conditions[i].Flag == "ResetNextIf") {
                        foundRni = true;
                        break;
                    }
                    if (IsTerminatingFlag(g.Conditions[i].Flag)) {
                        break;
                    }
                }
                if (foundRni)
                    continue;

                // no RNI found, so if there isn't a reset, that's a problem
                if (!hasResetIf) {
                    issues.append(ValidationIssue(FeedbackSeverity::Fail, "Hits Without Reset",
                                                  "Hit counts require a reset, either via ResetIf or ResetNextIf.", req,
                                                  "https://docs.retroachievements.org/developer-docs/hit-counts.html", asset, "Logic",
                                                  segmentName, gi));
                }
            }
        }
    }
}

void LogicValidator::CheckUselessAndNext(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                         std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    QList<QPair<int, AchievementCondition>> andNextChain;
    bool valid = false;

    for (int ri = 0; ri < group.Conditions.size(); ++ri) {
        const auto& req = group.Conditions[ri];
        // add all AndNexts to a chain
        if (req.Flag == "AndNext")
            andNextChain.append({ri, req});
        // if there's an OrNext flag, I'm just going to let this go
        if (req.Flag == "OrNext")
            valid = true;
        // if there are hitcounts *anywhere* in this chain, valid
        if (req.RequiredHits != 0)
            valid = true;

        if (IsTerminatingFlag(req.Flag)) {
            // if the chain terminates at any actual flag, valid
            if (!req.Flag.isEmpty() || req.RequiredHits > 0)
                valid = true;

            if (!andNextChain.isEmpty() && !valid) {
                for (const auto& entry : andNextChain) {
                    SuggestedLogicFix fix;
                    fix.StartConditionIndex = entry.first;
                    fix.ReplaceCount = 1;
                    AchievementCondition fixed = entry.second.Clone();
                    fixed.Flag = "";
                    fix.ReplacementConditions.append(fixed);

                    issues.append(ValidationIssue(
                        FeedbackSeverity::Warning, "Useless AndNext",
                        "Combining requirements with AND is the default behavior. Useless AndNext flags should be removed.", entry.second,
                        "https://docs.retroachievements.org/developer-docs/flags/andnext-ornext.html", asset, "Logic", segmentName,
                        groupIndex, "", "", fix));
                }
            }
            andNextChain.clear();
            valid = false;
        }
    }
}

void LogicValidator::CheckUselessPause(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                                       std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    bool hasHits = false;
    for (const auto& g : groups) {
        for (const auto& req : g.Conditions) {
            if (req.RequiredHits > 0) {
                hasHits = true;
                break;
            }
        }
        if (hasHits)
            break;
    }

    for (int gi = 0; gi < groups.size(); ++gi) {
        const auto& g = groups[gi];
        bool hasMeasured = false;
        for (const auto& req : g.Conditions) {
            if (req.Flag == "Measured" || req.Flag == "Measured%") {
                hasMeasured = true;
                break;
            }
        }

        for (int ri = 0; ri < g.Conditions.size(); ++ri) {
            const auto& req = g.Conditions[ri];
            if (req.Flag == "Pause If" && !hasHits) {
                // if the group has a Measured flag, give a slightly different warning
                if (hasMeasured) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Pass, "Pausing Measured",
                        "PauseIf should only be used with requirements containing hit targets, unless being used to freeze updates to a "
                        "Measured requirement.",
                        req, "https://docs.retroachievements.org/developer-docs/flags/measured.html#measured", asset, "Logic", segmentName,
                        gi));
                }
                // pause in a value group can freeze the reported value, and therefore is fine
                else if (!isValue) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Fail, "Useless Use of PauseIf",
                        "PauseIf should only be used with requirements containing hit targets.", req,
                        "https://docs.retroachievements.org/developer-docs/flags/pauseif.html", asset, "Logic", segmentName, gi, "", "",
                        BuildInvertedChainFix(g, ri)));
                }
            }
        }
    }
}

void LogicValidator::CheckUselessReset(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                                       std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    bool hasHits = false;
    for (const auto& g : groups) {
        for (const auto& req : g.Conditions) {
            if (req.RequiredHits > 0) {
                hasHits = true;
                break;
            }
        }
        if (hasHits)
            break;
    }

    for (int gi = 0; gi < groups.size(); ++gi) {
        const auto& g = groups[gi];
        bool hasMeasured = false;
        for (const auto& req : g.Conditions) {
            if (req.Flag == "Measured" || req.Flag == "Measured%") {
                hasMeasured = true;
                break;
            }
        }

        for (int ri = 0; ri < g.Conditions.size(); ++ri) {
            const auto& req = g.Conditions[ri];
            if (req.Flag == "Reset If" && !hasHits) {
                // ResetIf with a measured should be fine in a value
                if (!isValue || hasMeasured) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Fail, "Useless Use of ResetIf",
                        "ResetIf should only be used with requirements containing hit targets.", req,
                        "https://docs.retroachievements.org/developer-docs/flags/resetif.html", asset, "Logic", segmentName, gi, "", "",
                        BuildInvertedChainFix(g, ri)));
                }
            }
        }
    }
}

void LogicValidator::CheckResetWithHits(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                        std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    bool hasAddHits = false;
    for (int ri = 0; ri < group.Conditions.size(); ++ri) {
        const auto& req = group.Conditions[ri];
        if (!hasAddHits && req.Flag == "Reset If" && req.RequiredHits == 1) {
            SuggestedLogicFix fix;
            fix.StartConditionIndex = ri;
            fix.ReplaceCount = 1;
            AchievementCondition fixed = req.Clone();
            fixed.RequiredHits = 0;
            fix.ReplacementConditions.append(fixed);

            issues.append(ValidationIssue(
                FeedbackSeverity::Warning, "ResetIf Hitcount = 1",
                "A ResetIf or ResetNextIf with a hitcount of 1 does not require a hitcount. The hitcount can be safely removed.", req,
                "https://docs.retroachievements.org/developer-docs/flags/resetif.html", asset, "Logic", segmentName, groupIndex, "", "",
                fix));
        }

        if (req.Flag == "Add Hits")
            hasAddHits = true;
        else if (!IsChainFlag(req.Flag))
            hasAddHits = false;
    }
}

void LogicValidator::CheckUselessResetNextIf(const AchievementConditionGroup& group, int groupIndex, bool isValue,
                                             QList<ValidationIssue>& issues, std::shared_ptr<IAssetItem> asset,
                                             const QString& segmentName) {
    for (int ri = 0; ri < group.Conditions.size(); ++ri) {
        const auto& req = group.Conditions[ri];
        if (req.Flag == "ResetNextIf") {
            for (int i = ri + 1; i < group.Conditions.size(); ++i) {
                const auto& next = group.Conditions[i];
                // if the requirement has hits, RNI is valid
                if (next.RequiredHits > 0)
                    break;

                // if this is a combining flag like AddAddress or AndNext, the chain continues
                if (!IsTerminatingFlag(next.Flag))
                    continue;

                // ResetNextIf with any Add/SubHits should always be valid
                if (next.Flag == "Add Hits" || next.Flag == "Sub Hits")
                    break;

                // ResetNextIf with a measured should be fine in a value
                if (isValue && (next.Flag == "Measured" || next.Flag == "Measured%"))
                    break;

                // RNI(1+)->PauseIf(0) is `Pause Until`
                if (req.RequiredHits > 0 && next.Flag == "Pause If")
                    break;

                // otherwise, RNI was not valid
                issues.append(ValidationIssue(
                    FeedbackSeverity::Fail, "Useless Use of ResetNextIf",
                    "ResetNextIf should only be used with requirements containing hit targets, and must be placed immediately before the "
                    "requirement with the hit target.",
                    req, "https://docs.retroachievements.org/developer-docs/flags/resetnextif.html", asset, "Logic", segmentName,
                    groupIndex));
                break;
            }
        }
    }
}

void LogicValidator::CheckUselessAddHits(const AchievementConditionGroup& group, int groupIndex, bool isValue,
                                         QList<ValidationIssue>& issues, std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    bool hasAddHits = false;
    for (const auto& req : group.Conditions) {
        if (req.Flag == "Add Hits" || req.Flag == "Sub Hits")
            hasAddHits = true;

        if (!IsChainFlag(req.Flag)) {
            if (!isValue && hasAddHits && req.RequiredHits == 0) {
                issues.append(ValidationIssue(
                    FeedbackSeverity::Error, "AddHits Without Target",
                    "AddHits in a chain should end in a hit target. Without a hit target, AddHits does nothing.", req, "", asset, "Logic",
                    segmentName, groupIndex));
            }
            hasAddHits = false;
        }
    }
}

void LogicValidator::CheckStaleAddAddress(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                          std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    for (const auto& cond : group.Conditions) {
        if (cond.Flag == "Add Address") {
            if (cond.LeftOperand.Type == "Delta" || cond.LeftOperand.Type == "Prior") {
                issues.append(ValidationIssue(
                    FeedbackSeverity::Info, "Stale AddAddress",
                    "Stale references with AddAddress can be dangerous. Use caution when reading a pointer from the previous frame "
                    "(AddAddress + Delta).",
                    cond, "https://docs.retroachievements.org/developer-docs/flags/addaddress.html#using-delta-with-chained-pointers",
                    asset, "Logic", segmentName, groupIndex));
            }
        }
    }
}

void LogicValidator::CheckValidOffsets(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                       std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    bool isOffset = false;
    for (const auto& req : group.Conditions) {
        if (isOffset) {
            for (const Operand* op : {&req.LeftOperand, &req.RightOperand}) {
                uint64_t val = 0;
                if (TryParseAddress(*op, val) && val >= 0x80000000ULL) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Warning, "Negative Offset",
                        "Negative pointer offsets are wrong in the vast majority of cases.", req,
                        "https://docs.retroachievements.org/developer-docs/flags/addaddress.html#calculating-your-offset", asset, "Logic",
                        segmentName, groupIndex));
                }
            }
        }
        isOffset = (req.Flag == "Add Address");
    }
}

void LogicValidator::CheckPriors(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                 std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    for (const auto& a : group.Conditions) {
        if (OperandsSameValue(a.LeftOperand, a.RightOperand) && a.Operator == "!=") {
            const AchievementCondition ca = CanonicalizeCondition(a);
            if (GetAddrType(ca.LeftOperand) == AddrType::Current && ca.RightOperand.Type == "Prior") {
                issues.append(ValidationIssue(
                    FeedbackSeverity::Fail, "Questionable Use of Prior",
                    "A memory value will always be not-equal to its prior, unless the value has never changed. This requirement most "
                    "likely does not accomplish anything and is probably safe to remove.",
                    a, "https://docs.retroachievements.org/developer-docs/prior-values.html", asset, "Logic", segmentName, groupIndex));
            }
        }
    }

    for (int ai = 0; ai < group.Conditions.size(); ++ai) {
        const auto& a = group.Conditions[ai];
        const AchievementCondition ca = CanonicalizeCondition(a);
        if (ca.Operator == "!=" && ca.LeftOperand.Type == "Prior" && !ca.RightOperand.Type.isEmpty() && !IsMemAddr(ca.RightOperand)) {
            for (int bi = 0; bi < group.Conditions.size(); ++bi) {
                if (ai == bi)
                    continue;
                const auto& b = group.Conditions[bi];
                const AchievementCondition cb = CanonicalizeCondition(b);
                if (cb.Operator == "=" && GetAddrType(cb.LeftOperand) == AddrType::Current && !cb.RightOperand.Type.isEmpty() &&
                    !IsMemAddr(cb.RightOperand)) {
                    if (OperandsEqual(ca.RightOperand, cb.RightOperand) && OperandsSameValue(ca.LeftOperand, cb.LeftOperand)) {
                        issues.append(ValidationIssue(
                            FeedbackSeverity::Fail, "Questionable Use of Prior",
                            QString("The prior comparison will always be true when %1, unless the value has never changed. This "
                                    "requirement most likely does not accomplish anything and is probably safe to remove.")
                                .arg(LogicFormatter::ConditionToString(b)),
                            a, "https://docs.retroachievements.org/developer-docs/prior-values.html", asset, "Logic", segmentName,
                            groupIndex));
                    }
                }
            }
        }
    }
}

void LogicValidator::CheckPointers(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                   std::shared_ptr<IAssetItem> asset, const QString& segmentName,
                                   const QList<ParsedCodeNote>& parsedNotes) {
    if (parsedNotes.isEmpty())
        return;

    for (const auto& req : group.Conditions) {
        uint64_t addr = 0;
        if (!TryParseAddress(req.LeftOperand, addr))
            continue;

        const ParsedCodeNote* note = FindNoteContainingAddr(parsedNotes, static_cast<int64_t>(addr));
        if (note == nullptr)
            continue;

        int64_t rhsVal = 0;
        if (note->ProbablePointer && IsComparisonOp(req.Operator) && req.RightOperand.Type == "Value" &&
            TryParseValueInt(req.RightOperand, rhsVal) && rhsVal != 0) {
            issues.append(ValidationIssue(
                FeedbackSeverity::Info, "Pointer Comparison",
                "Comparison between pointer and non-zero value is usually incorrect, unless pointing to data in ROM.", req, "", asset,
                "Logic", segmentName, groupIndex));
        }
    }
}

void LogicValidator::CheckMismatchNotes(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                        std::shared_ptr<IAssetItem> asset, const QString& segmentName,
                                        const QList<ParsedCodeNote>& parsedNotes) {
    if (parsedNotes.isEmpty())
        return;

    bool prevAddAddress = false;
    for (const auto& req : group.Conditions) {
        if (!prevAddAddress) {
            for (const Operand* op : {&req.LeftOperand, &req.RightOperand}) {
                uint64_t addr = 0;
                if (!TryParseAddress(*op, addr))
                    continue;

                const ParsedCodeNote* note = FindNoteContainingAddr(parsedNotes, static_cast<int64_t>(addr));
                if (note == nullptr)
                    continue;

                if (!note->TypeName.isEmpty() && !op->Size.isEmpty() && !IsPartialAccessSize(op->Size) && op->Size != note->TypeName) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Info, "Type Mismatch",
                        QString("Accessing %1 as %2, but matching code note at %3 is marked as %4.")
                            .arg(ToDisplayHex(static_cast<int64_t>(addr)), op->Size, ToDisplayHex(note->Addr), note->TypeName),
                        req, "https://docs.retroachievements.org/developer-docs/memory-inspector.html", asset, "Logic", segmentName,
                        groupIndex));
                }
            }
        }
        prevAddAddress = (req.Flag == "Add Address");
    }
}

void LogicValidator::CheckOneCondition(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                                       std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    if (isValue)
        return;

    QSet<QString> virt;
    for (const auto& g : groups) {
        QString prefix;
        for (const auto& req : g.Conditions) {
            if (req.Flag == "Add Address") {
                prefix += OperandToValueString(req.LeftOperand) +
                          (req.Operator.isEmpty() ? QString() : req.Operator + OperandToValueString(req.RightOperand)) + ":";
            } else {
                if (IsMemAddr(req.LeftOperand))
                    virt.insert(prefix + OperandToValueString(req.LeftOperand));
                if (IsMemAddr(req.RightOperand))
                    virt.insert(prefix + OperandToValueString(req.RightOperand));
                prefix.clear();
            }
        }
    }

    if (virt.size() <= 1) {
        issues.append(ValidationIssue(
            FeedbackSeverity::Fail, "One-Condition Achievement",
            "One-condition achievements are dangerous and should be avoided. Trigger logic should make use of, at minimum, two different "
            "memory requirements to avoid misfires.",
            std::nullopt, "https://docs.retroachievements.org/developer-docs/tips-and-tricks.html#achievement-creation-tips", asset,
            "Logic", segmentName, -1));
    }
}

void LogicValidator::CheckDeltas(const QList<AchievementConditionGroup>& groups, QList<ValidationIssue>& issues,
                                 std::shared_ptr<IAssetItem> asset, const QString& segmentName) {
    if (groups.isEmpty())
        return;

    bool hasAnyDelta = false;
    for (const auto& g : groups) {
        for (const auto& c : g.Conditions) {
            if (c.LeftOperand.Type == "Delta" || c.RightOperand.Type == "Delta") {
                hasAnyDelta = true;
                break;
            }
        }
        if (hasAnyDelta)
            break;
    }

    if (!hasAnyDelta) {
        issues.append(
            ValidationIssue(FeedbackSeverity::Fail, "Missing Delta",
                            "Logic must contain a Delta to isolate the specific moment that an asset should trigger.", std::nullopt,
                            "https://docs.retroachievements.org/developer-docs/delta-values.html", asset, "Logic", segmentName, -1));
        return;
    }

    QSet<QString> coreMemSet;
    {
        QString prefix;
        for (const auto& req : groups[0].Conditions) {
            if (req.Flag == "Add Address") {
                prefix += OperandToValueString(req.LeftOperand) +
                          (req.Operator.isEmpty() ? QString() : req.Operator + OperandToValueString(req.RightOperand)) + ":";
            } else {
                if (GetAddrType(req.LeftOperand) == AddrType::Current)
                    coreMemSet.insert(prefix + OperandToValueString(req.LeftOperand));
                if (GetAddrType(req.RightOperand) == AddrType::Current)
                    coreMemSet.insert(prefix + OperandToValueString(req.RightOperand));
                prefix.clear();
            }
        }
    }

    QSet<QString> coreUnmatchedDeltaSet;
    QList<bool> deltaGroups;
    deltaGroups.reserve(groups.size());

    for (int gi = 0; gi < groups.size(); ++gi) {
        const auto& g = groups[gi];
        // If the group contains an always-false condition, it can never trigger.
        // Therefore, it doesn't need a delta. Consider it "valid" for this check.
        bool hasAlwaysFalse = false;
        for (const auto& req : g.Conditions) {
            if (IsConditionAlwaysFalse(req)) {
                hasAlwaysFalse = true;
                break;
            }
        }
        if (hasAlwaysFalse) {
            deltaGroups.append(true);
            continue;
        }

        QSet<QString> memSet = coreMemSet;
        {
            QString prefix;
            for (const auto& req : g.Conditions) {
                if (req.Flag == "Add Address") {
                    prefix += OperandToValueString(req.LeftOperand) +
                              (req.Operator.isEmpty() ? QString() : req.Operator + OperandToValueString(req.RightOperand)) + ":";
                } else {
                    if (GetAddrType(req.LeftOperand) == AddrType::Current)
                        memSet.insert(prefix + OperandToValueString(req.LeftOperand));
                    if (GetAddrType(req.RightOperand) == AddrType::Current)
                        memSet.insert(prefix + OperandToValueString(req.RightOperand));
                    prefix.clear();
                }
            }
        }

        bool groupValid = false;
        bool hasDelta = false;
        QString prefix;
        for (const auto& req : g.Conditions) {
            if (req.Flag == "Add Address") {
                prefix += OperandToValueString(req.LeftOperand) +
                          (req.Operator.isEmpty() ? QString() : req.Operator + OperandToValueString(req.RightOperand)) + ":";
            } else {
                // a delta only counts if it has a matching mem value
                for (const Operand* op : {&req.LeftOperand, &req.RightOperand}) {
                    if (op->Type == "Delta") {
                        const QString key = prefix + OperandToValueString(*op);
                        const bool deltaHasMem = memSet.contains(key);
                        hasDelta = hasDelta || deltaHasMem;

                        if (!deltaHasMem && gi == 0)
                            coreUnmatchedDeltaSet.insert(key);
                    }
                }
                prefix.clear();
            }

            if (IsTerminatingFlag(req.Flag)) {
                // this is the end of a chain that contained a delta and wasnt a reset or pause
                if (hasDelta && !IsPauseOrResetFlag(req.Flag)) {
                    groupValid = true;
                    break;
                }
                hasDelta = false;
            }
        }
        deltaGroups.append(groupValid);
    }

    // either the core group must have the valid mem/delta check, or *all* alt groups
    bool allAltsValid = (deltaGroups.size() > 1);
    for (int gi = 1; gi < deltaGroups.size(); ++gi) {
        if (!deltaGroups[gi]) {
            allAltsValid = false;
            break;
        }
    }
    if (deltaGroups[0] || allAltsValid)
        return;

    // At this point, delta_groups[0] could be "wrongfully" tagged as false in the case the core holds a delta that is
    //  matched by a Mem in each alt (but not in core)
    for (const QString& coreUnmatchedDelta : coreUnmatchedDeltaSet) {
        if (groups.size() <= 1)
            break;

        bool allAltsMatchMem = true;
        for (int gi = 1; gi < groups.size(); ++gi) {
            const auto& g = groups[gi];
            bool hasAlwaysFalse = false;
            for (const auto& req : g.Conditions) {
                if (IsConditionAlwaysFalse(req)) {
                    hasAlwaysFalse = true;
                    break;
                }
            }
            if (hasAlwaysFalse)
                continue;

            bool matched = false;
            QString prefix;
            for (const auto& req : g.Conditions) {
                if (req.Flag == "Add Address") {
                    prefix += OperandToValueString(req.LeftOperand) +
                              (req.Operator.isEmpty() ? QString() : req.Operator + OperandToValueString(req.RightOperand)) + ":";
                } else {
                    for (const Operand* op : {&req.LeftOperand, &req.RightOperand}) {
                        if (op->Type == "Mem" && coreUnmatchedDelta == (prefix + OperandToValueString(*op))) {
                            matched = true;
                            break;
                        }
                    }
                    if (matched)
                        break;
                    prefix.clear();
                }
            }

            if (!matched) {
                allAltsMatchMem = false;
                break;
            }
        }

        // We only need one delta in core that is matched by a mem in all alts
        if (allAltsMatchMem)
            return;
    }

    // we know there's an issue
    issues.append(
        ValidationIssue(FeedbackSeverity::Warning, "Improper Delta",
                        "Proper use of Delta can help identify the precise moment that an asset should trigger. Appropriate use requires "
                        "a Delta matched by a corresponding Mem on the same address (not in ResetIf/ResetNextIf/PauseIf) in the Core group "
                        "or in ALL Alt groups.",
                        std::nullopt, "https://docs.retroachievements.org/developer-docs/delta-values.html", asset, "Logic", segmentName,
                        -1));
}

bool LogicValidator::IsChainFlag(const QString& flag) {
    return flag == "Add Source" || flag == "Sub Source" || flag == "Add Hits" || flag == "Sub Hits" || flag == "Add Address" ||
           flag == "AndNext" || flag == "OrNext" || flag == "ResetNextIf" || flag == "Remember";
}

// Check for undocumented memory addresses (Missing Code Notes)
void LogicValidator::CheckMissingNotes(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                                       std::shared_ptr<IAssetItem> asset, const QString& segmentName,
                                       const QMap<QString, QList<std::shared_ptr<NoteNode>>>& notesLookup,
                                       const QList<ConditionFormatter::RangeEntry>* rangeCache,
                                       const QList<ParsedCodeNote>& parsedNotes) {
    auto ParseHex = [](const QString& s) -> int64_t {
        if (s.trimmed().isEmpty())
            return -1;
        QString clean = s.trimmed();
        clean.remove('|');
        bool isNeg = clean.startsWith('-');
        if (isNeg || clean.startsWith('+'))
            clean = clean.mid(1).trimmed();
        clean.remove("0x", Qt::CaseInsensitive);
        static const QRegularExpression hexFilterRegex("[^0-9A-Fa-f]");
        clean.remove(hexFilterRegex);
        bool ok = false;
        int64_t v = clean.toLongLong(&ok, 16);
        if (!ok)
            return -1;
        return isNeg ? -v : v;
    };

    struct ChainStep {
        bool IsBase = false;
        QString Value;
    };

    QList<ChainStep> chain;
    bool chainIsDynamic = false;

    for (int i = 0; i < group.Conditions.size(); i++) {
        const auto& cond = group.Conditions[i];

        // Pointer Chain Logic:
        // If this requirement is an Add Address, we build up the chain context
        // and skip validation, as these are intermediate steps.
        if (cond.Flag == "Add Address") {
            if (cond.LeftOperand.Type == "Recall") {
                chainIsDynamic = true;
            }

            if (chain.isEmpty()) {
                bool hasEffectiveNote = false;
                uint64_t addrVal = 0;
                if (TryParseAddress(cond.LeftOperand, addrVal)) {
                    hasEffectiveNote =
                        (ConditionFormatter::GetEffectiveNote(notesLookup, static_cast<int64_t>(addrVal), rangeCache) != nullptr) ||
                        (GetEffectiveParsedNote(parsedNotes, static_cast<int64_t>(addrVal)) != nullptr);
                }

                if (!IsMemAddr(cond.LeftOperand) || !hasEffectiveNote) {
                    if (IsMemAddr(cond.LeftOperand)) {
                        const QString msg = QString("Address %1 has no Code Note.").arg(ToDisplayHex(static_cast<int64_t>(addrVal)));
                        issues.append(ValidationIssue(
                            FeedbackSeverity::Fail, "Undocumented Memory", msg, cond,
                            "https://docs.retroachievements.org/guidelines/content/code-notes.html", asset, "Logic", segmentName,
                            groupIndex));
                    }
                    // Do not add to chain; treat as index adjustment
                    continue;
                }
            }

            if (chain.isEmpty() && IsMemAddr(cond.LeftOperand)) {
                chain.append({true, cond.LeftOperand.Value});
            } else if (IsMemAddr(cond.LeftOperand)) {
                chain.append({false, cond.LeftOperand.Value});
            }
            if (IsMemAddr(cond.RightOperand)) {
                chain.append({false, cond.RightOperand.Value});
            }
            continue;
        }

        if (chainIsDynamic) {
            chain.clear();
            chainIsDynamic = false;
            continue;
        }

        int64_t lastReport = -1;
        for (const Operand* opPtr : {&cond.LeftOperand, &cond.RightOperand}) {
            const Operand& op = *opPtr;
            if (!IsMemAddr(op))
                continue;

            uint64_t addrUint = 0;
            if (!TryParseAddress(op, addrUint))
                continue;
            const int64_t addrVal = static_cast<int64_t>(addrUint);

            const QString address = LogicFormatter::NormalizeAddress(op.Value);

            // ResolveAlias handles the pointer chain math (looking backwards at AddAddress lines)
            // and returns the note name for the final resolved address (Leaf).
            const QString alias = ConditionFormatter::ResolveAlias(address, op.Size, &group, i, notesLookup, rangeCache);
            bool hasNote = !alias.trimmed().isEmpty();

            if (!hasNote && !parsedNotes.isEmpty()) {
                // Direct containment check (covers notes with non-bracketed sizes like "16-bit Health"
                // as well as AutoCR's direct-note fallback for array indexing).
                if (FindNoteContainingAddr(parsedNotes, addrVal) != nullptr)
                    hasNote = true;
            }

            if (!hasNote) {
                if (lastReport == addrVal)
                    continue;
                lastReport = addrVal;

                QString msg = QString("Address %1 has no Code Note.").arg(ToDisplayHex(addrVal));

                // Improved Detection: Check if this is a failure in a Pointer Chain where the Base IS documented
                if (!chain.isEmpty()) {
                    const QString baseStr = chain[0].Value;
                    const QString normBase = LogicFormatter::NormalizeAddress(baseStr);

                    QList<std::shared_ptr<NoteNode>> currentNodes;
                    bool hasCurrentNodes = false;
                    QString contextMsg = "";
                    int64_t matchedRangeRel = -1;

                    // Try Direct Lookup
                    if (notesLookup.contains(normBase)) {
                        currentNodes = notesLookup.value(normBase);
                        hasCurrentNodes = true;
                    }
                    // Try Range Cache Lookup
                    else if (rangeCache != nullptr) {
                        int64_t baseVal = ParseHex(baseStr);
                        if (baseVal != -1) {
                            const ConditionFormatter::RangeEntry* bestRange = nullptr;
                            for (const auto& match : *rangeCache) {
                                if (baseVal >= match.Start && baseVal < match.End && match.Node != nullptr &&
                                    !match.Node->IsPointerArray) {
                                    if (bestRange == nullptr || match.Start > bestRange->Start ||
                                        (match.Start == bestRange->Start &&
                                         (match.End - match.Start) < (bestRange->End - bestRange->Start))) {
                                        bestRange = &match;
                                    }
                                }
                            }
                            if (bestRange != nullptr) {
                                QString startKey = QString("0x%1").arg(bestRange->Start, 0, 16);
                                if (notesLookup.contains(startKey)) {
                                    currentNodes = notesLookup.value(startKey);
                                } else {
                                    currentNodes = {bestRange->Node};
                                }
                                hasCurrentNodes = true;
                                matchedRangeRel = baseVal - bestRange->Start;
                                contextMsg = " (Base found in Struct Range)";
                            }
                        }
                    }

                    if (hasCurrentNodes) {
                        // Base Found. Loop to resolve Redirects (Virtual Links).
                        int redirects = 0;
                        static const QRegularExpression referRegex(R"(refer to \$?0x([0-9a-f]+))",
                                                                   QRegularExpression::CaseInsensitiveOption);

                        while (redirects < 5) {
                            if (currentNodes.isEmpty())
                                break;

                            std::shared_ptr<NoteNode> root = nullptr;
                            for (const auto& n : currentNodes) {
                                if (n->IndentLevel == -2) {
                                    root = n;
                                    break;
                                }
                            }
                            if (root == nullptr)
                                root = currentNodes[0];

                            QString content = root->Content;

                            if (content.contains("refer to", Qt::CaseInsensitive)) {
                                auto match = referRegex.match(content);
                                if (match.hasMatch()) {
                                    QString targetAddr = match.captured(1);
                                    QString normTarget = LogicFormatter::NormalizeAddress(targetAddr);

                                    if (notesLookup.contains(normTarget)) {
                                        currentNodes = notesLookup.value(normTarget);
                                        contextMsg = QString(" (Base Linked to 0x%1, resolved)").arg(targetAddr);
                                        redirects++;
                                        continue;
                                    } else {
                                        contextMsg = QString(" (Base Linked to 0x%1 but target missing)").arg(targetAddr);
                                        break;
                                    }
                                }
                            }
                            break;
                        }

                        // 2. Trace the Chain using the Resolved Nodes
                        QString brokenOffset = "";
                        std::shared_ptr<NoteNode> parentContainer = nullptr;
                        QList<std::shared_ptr<NoteNode>> levelNodes;
                        for (const auto& n : currentNodes) {
                            if (n->IndentLevel == -2) {
                                parentContainer = n;
                                for (const auto& ch : n->Children) {
                                    if (ch->IndentLevel == -1)
                                        levelNodes.append(ch->Children);
                                    else if (ch->IndentLevel != -2)
                                        levelNodes.append(ch);
                                }
                            }
                        }
                        if (levelNodes.isEmpty()) {
                            for (const auto& n : currentNodes) {
                                if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                                    levelNodes.append(n);
                            }
                        }

                        if (matchedRangeRel != -1) {
                            auto next =
                                ConditionFormatter::FindBestMatchingNode(levelNodes, matchedRangeRel, false, nullptr, parentContainer);
                            if (next == nullptr) {
                                brokenOffset = QString("0x%1").arg(matchedRangeRel, 0, 16);
                            } else {
                                levelNodes = next->Children;
                                parentContainer = next;
                            }
                        }

                        // Iterate intermediate offsets
                        if (brokenOffset.isEmpty()) {
                            for (int k = 1; k < chain.size(); k++) {
                                int64_t offVal = ParseHex(chain[k].Value);
                                auto next =
                                    ConditionFormatter::FindBestMatchingNode(levelNodes, offVal, false, nullptr, parentContainer);

                                if (next == nullptr) {
                                    brokenOffset = chain[k].Value;
                                    break;
                                }
                                levelNodes = next->Children;
                                parentContainer = next;
                            }
                        }

                        if (!brokenOffset.isEmpty()) {
                            msg = QString("Pointer chain broken at intermediate offset +%1.%2 Verify note structure.")
                                      .arg(brokenOffset, contextMsg);
                        } else {
                            // Chain OK, check Leaf (op.Value)
                            int64_t leafVal = ParseHex(op.Value);

                            bool leafFound =
                                (ConditionFormatter::FindBestMatchingNode(levelNodes, leafVal, true, nullptr, parentContainer) !=
                                 nullptr);

                            if (!leafFound) {
                                msg = QString("Pointer leaf (+%1) not found in resolved structure of Base %2.%3")
                                          .arg(op.Value, baseStr, contextMsg);
                            } else {
                                msg = QString("ConditionFormatter.ResolveAlias failed but Validator trace succeeded. Base: %1%2")
                                          .arg(baseStr, contextMsg);
                            }
                        }
                    } else {
                        msg = QString("Pointer Base %1 is undocumented. Cannot resolve chain.").arg(baseStr);
                    }
                }

                issues.append(ValidationIssue(FeedbackSeverity::Fail, "Undocumented Memory", msg, cond,
                                              "https://docs.retroachievements.org/guidelines/content/code-notes.html", asset, "Logic",
                                              segmentName, groupIndex));
            }
        }

        chain.clear();
        chainIsDynamic = false;
    }
}

void LogicValidator::CheckPresentation(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues) {
    CheckTitleCase(asset, issues);
    CheckWritingMistakes(asset, issues);
    CheckBrackets(asset, issues);
}

void LogicValidator::CheckTitleCase(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues) {
    const auto analysis = TitleCaseAnalyzer::Analyze(asset->Title);
    if (!analysis.Changes.isEmpty()) {
        QStringList changeDescs;
        for (const auto& c : analysis.Changes) {
            changeDescs.append(QString("%1 -> %2 (%3)").arg(c.Word, c.Cased, c.Reason));
        }
        const QString desc = QString("Titles should be written in CMOS title case. Suggested: \"%1\" [%2]")
                                 .arg(analysis.Suggestion, changeDescs.join("; "));
        issues.append(ValidationIssue(FeedbackSeverity::Info, "Title Case", desc, std::nullopt,
                                      "https://en.wikipedia.org/wiki/Title_case#Chicago_Manual_of_Style", asset, "Presentation", "", -1,
                                      "title"));
    }
}

void LogicValidator::CheckWritingMistakes(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues) {
    static const QRegularExpression typographyPunctRe(QString::fromUtf8(R"([\x{2018}\x{2019}\x{201C}\x{201D}])"));
    static const QRegularExpression foreignRe(
        R"(([\p{Arabic}\p{Armenian}\p{Bengali}\p{Bopomofo}\p{Braille}\p{Buhid}\p{Canadian_Aboriginal}\p{Cherokee}\p{Cyrillic}\p{Devanagari}\p{Ethiopic}\p{Georgian}\p{Greek}\p{Gujarati}\p{Gurmukhi}\p{Han}\p{Hangul}\p{Hanunoo}\p{Hebrew}\p{Hiragana}\p{Inherited}\p{Kannada}\p{Katakana}\p{Khmer}\p{Lao}\p{Limbu}\p{Malayalam}\p{Mongolian}\p{Myanmar}\p{Ogham}\p{Oriya}\p{Runic}\p{Sinhala}\p{Syriac}\p{Tagalog}\p{Tagbanwa}\p{Tamil}\p{Telugu}\p{Thaana}\p{Thai}\p{Tibetan}\p{Yi}]+))",
        QRegularExpression::UseUnicodePropertiesOption);
    static const QRegularExpression nonAsciiRe(R"(([^\x{0000}-\x{007F}\x{00A5}\x{00A3}\p{L}]+))",
                                               QRegularExpression::UseUnicodePropertiesOption);

    const QList<QPair<QString, QString>> fields = {{"title", asset->Title}, {"desc", asset->Description}};

    for (const auto& field : fields) {
        const QString& elt = field.first;
        const QString& text = field.second;
        const QString fieldLabel = (elt == "title") ? "Title" : "Description";

        if (ContainsEmojiPresentation(text)) {
            issues.append(ValidationIssue(FeedbackSeverity::Warning, "Emoji in Text",
                                          QString("Asset titles and descriptions may not contain emoji (in %1).").arg(fieldLabel),
                                          std::nullopt,
                                          "https://docs.retroachievements.org/guidelines/content/writing-policy.html#emojis", asset,
                                          "Presentation", "", -1, elt));
        } else if (typographyPunctRe.match(text).hasMatch()) {
            QString corrected = text;
            corrected.replace(QChar(0x2018), '\'').replace(QChar(0x2019), '\'').replace(QChar(0x201C), '"').replace(QChar(0x201D), '"');
            issues.append(ValidationIssue(
                FeedbackSeverity::Warning, "Smart Quotes / Special Characters",
                QString("Smart quotes in %1 can cause rendering issues in emulators. Suggested: \"%2\"").arg(fieldLabel, corrected),
                std::nullopt, "https://docs.retroachievements.org/developer-docs/tips-and-tricks.html#naming-convention-tips", asset,
                "Presentation", "", -1, elt));
        } else {
            const auto foreignMatch = foreignRe.match(text);
            if (foreignMatch.hasMatch()) {
                issues.append(ValidationIssue(
                    FeedbackSeverity::Info, "Foreign Language Characters",
                    QString("Asset titles and descriptions should be written in English (found \"%1\" in %2).")
                        .arg(foreignMatch.captured(1), fieldLabel),
                    std::nullopt, "https://docs.retroachievements.org/guidelines/content/writing-policy.html#language", asset,
                    "Presentation", "", -1, elt));
            } else {
                const auto nonAsciiMatch = nonAsciiRe.match(text);
                if (nonAsciiMatch.hasMatch()) {
                    issues.append(ValidationIssue(
                        FeedbackSeverity::Warning, "Special Characters",
                        QString("Avoid using accented/special characters in %1 (found \"%2\"), as they can have rendering issues.")
                            .arg(fieldLabel, nonAsciiMatch.captured(1)),
                        std::nullopt,
                        "https://docs.retroachievements.org/developer-docs/tips-and-tricks.html#naming-convention-tips", asset,
                        "Presentation", "", -1, elt));
                }
            }
        }
    }
}

void LogicValidator::CheckBrackets(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues) {
    static const QRegularExpression bracketsRe(R"(.[\{\[\(](.+)[\}\]\)])");
    if (bracketsRe.match(asset->Description.trimmed()).hasMatch()) {
        issues.append(ValidationIssue(
            FeedbackSeverity::Info, "Brackets in Description",
            "Asset descriptions should avoid brackets where possible.", std::nullopt,
            "https://docs.retroachievements.org/guidelines/content/writing-policy.html#brackets-parentheses", asset, "Presentation", "",
            -1, "desc"));
    }
}

QList<ValidationIssue> LogicValidator::ValidateCodeNotes(const QList<CodeNote>& notes, int consoleId) {
    QList<ValidationIssue> issues;
    if (notes.isEmpty())
        return issues;

    const QList<ParsedCodeNote> parsedNotes = BuildParsedNotes(notes);

    // 1. check_notes_bad_regions
    const ConsoleInfo* consoleInfo = ConsoleRegions::GetConsoleInfo(consoleId);
    if (consoleInfo != nullptr && !consoleInfo->Regions.isEmpty()) {
        const auto& regions = consoleInfo->Regions;
        int ri = 0;
        for (const auto& note : parsedNotes) {
            if (note.IsGeneralGameNotesOnly() || note.Addr < 0)
                continue;

            while (ri < regions.size() && note.Addr > regions[ri].End)
                ri++;
            if (ri >= regions.size())
                break;

            const auto& r = regions[ri];
            if (note.Addr >= r.Start) {
                QString desc = QString("Code note at %1 (%2) appears in the %3 region (%4-%5).")
                                   .arg(note.AddressHex, note.Header(), r.Name, ToDisplayHex(r.Start), ToDisplayHex(r.End));
                if (!r.IsError) {
                    desc += QString(" This is only a warning based on the way memory on %1 is commonly used.").arg(consoleInfo->Name);
                }
                if (r.Transform) {
                    desc += QString(" Suggested fix: Move this note to %1.").arg(ToDisplayHex(r.Transform(note.Addr)));
                }
                issues.append(ValidationIssue(
                    r.IsError ? FeedbackSeverity::Fail : FeedbackSeverity::Info, "Bad Memory Region (Code Note)", desc, std::nullopt,
                    "https://docs.retroachievements.org/developer-docs/console-specific-tips.html", nullptr, "Code Notes", "", -1, "",
                    note.AddressHex));
            }
        }
    }

    // 2. check_notes_missing_size
    for (const auto& note : parsedNotes) {
        if (note.TypeName.isEmpty() && note.Size == 1 && !note.IsGeneralGameNotesOnly()) {
            issues.append(ValidationIssue(
                FeedbackSeverity::Fail, "Code Note Missing Size",
                QString("Code note at %1 (%2) must have size information.").arg(note.AddressHex, note.Header()), std::nullopt,
                "https://docs.retroachievements.org/guidelines/content/code-notes.html#specifying-memory-addresses-size", nullptr,
                "Code Notes", "", -1, "", note.AddressHex));
        }
    }

    // 3. check_notes_enum_hex
    static const QRegularExpression numericRe(R"(\b(0x)?([0-9a-f]{2,})\b)", QRegularExpression::CaseInsensitiveOption);
    static const QRegularExpression hexLetterRe(R"([a-f])", QRegularExpression::CaseInsensitiveOption);
    for (const auto& note : parsedNotes) {
        if (note.Enumerations.isEmpty())
            continue;
        QStringList found;
        for (const auto& entry : note.Enumerations) {
            auto it = numericRe.globalMatch(entry.Literal);
            while (it.hasNext()) {
                const auto m = it.next();
                if (hexLetterRe.match(m.captured(2)).hasMatch() && m.captured(1).isEmpty()) {
                    found.append(entry.Literal);
                }
            }
        }
        if (!found.isEmpty()) {
            issues.append(ValidationIssue(
                FeedbackSeverity::Fail, "Unprefixed Hex Enumeration",
                QString("Enumerated hex values in code note at %1 (%2) should be prefixed with \"0x\": %3")
                    .arg(note.AddressHex, note.Header(), found.join(", ")),
                std::nullopt,
                "https://docs.retroachievements.org/guidelines/content/code-notes.html#adding-values-and-labels", nullptr, "Code Notes", "",
                -1, "", note.AddressHex));
        }
    }

    // 4. check_notes_enum_size_mismatch
    for (const auto& note : parsedNotes) {
        if (note.Enumerations.isEmpty() || note.TypeName.isEmpty() || note.HasInfiniteMaxValue)
            continue;
        QStringList found;
        for (const auto& entry : note.Enumerations) {
            if (entry.Value > static_cast<double>(note.TypeMaxValue))
                found.append(entry.Literal);
        }
        if (!found.isEmpty()) {
            issues.append(ValidationIssue(
                FeedbackSeverity::Fail, "Enumeration Exceeds Note Size",
                QString("Code note at %1 (%2) is marked %3 (max 0x%4), but enumerated values are too large: %5")
                    .arg(note.AddressHex, note.Header(), note.TypeName, QString::number(note.TypeMaxValue, 16).toUpper(), found.join(", ")),
                std::nullopt,
                "https://docs.retroachievements.org/guidelines/content/code-notes.html#adding-values-and-labels", nullptr, "Code Notes", "",
                -1, "", note.AddressHex));
        }
    }

    return issues;
}

QList<ValidationIssue> LogicValidator::ValidateSet(const QList<std::shared_ptr<IAssetItem>>& allAssets) {
    QList<ValidationIssue> issues;

    QList<std::shared_ptr<IAssetItem>> achievements;
    QList<std::shared_ptr<IAssetItem>> leaderboards;

    for (const auto& asset : allAssets) {
        if (!NeedsFeedback(asset.get()))
            continue;
        if (asset->isLeaderboard())
            leaderboards.append(asset);
        else
            achievements.append(asset);
    }

    // 1. check_progression_typing
    bool hasWin = false;
    bool hasProg = false;
    for (const auto& ach : achievements) {
        if (ach->Type == "win_condition")
            hasWin = true;
        else if (ach->Type == "progression")
            hasProg = true;
    }

    if (!hasWin && !hasProg) {
        issues.append(ValidationIssue(
            FeedbackSeverity::Warning, "No Progression/Win Typing",
            "Set lacks progression and win condition typing.", std::nullopt,
            "https://docs.retroachievements.org/guidelines/content/progression-and-win-condition-guidelines.html#progression-conditions",
            nullptr, "Set Design"));
    } else if (!hasProg) {
        issues.append(ValidationIssue(
            FeedbackSeverity::Info, "No Progression Typing",
            "Set lacks progression achievements (win conditions found). This might be unavoidable, depending on the game, but progression "
            "achievements should be added when possible.",
            std::nullopt,
            "https://docs.retroachievements.org/guidelines/content/progression-and-win-condition-guidelines.html#progression-conditions",
            nullptr, "Set Design"));
    }

    // 2. check_duplicate_text
    QMap<QString, QList<std::shared_ptr<IAssetItem>>> achTitleGroups;
    QMap<QString, QList<std::shared_ptr<IAssetItem>>> achDescGroups;
    for (const auto& ach : achievements) {
        achTitleGroups[ach->Title].append(ach);
        achDescGroups[ach->Description].append(ach);
    }

    for (auto it = achTitleGroups.cbegin(); it != achTitleGroups.cend(); ++it) {
        if (it.value().size() > 1) {
            issues.append(ValidationIssue(
                FeedbackSeverity::Fail, "Duplicate Achievement Titles",
                QString("%1 achievements share the title \"%2\".").arg(it.value().size()).arg(it.key()), std::nullopt, "", nullptr,
                "Set Design"));
        }
    }

    for (auto it = achDescGroups.cbegin(); it != achDescGroups.cend(); ++it) {
        if (it.value().size() > 1) {
            QStringList titles;
            for (const auto& a : it.value())
                titles.append(a->Title);
            issues.append(ValidationIssue(
                FeedbackSeverity::Fail, "Duplicate Achievement Descriptions",
                QString("%1 achievements share the same description: %2").arg(it.value().size()).arg(titles.join(", ")), std::nullopt, "",
                nullptr, "Set Design"));
        }
    }

    QMap<QString, QList<std::shared_ptr<IAssetItem>>> lbTitleGroups;
    for (const auto& lb : leaderboards) {
        lbTitleGroups[lb->Title].append(lb);
    }

    for (auto it = lbTitleGroups.cbegin(); it != lbTitleGroups.cend(); ++it) {
        if (it.value().size() > 1) {
            issues.append(ValidationIssue(
                FeedbackSeverity::Fail, "Duplicate Leaderboard Titles",
                QString("%1 leaderboards share the title \"%2\".").arg(it.value().size()).arg(it.key()), std::nullopt, "", nullptr,
                "Set Design"));
        }
    }

    return issues;
}

} // namespace RALE
