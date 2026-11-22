#pragma once

#include <QList>
#include <QString>

namespace RALE {

class TitleCaseAnalyzer {
  public:
    struct WordChange {
        QString Word;
        QString Cased;
        QString Reason;
    };

    struct Result {
        QString Suggestion;
        QList<WordChange> Changes;
    };

    static Result Analyze(const QString& phrase);
};

} // namespace RALE
