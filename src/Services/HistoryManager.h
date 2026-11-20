#pragma once

#include "../Models/GeneratorModels.h"

#include <QMap>
#include <QStack>
#include <QString>

#include <functional>

namespace RALE {

template <typename T> class HistoryManager {
  private:
    QStack<T> _undoStack;
    QStack<T> _redoStack;

  public:
    bool IsUndoingRedoing = false;
    std::function<void()> HistoryChanged;

    bool CanUndo() const { return !_undoStack.isEmpty(); }
    bool CanRedo() const { return !_redoStack.isEmpty(); }

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
        _redoStack.clear();
        OnHistoryChanged();
    }

    T Undo(const T& currentState) {
        if (_undoStack.isEmpty())
            return currentState;

        IsUndoingRedoing = true;
        _redoStack.push(currentState);
        T previousState = _undoStack.pop();
        IsUndoingRedoing = false;

        OnHistoryChanged();
        return previousState;
    }

    T Redo(const T& currentState) {
        if (_redoStack.isEmpty())
            return currentState;

        IsUndoingRedoing = true;
        _undoStack.push(currentState);
        T nextState = _redoStack.pop();
        IsUndoingRedoing = false;

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
    QString LogicString = "";
    QMap<int, GeneratorDefinition> GeneratorRules;

    TriggerEditorState Clone() const {
        TriggerEditorState clone;
        clone.LogicString = this->LogicString;
        for (auto it = this->GeneratorRules.cbegin(); it != this->GeneratorRules.cend(); ++it) {
            GeneratorDefinition ruleClone;
            ruleClone.Copies = it.value().Copies;
            ruleClone.IsArray = it.value().IsArray;
            ruleClone.TemplateConditionCount = it.value().TemplateConditionCount;
            for (const auto& r : it.value().Rules) {
                GeneratorRule gr;
                gr.SourceAddress = r.SourceAddress;
                gr.Stride = r.Stride;
                ruleClone.Rules.append(gr);
            }
            clone.GeneratorRules[it.key()] = ruleClone;
        }
        return clone;
    }

    bool operator==(const TriggerEditorState& other) const {
        if (LogicString != other.LogicString)
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
            if (r1.TemplateConditionCount != r2.TemplateConditionCount)
                return false;
            if (r1.Rules.size() != r2.Rules.size())
                return false;
            // Simple rule check assuming order might matter or strict equality
            for (int i = 0; i < r1.Rules.size(); i++) {
                if (r1.Rules[i].SourceAddress != r2.Rules[i].SourceAddress)
                    return false;
                if (r1.Rules[i].Stride != r2.Rules[i].Stride)
                    return false;
            }
        }

        return true;
    }

    bool operator!=(const TriggerEditorState& other) const { return !(*this == other); }
};

} // namespace RALE
