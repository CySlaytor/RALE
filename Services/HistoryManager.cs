using System.Collections.Generic;

namespace RALogicEditor.Services
{
    public class HistoryManager<T>
    {
        private Stack<T> _undoStack = new();
        private Stack<T> _redoStack = new();

        public bool IsUndoingRedoing { get; private set; }

        public void PushState(T state)
        {
            if (IsUndoingRedoing) return;

            // Prevent duplicate states if the new state matches the top of stack
            // Note: This equality check might be shallow for complex objects. 
            // For the complex state we are about to introduce, we might rely on the caller to ensure they aren't pushing redundant states
            // or implement deep comparison. For now, we keep the standard check.
            if (_undoStack.Count > 0 && EqualityComparer<T>.Default.Equals(_undoStack.Peek(), state))
                return;

            _undoStack.Push(state);
            _redoStack.Clear();
        }

        public T Undo(T currentState)
        {
            if (_undoStack.Count == 0) return currentState;

            IsUndoingRedoing = true;
            _redoStack.Push(currentState);
            var previousState = _undoStack.Pop();
            IsUndoingRedoing = false;

            return previousState;
        }

        public T Redo(T currentState)
        {
            if (_redoStack.Count == 0) return currentState;

            IsUndoingRedoing = true;
            _undoStack.Push(currentState);
            var nextState = _redoStack.Pop();
            IsUndoingRedoing = false;

            return nextState;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            IsUndoingRedoing = false;
        }
    }

    // Helper class to encapsulate the full state of the Trigger Editor
    public class TriggerEditorState
    {
        public string LogicString { get; set; } = "";
        // We must clone the dictionary to avoid reference issues in the stack
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
    }
}