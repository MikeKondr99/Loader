# JDBC Provider

## Цель

JDBC provider нужен как тестовый мост для БД, где .NET-native provider-а нет или он хуже поддерживается.
Скрипт не должен содержать путь к driver jar, user/password или JDBC URL.
Все это хранится во внешнем `ConnectionRegistry`.

## C# bridge

Выбран `IKVM`:

- open-source JVM/bytecode bridge для .NET;
- позволяет работать с Java API из C#;
- позволяет грузить jar по пути, без глобальной установки JDBC-драйвера в систему.

Альтернатива `IKVM.Maven.Sdk` полезна, если driver jar надо фиксировать как build dependency через Maven artifact.
Для Loader это хуже как default, потому что нам нужен runtime path из `Connect`, а не compile-time dependency на конкретный JDBC-драйвер.

## Driver artifacts

Hive 4.0.0:

```text
https://repo1.maven.org/maven2/org/apache/hive/hive-jdbc/4.0.0/hive-jdbc-4.0.0-standalone.jar
```

Kyuubi 1.9:

```text
https://repo1.maven.org/maven2/org/apache/kyuubi/kyuubi-hive-jdbc-shaded/1.9.0/kyuubi-hive-jdbc-shaded-1.9.0.jar
```

Для Kyuubi предпочтителен shaded artifact, чтобы не собирать вручную classpath из зависимостей.

## Connection Registry

Пример registry connection:

```json
{
  "Name": "dev_hive_jdbc",
  "Type": "Jdbc",
  "ConnectionString": "JarPath=C:\\drivers\\hive-4-classpath;DriverClass=org.apache.hive.jdbc.HiveDriver;JdbcUrl=jdbc:hive2://localhost:10000/default;User=hive;Password=hive"
}
```

`JarPath` может указывать на один jar или на директорию. Если указана директория, provider рекурсивно добавит все `*.jar`.
Для Hive 4.0.0 одного `hive-jdbc-4.0.0-standalone.jar` оказалось недостаточно: driver при `connect()` требует Hadoop classes.
Для playground classpath собран из контейнерных `/opt/hive/lib` и `/opt/hadoop/share/hadoop/common`.

## Script

```text
hive_orders:
LOAD *
FROM Connect(name='dev_hive_jdbc')
SQL
  SELECT id, city, amount, created_at
  FROM default.orders;
```

## Ограничения прототипа

- Нет нормализующего `DbDataReaderDecorator`.
- Типы берутся из `ResultSetMetaData` и базово мапятся в CLR-типы.
- Сложные JDBC значения читаются как `ToString()`.
- Hive `TIMESTAMP` в дефолтном JDBC чтении может приходить со timezone shift.
- Реальные Hive/Kyuubi тесты требуют живой сервер и локальный путь к jar.
