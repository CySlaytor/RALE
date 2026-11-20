#include "AppSettings.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QGuiApplication>
#include <QJsonDocument>
#include <QJsonObject>
#include <QScreen>
#include <QSettings>
#include <QWidget>

#include <algorithm>

namespace RALE {

UserSettings AppSettings::Current;

QString AppSettings::GetSettingsPath() { return QDir(QCoreApplication::applicationDirPath()).filePath("settings.ini"); }

QString AppSettings::GetLayoutPath() {
    QString folder = QDir(QCoreApplication::applicationDirPath()).filePath("Metadata");
    if (!QDir(folder).exists())
        QDir().mkpath(folder);
    return QDir(folder).filePath("layout.ini");
}

void AppSettings::MigrateFromJsonIfPresent(const QString& jsonPath) {
    if (!QFile::exists(jsonPath))
        return;

    try {
        QFile file(jsonPath);
        if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
            QByteArray json = file.readAll();
            file.close();
            auto doc = QJsonDocument::fromJson(json);
            if (doc.isObject()) {
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

                Current.CodeNotePrefix = obj.value("CodeNotePrefix").toString(Current.CodeNotePrefix);
                Current.CodeNoteSuffix = obj.value("CodeNoteSuffix").toString(Current.CodeNoteSuffix);
                Current.CodeNoteAlignSuffixes = obj.value("CodeNoteAlignSuffixes").toBool(Current.CodeNoteAlignSuffixes);
                Current.CodeNoteSuffixOnLastLineOnly =
                    obj.value("CodeNoteSuffixOnLastLineOnly").toBool(Current.CodeNoteSuffixOnLastLineOnly);
                Current.CodeNoteAutoPopulatePointers =
                    obj.value("CodeNoteAutoPopulatePointers").toBool(Current.CodeNoteAutoPopulatePointers);

                if (obj.contains("CodeNoteUnicodeTree"))
                    Current.CodeNoteUnicodeTree = obj.value("CodeNoteUnicodeTree").toBool(Current.CodeNoteUnicodeTree);
                else if (obj.contains("NoteIndentStyle"))
                    Current.CodeNoteUnicodeTree = obj.value("NoteIndentStyle").toString().startsWith("Unicode Tree", Qt::CaseInsensitive);

                if (obj.contains("CodeNoteIndentDescriptions"))
                    Current.CodeNoteIndentDescriptions = obj.value("CodeNoteIndentDescriptions").toBool(Current.CodeNoteIndentDescriptions);
                else if (obj.contains("Notes_IndentValues"))
                    Current.CodeNoteIndentDescriptions = obj.value("Notes_IndentValues").toBool(Current.CodeNoteIndentDescriptions);
            }
        }
        Save();
        QFile::remove(jsonPath);
    } catch (...) {
    }
}

