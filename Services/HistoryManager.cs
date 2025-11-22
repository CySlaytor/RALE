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
}   