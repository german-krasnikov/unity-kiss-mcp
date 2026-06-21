using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal sealed class AnnotationHistory
    {
        private readonly List<IAnnotationCommand> _commands = new List<IAnnotationCommand>();
        private int _cursor; // points past the last active command

        internal IReadOnlyList<IAnnotationCommand> Active
        {
            get
            {
                // Return only [0.._cursor) — active slice
                if (_cursor == _commands.Count) return _commands;
                return _commands.GetRange(0, _cursor);
            }
        }

        internal int Count => _cursor;
        internal bool CanUndo => _cursor > 0;
        internal bool CanRedo => _cursor < _commands.Count;

        internal void Add(IAnnotationCommand cmd)
        {
            // Truncate redo branch
            if (_cursor < _commands.Count)
                _commands.RemoveRange(_cursor, _commands.Count - _cursor);
            _commands.Add(cmd);
            _cursor++;
        }

        internal void Undo()
        {
            if (_cursor > 0) _cursor--;
        }

        internal void Redo()
        {
            if (_cursor < _commands.Count) _cursor++;
        }

        internal void Clear()
        {
            _commands.Clear();
            _cursor = 0;
        }
    }
}
