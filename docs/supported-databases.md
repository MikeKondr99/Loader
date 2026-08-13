# Supported Database Versions

Эти версии используются в `Loader.Tests.Common` для Testcontainers и считаются базовой dev/test матрицей.

| Database | Docker image |
| --- | --- |
| ClickHouse | `clickhouse/clickhouse-server:24.8.14.39` |
| PostgreSQL | `postgres:18.4-alpine` |
| SQL Server | `mcr.microsoft.com/mssql/server:2022-CU23-ubuntu-22.04` |
| Oracle | `gvenzl/oracle-free:23.26.2-slim-faststart` |
| YDB | `ydbplatform/local-ydb:26.1.1.22` |

YDB local container discovery возвращает порт из `GRPC_PORT`, а не из Docker mapped port.
Test fixture выбирает свободный local port и передает его в `GRPC_PORT`, чтобы не конфликтовать с dev-контейнером на `2136`.
YDB tests используют parallel limiter `1`, потому что каждый контейнер должен advertising-ить свой host port, а свободный port выбирается до `docker run`.
Параллельный старт увеличивает риск race за порт и нестабильных discovery endpoint-ов.
