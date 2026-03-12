using System;
using System.Collections.Generic;

namespace Pixellum.Core
{
    public class HistoryManager
    {
        private readonly Stack<ICommand> _undoStack = new Stack<ICommand>();
        private readonly Stack<ICommand> _redoStack = new Stack<ICommand>();
        private const int MAX_HISTORY = 50;
        /// <summary>
        /// </summary>
        public void Do(ICommand command)
        {
            if (command == null)
                return;

            try
            {
                command.Execute();
                _undoStack.Push(command);
                _redoStack.Clear();

                if (_undoStack.Count > MAX_HISTORY)
                {
                    var items = _undoStack.ToArray();
                    _undoStack.Clear();
                    for (int i = 0; i < MAX_HISTORY; i++)
                        _undoStack.Push(items[i]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Command execution failed: {ex.Message}");
            }
        }

        public void Undo()
        {
            if (_undoStack.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Nothing to undo");
                return;
            }

            try
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Undo failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        public void Redo()
        {
            if (_redoStack.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to redo");
                return;
            }
            try
            {
                var command = _redoStack.Pop();
                command.Redo();
                _undoStack.Push(command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Redo failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

  
        public bool CanUndo => _undoStack.Count > 0;

        public bool CanRedo => _redoStack.Count > 0;

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}