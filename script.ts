

// LOAD 
//     '[${variety.Upper()}]' as group,
//     [sepal.length].Num() as sepal_l,
//     [sepal.width].Num() as sepal_w,
//     [petal.length].Num() as petal_l,
//     [petal.width].Num() as petal_w
// FROM [iris.csv]();


// LOAD 
//   *
// FROM 
// [.\benchmarks\Loader.Benchmarks\Fixtures\Generated\src_brd_data_qpr_testoutmem.qvd];

// LOAD 
//   *
// FROM 
// [.\benchmarks\Loader.Benchmarks\Fixtures\Generated\wide-v2-1000000.csv];

LOAD 
  *
FROM 
[.\benchmarks\Loader.Benchmarks\Fixtures\Generated\wide-v2-1000000.xml]
(table='row');

// LOAD
//     database,
//     name,
//     engine,
//     total_rows,
//     total_bytes
// FROM [Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader]
// (clickhouse, table='system.tables');