namespace Gameyogi.BuildChecklist.Editor.Rules
{
    /// <summary>
    /// Operators supported when comparing a serialized field's current value
    /// against the rule's expected value.
    /// </summary>
    public enum ComparisonOp
    {
        Equals = 0,
        NotEquals = 1,
        Min = 2,         // currentValue >= expected
        Max = 3,         // currentValue <= expected
        Range = 4,       // expected <= currentValue <= expectedMax
        NotNull = 5,     // UnityEngine.Object reference must be assigned
        NotEmpty = 6,    // string / collection must have length > 0
        OneOf = 7        // currentValue is in oneOfValues
    }
}
