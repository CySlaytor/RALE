#pragma once

#include "../Models/LogicModels.h"
#include "../Models/NoteNode.h"

#include <QList>
#include <QString>

#include <cstdint>

namespace RALE {

class CodeNoteModifier {
  public:
    enum class IndentStyle { Plus, Mixed, Dot, Unicode };
    enum class SeparatorStyle { Pipe, Equals, Colon, Space };

    class NoteStyle {
      public:
        IndentStyle Indentation = IndentStyle::Plus;
        SeparatorStyle Separator = SeparatorStyle::Pipe; // Changed from Space

        QString GetSeparatorString() const;
    };

    struct SubtreeInjection {
        QList<int64_t> ChainOffsets;
        QString NodeBlockText;
    };

    static NoteStyle DetectStyle(const QString& currentNote, const QList<CodeNote>& allNotes);
    static NoteStyle DetectGlobalStyle(const QList<CodeNote>& allNotes);

    static QString DeleteNodeFamily(const QString& baseNoteText, const std::shared_ptr<NoteNode>& node);
    static QString UpdateRealNode(const QString& baseNoteText, const std::shared_ptr<NoteNode>& node, const QString& newContent,
                                  const NoteStyle& globalStyle);

    static QString InjectNoteContent(const QString& baseNoteText, const QList<int64_t>& chainOffsets, const QString& newContent,
                                     const NoteStyle& globalStyle, const QString& indentStylePref, bool indentDescriptions);

    static QString InjectNoteSubtrees(const QString& baseNoteText, const QList<SubtreeInjection>& injections,
                                      const QString& indentStylePref, bool indentDescriptions, int consoleId = 0);

  private:
    class StyleStats {
      public:
        int PlusCount = 0;
        int MixedCount = 0;
        int DotCount = 0;
        int UnicodeCount = 0;
        int PipeCount = 0;
        int EqCount = 0;
        int ColCount = 0;

        bool HasIndent() const { return PlusCount > 0 || MixedCount > 0 || DotCount > 0 || UnicodeCount > 0; }
        bool HasSeparator() const { return PipeCount > 0 || EqCount > 0 || ColCount > 0; }
    };

    static bool IsNodeBoundary(const QString& line);
    static QString ExtractExactPrefix(const QString& line);
    static StyleStats ScanStats(const QStringList& notes);
    static NoteStyle ResolveStyle(const StyleStats& stats);
    static NoteStyle DetectLocalStyle(const QString& parentLine, const NoteStyle& fallbackStyle);
};

} // namespace RALE
