#pragma once

#include <QString>

namespace RALogicEditor {

class UserSettings
{
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
    int VerticalSplitterDistance = 400;

    // Filters
    int LastAssetTypeIndex = 0; // 0=Ach, 1=LB
    int LastCategoryIndex = 0;  // 0=Core
    int LastSourceIndex = 0;    // 0=JSON

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
};

class AppSettings
{
public:
    static UserSettings Current;

    static void Load();
    static void Save();

private:
    static QString GetSettingsPath();
};

} // namespace RALogicEditor
