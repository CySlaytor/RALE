#include "SnippetManager.h"
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>

namespace RALE {

QList<Snippet> SnippetManager::_cache;

QString SnippetManager::GetSnippetPath()
{
    return QDir(QCoreApplication::applicationDirPath()).filePath("snippets.json");
}

QList<Snippet> SnippetManager::Load()
{
    QString path = GetSnippetPath();
    if (!QFile::exists(path)) return {};
    try
    {
        QFile file(path);
        if (file.open(QIODevice::ReadOnly | QIODevice::Text))
        {
            QByteArray json = file.readAll();
            file.close();
            auto doc = QJsonDocument::fromJson(json);
            if (doc.isArray())
            {
                _cache.clear();
                for (const auto &val : doc.array())
                {
                    if (val.isObject())
                    {
                        auto obj = val.toObject();
                        Snippet s;
                        s.Name = obj.value("Name").toString();
                        if (obj.contains("LogicCode"))
                            s.Logic = obj.value("LogicCode").toString();
                        else if (obj.contains("Logic"))
                            s.Logic = obj.value("Logic").toString();
                        _cache.append(s);
                    }
                }
            }
            else
            {
                // C# treats any non-deserialisable document as an empty library.
                _cache.clear();
            }
        }
    }
    catch (...)
    {
        _cache.clear();
    }
    return _cache;
}

void SnippetManager::SaveSnippet(const QString &name, const QString &logic)
{
    // Ensure loaded
    if (_cache.isEmpty() && QFile::exists(GetSnippetPath())) Load();

    Snippet newSnippet;
    newSnippet.Name = name;
    newSnippet.Logic = logic;
    _cache.append(newSnippet);
    SaveDisk();
}

void SnippetManager::DeleteSnippet(const Snippet &snippet)
{
    for (int i = 0; i < _cache.size(); i++)
    {
        if (_cache[i].Name == snippet.Name && _cache[i].Logic == snippet.Logic)
        {
            _cache.removeAt(i);
            SaveDisk();
            break;
        }
    }
}

void SnippetManager::SaveDisk()
{
    try
    {
        QJsonArray arr;
        for (const auto &s : _cache)
        {
            QJsonObject obj;
            obj.insert("Name", s.Name);
            // The C# model exposes this property as "LogicCode" (JsonPropertyName);
            // writing "Logic" made Qt-saved snippets load with empty logic in C#.
            obj.insert("LogicCode", s.Logic);
            arr.append(obj);
        }

        QFile file(GetSnippetPath());
        if (file.open(QIODevice::WriteOnly | QIODevice::Text))
        {
            QJsonDocument doc(arr);
            file.write(doc.toJson(QJsonDocument::Indented));
            file.close();
        }
    }
    catch (...) {}
}

} // namespace RALE
