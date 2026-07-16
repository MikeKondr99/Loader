namespace Loader.Lang;

/// <summary>
/// Диапазон текста в пользовательском выражении.
/// </summary>
public readonly record struct ExprSpan(uint StartRow, uint StartColumn, uint EndRow, uint EndColumn);
