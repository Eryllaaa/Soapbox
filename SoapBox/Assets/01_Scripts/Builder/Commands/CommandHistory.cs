using System;
using System.Collections.Generic;

namespace Soapbox.Builder.Commands
{
    /// <summary>
    /// Undo/redo stack for <see cref="IBuilderCommand"/>s. A new command clears the redo
    /// stack. Use <see cref="Execute"/> for actions to perform now, or <see cref="Record"/>
    /// for actions that already happened (e.g. a part the placement system just placed).
    /// </summary>
    public sealed class CommandHistory
    {
        private readonly Stack<IBuilderCommand> _undo = new();
        private readonly Stack<IBuilderCommand> _redo = new();

        /// <summary>Raised whenever the stacks change (for enabling/disabling UI buttons).</summary>
        public event Action Changed;

        /// <summary>True when there is something to undo.</summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>True when there is something to redo.</summary>
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Executes a command and pushes it onto the undo stack.</summary>
        public void Execute(IBuilderCommand command)
        {
            if (command == null) return;
            command.Execute();
            _undo.Push(command);
            _redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>Pushes an already-performed command onto the undo stack without executing it.</summary>
        public void Record(IBuilderCommand command)
        {
            if (command == null) return;
            _undo.Push(command);
            _redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>Undoes the most recent command.</summary>
        public void Undo()
        {
            if (_undo.Count == 0) return;
            IBuilderCommand command = _undo.Pop();
            command.Undo();
            _redo.Push(command);
            Changed?.Invoke();
        }

        /// <summary>Re-executes the most recently undone command.</summary>
        public void Redo()
        {
            if (_redo.Count == 0) return;
            IBuilderCommand command = _redo.Pop();
            command.Execute();
            _undo.Push(command);
            Changed?.Invoke();
        }

        /// <summary>Clears both stacks (e.g. after loading a saved vehicle).</summary>
        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }
    }
}
