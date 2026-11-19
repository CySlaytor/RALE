#pragma once

#include <QString>
#include <QList>
#include "../Models/SnippetModels.h"

namespace RALE {

class SnippetManager
{
public:
    static QList<Snippet> Load();
    static void SaveSnippet(const QString &name, const QString &logic);
    static void DeleteSnippet(const Snippet &snippet);

private:
    static void SaveDisk();
    static QString GetSnippetPath();
    static QList<Snippet> _cache;
};

} // namespace RALE
