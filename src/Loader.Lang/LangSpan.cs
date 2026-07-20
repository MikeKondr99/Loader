namespace Loader.Lang;

/// <summary>
/// Диапазон текста в пользовательском языке Loader.
/// </summary>
public readonly record struct LangSpan(uint StartRow, uint StartColumn, uint EndRow, uint EndColumn);
