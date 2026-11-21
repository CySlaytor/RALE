#pragma once

#include "../../Common/ConditionFormatter.h"
#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../../Parsers/IPointerTreeParser.h"

#include <QList>
#include <QObject>
#include <QStack>
#include <QString>

#include <memory>

namespace RALE {

// ViewModel to decouple complex pointer logic from the WinForms UI.
// Maintains the state of notes, pointer trees, and virtual selections.
class CodeNoteViewModel : public QObject {
    Q_OBJECT

  public:
    // Injected Parser Dependency
    IPointerTreeParser& _parser;

    // Active project platform ConsoleId (e.g. 19 = Wii, 16 = GameCube, 21 = PS2, 5 = GBA, etc.)
    int ConsoleId = 0;

    // -- State --
    QList<CodeNote> AllNotes;

    // Cache for range-based notes (Start, End, Note, Node)
    struct RangeCacheEntry {
        int64_t Start = 0;
        int64_t End = 0;
        CodeNote Note;
        std::shared_ptr<NoteNode> Node;
    };
    QList<RangeCacheEntry> _rangeCache;

    // The currently selected base note (parent of the pointer tree)
    std::shared_ptr<CodeNote> CurrentBaseNote = nullptr;

    // The full parsed tree of the current note
    QList<std::shared_ptr<NoteNode>> CurrentParsedNodes;

    // -- Redirection State --
    bool IsRedirected = false;
    QString RedirectedContent = "";
    QString RedirectTargetAddress = "";

    // -- Undo/Redo State (Local to currently edited note) --
    bool CanUndo() const { return !_undoStack.isEmpty(); }
    bool CanRedo() const { return !_redoStack.isEmpty(); }

    // -- Virtual/Edit State --
    // Unified State for both Virtual (Ghost) and Real (Existing) edits.
    // This acts as a buffer: changing DisplayText does not immediately affect the CodeNote.
    class EditorState {
      public:
        bool IsVirtual = false; // True if creating new node, False if editing existing node
        bool IsDirty = false;   // Tracks if the user has modified the text
        QString Description = "";
        QString LogicCode = "";
        QString LogicType = "";
        QString LogicSize = "";
        QString DisplayText = ""; // The text to show in the body box

        // Store the offsets required to recreate this virtual path
        QList<int64_t> ChainOffsets;

        // Store the target node if editing a real existing node
        std::shared_ptr<NoteNode> TargetNode = nullptr;
    };
    EditorState EditState;

    // Constructor Injection
    explicit CodeNoteViewModel(IPointerTreeParser& parser, QObject* parent = nullptr);

    // Undo / Redo
    void PushUndoState();
    void Undo();
    void Redo();

    // -- Logic --
    void LoadNotes(const QList<CodeNote>& notes);
    void ApplyCloneStrideNotes(const QList<CodeNote>& generatedNotes, int& outAddedCount, int& outUpdatedCount);
    void IntelligentDelete(const std::shared_ptr<NoteNode>& target);
    void SelectBaseNote(const CodeNote& note);
    void UpdateNoteContent(const QString& newText);
    void UpdateBufferedText(const QString& newText);
    void CommitEditState();
    // By value: committing a pending edit re-parses and frees the old tree, so the
    // caller's node must stay alive across that implicit commit.
    void RequestNodeSelection(std::shared_ptr<NoteNode> node);
    std::tuple<QString, QString, QString> GetSelectedLogic(const std::shared_ptr<NoteNode>& uiSelectedNode);
    void ClearEditState();
    void HighlightAddress(const QString& address);
    void HighlightVariable(QList<int64_t> offsets);

    const QList<CodeNote>& GetAllNotes() const { return AllNotes; }

  signals:
    // -- Events --
    void NotesLoaded();
    void NoteContentChanged();
    void EditStateChanged(); // Changed from VirtualStateChanged to reflect unified editing
    void SelectionRequested(std::shared_ptr<NoteNode> node);
    void HistoryChanged();

  private:
    QStack<QString> _undoStack;
    QStack<QString> _redoStack;

    void RebuildRangeCache();
    std::shared_ptr<NoteNode> FindNodeByOffsetString(const QString& offset);
    void SetEditState(bool isVirtual, const QString& description, const QString& displayText, const QString& logicCode, const QString& type,
                      const QString& size, const QList<int64_t>* offsets);
    int64_t ParseAddress(const QString& addr) const;
    QMap<QString, QList<std::shared_ptr<NoteNode>>> NotesLookup();
};

} // namespace RALE
