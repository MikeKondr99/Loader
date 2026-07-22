

// LOAD 
//     '[${variety.Upper()}]' as group,
//     [sepal.length].Num() as sepal_l,
//     [sepal.width].Num() as sepal_w,
//     [petal.length].Num() as petal_l,
//     [petal.width].Num() as petal_w
// FROM [iris.csv]();


LOAD 
  If(active = 'true', 'Активен', 'Не активен') as active,
  city_low_card as city,
  city_low_card.Substring(city_low_card.Index('_') + 1) as city_id 
FROM 
[.\benchmarks\Loader.Benchmarks\Fixtures\Generated\wide-v2-1000000.csv]
;


// LOAD
//     database,
//     name,
//     engine,
//     total_rows,
//     total_bytes
// FROM [Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader]
// (clickhouse, table='system.tables');