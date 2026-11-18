#include "AppSettings.h"
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>

namespace RALogicEditor {

UserSettings AppSettings::Current;

QString AppSettings::GetSettingsPath()
{
    return QDir(QCoreApplication::applicationDirPath()).filePath("settings.json");
}

void AppSettings::Load()
{
    QString path = GetSettingsPath();
    if (QFile::exists(path))
    {
        try
        {
            QFile file(path);
            if (file.open(QIODevice::ReadOnly | QIODevice::Text))
            {
                QByteArray json = file.readAll();
                file.close();
                auto doc = QJsonDocument::fromJson(json);
                if (doc.isObject())
                {
                    auto obj = doc.object();
                    Current.ShowLogicReviewer = obj.value("ShowLogicReviewer").toBool(Current.ShowLogicReviewer);
                    Current.Theme = obj.value("Theme").toString(Current.Theme);
                    Current.WindowX = obj.value("WindowX").toInt(Current.WindowX);
                    Current.WindowY = obj.value("WindowY").toInt(Current.WindowY);
                    Current.WindowWidth = obj.value("WindowWidth").toInt(Current.WindowWidth);
                    Current.WindowHeight = obj.value("WindowHeight").toInt(Current.WindowHeight);
                    Current.IsMaximized = obj.value("IsMaximized").toBool(Current.IsMaximized);

                    Current.MainSplitterDistance = obj.value("MainSplitterDistance").toInt(Current.MainSplitterDistance);
                    Current.EditorSplitterDistance = obj.value("EditorSplitterDistance").toInt(Current.EditorSplitterDistance);
                    Current.VerticalSplitterDistance = obj.value("VerticalSplitterDistance").toInt(Current.VerticalSplitterDistance);

                    Current.LastAssetTypeIndex = obj.value("LastAssetTypeIndex").toInt(Current.LastAssetTypeIndex);
                    Current.LastCategoryIndex = obj.value("LastCategoryIndex").toInt(Current.LastCategoryIndex);
                    Current.LastSourceIndex = obj.value("LastSourceIndex").toInt(Current.LastSourceIndex);

                    Current.Trigger_ShowDecimal = obj.value("Trigger_ShowDecimal").toBool(Current.Trigger_ShowDecimal);
                    Current.Trigger_ShowAliases = obj.value("Trigger_ShowAliases").toBool(Current.Trigger_ShowAliases);
                    Current.Trigger_CollapseChains = obj.value("Trigger_CollapseChains").toBool(Current.Trigger_CollapseChains);

                    Current.Notes_ShowLeafsOnly = obj.value("Notes_ShowLeafsOnly").toBool(Current.Notes_ShowLeafsOnly);
                    Current.Notes_IndentValues = obj.value("Notes_IndentValues").toBool(Current.Notes_IndentValues);
                    Current.NoteIndentStyle = obj.value("NoteIndentStyle").toString(Current.NoteIndentStyle);

                    // Code Note Formatting Options
                    Current.CodeNotePrefix = obj.value("CodeNotePrefix").toString(Current.CodeNotePrefix);
                    Current.CodeNoteSuffix = obj.value("CodeNoteSuffix").toString(Current.CodeNoteSuffix);
                    Current.CodeNoteAlignSuffixes = obj.value("CodeNoteAlignSuffixes").toBool(Current.CodeNoteAlignSuffixes);
                    Current.CodeNoteSuffixOnLastLineOnly = obj.value("CodeNoteSuffixOnLastLineOnly").toBool(Current.CodeNoteSuffixOnLastLineOnly);
                    Current.CodeNoteAutoPopulatePointers = obj.value("CodeNoteAutoPopulatePointers").toBool(Current.CodeNoteAutoPopulatePointers);

                    if (obj.contains("CodeNoteUnicodeTree"))
                    {
                        Current.CodeNoteUnicodeTree = obj.value("CodeNoteUnicodeTree").toBool(Current.CodeNoteUnicodeTree);
                    }
                    else if (obj.contains("NoteIndentStyle"))
                    {
                        Current.CodeNoteUnicodeTree = obj.value("NoteIndentStyle").toString().startsWith("Unicode Tree", Qt::CaseInsensitive);
                    }

                    if (obj.contains("CodeNoteIndentDescriptions"))
                    {
                        Current.CodeNoteIndentDescriptions = obj.value("CodeNoteIndentDescriptions").toBool(Current.CodeNoteIndentDescriptions);
                    }
                    else if (obj.contains("Notes_IndentValues"))
                    {
                        Current.CodeNoteIndentDescriptions = obj.value("Notes_IndentValues").toBool(Current.CodeNoteIndentDescriptions);
                    }
                }
            }
        }
        catch (...) { /* Ignore errors, use defaults */ }
    }
}

void AppSettings::Save()
{
    try
    {
        QJsonObject obj;
        obj.insert("ShowLogicReviewer", Current.ShowLogicReviewer);
        obj.insert("Theme", Current.Theme);
        obj.insert("WindowX", Current.WindowX);
        obj.insert("WindowY", Current.WindowY);
        obj.insert("WindowWidth", Current.WindowWidth);
        obj.insert("WindowHeight", Current.WindowHeight);
        obj.insert("IsMaximized", Current.IsMaximized);

        obj.insert("MainSplitterDistance", Current.MainSplitterDistance);
        obj.insert("EditorSplitterDistance", Current.EditorSplitterDistance);
        obj.insert("VerticalSplitterDistance", Current.VerticalSplitterDistance);

        obj.insert("LastAssetTypeIndex", Current.LastAssetTypeIndex);
        obj.insert("LastCategoryIndex", Current.LastCategoryIndex);
        obj.insert("LastSourceIndex", Current.LastSourceIndex);

        obj.insert("Trigger_ShowDecimal", Current.Trigger_ShowDecimal);
        obj.insert("Trigger_ShowAliases", Current.Trigger_ShowAliases);
        obj.insert("Trigger_CollapseChains", Current.Trigger_CollapseChains);

        obj.insert("Notes_ShowLeafsOnly", Current.Notes_ShowLeafsOnly);
        obj.insert("Notes_IndentValues", Current.Notes_IndentValues);
        obj.insert("NoteIndentStyle", Current.NoteIndentStyle);

        // Code Note Formatting Options
        obj.insert("CodeNotePrefix", Current.CodeNotePrefix);
        obj.insert("CodeNoteSuffix", Current.CodeNoteSuffix);
        obj.insert("CodeNoteAlignSuffixes", Current.CodeNoteAlignSuffixes);
        obj.insert("CodeNoteSuffixOnLastLineOnly", Current.CodeNoteSuffixOnLastLineOnly);
        obj.insert("CodeNoteAutoPopulatePointers", Current.CodeNoteAutoPopulatePointers);
        obj.insert("CodeNoteUnicodeTree", Current.CodeNoteUnicodeTree);
        obj.insert("CodeNoteIndentDescriptions", Current.CodeNoteIndentDescriptions);

        QFile file(GetSettingsPath());
        if (file.open(QIODevice::WriteOnly | QIODevice::Text))
        {
            QJsonDocument doc(obj);
            file.write(doc.toJson(QJsonDocument::Indented));
            file.close();
        }
    }
    catch (...) { /* Ignore errors */ }
}

} // namespace RALogicEditor
