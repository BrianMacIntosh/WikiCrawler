using FreeImageAPI;
using System;

namespace ImageProcessing
{
	public struct FloatColor
	{
		public float R;
		public float G;
		public float B;
		public float A;

		public FloatColor(RGBQUAD byteColor)
		{
			R = byteColor.rgbRed / 255f;
			G = byteColor.rgbGreen / 255f;
			B = byteColor.rgbBlue / 255f;
			A = byteColor.rgbReserved / 255f;
		}

		public static explicit operator RGBQUAD(FloatColor color)
		{
			RGBQUAD quad = new RGBQUAD();
			quad.rgbRed = SafeByteCast(255f * color.R);
			quad.rgbGreen = SafeByteCast(255f * color.G);
			quad.rgbBlue = SafeByteCast(255f * color.B);
			quad.rgbReserved = SafeByteCast(255f * color.A);
			return quad;
		}

		private static byte SafeByteCast(float color)
		{
			return (byte)Math.Max(Math.Min(color, 255), 0);
		}
	}
}