void AppSettings::Load() {
    QString iniPath = GetSettingsPath();
    QString legacyJsonPath = QDir(QCoreApplication::applicationDirPath()).filePath("settings.json");

    if (!QFile::exists(iniPath) && QFile::exists(legacyJsonPath)) {
        MigrateFromJsonIfPresent(legacyJsonPath);
        return;
    }

    if (!QFile::exists(iniPath)) {
        return;
    }

    try {
        QSettings ini(iniPath, QSettings::IniFormat);

        ini.beginGroup("General");
        Current.Theme = ini.value("Theme", Current.Theme).toString();
        Current.ShowLogicReviewer = ini.value("ShowLogicReviewer", Current.ShowLogicReviewer).toBool();
        Current.LastAssetTypeIndex = ini.value("LastAssetTypeIndex", Current.LastAssetTypeIndex).toInt();
        Current.LastCategoryIndex = ini.value("LastCategoryIndex", Current.LastCategoryIndex).toInt();
        Current.LastSourceIndex = ini.value("LastSourceIndex", Current.LastSourceIndex).toInt();
        Current.CodeReview_FilterIndex = ini.value("CodeReview_FilterIndex", Current.CodeReview_FilterIndex).toInt();
        ini.endGroup();

        ini.beginGroup("MainWindow");
        Current.WindowX = ini.value("WindowX", Current.WindowX).toInt();
        Current.WindowY = ini.value("WindowY", Current.WindowY).toInt();
        Current.WindowWidth = ini.value("WindowWidth", Current.WindowWidth).toInt();
        Current.WindowHeight = ini.value("WindowHeight", Current.WindowHeight).toInt();
        Current.IsMaximized = ini.value("IsMaximized", Current.IsMaximized).toBool();
        Current.MainSplitterDistance = ini.value("MainSplitterDistance", Current.MainSplitterDistance).toInt();
        Current.EditorSplitterDistance = ini.value("EditorSplitterDistance", Current.EditorSplitterDistance).toInt();
        Current.CodeNoteSplitterDistance = ini.value("CodeNoteSplitterDistance", Current.CodeNoteSplitterDistance).toInt();
        Current.VerticalSplitterDistance = ini.value("VerticalSplitterDistance", Current.VerticalSplitterDistance).toInt();
        Current.VerticalSplitterBottomDistance =
            ini.value("VerticalSplitterBottomDistance", Current.VerticalSplitterBottomDistance).toInt();
        ini.endGroup();

        ini.beginGroup("TriggerEditor");
        Current.Trigger_ShowDecimal = ini.value("ShowDecimal", Current.Trigger_ShowDecimal).toBool();
        Current.Trigger_ShowAliases = ini.value("ShowAliases", Current.Trigger_ShowAliases).toBool();
        Current.Trigger_CollapseChains = ini.value("CollapseChains", Current.Trigger_CollapseChains).toBool();
        ini.endGroup();

        ini.beginGroup("CodeNotes");
        Current.Notes_ShowLeafsOnly = ini.value("ShowLeafsOnly", Current.Notes_ShowLeafsOnly).toBool();
        Current.Notes_IndentValues = ini.value("IndentValues", Current.Notes_IndentValues).toBool();
        Current.NoteIndentStyle = ini.value("NoteIndentStyle", Current.NoteIndentStyle).toString();
        Current.CodeNotePrefix = ini.value("Prefix", Current.CodeNotePrefix).toString();
        Current.CodeNoteSuffix = ini.value("Suffix", Current.CodeNoteSuffix).toString();
        Current.CodeNoteAlignSuffixes = ini.value("AlignSuffixes", Current.CodeNoteAlignSuffixes).toBool();
        Current.CodeNoteSuffixOnLastLineOnly = ini.value("SuffixOnLastLineOnly", Current.CodeNoteSuffixOnLastLineOnly).toBool();
        Current.CodeNoteAutoPopulatePointers = ini.value("AutoPopulatePointers", Current.CodeNoteAutoPopulatePointers).toBool();
        Current.CodeNoteUnicodeTree = ini.value("UnicodeTree", Current.CodeNoteUnicodeTree).toBool();
        Current.CodeNoteIndentDescriptions = ini.value("IndentDescriptions", Current.CodeNoteIndentDescriptions).toBool();
        ini.endGroup();

        ini.beginGroup("DialogStates");
        Current.MultiRegion_UsePauseLocks = ini.value("MultiRegion_UsePauseLocks", Current.MultiRegion_UsePauseLocks).toBool();
        Current.MultiRegion_UpdateRP = ini.value("MultiRegion_UpdateRP", Current.MultiRegion_UpdateRP).toBool();

        Current.BatchMultiply_Copies = ini.value("BatchMultiply_Copies", Current.BatchMultiply_Copies).toInt();
        Current.BatchMultiply_Stride = ini.value("BatchMultiply_Stride", Current.BatchMultiply_Stride).toString();
        Current.BatchMultiply_StartNum = ini.value("BatchMultiply_StartNum", Current.BatchMultiply_StartNum).toInt();
        Current.BatchMultiply_Search = ini.value("BatchMultiply_Search", Current.BatchMultiply_Search).toString();
        Current.BatchMultiply_Replace = ini.value("BatchMultiply_Replace", Current.BatchMultiply_Replace).toString();

        Current.NoteMultiply_Copies = ini.value("NoteMultiply_Copies", Current.NoteMultiply_Copies).toInt();
        Current.NoteMultiply_Stride = ini.value("NoteMultiply_Stride", Current.NoteMultiply_Stride).toString();
        Current.NoteMultiply_StartNum = ini.value("NoteMultiply_StartNum", Current.NoteMultiply_StartNum).toInt();
        Current.NoteMultiply_NamingIdx = ini.value("NoteMultiply_NamingIdx", Current.NoteMultiply_NamingIdx).toInt();
        Current.NoteMultiply_Search = ini.value("NoteMultiply_Search", Current.NoteMultiply_Search).toString();
        Current.NoteMultiply_Replace = ini.value("NoteMultiply_Replace", Current.NoteMultiply_Replace).toString();
        Current.NoteMultiply_TargetIdx = ini.value("NoteMultiply_TargetIdx", Current.NoteMultiply_TargetIdx).toInt();

        Current.Generator_Copies = ini.value("Generator_Copies", Current.Generator_Copies).toString();
        Current.Generator_Stride = ini.value("Generator_Stride", Current.Generator_Stride).toString();
        Current.Generator_IsArray = ini.value("Generator_IsArray", Current.Generator_IsArray).toBool();
        ini.endGroup();
    } catch (...) { /* Ignore errors, use defaults */
    }
}

