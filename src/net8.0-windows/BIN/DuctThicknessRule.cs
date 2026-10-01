using System.ComponentModel;

namespace BIN;

public class DuctThicknessRule : INotifyPropertyChanged
{
	private double _limit;

	private double _thickness;

	private double _lowerLimit;

	public double Limit
	{
		get
		{
			return _limit;
		}
		set
		{
			_limit = value;
			OnPropertyChanged("Limit");
			OnPropertyChanged("DuctSizeText");
		}
	}

	public double Thickness
	{
		get
		{
			return _thickness;
		}
		set
		{
			_thickness = value;
			OnPropertyChanged("Thickness");
		}
	}

	public double LowerLimit
	{
		get
		{
			return _lowerLimit;
		}
		set
		{
			_lowerLimit = value;
			OnPropertyChanged("LowerLimit");
		}
	}

	public string DuctSizeText
	{
		get
		{
			if (Limit == 200.0)
			{
				return "1-200";
			}
			if (Limit == 350.0)
			{
				return "201 – 350";
			}
			if (Limit == 500.0)
			{
				return "351 – 500";
			}
			if (Limit == 750.0)
			{
				return "501 – 750";
			}
			if (Limit == 1000.0)
			{
				return "751 – 1000";
			}
			if (Limit == 1250.0)
			{
				return "1001 – 1250";
			}
			if (Limit == 1600.0)
			{
				return "1251 – 1600";
			}
			if (Limit == 2100.0)
			{
				return "1601 – 2100";
			}
			if (Limit == 2500.0)
			{
				return "2101 – 2500";
			}
			if (Limit == 99999.0)
			{
				return "2501 TRỞ LÊN";
			}
			return $"<= {Limit}";
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
