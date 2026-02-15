using System.Globalization;

namespace ChatLLM.Converters;

public class InvertedBoolConverter : IValueConverter
{
    // Añadimos el símbolo '?' a object para permitir nulos, tal como pide la interfaz
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;

        return false; // Valor por defecto si el input no es un booleano
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;

        return false;
    }
}