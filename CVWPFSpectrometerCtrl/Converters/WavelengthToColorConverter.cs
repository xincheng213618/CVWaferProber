using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CVWPFSpectrometerCtrl.Converters
{
    public static class WavelengthToColorConverter
    {
        public static Color ConvertWavelengthToColor(double wavelength)
        {
            if (wavelength < 380 || wavelength > 780)
                return Colors.Transparent;

            double gamma = 0.8;
            double intensityMax = 255;

            double factor;
            double red = 0, green = 0, blue = 0;

            if (wavelength >= 380 && wavelength < 440)
            {
                red = -(wavelength - 440) / (440 - 380);
                green = 0.0;
                blue = 1.0;
            }
            else if (wavelength >= 440 && wavelength < 490)
            {
                red = 0.0;
                green = (wavelength - 440) / (490 - 440);
                blue = 1.0;
            }
            else if (wavelength >= 490 && wavelength < 510)
            {
                red = 0.0;
                green = 1.0;
                blue = -(wavelength - 510) / (510 - 490);
            }
            else if (wavelength >= 510 && wavelength < 580)
            {
                red = (wavelength - 510) / (580 - 510);
                green = 1.0;
                blue = 0.0;
            }
            else if (wavelength >= 580 && wavelength < 645)
            {
                red = 1.0;
                green = -(wavelength - 645) / (645 - 580);
                blue = 0.0;
            }
            else if (wavelength >= 645 && wavelength < 781)
            {
                red = 1.0;
                green = 0.0;
                blue = 0.0;
            }

            // 调整强度
            if (wavelength >= 380 && wavelength < 420)
            {
                factor = 0.3 + 0.7 * (wavelength - 380) / (420 - 380);
            }
            else if (wavelength >= 420 && wavelength < 701)
            {
                factor = 1.0;
            }
            else if (wavelength >= 701 && wavelength < 781)
            {
                factor = 0.3 + 0.7 * (780 - wavelength) / (780 - 700);
            }
            else
            {
                factor = 0.0;
            }

            // 伽马校正
            byte r = (byte)(Math.Pow(red * factor, gamma) * intensityMax);
            byte g = (byte)(Math.Pow(green * factor, gamma) * intensityMax);
            byte b = (byte)(Math.Pow(blue * factor, gamma) * intensityMax);

            return Color.FromRgb(r, g, b);
        }
    }
}
