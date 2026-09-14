using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Nezabudka.App.Services;

namespace Nezabudka.App.ViewModels;

public sealed class CalculatorViewModel : INotifyPropertyChanged
{
    private decimal _storedValue;
    private string? _pendingOperation;
    // Keep input and expressions culture-independent so changing language mid-calculation is safe.
    private string _display = "0";
    private string _historyKey = "CalculatorReady";
    private string? _historyExpression;
    private bool _replaceDisplay = true;
    private bool _hasError;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Display => HasError
        ? LocalizationService.Instance.Get("CalculatorError")
        : LocalizeNumber(_display);

    public string History => _historyExpression is null
        ? LocalizationService.Instance.Get(_historyKey)
        : LocalizeNumber(_historyExpression);

    public string DecimalSeparator => LocalizationService.Instance.Culture.NumberFormat.NumberDecimalSeparator;

    public bool HasError => _hasError;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(DecimalSeparator));
    }

    public void Press(string key)
    {
        if (_hasError && key != "C")
        {
            Clear();
        }

        if (key.Length == 1 && key[0] is >= '0' and <= '9')
        {
            EnterDigit(key);
            return;
        }

        switch (key)
        {
            case ",":
            case ".":
                EnterDecimalSeparator();
                break;
            case "+":
            case "−":
            case "×":
            case "÷":
                ChooseOperation(key);
                break;
            case "=":
                CompleteOperation();
                break;
            case "±":
                ToggleSign();
                break;
            case "%":
                ApplyPercent();
                break;
            case "⌫":
                Backspace();
                break;
            case "C":
                Clear();
                break;
        }
    }

    private void EnterDigit(string digit)
    {
        if (_replaceDisplay || _display == "0")
        {
            SetDisplay(digit);
            _replaceDisplay = false;
            return;
        }

        if (_display.Length < 18)
        {
            SetDisplay(_display + digit);
        }
    }

    private void EnterDecimalSeparator()
    {
        if (_replaceDisplay)
        {
            SetDisplay("0.");
            _replaceDisplay = false;
        }
        else if (!_display.Contains('.'))
        {
            SetDisplay(_display + ".");
        }
    }

    private void ChooseOperation(string operation)
    {
        var currentValue = ReadDisplay();
        if (_pendingOperation is not null && !_replaceDisplay)
        {
            currentValue = Calculate(_storedValue, currentValue, _pendingOperation);
            if (_hasError)
            {
                return;
            }

            SetDisplay(Format(currentValue));
        }

        _storedValue = currentValue;
        _pendingOperation = operation;
        SetHistoryExpression($"{Format(_storedValue)} {operation}");
        _replaceDisplay = true;
    }

    private void CompleteOperation()
    {
        if (_pendingOperation is null)
        {
            return;
        }

        var rightValue = ReadDisplay();
        var operation = _pendingOperation;
        var result = Calculate(_storedValue, rightValue, operation);
        if (_hasError)
        {
            return;
        }

        SetHistoryExpression($"{Format(_storedValue)} {operation} {Format(rightValue)} =");
        SetDisplay(Format(result));
        _storedValue = result;
        _pendingOperation = null;
        _replaceDisplay = true;
    }

    private void ToggleSign()
    {
        SetDisplay(Format(-ReadDisplay()));
        _replaceDisplay = true;
    }

    private void ApplyPercent()
    {
        SetDisplay(Format(ReadDisplay() / 100m));
        SetHistoryKey("CalculatorPercent");
        _replaceDisplay = true;
    }

    private void Backspace()
    {
        if (_replaceDisplay || _display.Length <= 1)
        {
            SetDisplay("0");
            _replaceDisplay = true;
            return;
        }

        SetDisplay(_display[..^1]);
        if (_display is "" or "-")
        {
            SetDisplay("0");
            _replaceDisplay = true;
        }
    }

    private void Clear()
    {
        _storedValue = 0;
        _pendingOperation = null;
        _replaceDisplay = true;
        _hasError = false;
        OnPropertyChanged(nameof(HasError));
        SetDisplay("0");
        SetHistoryKey("CalculatorReady");
    }

    private decimal Calculate(decimal left, decimal right, string operation)
    {
        try
        {
            return operation switch
            {
                "+" => left + right,
                "−" => left - right,
                "×" => left * right,
                "÷" when right != 0 => left / right,
                "÷" => SetError(),
                _ => right
            };
        }
        catch (OverflowException)
        {
            return SetError();
        }
    }

    private decimal SetError()
    {
        _hasError = true;
        _pendingOperation = null;
        _replaceDisplay = true;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(Display));
        SetHistoryKey("CalculatorCannotCalculate");
        return 0;
    }

    private void SetDisplay(string value)
    {
        _display = value;
        OnPropertyChanged(nameof(Display));
    }

    private void SetHistoryKey(string key)
    {
        _historyKey = key;
        _historyExpression = null;
        OnPropertyChanged(nameof(History));
    }

    private void SetHistoryExpression(string expression)
    {
        _historyExpression = expression;
        OnPropertyChanged(nameof(History));
    }

    private decimal ReadDisplay() =>
        decimal.TryParse(_display, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static string Format(decimal value) => value.ToString("0.###############", CultureInfo.InvariantCulture);

    private string LocalizeNumber(string value) => value.Replace(".", DecimalSeparator, StringComparison.Ordinal);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
