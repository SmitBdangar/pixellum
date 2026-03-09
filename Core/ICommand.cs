using System;
namespace Pixellum.Core
{
    /// <summary>
    /// Interface for commands that can be executed, undone, and redone. [cite: 30]
    /// </summary>
    public interface ICommand
    {
        void Execute(); 
        void Undo();    
        void Redo();    
    }
}
