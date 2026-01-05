using ColorVision.Core.Entities;
using CVCommCore.CVSpectrum;
using CVWaferProber.Core.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl.Models
{
    public class SpectralData: ViewModelBase
    {
        public float V { get; set; }
        public float I { get; set; }

        /// <summary>
        /// IP
        /// </summary>
        public string IP { get; set; }
        /// <summary>
        /// 亮度Lv(cd/m2)
        /// </summary>
        public string Lv { get; set; }

        /// <summary>
        /// 蓝光
        /// </summary>
        public string Blue { get; set; }

        public float fx { get; set; }
        public float fy { get; set; }
        public float fu { get; set; }
        public float fv { get; set; }

        /// <summary>
        /// 相关色温(K)
        /// </summary>
        public float fCCT { get; set; }
        /// <summary>
        /// 色差dC
        /// </summary>
        public float dC { get; set; }
        /// <summary>
        /// 主波长(nm)
        /// </summary>
        public float fLd { get; set; }
        /// <summary>
        /// 色纯度(%)
        /// </summary>
        public float fPur { get; set; }
        /// <summary>
        /// 峰值波长(nm)
        /// </summary>
        public float fLp { get; set; }
        /// <summary>
        /// 半波宽(nm)
        /// </summary>
        public float fHW { get; set; }
        /// <summary>
        /// 平均波长(nm)
        /// </summary>
        public float fLav { get; set; }
        /// <summary>
        /// 显色性指数 Ra
        /// </summary>
        public float fRa { get; set; }
        /// <summary>
        /// 红色比
        /// </summary>
        public float fRR { get; set; }
        /// <summary>
        /// 绿色比
        /// </summary>
        public float fGR { get; set; }
        /// <summary>
        /// 蓝色比
        /// </summary>
        public float fBR { get; set; }
        /// <summary>
        /// 显色性指数 R1-R15
        /// </summary>
        public float[] fRi { get; set; }
        /// <summary>
        /// 峰值AD
        /// </summary>
        public float fIp { get; set; }
        /// <summary>
        /// 光度值
        /// </summary>
        public float fPh { get; set; }
        /// <summary>
        /// 辐射度值
        /// </summary>
        public float fPhe { get; set; }
        /// <summary>
        /// 绝对光谱洗漱
        /// </summary>
        public float fPlambda { get; set; }
        /// <summary>
        /// 起始波长
        /// </summary>
        public float fSpect1 { get; set; }
        /// <summary>
        /// 结束波长
        /// </summary>
        public float fSpect2 { get; set; }
        /// <summary>
        /// 波长间隔
        /// </summary>
        public float fInterval { get; set; }
        /// <summary>
        /// 光谱数据
        /// </summary>
        public float[] fPL { get; set; }
        private static int No;

        [DisplayName("SerialNumber1")]
        public int Id { get; set; }

        [DisplayName("CreateTime")]
        public DateTime? CreateTime { get; set; } = DateTime.Now;
        public string? Batch { get; set; }
        public int? BatchID { get; set; }
        public List<SpectralDataPoint> DataPoints { get; set; } = new List<SpectralDataPoint>();
        // public SpectralData(VScgdMeasureResultSpectrometer item)
        //{
        //    Id  = item.Id;
        //    BatchID = item.BatchId;
        //    CreateTime = item.CreateDate;
        //    fx = item.Fx ?? 0;
        //    fy = item.Fy ?? 0;
        //    fu = item.Fu ?? 0;
        //    fv = item.Fv ?? 0;
        //    fCCT = item.FCCT ?? 0;
        //    dC = item.DC ?? 0;
        //    fLd = item.FLd ?? 0;
        //    fPur = item.FPur ?? 0;
        //    fLp = item.FLp ?? 0;
        //    fHW = item.FHW ?? 0;
        //    fLav = item.FLav ?? 0;
        //    fRa = item.FRa ?? 0;
        //    fRR = item.FRR ?? 0;
        //    fGR = item.FGR ?? 0;
        //    fBR = item.FBR ?? 0;
        //    fIp = item.FIp ?? 0;
        //    fPh = item.FPh ?? 0;
        //    fPhe = item.FPhe ?? 0;
        //    fPlambda = item.FPlambda ?? 0;
        //    fSpect1 = item.FSpect1 ?? 0;
        //    fSpect2 = item.FSpect2 ?? 0;
        //    fInterval = item.FInterval ?? 0;
        //    fPL = JsonConvert.DeserializeObject<float[]>(item.FPL ?? string.Empty) ?? Array.Empty<float>();
        //    fRi = JsonConvert.DeserializeObject<float[]>(item.FRi ?? string.Empty) ?? Array.Empty<float>();
        //    GenerateSampleData();
        //}
        //public SpectralData(COLOR_PARA colorParam)
        //{
        //    Id = No++;
        //    fx = colorParam.fx;
        //    fy = colorParam.fy;
        //    fu = colorParam.fu;
        //    fv = colorParam.fv;
        //    fCCT = colorParam.fCCT;
        //    dC = colorParam.dC;
        //    fLd = colorParam.fLd;
        //    fPur = colorParam.fPur;
        //    fLp = colorParam.fLp;
        //    fHW = colorParam.fHW;
        //    fLav = colorParam.fLav;
        //    dC = colorParam.dC;
        //    fRa = colorParam.fRa;
        //    fRR = colorParam.fRR;
        //    fGR = colorParam.fGR;
        //    fBR = colorParam.fBR;
        //    fRi = colorParam.fRi;
        //    fIp = colorParam.fIp;
        //    fPh = colorParam.fPh;
        //    fPhe = colorParam.fPhe;
        //    fPlambda = colorParam.fPlambda;
        //    fSpect1 = colorParam.fSpect1;
        //    fSpect2 = colorParam.fSpect2;
        //    fInterval = colorParam.fInterval;
        //    fPL = colorParam.fPL;
        //    GenerateSampleData();

        //}
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
