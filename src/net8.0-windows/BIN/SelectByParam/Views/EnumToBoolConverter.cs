using System;
using System.Globalization;
using System.Windows.Data;

namespace BIN.SelectByParam.Views;

public class EnumToBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null || parameter == null)
		{
			return false;
		}
		string enumValue = value.ToString();
		string targetValue = parameter.ToString();
		return enumValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null || parameter == null)
		{
			return Binding.DoNothing;
		}
		if (!(bool)value)
		{
			return Binding.DoNothing;
		}
		string targetValue = parameter.ToString();
		return Enum.Parse(targetType, targetValue);
	}
}
