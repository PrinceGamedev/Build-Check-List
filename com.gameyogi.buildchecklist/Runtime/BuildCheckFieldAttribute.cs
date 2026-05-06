using System;

namespace Gameyogi.BuildChecklist
{
    /// <summary>
    /// Marks a serialized field as participating in the Build Checklist.
    /// The actual rule (operator, expected value, severity) is defined in the
    /// Build Checklist editor window, not in this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class BuildCheckFieldAttribute : Attribute
    {
        public string Category { get; set; } = "General";
        public string Description { get; set; }
    }
}
