namespace CoffinTranslate.Core.SourceData;

/// <summary>Thrown when a Translator.dat file can be decompressed and unpickled but does not
/// have the expected top-level shape.</summary>
public sealed class GameSourceFormatException(string message) : Exception(message);
