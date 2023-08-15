using SkiaSharp;
using System.Globalization;
using System.Reflection;

internal class PriceChart
{
	public SKColor GraphColor { get; set; } = SKColors.Black;
	public string ImageResource { get; set; }
	int width = 600;
	int height = 400;
	int paddingLeft = 50;
	int paddingRight = 20;
	int paddingTop = 5;
	int paddingBottom = 20;
	SKColor rectColor = SKColors.Black;
	SKColor backColor = SKColors.White;
	SKColor greenColor = SKColors.Green;
	SKColor redColor = SKColors.Red;
	int indentX = 5;
	int indentY = 5;
	int hourTickHeight = 5;
	double hourStep = 6;

	public void Create(List<OHLC> data, Stream output)
	{
		var minValue = data.Min(o => o.Close);
		var maxValue = data.Max(o => o.Close);
		var minTime = data.Min(o => o.TimeStamp);
		var maxTime = data.Max(o => o.TimeStamp);
		var w = width - paddingLeft - paddingRight - indentX * 2;
		var pixPerHour = w / maxTime.Subtract(minTime).TotalHours;
		var h = height - paddingTop - paddingBottom - indentY * 2;
		var ky = h / (maxValue - minValue);

		var bitmap = new SKBitmap(width, height);
		var canvas = new SKCanvas(bitmap);
		var font = new SKFont(SKTypeface.FromFamilyName("Arial", 1, 1, SKFontStyleSlant.Upright), 14);
		font.Edging = SKFontEdging.SubpixelAntialias;
		var paintBack = new SKPaint { Style = SKPaintStyle.Fill, Color = backColor };
		var paintRect = new SKPaint { Style = SKPaintStyle.Stroke, Color = rectColor };
		var paintFont = new SKPaint { Style = SKPaintStyle.Fill, Color = rectColor, Typeface = font.Typeface, TextSize = 14 };
		var barGreen = new SKPaint { Color = greenColor, StrokeWidth = 4, IsAntialias = true };
		var barRed = new SKPaint { Color = redColor, StrokeWidth = 4, IsAntialias = true };
		var graph = new SKPaint { Color = SKColors.Black, StrokeWidth = 4, IsAntialias = true, StrokeJoin = SKStrokeJoin.Round };
		var paintGrid = new SKPaint { Color = SKColors.LightGray };

		canvas.Clear(backColor);
		if (ImageResource != null)
		canvas.DrawImage(SKImage.FromBitmap(SKBitmap.Decode(Assembly.GetExecutingAssembly().GetManifestResourceStream("suinotifierbot.Resources." + ImageResource))), 0, 0);

		var prev_h = Math.Floor(minTime.AddHours(-1 / pixPerHour).Hour / hourStep);
		for (int x = 0; x < w; x++)
		{
			var t = minTime.AddHours(x / pixPerHour);
			if (prev_h != Math.Floor(t.Hour / hourStep))
			{
				//if (t.Hour == 0)
				//{
					canvas.DrawLine(x + paddingLeft + indentX, height - paddingBottom, x + paddingLeft + indentX, height - paddingBottom + hourTickHeight, paintRect);
					canvas.DrawLine(x + paddingLeft + indentX, paddingTop, x + paddingLeft + indentX, height - paddingBottom, paintGrid);
					var tw = paintFont.MeasureText(t.ToString("d MMM HH", CultureInfo.InvariantCulture) + ":00");
					canvas.DrawText(t.ToString("d MMM HH", CultureInfo.InvariantCulture) + ":00", x + paddingLeft + indentX - tw / 2, height - paddingBottom + hourTickHeight * 3 + 2, font, paintFont);
				//}
				prev_h = Math.Floor(t.Hour / hourStep);
			}
		}

		var delta = (double)maxValue - (double)minValue;
		var step = delta / 10;
		var pow = (int)Math.Log10(step);
		step = Math.Pow(10, pow);
		while (delta / step > 25)
			step *= 10;
		var prev_v = Math.Floor((double)minValue / step) * step;
		for (int y = 0; y < h; y++)
		{
			var v = (double)minValue + y / (double)ky;
			if (v >= prev_v + step)
			{
				canvas.DrawLine(paddingLeft - hourTickHeight, height - paddingBottom - indentX - y, paddingLeft, height - paddingBottom - indentX - y, paintRect);
				canvas.DrawLine(paddingLeft, height - paddingBottom - indentX - y, width - paddingRight, height - paddingBottom - indentX - y, paintGrid);
				canvas.DrawText((pow < 0 ? Math.Round(v, -pow) : Math.Round(v / Math.Pow(10, pow)) * Math.Pow(10, pow)).ToString("0." + (pow < 0 ? "".PadLeft(-pow, '0') : "")), 5, height - paddingBottom - indentX - y + 6, font, paintFont);
				prev_v = v;
			}
		}
		canvas.DrawRect(paddingLeft, paddingTop, width - paddingRight - paddingLeft, height - paddingBottom - paddingTop, paintRect);

		int prevY = -1;
		int prevX = -1;
		using (SKPath path = new SKPath())
		{
			SKPaint paint = new SKPaint {
				Style = SKPaintStyle.Stroke,
				Color = GraphColor,
				StrokeWidth = 2,
				StrokeJoin = SKStrokeJoin.Round,
				IsAntialias = true
			};
			foreach (var bar in data)
			{
				var x = paddingLeft + indentX + bar.TimeStamp.Subtract(minTime).TotalHours * pixPerHour;
				var y = height - paddingBottom - indentY - (bar.Close - minValue) * ky;
				//if (prevY > 0)
				//	canvas.DrawLine((int)prevX, (int)prevY, (int)x, (int)y, graph);
				if (prevY > 0)
					path.LineTo((int)x, (int)y);
				else
					path.MoveTo((int)x, (int)y);
				prevY = (int)y;
				prevX = (int)x;
			}
			canvas.DrawPath(path, paint);
		}
		bitmap.Encode(output, SKEncodedImageFormat.Png, 0);
	}
}
