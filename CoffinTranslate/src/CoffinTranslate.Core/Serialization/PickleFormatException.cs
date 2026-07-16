namespace CoffinTranslate.Core.Serialization;

/// <summary>
/// Thrown when a byte stream is not a pickle we can safely read — either malformed,
/// or using an opcode outside the data-only subset (see <see cref="PythonPickle"/>).
/// </summary>
public sealed class PickleFormatException(string message) : Exception(message);
