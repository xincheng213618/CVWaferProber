using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrumControl.Models
{
    public class SpectralData
    {
        public List<SpectralDataPoint> DataPoints { get; set; } = new List<SpectralDataPoint>();

        public void GenerateSampleData(double centerWavelength = 550, double bandwidth = 50)
        {
            DataPoints.Clear();
            for (double wavelength = 380; wavelength <= 780; wavelength += 1)
            {
                // 高斯分布模拟光谱峰值
                double intensity = Math.Exp(-Math.Pow(wavelength - centerWavelength, 2) / (2 * Math.Pow(bandwidth, 2)));
                DataPoints.Add(new SpectralDataPoint { Wavelength = wavelength, Intensity = intensity });
            }
        }

        public void GenerateMultiPeakData()
        {
            DataPoints.Clear();
            for (double wavelength = 380; wavelength <= 780; wavelength += 1)
            {
                // 多峰值光谱
                double peak1 = Math.Exp(-Math.Pow(wavelength - 450, 2) / (2 * Math.Pow(30, 2)));
                double peak2 = Math.Exp(-Math.Pow(wavelength - 550, 2) / (2 * Math.Pow(40, 2)));
                double peak3 = Math.Exp(-Math.Pow(wavelength - 650, 2) / (2 * Math.Pow(35, 2)));

                double intensity = Math.Min(peak1 + peak2 * 0.7 + peak3 * 0.5, 1.0);
                DataPoints.Add(new SpectralDataPoint { Wavelength = wavelength, Intensity = intensity });
            }
        }

        public void SetData(float[] waves, float[] intensitys)
        {
            DataPoints.Clear();
            for(int i = 0; i < waves.Length; i++)
            {
                if(Math.Abs(waves[i] % 1) < 0.0001f && (Math.Abs(intensitys[i] - 0.0) > 0.0001f && i!=0))
                DataPoints.Add(new SpectralDataPoint { Wavelength = waves[i], Intensity = intensitys[i] });
            }
        }

        public double MaxIntensity => DataPoints.Any() ? DataPoints.Max(p => p.Intensity) : 0;
        public double MinWavelength => DataPoints.Any() ? DataPoints.Min(p => p.Wavelength) : 380;
        public double MaxWavelength => DataPoints.Any() ? DataPoints.Max(p => p.Wavelength) : 780;
    }
}
