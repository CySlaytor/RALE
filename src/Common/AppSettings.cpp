#include "AppSettings.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QGuiApplication>
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

void AppSettings::Load() {
    QString iniPath = GetSettingsPath();
    if (!QFile::exists(iniPath)) {
        return;
    }

    try {
        QSettings ini(iniPath, QSettings::IniFormat);

        ini.beginGroup("General");
        Current.Theme = ini.value("Theme", Current.Theme).toString();
        Current.ShowLogicReviewer = ini.value("ShowLogicReviewer", Current.ShowLogicReviewer).toBool();
        Current.SkippedUpdateVersion = ini.value("SkippedUpdateVersion", Current.SkippedUpdateVersion).toString();
        Current.LastAssetTypeIndex = ini.value("LastAssetTypeIndex", Current.LastAssetTypeIndex).toInt();
        Current.LastCategoryIndex = ini.value("LastCategoryIndex", Current.LastCategoryIndex).toInt();
        Current.LastSourceIndex = ini.value("LastSourceIndex", Current.LastSourceIndex).toInt();
        Current.CodeReview_FilterIndex = ini.value("CodeReview_FilterIndex", Current.CodeReview_FilterIndex).toInt();
        ini.endGroup();

        ini.beginGroup("MainWindow");
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

        Current.Generator_Copies = ini.value("Generator_Copies", Current.Generator_Copies).toString();
        Current.Generator_Stride = ini.value("Generator_Stride", Current.Generator_Stride).toString();
        Current.Generator_IsArray = ini.value("Generator_IsArray", Current.Generator_IsArray).toBool();
        Current.Generator_DuplicateSelectedOnly =
            ini.value("Generator_DuplicateSelectedOnly", Current.Generator_DuplicateSelectedOnly).toBool();
        Current.Generator_DuplicateNotes = ini.value("Generator_DuplicateNotes", Current.Generator_DuplicateNotes).toBool();

        Current.SuggestedFix_ShowDecimal = ini.value("SuggestedFix_ShowDecimal", Current.SuggestedFix_ShowDecimal).toBool();
        Current.SuggestedFix_ShowAliases = ini.value("SuggestedFix_ShowAliases", Current.SuggestedFix_ShowAliases).toBool();
        Current.SuggestedFix_CollapseChains = ini.value("SuggestedFix_CollapseChains", Current.SuggestedFix_CollapseChains).toBool();
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
        ini.setValue("SkippedUpdateVersion", Current.SkippedUpdateVersion);
        ini.setValue("LastAssetTypeIndex", Current.LastAssetTypeIndex);
        ini.setValue("LastCategoryIndex", Current.LastCategoryIndex);
        ini.setValue("LastSourceIndex", Current.LastSourceIndex);
        ini.setValue("CodeReview_FilterIndex", Current.CodeReview_FilterIndex);
        ini.endGroup();

        ini.beginGroup("MainWindow");
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

        ini.setValue("Generator_Copies", Current.Generator_Copies);
        ini.setValue("Generator_Stride", Current.Generator_Stride);
        ini.setValue("Generator_IsArray", Current.Generator_IsArray);
        ini.setValue("Generator_DuplicateSelectedOnly", Current.Generator_DuplicateSelectedOnly);
        ini.setValue("Generator_DuplicateNotes", Current.Generator_DuplicateNotes);

        ini.setValue("SuggestedFix_ShowDecimal", Current.SuggestedFix_ShowDecimal);
        ini.setValue("SuggestedFix_ShowAliases", Current.SuggestedFix_ShowAliases);
        ini.setValue("SuggestedFix_CollapseChains", Current.SuggestedFix_CollapseChains);
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
        QString layoutPath = GetLayoutPath();
        if (!QFile::exists(layoutPath))
            return false;

        QSettings layoutIni(layoutPath, QSettings::IniFormat);
        layoutIni.beginGroup("DialogPositions");
        if (!layoutIni.contains(dialogName))
            return false;
        QString val = layoutIni.value(dialogName).toString();
        layoutIni.endGroup();

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
