#pragma once

#include <QString>
#include <cstdint>

namespace RALogicEditor {

enum class NamingStyle
{
    AppendSlot,
    ReplaceSlot1,
    ReplacePlayer1,
    Custom
};

enum class StrideTarget
{
    All,
    ParentsOnly,
    ChildrenOnly
};

class CodeNoteMultiplier
{
public:
    static QString GenerateText(
        const QString &templateText,
        int copies,
        int64_t stride,
        NamingStyle naming,
        const QString &search,
        const QString &replace,
        int startNumber,
        StrideTarget target);

    static QString ApplyNamingToNote(
        const QString &noteText,
        NamingStyle style,
        const QString &search,
        const QString &replace,
        int number);

private:
    static QString ApplyNaming(
        const QString &input,
        NamingStyle style,
        const QString &search,
        const QString &replace,
        int number);
};

} // namespace RALogicEditor
