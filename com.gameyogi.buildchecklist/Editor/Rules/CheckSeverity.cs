namespace Gameyogi.BuildChecklist.Editor.Rules
{
    /// <summary>
    /// Result severity. Only <see cref="Error"/> blocks the build.
    /// </summary>
    public enum CheckSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
}
