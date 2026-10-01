using System.Windows.Media.Imaging;

namespace BIN;

public class PictureItem
{
	public BitmapImage Image { get; set; }

	public string Option { get; set; }

	public PictureItem(BitmapImage image, string option)
	{
		Image = image;
		Option = option;
	}
}
