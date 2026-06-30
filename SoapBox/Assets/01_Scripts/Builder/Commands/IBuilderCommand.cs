namespace Soapbox.Builder.Commands
{
    /// <summary>
    /// A reversible builder action. Implements the Command pattern so the builder can
    /// support undo/redo uniformly across placing, deleting, transforming and duplicating.
    /// </summary>
    public interface IBuilderCommand
    {
        /// <summary>Performs (or re-performs, on redo) the action.</summary>
        void Execute();

        /// <summary>Reverses the action.</summary>
        void Undo();
    }
}
