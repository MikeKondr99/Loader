# Round, Floor, Ceil

`Round`, `Floor` и `Ceil` округляют `num`.

Все три функции поддерживают три варианта:

- `Func(value)` - округление до целого.
- `Func(value, step)` - округление до ближайшего кратного `step`.
- `Func(value, step, offset)` - округление до сетки с заданным шагом и смещением.

## Floor(value)

`Floor(value)` округляет число вниз до ближайшего целого.

Примеры:

| Expression | Result |
| --- | --- |
| `Floor(1.2)` | `1.0` |
| `Floor(1.8)` | `1.0` |
| `Floor(2.4)` | `2.0` |
| `Floor(4.2)` | `4.0` |

## Floor(value, step)

`Floor(value, step)` округляет число вниз до ближайшего кратного `step`.

Примеры:

| Expression | Result |
| --- | --- |
| `Floor(4.7, 2.0)` | `4.0` |
| `Floor(3.88, .1)` | `3.8` |
| `Floor(3.88, 5.0)` | `0.0` |
| `Floor(4.7, .5)` | `4.5` |

## Floor(value, step, offset)

`Floor(value, step, offset)` округляет число вниз по сетке `offset + step * n`.

Примеры:

| Expression | Result |
| --- | --- |
| `Floor(1.1, 1.0, 0.5)` | `0.5` |
| `Floor(-150.0, 50.0, 25.0)` | `-175.0` |

## Ceil(value)

`Ceil(value)` округляет число вверх до ближайшего целого.

Примеры:

| Expression | Result |
| --- | --- |
| `Ceil(1.2)` | `2.0` |
| `Ceil(1.8)` | `2.0` |

## Ceil(value, step)

`Ceil(value, step)` округляет число вверх до ближайшего кратного `step`.

Примеры:

| Expression | Result |
| --- | --- |
| `Ceil(4.7, .5)` | `5.0` |
| `Ceil(4.7, 2.0)` | `6.0` |

## Ceil(value, step, offset)

`Ceil(value, step, offset)` округляет число вверх по сетке `offset + step * n`.

Примеры:

| Expression | Result |
| --- | --- |
| `Ceil(1.1, 1.0, -0.01)` | `1.99` |

## Round(value)

`Round(value)` округляет число до ближайшего целого.

Примеры:

| Expression | Result |
| --- | --- |
| `Round(1.2)` | `1.0` |
| `Round(1.8)` | `2.0` |
| `Round(0.5)` | `1.0` |
| `Round(0.7)` | `1.0` |

## Round(value, step)

`Round(value, step)` округляет число до ближайшего кратного `step`.

Примеры:

| Expression | Result |
| --- | --- |
| `Round(4.7, 2.0)` | `4.0` |
| `Round(5.3, 2.0)` | `6.0` |
| `Round(4.7, .5)` | `4.5` |
| `Round(5.3, .5)` | `5.5` |
| `Round(2.5, 1.0)` | `3.0` |
| `Round(2.0, 4.0)` | `4.0` |

## Round(value, step, offset)

`Round(value, step, offset)` округляет число до ближайшей точки сетки `offset + step * n`.

Примеры:

| Expression | Result |
| --- | --- |
| `Round(1.1, 1.0, .5)` | `1.5` |
| `Round(2.0, 4.0, .0)` | `4.0` |
| `Round(100.0, 1.0, 200.0)` | `100.0` |
| `Round(-130.0, 50.0, 25.0)` | `-125.0` |
