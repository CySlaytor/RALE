#pragma once

#include <QPoint>
#include <QSize>
#include <QString>

class QWidget;

namespace RALE {

class UserSettings {
  public:
    // View State
    bool ShowLogicReviewer = false;
    QString Theme = "Light"; // "Light" or "Dark"

    // Window Geometry
    int WindowX = -1;
    int WindowY = -1;
    int WindowWidth = 1200;
    int WindowHeight = 700;
    bool IsMaximized = false;

    // Layout (Splitters)
    int MainSplitterDistance = 260;
    int EditorSplitterDistance = 600;
    int CodeNoteSplitterDistance = 340;
    int VerticalSplitterDistance = 400;
    int VerticalSplitterBottomDistance = 250;

    // Filters
    int LastAssetTypeIndex = 0; // 0=Ach, 1=LB
    int LastCategoryIndex = 0;  // 0=Core
    int LastSourceIndex = 0;    // 0=JSON
    int CodeReview_FilterIndex = 0;

    // Trigger Editor State
    bool Trigger_ShowDecimal = false;
    bool Trigger_ShowAliases = false;
    bool Trigger_CollapseChains = false;

    // Code Note Editor State
    bool Notes_ShowLeafsOnly = false;
    bool Notes_IndentValues = true;
    QString NoteIndentStyle = "Unicode Tree (├─)";

    // Code Note Formatting Options (Ported from Pointer Finder 2.0)
    QString CodeNotePrefix = "+";
    QString CodeNoteSuffix = "=";
    bool CodeNoteAlignSuffixes = false;
    bool CodeNoteSuffixOnLastLineOnly = false;
    bool CodeNoteAutoPopulatePointers = true;
    bool CodeNoteUnicodeTree = false;
    bool CodeNoteIndentDescriptions = true;

    // Dialog States & Preferences
    bool MultiRegion_UsePauseLocks = true;
    bool MultiRegion_UpdateRP = true;

    int BatchMultiply_Copies = 1;
    QString BatchMultiply_Stride = "0x0";
    int BatchMultiply_StartNum = 2;
    QString BatchMultiply_Search = "";
    QString BatchMultiply_Replace = "";

    int NoteMultiply_Copies = 1;
    QString NoteMultiply_Stride = "0x0";
    int NoteMultiply_StartNum = 2;
    int NoteMultiply_NamingIdx = 1;
    QString NoteMultiply_Search = "";
    QString NoteMultiply_Replace = "";
    int NoteMultiply_TargetIdx = 0;

    QString Generator_Copies = "1";
    QString Generator_Stride = "0x0";
    bool Generator_IsArray = false;
};

class AppSettings {
  public:
    static UserSettings Current;

    static void Load();
    static void Save();

    // Window & Dialog Geometry Persistence in Metadata/layout.ini
    static void SaveWindowGeometry(const QString& windowName, const QWidget* widget);
    static bool LoadWindowGeometry(const QString& windowName, QWidget* widget);

    // Dialog Positions and Sizes Persistence (matching PF2 conventions)
    static void SaveDialogGeometry(const QString& dialogName, const QPoint& pos, const QSize& size = QSize());
    static bool LoadDialogGeometry(const QString& dialogName, QPoint& pos, QSize& size);

    static QString GetSettingsPath();
    static QString GetLayoutPath();

  private:
    static void MigrateFromJsonIfPresent(const QString& jsonPath);
};

} // namespace RALE
