#include "CodeNoteViewModel.h"
#include "../../Common/CodeNoteModifier.h"
#include "../../Common/AppSettings.h"
#include "../../Utilities/LogicFormatter.h"
#include <QRegularExpression>
#include <algorithm>

namespace RALogicEditor {

namespace {

// Finds the owning shared_ptr for a raw node pointer anywhere in the tree.
std::shared_ptr<NoteNode> FindNodeByRawPointer(
    const QList<std::shared_ptr<NoteNode>> &nodes,
    const NoteNode *target)
{
    for (const auto &node : nodes)
    {
        if (node.get() == target) return node;
        auto found = FindNodeByRawPointer(node->Children, target);
        if (found != nullptr) return found;
    }
    return nullptr;
}

} // namespace

// Constructor Injection
CodeNoteViewModel::CodeNoteViewModel(IPointerTreeParser &parser, QObject *parent)
    : QObject(parent), _parser(parser)
{
}

#pragma region Undo / Redo

void CodeNoteViewModel::PushUndoState()
{
    if (CurrentBaseNote != nullptr)
    {
        // Only push if the state is actually different from the top of the stack
        if (_undoStack.isEmpty() || _undoStack.top() != CurrentBaseNote->Note)
        {
            _undoStack.push(CurrentBaseNote->Note);
            _redoStack.clear();
        }
    }
}

void CodeNoteViewModel::Undo()
{
    if (CurrentBaseNote != nullptr && !_undoStack.isEmpty())
    {
        _redoStack.push(CurrentBaseNote->Note);
        QString previous = _undoStack.pop();

        CurrentBaseNote->Note = previous;

        for (auto &n : AllNotes)
        {
            if (n.Address.compare(CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
            {
                n.Note = previous;
                break;
            }
        }

        CurrentParsedNodes = _parser.ParseNoteText(previous);

        // Clear active buffer so UI refreshes cleanly
        ClearEditState();
        emit NoteContentChanged();
    }
}

void CodeNoteViewModel::Redo()
{
    if (CurrentBaseNote != nullptr && !_redoStack.isEmpty())
    {
        _undoStack.push(CurrentBaseNote->Note);
        QString next = _redoStack.pop();

        CurrentBaseNote->Note = next;

        for (auto &n : AllNotes)
        {
            if (n.Address.compare(CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
            {
                n.Note = next;
                break;
            }
        }

        CurrentParsedNodes = _parser.ParseNoteText(next);

        ClearEditState();
        emit NoteContentChanged();
    }
}

#pragma endregion

// -- Logic --

void CodeNoteViewModel::LoadNotes(const QList<CodeNote> &notes)
{
    AllNotes = notes;
    _rangeCache.clear();

    // Rebuild Range Cache
    for (const auto &note : AllNotes)
    {
        QString cleanAddr = note.Address;
        cleanAddr.remove("0x", Qt::CaseInsensitive);
        cleanAddr = cleanAddr.trimmed();
        bool ok = false;
        int64_t startAddr = cleanAddr.toLongLong(&ok, 16);
        if (ok)
        {
            auto nodes = _parser.ParseNoteText(note.Note);
            if (!nodes.isEmpty())
            {
                auto rootNode = nodes.first();
                if (rootNode->Size > 1)
                {
                    int64_t endAddr = startAddr + rootNode->Size;
                    _rangeCache.append(RangeCacheEntry{ startAddr, endAddr, note, rootNode });
                }
            }
        }
    }
    emit NotesLoaded();
}

void CodeNoteViewModel::BatchMultiplyNotes(
    const QList<CodeNote> &sourceNotes,
    const QString &searchKeyword,
    const QString &replacePattern,
    int copies,
    int64_t stride,
    int startNum)
{
    if (sourceNotes.isEmpty()) return;

    QList<CodeNote> newNotes;

    for (const auto &baseNote : sourceNotes)
    {
        int64_t baseAddr = ParseAddress(baseNote.Address);
        if (baseAddr == -1) continue;

        for (int i = 0; i < copies; i++)
        {
            int num = startNum + i;
            int64_t newAddr = baseAddr + (stride * (i + 1));
            QString newAddrStr = QString("0x%1").arg(newAddr, 0, 16);

            QString newNoteText = baseNote.Note;

            // Only perform text replacement if fields are provided
            if (!searchKeyword.isEmpty())
            {
                QString replacement = replacePattern;
                replacement.replace("{N}", QString::number(num));
                // Apply replacement globally inside the note text, ignoring case
                QRegularExpression regex(QRegularExpression::escape(searchKeyword), QRegularExpression::CaseInsensitiveOption);
                newNoteText.replace(regex, replacement);
            }

            CodeNote note;
            note.Address = newAddrStr;
            note.Note = newNoteText;
            note.User = "Local";
            newNotes.append(note);
        }
    }

    AllNotes.append(newNotes);

    // Sort by Address to keep the combo box list clean
    std::sort(AllNotes.begin(), AllNotes.end(), [this](const CodeNote &a, const CodeNote &b) {
        return ParseAddress(a.Address) < ParseAddress(b.Address);
    });

    // Trigger complete UI refresh
    emit NotesLoaded();
}

void CodeNoteViewModel::IntelligentDelete(const std::shared_ptr<NoteNode> &target)
{
    if (CurrentBaseNote == nullptr || target == nullptr || target->IndentLevel == -2) return; // Cannot delete full note from here

    PushUndoState();

    // 1. Find the highest parent that has only 1 child leading to our target
    std::shared_ptr<NoteNode> highestToDelete = target;
    NoteNode* current = target->Parent;

    while (current != nullptr && current->IndentLevel >= 0)
    {
        // If this parent only has 1 child, it's an orphan once we delete the target.
        if (current->Children.size() == 1)
        {
            // The parent may be nested below the top level, so search the whole tree
            // instead of only CurrentParsedNodes (which holds the root + top-level nodes).
            auto parentPtr = FindNodeByRawPointer(CurrentParsedNodes, current);
            if (parentPtr != nullptr) highestToDelete = parentPtr;
            current = current->Parent;
        }
        else
        {
            break;
        }
    }

    // 2. Call our robust bounds deletion logic
    QString updatedNote = CodeNoteModifier::DeleteNodeFamily(CurrentBaseNote->Note, highestToDelete);

    // 3. Apply update
    CurrentBaseNote->Note = updatedNote;

    for (auto &n : AllNotes)
    {
        if (n.Address.compare(CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
        {
            n.Note = updatedNote;
            break;
        }
    }

    CurrentParsedNodes = _parser.ParseNoteText(updatedNote);

    ClearEditState();
    emit NoteContentChanged();

    // We do NOT set EditState.IsDirty = true here.
    // If we did, the next time the user hits "Save", the CommitEditState() method
    // would think there is uncommitted text in the UI text box buffer (which is empty
    // because of ClearEditState) and aggressively overwrite the note we just successfully deleted from.
}

void CodeNoteViewModel::SelectBaseNote(const CodeNote &note)
{
    // Auto-commit any pending edits before switching context
    if (EditState.IsDirty) CommitEditState();

    CurrentBaseNote = std::make_shared<CodeNote>(note);
    _undoStack.clear(); // Reset undo stack on new note selection
    _redoStack.clear();

    ClearEditState();

    // Check for Redirection (Refer to $0x...)
    // Logic: If the note is essentially empty (no structure) AND has a referral, follow it.
    // We check if the note has actual pointer structure (plus/dots). If not, we assume it's a stub.
    static const QRegularExpression structRegex(
        QString::fromUtf8(R"(^[.\+\s\x{2500}-\x{257F}])"),
        QRegularExpression::MultilineOption);
    bool hasStructure = structRegex.match(note.Note).hasMatch();

    static const QRegularExpression referralRegex(R"(refer to \$0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
    auto referralMatch = referralRegex.match(note.Note);

    IsRedirected = false;
    RedirectedContent = "";
    RedirectTargetAddress = "";

    QString contentToParse = note.Note;

    if (!hasStructure && referralMatch.hasMatch())
    {
        QString targetAddr = referralMatch.captured(1);
        // Find the target note
        // Normalize both to lower hex
        QString cleanTarget = targetAddr.toLower();
        while (cleanTarget.startsWith('0') && cleanTarget.length() > 1) cleanTarget = cleanTarget.mid(1);

        for (const auto &n : AllNotes)
        {
            QString nAddr = n.Address;
            nAddr.remove("0x", Qt::CaseInsensitive);
            nAddr = nAddr.toLower();
            while (nAddr.startsWith('0') && nAddr.length() > 1) nAddr = nAddr.mid(1);

            if (nAddr == cleanTarget)
            {
                IsRedirected = true;
                RedirectedContent = n.Note;
                RedirectTargetAddress = n.Address;
                contentToParse = n.Note;
                break;
            }
        }
    }

    QString normalized = contentToParse;
    normalized.replace("\r\n", "\n").replace('\r', '\n').replace('\n', "\r\n");
    CurrentParsedNodes = _parser.ParseNoteText(normalized);

    emit NoteContentChanged();
}

void CodeNoteViewModel::UpdateNoteContent(const QString &newText)
{
    if (CurrentBaseNote == nullptr || EditState.IsVirtual || IsRedirected) return;

    CurrentBaseNote->Note = newText;

    for (auto &n : AllNotes)
    {
        if (n.Address.compare(CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
        {
            n.Note = newText;
            break;
        }
    }

    CurrentParsedNodes = _parser.ParseNoteText(newText);
    emit NoteContentChanged();
}

// Called by text box on every keystroke
// This updates the buffer but NOT the underlying CodeNote.
void CodeNoteViewModel::UpdateBufferedText(const QString &newText)
{
    if (CurrentBaseNote == nullptr || IsRedirected) return;

    // If we weren't in a specific edit state, we are now editing the Root note (default fallback)
    if (EditState.TargetNode == nullptr && !EditState.IsVirtual)
    {
        // Root editing context is usually handled by SelectBaseNote, 
        // but if user just starts typing, we assume root.
    }

    EditState.DisplayText = newText;
    EditState.IsDirty = true;
}

// Commits the buffered text into the actual Code Note structure.
// Called by Save (Ctrl+S) or Navigation.
void CodeNoteViewModel::CommitEditState()
{
    if (!EditState.IsDirty || CurrentBaseNote == nullptr || IsRedirected) return;

    QString updatedBaseNote = "";

    // UPDATED: Use DetectStyle with Current Note context first, fallback to AllNotes
    auto style = CodeNoteModifier::DetectStyle(CurrentBaseNote->Note, AllNotes);
    QString styleStr = AppSettings::Current.CodeNoteUnicodeTree ? "Unicode Tree (├─)" : "Plusses (+)";
    bool indentDescriptions = AppSettings::Current.CodeNoteIndentDescriptions;

    if (EditState.IsVirtual)
    {
        // Inject new pointer chain
        if (EditState.ChainOffsets.isEmpty()) return;

        updatedBaseNote = CodeNoteModifier::InjectNoteContent(
            CurrentBaseNote->Note,
            EditState.ChainOffsets,
            EditState.DisplayText,
            style,
            styleStr,
            indentDescriptions
        );
    }
    else
    {
        // Update existing node
        if (EditState.TargetNode != nullptr)
        {
            // If editing Root, simple replace
            if (EditState.TargetNode->IndentLevel == -2)
            {
                updatedBaseNote = EditState.DisplayText;
            }
            else
            {
                // Splicing for child node
                updatedBaseNote = CodeNoteModifier::UpdateRealNode(
                    CurrentBaseNote->Note,
                    EditState.TargetNode,
                    EditState.DisplayText,
                    style,
                    styleStr,
                    indentDescriptions
                );
            }
        }
        else
        {
            // Fallback -> editing root
            updatedBaseNote = EditState.DisplayText;
        }
    }

    // The C# TextBox yields CRLF; QPlainTextEdit yields LF. Normalise so note text
    // compares equal to server notes (ProjectRepository::IsNoteSynced is exact).
    updatedBaseNote.replace("\r\n", "\n").replace('\r', '\n').replace("\n", "\r\n");

    PushUndoState(); // Save state before committing

    // Apply to Model
    CurrentBaseNote->Note = updatedBaseNote;

    // Synchronize the committed change back into AllNotes
    for (auto &n : AllNotes)
    {
        if (n.Address.compare(CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
        {
            n.Note = updatedBaseNote;
            break;
        }
    }

    // Re-parse
    CurrentParsedNodes = _parser.ParseNoteText(updatedBaseNote);

    // Save context to restore selection after clear
    QList<int64_t> savedOffsets = EditState.IsVirtual ? EditState.ChainOffsets : QList<int64_t>();
    QString savedNodeOffset = (!EditState.IsVirtual && EditState.TargetNode != nullptr) ? EditState.TargetNode->Offset : "";

    ClearEditState();

    // IMPORTANT: Notify UI that content changed so it refreshes the node list (CurrentParsedNodes).
    emit NoteContentChanged();

    // Restore Selection
    if (!savedOffsets.isEmpty())
    {
        HighlightVariable(savedOffsets);
    }
    else if (!savedNodeOffset.isEmpty())
    {
        // Try to find the node we just edited by offset string match
        auto node = FindNodeByOffsetString(savedNodeOffset);
        if (node != nullptr) RequestNodeSelection(node);
    }
}

std::shared_ptr<NoteNode> CodeNoteViewModel::FindNodeByOffsetString(const QString &offset)
{
    QStack<std::shared_ptr<NoteNode>> stack;
    for (const auto &n : CurrentParsedNodes) stack.push(n);

    while (!stack.isEmpty())
    {
        auto n = stack.pop();
        if (n->Offset == offset) return n;
        for (const auto &c : n->Children) stack.push(c);
    }
    return nullptr;
}

// Set up state for editing a Real Node
void CodeNoteViewModel::RequestNodeSelection(std::shared_ptr<NoteNode> node)
{
    // If we were editing something else and it's dirty, commit it first
    if (EditState.IsDirty) CommitEditState();

    QString content = "";
    // Extract content logic
    if (node->IndentLevel == -2)
    {
        // If redirected, use redirected content, else real note
        content = IsRedirected ? RedirectedContent : (CurrentBaseNote != nullptr ? CurrentBaseNote->Note : "");
    }
    else
    {
        // Combine Description + Content so user can edit the header description (Title/Size)
        // and the body content together.
        QString sb;
        if (!node->Description.trimmed().isEmpty())
        {
            sb += node->Description + "\n";
        }

        if (!node->Content.trimmed().isEmpty())
        {
            sb += node->Content;
        }

        content = sb.trimmed();
    }

    QString desc = node->Description;
    if (desc.isEmpty()) desc = node->Offset;

    // Set TargetNode BEFORE setting edit state to avoid race conditions in UI update
    EditState.TargetNode = node;

    SetEditState(
        false, // IsVirtual
        desc,
        content, // Initial Text
        "", "", "", // Logic strings not needed for real editing display
        nullptr
    );

    emit SelectionRequested(node);
}

std::tuple<QString, QString, QString> CodeNoteViewModel::GetSelectedLogic(const std::shared_ptr<NoteNode> &uiSelectedNode)
{
    if (EditState.IsVirtual)
    {
        return std::make_tuple(EditState.LogicCode, EditState.LogicType, EditState.LogicSize);
    }

    if (CurrentBaseNote == nullptr || uiSelectedNode == nullptr) return std::make_tuple("", "", "");

    // If redirected, we still generate logic using the BASE NOTE address
    // because the user is on the PAL note (e.g. 0x1234), but looking at USA structure.
    // The logic should point to PAL (0x1234) + Offsets.
    // The parser uses CurrentBaseNote address as the root I:0x...
    // So this works perfectly: Logic is built relative to PAL base, using USA tree structure.
    return _parser.GeneratePointerLogic(*CurrentBaseNote, uiSelectedNode, ConsoleId);
}

void CodeNoteViewModel::ClearEditState()
{
    EditState = EditorState();
    emit EditStateChanged();
}

void CodeNoteViewModel::HighlightAddress(const QString &address)
{
    if (EditState.IsDirty) CommitEditState();

    if (address.isEmpty()) return;
    QString hex = address.trimmed();
    if (hex.startsWith("0x", Qt::CaseInsensitive)) hex = hex.mid(2);
    bool ok = false;
    int64_t targetVal = hex.toLongLong(&ok, 16);
    if (!ok) return;

    // 1. Exact Match
    for (const auto &n : AllNotes)
    {
        if (ParseAddress(n.Address) == targetVal)
        {
            SelectBaseNote(n);
            return;
        }
    }

    // 2. Range Match
    for (const auto &r : _rangeCache)
    {
        if (targetVal >= r.Start && targetVal < r.End && r.Node != nullptr)
        {
            // Resolve the live note by address: RangeCacheEntry holds a snapshot taken
            // during LoadNotes, and using it would discard edits made since then.
            CodeNote liveNote = r.Note;
            for (const auto &n : AllNotes)
            {
                if (n.Address.compare(r.Note.Address, Qt::CaseInsensitive) == 0)
                {
                    liveNote = n;
                    break;
                }
            }
            SelectBaseNote(liveNote);

            int64_t delta = targetVal - r.Start;
            QString desc = r.Node->Description;
            if (desc.isEmpty() || desc == "Full Note")
            {
                QString norm = r.Node->Content;
                norm.replace("\r\n", "\n").replace('\r', '\n');
                QStringList parts = norm.split('\n', Qt::SkipEmptyParts);
                if (!parts.isEmpty()) desc = parts.first().trimmed();
            }

            QString virtualDesc = QString("%1 +0x%2").arg(desc, QString::number(delta, 16).toUpper());

            // Setup Virtual Mode
            QList<int64_t> offsets = { delta };
            SetEditState(
                true,
                QString("[Indirect from %1] %2").arg(liveNote.Address, virtualDesc),
                virtualDesc, // Default Text
                QString("0x%1").arg(targetVal, 0, 16),
                "Mem",
                "8-bit",
                &offsets // Treat as offset from base
            );
            return;
        }
    }

    // 3. No Note Found - Create New
    QString newAddress = QString("0x%1").arg(targetVal, 0, 16);
    CodeNote newNote;
    newNote.Address = newAddress;
    newNote.Note = "[No code note for this address]"; // Placeholder text
    newNote.User = "Local";

    // Add to collection and sort to maintain order
    AllNotes.append(newNote);
    std::sort(AllNotes.begin(), AllNotes.end(), [this](const CodeNote &a, const CodeNote &b) {
        return ParseAddress(a.Address) < ParseAddress(b.Address);
    });

    // Notify UI to refresh dropdown list
    emit NotesLoaded();

    // Select the newly created note
    SelectBaseNote(newNote);
}

// Helper to provide NotesLookup for Range Array Indexing Filtering
QMap<QString, QList<std::shared_ptr<NoteNode>>> CodeNoteViewModel::NotesLookup()
{
    QMap<QString, QList<std::shared_ptr<NoteNode>>> lookup;
    for (const auto &note : AllNotes)
    {
        // Last duplicate wins, matching the C# dictionary indexer.
        lookup[LogicFormatter::NormalizeAddress(note.Address)] = _parser.ParseNoteText(note.Note);
    }
    return lookup;
}

void CodeNoteViewModel::HighlightVariable(QList<int64_t> offsets)
{
    if (EditState.IsDirty) CommitEditState();

    if (offsets.isEmpty()) return;

    // Re-apply Array Indexing Filter just in case it wasn't cleaned by the caller
    while (offsets.size() > 1)
    {
        int64_t baseAddr = offsets[0];
        if (ConditionFormatter::GetEffectiveNote(NotesLookup(), baseAddr) == nullptr)
        {
            offsets.removeAt(0);
        }
        else
        {
            break;
        }
    }

    QString baseHex = QString("0x%1").arg(offsets[0], 0, 16);
    for (const auto &n : AllNotes)
    {
        if (n.Address.compare(baseHex, Qt::CaseInsensitive) == 0)
        {
            SelectBaseNote(n);
            break;
        }
    }

    if (CurrentBaseNote == nullptr || CurrentParsedNodes.isEmpty()) return;

    const auto &nodes = CurrentParsedNodes;
    std::shared_ptr<NoteNode> bestMatchNode = nullptr;
    int matchedDepth = 0;
    int64_t finalRemainder = 0;

    auto ParseOffsetString = [](const QString &offsetStr) -> int64_t {
        QString clean = offsetStr.trimmed();
        clean.remove('|').remove('+').remove('-').remove("0x", Qt::CaseInsensitive);
        bool ok = false;
        int64_t val = clean.toLongLong(&ok, 16);
        if (ok)
        {
            if (offsetStr.contains('-')) return -val;
            return val;
        }
        return std::numeric_limits<int64_t>::min();
    };

    QList<std::shared_ptr<NoteNode>> rootNodes;
    for (const auto &n : nodes)
    {
        if (n->IndentLevel == 0 || (n->Parent != nullptr && n->Parent->IndentLevel == -1) || (n->Parent == nullptr && n->IndentLevel != -2))
            rootNodes.append(n);
    }

    // Skip the base address since we're already at the root of its note
    // offsets[0] is the base address, offsets[1] is the first actual inner offset
    if (offsets.size() <= 1) return;

    std::shared_ptr<NoteNode> current = nullptr;
    for (const auto &n : rootNodes)
    {
        if (ParseOffsetString(n->Offset) == offsets[1])
        {
            current = n;
            break;
        }
    }

    if (current == nullptr)
    {
        for (const auto &n : rootNodes)
        {
            int64_t start = ParseOffsetString(n->Offset);
            if (start != std::numeric_limits<int64_t>::min() && offsets[1] >= start && offsets[1] < start + n->Size)
            {
                current = n;
                finalRemainder = offsets[1] - start;
                break;
            }
        }
    }

    if (current != nullptr)
    {
        bestMatchNode = current;
        matchedDepth = 2; // Matched base + first offset

        // Continue matching even if we have a remainder (Array/Struct coverage logic)
        while (matchedDepth < offsets.size())
        {
            int64_t nextOffset = offsets[matchedDepth];

            // Try direct children or direct range match exactly at this specific level
            std::shared_ptr<NoteNode> nextNode = nullptr;
            for (const auto &n : current->Children)
            {
                if (ParseOffsetString(n->Offset) == nextOffset) { nextNode = n; break; }
            }

            int64_t stepRemainder = 0;

            if (nextNode == nullptr)
            {
                for (const auto &n : current->Children)
                {
                    int64_t start = ParseOffsetString(n->Offset);
                    if (start != std::numeric_limits<int64_t>::min() && nextOffset >= start && nextOffset < start + n->Size)
                    {
                        nextNode = n;
                        stepRemainder = nextOffset - start;
                        break;
                    }
                }
            }

            if (nextNode != nullptr)
            {
                current = nextNode;
                bestMatchNode = current;
                matchedDepth++;
                finalRemainder += stepRemainder;
            }
            else
            {
                // BREAK OUT IMMEDIATELY if we can't find the next node as a direct child!
                break;
            }
        }
    }

    if (bestMatchNode != nullptr && matchedDepth == offsets.size() && finalRemainder == 0)
    {
        // Exact match found -> Real Node Editing Mode
        RequestNodeSelection(bestMatchNode);
        return;
    }

    // Partial Match or No Match -> Virtual Mode
    if (bestMatchNode != nullptr)
    {
        auto [baseLogic, type, size] = _parser.GeneratePointerLogic(*CurrentBaseNote, bestMatchNode, ConsoleId);

        QString baseAddrStr = CurrentBaseNote->Address.startsWith("0x", Qt::CaseInsensitive)
            ? CurrentBaseNote->Address.mid(2)
            : CurrentBaseNote->Address;

        QString sbLogic = QString("I:0x%1").arg(baseAddrStr);
        for (int i = 1; i < offsets.size(); i++) // Skip base
        {
            sbLogic += "_";
            if (i == offsets.size() - 1) sbLogic += QString("0x%1").arg(offsets[i], 0, 16);
            else sbLogic += QString("I:0x%1").arg(offsets[i], 0, 16);
        }

        QString desc = bestMatchNode->Description.trimmed();
        // Only append remainders for the parts we didn't traverse
        for (int i = matchedDepth; i < offsets.size(); i++)
        {
            desc += QString(" +0x%1").arg(QString::number(offsets[i], 16).toUpper());
        }
        if (finalRemainder != 0)
        {
            desc += QString(" +0x%1").arg(QString::number(finalRemainder, 16).toUpper());
        }

        // Store chain offsets starting AFTER the base address to allow reconstruction inside InjectNoteContent correctly
        QList<int64_t> chainOffsets = offsets.mid(1);
        SetEditState(true, desc, desc, sbLogic, type, size, &chainOffsets);
        return;
    }

    if (bestMatchNode == nullptr)
    {
        QString baseAddrStr = CurrentBaseNote->Address.startsWith("0x", Qt::CaseInsensitive)
            ? CurrentBaseNote->Address.mid(2)
            : CurrentBaseNote->Address;

        QString sb = QString("I:0x%1").arg(baseAddrStr);

        for (int i = 1; i < offsets.size(); i++)
        {
            sb += "_";
            if (i == offsets.size() - 1) sb += QString("0x%1").arg(offsets[i], 0, 16);
            else sb += QString("I:0x%1").arg(offsets[i], 0, 16);
        }

        QString desc = QString("%1 Chain").arg(CurrentBaseNote->Address);
        for (int i = 1; i < offsets.size(); i++)
            desc += QString(" +0x%1").arg(QString::number(offsets[i], 16).toUpper());

        QList<int64_t> chainOffsets = offsets.mid(1);
        SetEditState(true, desc, desc, sb, "Mem", "32-bit", &chainOffsets);
        return;
    }
}

void CodeNoteViewModel::SetEditState(
    bool isVirtual,
    const QString &description,
    const QString &displayText,
    const QString &logicCode,
    const QString &type,
    const QString &size,
    const QList<int64_t> *offsets)
{
    EditState.IsVirtual = isVirtual;
    EditState.Description = description;
    EditState.DisplayText = displayText;
    EditState.LogicCode = logicCode;
    EditState.LogicType = type;
    EditState.LogicSize = size;
    EditState.ChainOffsets = (offsets != nullptr) ? *offsets : QList<int64_t>();
    EditState.IsDirty = false; // Reset dirty flag when creating new virtual state

    emit EditStateChanged();
}

int64_t CodeNoteViewModel::ParseAddress(const QString &addr) const
{
    if (addr.isEmpty()) return -1;
    QString hex = addr.trimmed();
    if (hex.startsWith("0x", Qt::CaseInsensitive)) hex = hex.mid(2);
    bool ok = false;
    int64_t result = hex.toLongLong(&ok, 16);
    return ok ? result : -1;
}

} // namespace RALogicEditor
