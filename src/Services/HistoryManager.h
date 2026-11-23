#pragma once

#include "../Models/GeneratorModels.h"
#include "../Models/LogicModels.h"

#include <QList>
#include <QMap>
#include <QStack>
#include <QString>

#include <algorithm>
#include <functional>

namespace RALE {

template <typename T> class HistoryManager {
  private:
    QStack<T> _undoStack;
    QStack<T> _redoStack;
    static constexpr int MaxDepth = 100;

  public:
    bool IsUndoingRedoing = false;
    std::function<void()> HistoryChanged;

    bool CanUndo() const { return !_undoStack.isEmpty(); }
    bool CanRedo() const { return !_redoStack.isEmpty(); }

    int UndoCount() const { return _undoStack.size(); }
    int RedoCount() const { return _redoStack.size(); }

    void PushState(const T& state) {
        if (IsUndoingRedoing)
            return;

        // Prevent duplicate states if the new state matches the top of stack
        if (!_undoStack.isEmpty()) {
            const T& top = _undoStack.top();
            if (top == state)
                return;
        }

        _undoStack.push(state);
        if (_undoStack.size() > MaxDepth) {
            _undoStack.removeFirst();
        }
        _redoStack.clear();
        OnHistoryChanged();
    }

    T Undo(const T& currentState) {
        if (_undoStack.isEmpty())
            return currentState;

        _redoStack.push(currentState);
        if (_redoStack.size() > MaxDepth) {
            _redoStack.removeFirst();
        }
        T previousState = _undoStack.pop();

        OnHistoryChanged();
        return previousState;
    }

    T Redo(const T& currentState) {
        if (_redoStack.isEmpty())
            return currentState;

        _undoStack.push(currentState);
        if (_undoStack.size() > MaxDepth) {
            _redoStack.removeFirst();
        }
        T nextState = _redoStack.pop();

        OnHistoryChanged();
        return nextState;
    }

    void Clear() {
        _undoStack.clear();
        _redoStack.clear();
        IsUndoingRedoing = false;
        OnHistoryChanged();
    }

  private:
    void OnHistoryChanged() {
        if (HistoryChanged)
            HistoryChanged();
    }
};

// Helper class to encapsulate the full state of the Trigger Editor
class TriggerEditorState {
  public:
    QMap<QString, QList<AchievementConditionGroup>> Segments;
    QString ActiveSegmentName = "Core";
    int ActiveGroupIndex = 0;
    QMap<QString, QMap<int, GeneratorDefinition>> SegmentGeneratorRules;
    QMap<int, GeneratorDefinition> GeneratorRules;
    QString LogicString = "";

    // Asset Header Fields
    QString Title = "";
    QString Description = "";
    int Points = 0;
    QString Type = "";
    QString Format = "";
    bool LowerIsBetter = false;

    TriggerEditorState Clone() const {
        TriggerEditorState clone;
        clone.Segments = this->Segments;
        clone.ActiveSegmentName = this->ActiveSegmentName;
        clone.ActiveGroupIndex = this->ActiveGroupIndex;
        clone.SegmentGeneratorRules = this->SegmentGeneratorRules;
        clone.GeneratorRules = this->GeneratorRules;
        clone.LogicString = this->LogicString;
        clone.Title = this->Title;
        clone.Description = this->Description;
        clone.Points = this->Points;
        clone.Type = this->Type;
        clone.Format = this->Format;
        clone.LowerIsBetter = this->LowerIsBetter;
        return clone;
    }

    bool operator==(const TriggerEditorState& other) const {
        if (LogicString != other.LogicString)
            return false;
        if (Title != other.Title)
            return false;
        if (Description != other.Description)
            return false;
        if (Points != other.Points)
            return false;
        if (Type != other.Type)
            return false;
        if (Format != other.Format)
            return false;
        if (LowerIsBetter != other.LowerIsBetter)
            return false;
        if (ActiveSegmentName != other.ActiveSegmentName)
            return false;
        if (ActiveGroupIndex != other.ActiveGroupIndex)
            return false;

        // Deep compare generators logic
        if (GeneratorRules.size() != other.GeneratorRules.size())
            return false;

        for (auto it = GeneratorRules.cbegin(); it != GeneratorRules.cend(); ++it) {
            if (!other.GeneratorRules.contains(it.key()))
                return false;
            const auto& r1 = it.value();
            const auto& r2 = other.GeneratorRules[it.key()];
            if (r1.Copies != r2.Copies)
                return false;
            if (r1.IsArray != r2.IsArray)
                return false;
            if (r1.DuplicateSelectedOnly != r2.DuplicateSelectedOnly)
                return false;
            if (r1.DuplicateNotes != r2.DuplicateNotes)
                return false;
            if (r1.TemplateConditionCount != r2.TemplateConditionCount)
                return false;
            if (r1.Rules.size() != r2.Rules.size())
                return false;
            // Simple rule check assuming order might matter or strict equality
            for (int i = 0; i < r1.Rules.size(); i++) {
                if (r1.Rules[i] != r2.Rules[i])
                    return false;
            }
        }

        if (SegmentGeneratorRules != other.SegmentGeneratorRules)
            return false;
        if (Segments != other.Segments)
            return false;

        return true;
    }

    bool operator!=(const TriggerEditorState& other) const { return !(*this == other); }
};

} // namespace RALE
