#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Services
{
    public class HistoryManager<T>
    {
        private Stack<T> _undoStack = new();
        private Stack<T> _redoStack = new();

        public bool IsUndoingRedoing { get; private set; }

        public event EventHandler? HistoryChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void PushState(T state)
        {
            if (IsUndoingRedoing) return;

            // Prevent duplicate states if the new state matches the top of stack
            if (_undoStack.Count > 0)
            {
                T top = _undoStack.Peek();
                if (top != null && top.Equals(state))
                    return;
            }

            _undoStack.Push(state);
            _redoStack.Clear();
            OnHistoryChanged();
        }

        public T Undo(T currentState)
        {
            if (_undoStack.Count == 0) return currentState;

            IsUndoingRedoing = true;
            _redoStack.Push(currentState);
            var previousState = _undoStack.Pop();
            IsUndoingRedoing = false;

            OnHistoryChanged();
            return previousState;
        }

        public T Redo(T currentState)
        {
            if (_redoStack.Count == 0) return currentState;

            IsUndoingRedoing = true;
            _undoStack.Push(currentState);
            var nextState = _redoStack.Pop();
            IsUndoingRedoing = false;

            OnHistoryChanged();
            return nextState;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            IsUndoingRedoing = false;
            OnHistoryChanged();
        }

        private void OnHistoryChanged()
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Helper class to encapsulate the full state of the Trigger Editor
    public class TriggerEditorState
    {
        public string LogicString { get; set; } = "";
        public Dictionary<int, RALogicEditor.Models.GeneratorDefinition> GeneratorRules { get; set; } = new();

        public TriggerEditorState Clone()
        {
            var clone = new TriggerEditorState { LogicString = this.LogicString };
            foreach (var kvp in this.GeneratorRules)
            {
                var ruleClone = new RALogicEditor.Models.GeneratorDefinition { Copies = kvp.Value.Copies };
                foreach (var r in kvp.Value.Rules)
                {
                    ruleClone.Rules.Add(new RALogicEditor.Models.GeneratorRule { SourceAddress = r.SourceAddress, Stride = r.Stride });
                }
                clone.GeneratorRules[kvp.Key] = ruleClone;
            }
            return clone;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not TriggerEditorState other) return false;
            if (LogicString != other.LogicString) return false;

            // Deep compare generators logic
            if (GeneratorRules.Count != other.GeneratorRules.Count) return false;

            foreach (var kvp in GeneratorRules)
            {
                if (!other.GeneratorRules.ContainsKey(kvp.Key)) return false;
                var r1 = kvp.Value;
                var r2 = other.GeneratorRules[kvp.Key];
                if (r1.Copies != r2.Copies) return false;
                if (r1.Rules.Count != r2.Rules.Count) return false;
                // Simple rule check assuming order might matter or strict equality
                for (int i = 0; i < r1.Rules.Count; i++)
                {
                    if (r1.Rules[i].SourceAddress != r2.Rules[i].SourceAddress) return false;
                    if (r1.Rules[i].Stride != r2.Rules[i].Stride) return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            return LogicString.GetHashCode() ^ GeneratorRules.Count.GetHashCode();
        }
    }
}