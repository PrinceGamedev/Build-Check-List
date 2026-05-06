using System;
using UnityObject = UnityEngine.Object;

namespace Gameyogi.BuildChecklist.Editor.Rules
{
    /// <summary>Discriminator for <see cref="SerializedValue"/>.</summary>
    public enum ValueKind
    {
        None = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        String = 4,
        Enum = 5,
        Object = 6
    }

    /// <summary>
    /// Type-erased value container that Unity can serialize directly (no
    /// <c>[SerializeReference]</c>). Stores an int / float / bool / string /
    /// enum / Object reference; <see cref="kind"/> is the active discriminator.
    /// </summary>
    [Serializable]
    public sealed class SerializedValue
    {
        public ValueKind kind = ValueKind.None;

        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;

        // Enum is stored as the int value plus the assembly-qualified type name,
        // so the rule survives renames/relocations of the enum type's namespace.
        public string enumTypeName;

        public UnityObject objectValue;

        public SerializedValue() { }

        // ── Factories ──────────────────────────────────────────────────────────

        public static SerializedValue OfInt(int v)
            => new SerializedValue { kind = ValueKind.Int, intValue = v };

        public static SerializedValue OfFloat(float v)
            => new SerializedValue { kind = ValueKind.Float, floatValue = v };

        public static SerializedValue OfBool(bool v)
            => new SerializedValue { kind = ValueKind.Bool, boolValue = v };

        public static SerializedValue OfString(string v)
            => new SerializedValue { kind = ValueKind.String, stringValue = v ?? string.Empty };

        public static SerializedValue OfEnum(Enum v)
        {
            if (v == null) throw new ArgumentNullException(nameof(v));
            return new SerializedValue
            {
                kind = ValueKind.Enum,
                intValue = Convert.ToInt32(v),
                enumTypeName = v.GetType().AssemblyQualifiedName
            };
        }

        public static SerializedValue OfObject(UnityObject v)
            => new SerializedValue { kind = ValueKind.Object, objectValue = v };

        // ── Accessors ──────────────────────────────────────────────────────────

        /// <summary>Returns the boxed value matching <see cref="kind"/>, or null.</summary>
        public object GetBoxed()
        {
            switch (kind)
            {
                case ValueKind.Int: return intValue;
                case ValueKind.Float: return floatValue;
                case ValueKind.Bool: return boolValue;
                case ValueKind.String: return stringValue;
                case ValueKind.Enum: return intValue; // enum compared as int
                case ValueKind.Object: return objectValue;
                default: return null;
            }
        }

        /// <summary>Human-readable summary used in window rows and result messages.</summary>
        public string Describe()
        {
            switch (kind)
            {
                case ValueKind.Int: return intValue.ToString();
                case ValueKind.Float: return floatValue.ToString("R");
                case ValueKind.Bool: return boolValue ? "true" : "false";
                case ValueKind.String: return $"\"{stringValue}\"";
                case ValueKind.Enum:
                    var t = ResolveEnumType();
                    return t != null ? Enum.ToObject(t, intValue).ToString() : intValue.ToString();
                case ValueKind.Object: return objectValue ? objectValue.name : "<null>";
                default: return "<unset>";
            }
        }

        public Type ResolveEnumType()
            => string.IsNullOrEmpty(enumTypeName) ? null : Type.GetType(enumTypeName, throwOnError: false);
    }
}
