using System.ComponentModel;

namespace BIN;

public class ThicknessEntry : INotifyPropertyChanged
{
	private double _pipeSizeMM;

	private double _insulationThicknessMM;

	public double PipeSizeMM
	{
		get
		{
			return _pipeSizeMM;
		}
		set
		{
			_pipeSizeMM = value;
			OnPropertyChanged("PipeSizeMM");
		}
	}

	public double InsulationThicknessMM
	{
		get
		{
			return _insulationThicknessMM;
		}
		set
		{
			_insulationThicknessMM = value;
			OnPropertyChanged("InsulationThicknessMM");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
