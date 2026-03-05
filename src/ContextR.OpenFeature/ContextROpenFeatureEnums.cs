namespace ContextR.OpenFeature;

/// <summary>
/// Determines how duplicate EvaluationContext keys are handled.
/// </summary>
public enum ContextROpenFeatureCollisionBehavior
{
    /// <summary>
    /// The latest mapped value wins.
    /// </summary>
    LastWriteWins = 0,

    /// <summary>
    /// Throws when a duplicate key is encountered.
    /// </summary>
    Throw = 1
}

/// <summary>
/// Determines behavior when mapped values cannot be converted to OpenFeature Value.
/// </summary>
public enum ContextROpenFeatureUnsupportedValueBehavior
{
    /// <summary>
    /// Skips unsupported mapped values.
    /// </summary>
    Ignore = 0,

    /// <summary>
    /// Throws when unsupported mapped values are encountered.
    /// </summary>
    Throw = 1
}