void AppSettings::Save() {
    try {
        QSettings ini(GetSettingsPath(), QSettings::IniFormat);

        ini.beginGroup("General");
        ini.setValue("Theme", Current.Theme);
        ini.setValue("ShowLogicReviewer", Current.ShowLogicReviewer);
        ini.setValue("LastAssetTypeIndex", Current.LastAssetTypeIndex);
        ini.setValue("LastCategoryIndex", Current.LastCategoryIndex);
        ini.setValue("LastSourceIndex", Current.LastSourceIndex);
        ini.setValue("CodeReview_FilterIndex", Current.CodeReview_FilterIndex);
        ini.endGroup();

        ini.beginGroup("MainWindow");
        ini.remove("WindowX");
        ini.remove("WindowY");
        ini.remove("WindowWidth");
        ini.remove("WindowHeight");
        ini.remove("IsMaximized");
        ini.setValue("MainSplitterDistance", Current.MainSplitterDistance);
        ini.setValue("EditorSplitterDistance", Current.EditorSplitterDistance);
        ini.setValue("CodeNoteSplitterDistance", Current.CodeNoteSplitterDistance);
        ini.setValue("VerticalSplitterDistance", Current.VerticalSplitterDistance);
        ini.setValue("VerticalSplitterBottomDistance", Current.VerticalSplitterBottomDistance);
        ini.endGroup();

        ini.beginGroup("TriggerEditor");
        ini.setValue("ShowDecimal", Current.Trigger_ShowDecimal);
        ini.setValue("ShowAliases", Current.Trigger_ShowAliases);
        ini.setValue("CollapseChains", Current.Trigger_CollapseChains);
        ini.endGroup();

        ini.beginGroup("CodeNotes");
        ini.setValue("ShowLeafsOnly", Current.Notes_ShowLeafsOnly);
        ini.setValue("IndentValues", Current.Notes_IndentValues);
        ini.setValue("NoteIndentStyle", Current.NoteIndentStyle);
        ini.setValue("Prefix", Current.CodeNotePrefix);
        ini.setValue("Suffix", Current.CodeNoteSuffix);
        ini.setValue("AlignSuffixes", Current.CodeNoteAlignSuffixes);
        ini.setValue("SuffixOnLastLineOnly", Current.CodeNoteSuffixOnLastLineOnly);
        ini.setValue("AutoPopulatePointers", Current.CodeNoteAutoPopulatePointers);
        ini.setValue("UnicodeTree", Current.CodeNoteUnicodeTree);
        ini.setValue("IndentDescriptions", Current.CodeNoteIndentDescriptions);
        ini.endGroup();

        ini.beginGroup("DialogStates");
        ini.setValue("MultiRegion_UsePauseLocks", Current.MultiRegion_UsePauseLocks);
        ini.setValue("MultiRegion_UpdateRP", Current.MultiRegion_UpdateRP);

        ini.setValue("BatchMultiply_Copies", Current.BatchMultiply_Copies);
        ini.setValue("BatchMultiply_Stride", Current.BatchMultiply_Stride);
        ini.setValue("BatchMultiply_StartNum", Current.BatchMultiply_StartNum);
        ini.setValue("BatchMultiply_Search", Current.BatchMultiply_Search);
        ini.setValue("BatchMultiply_Replace", Current.BatchMultiply_Replace);

        ini.setValue("NoteMultiply_Copies", Current.NoteMultiply_Copies);
        ini.setValue("NoteMultiply_Stride", Current.NoteMultiply_Stride);
        ini.setValue("NoteMultiply_StartNum", Current.NoteMultiply_StartNum);
        ini.setValue("NoteMultiply_NamingIdx", Current.NoteMultiply_NamingIdx);
        ini.setValue("NoteMultiply_Search", Current.NoteMultiply_Search);
        ini.setValue("NoteMultiply_Replace", Current.NoteMultiply_Replace);
        ini.setValue("NoteMultiply_TargetIdx", Current.NoteMultiply_TargetIdx);

        ini.setValue("Generator_Copies", Current.Generator_Copies);
        ini.setValue("Generator_Stride", Current.Generator_Stride);
        ini.setValue("Generator_IsArray", Current.Generator_IsArray);
        ini.endGroup();
    } catch (...) { /* Ignore errors */
    }
}

void AppSettings::SaveWindowGeometry(const QString& windowName, const QWidget* widget) {
    if (widget == nullptr)
        return;
    try {
        QSettings ini(GetLayoutPath(), QSettings::IniFormat);
        ini.beginGroup("Windows");
        ini.setValue(windowName, widget->saveGeometry());
        ini.endGroup();
    } catch (...) {
    }
}

