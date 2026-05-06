using System;
using System.Reflection;

namespace Gameyogi.BuildChecklist.Editor.Discovery
{
    /// <summary>
    /// A serialized field tagged with <see cref="BuildCheckFieldAttribute"/> that
    /// has been discovered by <see cref="AttributedFieldDiscovery"/>.
    /// Pure data DTO — no Unity-asset references.
    /// </summary>
    public sealed class DiscoveredField
    {
        /// <summary>
        /// Stable identifier persisted in rule entries.
        /// Format: "{Type.FullName}::{FieldName}".
        /// Renaming or deleting the underlying field will invalidate this id;
        /// the rule will then surface as a "missing field" row in the window.
        /// </summary>
        public string Id { get; }

        public Type DeclaringType { get; }
        public FieldInfo Field { get; }
        public BuildCheckFieldAttribute Attribute { get; }

        /// <summary>e.g. "GameConfig.targetFps" — for display in the window.</summary>
        public string DisplayName { get; }

        /// <summary>The field's declared type (int, bool, AudioMixer, …).</summary>
        public Type FieldType => Field.FieldType;

        public string Category =>
            string.IsNullOrWhiteSpace(Attribute.Category) ? "General" : Attribute.Category;

        public DiscoveredField(FieldInfo field, BuildCheckFieldAttribute attribute)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            DeclaringType = field.DeclaringType
                ?? throw new ArgumentException("Field has no declaring type", nameof(field));

            Id = MakeId(DeclaringType, field.Name);
            DisplayName = $"{DeclaringType.Name}.{field.Name}";
        }

        public static string MakeId(Type declaringType, string fieldName)
            => $"{declaringType.FullName}::{fieldName}";
    }
}
