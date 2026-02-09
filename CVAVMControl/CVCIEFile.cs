using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVVAMControl
{
    public class CVCIEFile : IDisposable
    {
        private bool _disposed;

        public uint Version { get; set; }

        //public CVType FileExtType { get; set; }

        public int Rows { get; set; }

        public int Cols { get; set; }

        public int Bpp { get; set; }

        public int Depth => Bpp switch
        {
            8 => 0,
            16 => 2,
            32 => 5,
            64 => 6,
            _ => 0,
        };

        public int Channels { get; set; }

        public float Gain { get; set; }

        public float[] Exp { get; set; }

        public string SrcFileName { get; set; }

        public byte[] Data { get; set; }

        public string FilePath { get; set; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Data = null;
                    Exp = null;
                }

                _disposed = true;
            }
        }
    }
}
