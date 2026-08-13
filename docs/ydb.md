# YDB

## Version

Dev/test image:

```text
ydbplatform/local-ydb:26.1.1.22
```

Эта же версия закреплена в `Loader.Tests.Common/TestDatabaseImages.cs`.

## Dev Container

Для playground контейнер запускается с фиксированным port mapping:

```powershell
docker run -d --name loader-playground-ydb --hostname localhost `
  -p 2135:2135 -p 2136:2136 -p 8765:8765 `
  -e GRPC_TLS_PORT=2135 `
  -e GRPC_PORT=2136 `
  -e MON_PORT=8765 `
  -e YDB_ANONYMOUS_CREDENTIALS=true `
  -e YDB_USE_IN_MEMORY_PDISKS=true `
  ydbplatform/local-ydb:26.1.1.22
```

Connection string для playground:

```text
Host=localhost;Port=2136;Database=/local
```

## Playground Tables

В dev контейнер загружены таблицы:

- `playground_customers`
- `playground_orders`
- `playground_payments`
- `playground_events`

## LOAD Script

```text
ydb_orders:
LOAD
  order_id,
  customer_id,
  city,
  category,
  amount,
  created_at
FROM Connect(name='dev_ydb')
SQL
  SELECT order_id, customer_id, city, category, amount, created_at
  FROM playground_orders
  ORDER BY order_id;

ydb_city_sales:
LOAD
  city,
  COUNT(order_id) AS order_count,
  SUM(amount) AS total_amount
FROM ydb_orders
WHERE amount > 0
GROUP BY city
ORDER BY city;

ydb_payments:
LOAD
  payment_id,
  order_id,
  method,
  paid_amount,
  paid_at
FROM Connect(name='dev_ydb')
SQL
  SELECT payment_id, order_id, method, paid_amount, paid_at
  FROM playground_payments
  ORDER BY payment_id;

ydb_paid_by_method:
LOAD
  method,
  COUNT(payment_id) AS payment_count,
  SUM(paid_amount) AS paid_total
FROM ydb_payments
GROUP BY method
ORDER BY method;

ydb_events:
LOAD
  event_id,
  order_id,
  event_type,
  Text(payload) AS payload,
  created_at
FROM Connect(name='dev_ydb')
SQL
  SELECT event_id, order_id, event_type, payload, created_at
  FROM playground_events
  ORDER BY event_id;
```
