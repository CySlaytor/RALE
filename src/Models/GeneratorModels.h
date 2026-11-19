#pragma once

#include <QString>
#include <QList>

namespace RALE {

class GeneratorRule
{
public:
    QString SourceAddress = "";
    int Stride = 0;

    bool operator==(const GeneratorRule &other) const
    {
        return SourceAddress == other.SourceAddress && Stride == other.Stride;
    }
    bool operator!=(const GeneratorRule &other) const
    {
        return !(*this == other);
    }
};

// Models for the logic generator functionality
class GeneratorDefinition
{
public:
    int Copies = 0;

    // Determines if we create new Alt groups (False) or append to the current group (True)
    bool IsArray = false;

    // Tracks how many conditions formed the original "Template".
    // This allows the ViewModel to clear previous generations before applying new ones.
    int TemplateConditionCount = 0;

    QList<GeneratorRule> Rules;

    bool operator==(const GeneratorDefinition &other) const
    {
        return Copies == other.Copies &&
               IsArray == other.IsArray &&
               TemplateConditionCount == other.TemplateConditionCount &&
               Rules == other.Rules;
    }
    bool operator!=(const GeneratorDefinition &other) const
    {
        return !(*this == other);
    }
};

} // namespace RALE
