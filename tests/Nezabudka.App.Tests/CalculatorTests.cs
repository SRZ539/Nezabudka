using Nezabudka.App.ViewModels;

namespace Nezabudka.App.Tests;

public sealed class CalculatorTests
{
    [Fact]
    public void AddsTwoNumbers()
    {
        var calculator = new CalculatorViewModel();

        calculator.Press("1");
        calculator.Press("2");
        calculator.Press("+");
        calculator.Press("3");
        calculator.Press("=");

        Assert.Equal("15", calculator.Display);
        Assert.Equal("12 + 3 =", calculator.History);
    }

    [Fact]
    public void DivisionByZeroShowsReadableError()
    {
        var calculator = new CalculatorViewModel();

        calculator.Press("8");
        calculator.Press("÷");
        calculator.Press("0");
        calculator.Press("=");

        Assert.Equal("Ошибка", calculator.Display);
        Assert.Equal("Невозможно выполнить операцию", calculator.History);
    }
}
