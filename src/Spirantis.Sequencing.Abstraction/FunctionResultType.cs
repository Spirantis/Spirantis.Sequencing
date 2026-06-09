namespace Spirantis.Sequencing;

/// <summary>
/// The outcome category of a sequence function, used to choose which continuation branch runs next.
/// </summary>
public enum FunctionResultType : byte
{
    /// <summary>The function succeeded with a truthy outcome.</summary>
    True,

    /// <summary>The function succeeded with a falsy outcome.</summary>
    False,

    /// <summary>The function produced a value to be inspected by value predicates.</summary>
    Indeterminate,

    /// <summary>The function failed; the sequence should stop unless an abort branch handles it.</summary>
    Abort,
}
