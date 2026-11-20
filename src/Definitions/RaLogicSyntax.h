#pragma once

#include <QChar>
#include <QMap>
#include <QSet>
#include <QString>
#include <QStringList>

namespace RALE {

// Centralizes all static definitions for RA Logic syntax
class RaLogicSyntax {
  public:
    static const QString NO_FLAG_TEXT;
    static const QString NO_OPERATOR_TEXT;

    static const QStringList Flags;
    static const QStringList OperandTypes;
    static const QStringList MemorySizes;
    static const QStringList ComparisonOperators;
    static const QStringList ArithmeticOperators;

    static const QSet<QString> ArithmeticFlags;
    static const QSet<QString> StrictComparisonFlags;
    // Removed AndNext and OrNext to allow Hit Counts on these flags
    static const QSet<QString> FlagsWithoutHits;
    static const QSet<QString> MemoryTypes;

    static const QMap<QString, QChar> ReverseFlagMap;
    static const QMap<QString, QChar> ReverseSizeMap;
    static const QMap<QString, QChar> ReverseFloatSizeMap;
    static const QMap<QChar, QString> FloatSizeMap;

    // Platform pointer descriptions and masking mapped to pointer-oriented RetroAchievements consoles
    static QString GetPlatformPointerTemplate(int consoleId, const QString& noteHeader = "");
    static QString GetPlatformPointerPrefix(int consoleId);
    static QString GetPlatformPointerMask(int consoleId);
};

} // namespace RALE