bool AppSettings::LoadWindowGeometry(const QString& windowName, QWidget* widget) {
    if (widget == nullptr)
        return false;
    try {
        QString layoutPath = GetLayoutPath();
        if (!QFile::exists(layoutPath))
            return false;

        QSettings ini(layoutPath, QSettings::IniFormat);
        ini.beginGroup("Windows");
        QByteArray geom = ini.value(windowName).toByteArray();
        ini.endGroup();

        if (geom.isEmpty() || !widget->restoreGeometry(geom))
            return false;

        // Clamp restored non-maximized window onto an active screen if disconnected
        if (!widget->isMaximized()) {
            QRect frame = widget->frameGeometry();
            bool visibleOnScreen = false;
            for (QScreen* screen : QGuiApplication::screens()) {
                if (screen != nullptr && screen->availableGeometry().intersects(frame)) {
                    visibleOnScreen = true;
                    break;
                }
            }
            if (!visibleOnScreen && QGuiApplication::primaryScreen() != nullptr) {
                QRect avail = QGuiApplication::primaryScreen()->availableGeometry();
                QSize clampedSize(std::min(widget->width(), avail.width()), std::min(widget->height(), avail.height()));
                widget->resize(clampedSize);
                widget->move(avail.center() - QPoint(clampedSize.width() / 2, clampedSize.height() / 2));
            }
        }
        return true;
    } catch (...) {
    }
    return false;
}

void AppSettings::SaveDialogGeometry(const QString& dialogName, const QPoint& pos, const QSize& size) {
    try {
        QSettings ini(GetLayoutPath(), QSettings::IniFormat);
        ini.beginGroup("DialogPositions");
        if (size.isValid() && size.width() > 0 && size.height() > 0) {
            ini.setValue(dialogName, QString("%1, %2, %3, %4").arg(pos.x()).arg(pos.y()).arg(size.width()).arg(size.height()));
        } else {
            ini.setValue(dialogName, QString("%1, %2").arg(pos.x()).arg(pos.y()));
        }
        ini.endGroup();
    } catch (...) {
    }
}

bool AppSettings::LoadDialogGeometry(const QString& dialogName, QPoint& pos, QSize& size) {
    try {
        QString val;
        QString layoutPath = GetLayoutPath();
        if (QFile::exists(layoutPath)) {
            QSettings layoutIni(layoutPath, QSettings::IniFormat);
            layoutIni.beginGroup("DialogPositions");
            if (layoutIni.contains(dialogName))
                val = layoutIni.value(dialogName).toString();
            layoutIni.endGroup();
        }

        // One-time fallback to legacy settings.ini DialogPositions if not yet in layout.ini
        if (val.isEmpty()) {
            QString iniPath = GetSettingsPath();
            if (!QFile::exists(iniPath))
                return false;

            QSettings ini(iniPath, QSettings::IniFormat);
            ini.beginGroup("DialogPositions");
            if (!ini.contains(dialogName))
                return false;
            val = ini.value(dialogName).toString();
            ini.endGroup();
        }

        if (val.startsWith("@ByteArray"))
            return false;

        bool parsed = false;
        QStringList parts = val.split(',');
        if (parts.size() >= 4) {
            bool okX = false, okY = false, okW = false, okH = false;
            int x = parts[0].trimmed().toInt(&okX);
            int y = parts[1].trimmed().toInt(&okY);
            int w = parts[2].trimmed().toInt(&okW);
            int h = parts[3].trimmed().toInt(&okH);
            if (okX && okY && okW && okH) {
                pos = QPoint(x, y);
                size = QSize(w, h);
                parsed = true;
            }
        } else if (parts.size() >= 2) {
            bool okX = false, okY = false;
            int x = parts[0].trimmed().toInt(&okX);
            int y = parts[1].trimmed().toInt(&okY);
            if (okX && okY) {
                pos = QPoint(x, y);
                parsed = true;
            }
        }

        if (!parsed)
            return false;

        // Clamp position onto an available screen so dialogs never open off-screen
        QSize checkSize = (size.isValid() && size.width() > 0 && size.height() > 0) ? size : QSize(200, 150);
        QRect targetRect(pos, checkSize);
        bool onScreen = false;
        for (QScreen* screen : QGuiApplication::screens()) {
            if (screen != nullptr && screen->availableGeometry().intersects(targetRect)) {
                onScreen = true;
                break;
            }
        }
        if (!onScreen && QGuiApplication::primaryScreen() != nullptr) {
            QRect avail = QGuiApplication::primaryScreen()->availableGeometry();
            int maxX = std::max(avail.left(), avail.right() - checkSize.width());
            int maxY = std::max(avail.top(), avail.bottom() - checkSize.height());
            pos = QPoint(std::clamp(pos.x(), avail.left(), maxX), std::clamp(pos.y(), avail.top(), maxY));
        }

        return true;
    } catch (...) {
    }
    return false;
}

} // namespace RALE
