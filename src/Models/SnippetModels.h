#pragma once

#include <QString>

namespace RALogicEditor {

class Snippet
{
public:
    QString Name = "";
    QString Logic = "";

    // Legacy fallback to gracefully support loading older snippets 
    // that were saved before the model property was renamed.
    QString getLogicCode() const { return Logic; }
    void setLogicCode(const QString &value) { Logic = value; }
};

} // namespace RALogicEditor
