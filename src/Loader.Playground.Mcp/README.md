# Loader.Playground MCP PoC

MCP server for iterating Loader scripts through a separately running `Loader.Playground`.

## Run

Start playground first:

```powershell
dotnet run --project src\Loader.Playground\Loader.Playground.csproj
```

Run MCP server over stdio:

```powershell
dotnet run --project src\Loader.Playground.Mcp\Loader.Playground.Mcp.csproj
```

By default it calls `http://localhost:5000`. Override it when needed:

```powershell
$env:LOADER_PLAYGROUND_URL = "http://localhost:5000"
dotnet run --project src\Loader.Playground.Mcp\Loader.Playground.Mcp.csproj
```

## Tools

- `GetContext` returns playground files, connection names/types, and last-run snapshot.
- `GetKnowledge` returns Loader script workflow, syntax, source probing, casts, cleaning defaults, and common fixes.
- `GetFile` returns the first lines of a playground file.
- `UploadFile` uploads or replaces a playground file. Use `contentEncoding='utf-8'` for text or `contentEncoding='base64'` for binary files.
- `DeleteFile` deletes a playground file.
- `TestRun` runs a script through `/api/test-run`, returns fields and preview rows, then asks playground to clean created tables.

`TestRun` uses the real playground execution pipeline, so it still needs ClickHouse and source connections. It does not update `last-run` and uses test table prefixes that are dropped after preview.

Text upload example:

```text
UploadFile(fileName='orders.csv', content='id,amount\n1,10', contentEncoding='utf-8')
```

Binary upload example:

```text
UploadFile(fileName='book.xlsx', content='<base64>', contentEncoding='base64')
```

The server also sends MCP server instructions asking agents to call `GetKnowledge` and `GetContext` before Loader script work. Restart the MCP server after changing these instructions or the knowledge text.
