using System;
namespace Pixellum.Core
{
    public interface ICommand
    {
        void Execute(); 
        void Undo();    
        void Redo();    
    }
}
