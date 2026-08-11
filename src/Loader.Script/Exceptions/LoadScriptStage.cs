namespace Loader.Script;

public enum LoadScriptStage
{
    ProviderResolution,
    SourceOpen,
    TempTableWrite,
    QueryResolution,
    QueryCompilation,
    FinalTableWrite,
    DropTable
}
