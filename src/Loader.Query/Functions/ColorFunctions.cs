using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class ColorFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Function("Rgb")
            .Doc("Создает цвет из компонентов RGB (0-255) в формате 0xFFRRGGBB")
            .Arg("r", DataType.Integer)
            .Arg("g", DataType.Integer)
            .Arg("b", DataType.Integer)
            .Returns(DataType.Integer)
            .Template($"bitOr(toUInt64(0xFF000000), bitOr(bitShiftLeft(bitAnd(toUInt64({0}), 255), 16), bitOr(bitShiftLeft(bitAnd(toUInt64({1}), 255), 8), bitAnd(toUInt64({2}), 255))))");
    }
}
