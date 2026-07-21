# AGENTS

## TUnit

- В этом репозитории TUnit запускается через .NET Testing Platform, поэтому `dotnet test --filter ...` не использовать.
- Для выбора тестов использовать `--treenode-filter` после `--`.
- Формат фильтра: `/<Assembly>/<Namespace>/<Class>/<Test>`.
- Wildcard работает в отдельных сегментах пути.

Примеры:

```powershell
dotnet test --project tests\Loader.Core.Tests\Loader.Core.Tests.csproj --no-restore -- --treenode-filter "/*/*/JsonProviderTests/*" --timeout 120s
dotnet test --project tests\Loader.Core.Tests\Loader.Core.Tests.csproj --no-restore -- --treenode-filter "/*/*/JsonProviderAnalyzeTests/*" --timeout 120s
```
