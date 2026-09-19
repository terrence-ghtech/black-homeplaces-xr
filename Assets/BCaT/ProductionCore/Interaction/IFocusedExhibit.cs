namespace BCaT.Production.Interaction
{
    /// <summary>
    /// Minimal lifecycle contract for an exhibit that exclusively owns the
    /// visitor's focused exhibit UI.
    /// </summary>
    public interface IFocusedExhibit
    {
        bool IsOpen { get; }
        void Open();
        void Close();
    }

    /// <summary>
    /// Optional identity bridge for router targets that open a separate focused
    /// session component.
    /// </summary>
    public interface IFocusedExhibitTarget
    {
        IFocusedExhibit FocusedExhibit { get; }
    }
}
