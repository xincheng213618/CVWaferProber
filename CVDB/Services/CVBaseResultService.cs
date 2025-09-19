using CVRepositoryLib.Services.Measure;

namespace CVRepositoryLib.Services
{
    public class CVBaseResultService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVBaseResultService));
        public CVBaseResultService()
        {
        }

        public static int GetBatchId(string serialNumber, short archivedFlag = 0)
        {
            return MeasureBatchService.GetBatchId(serialNumber, archivedFlag);
        }
    }
}
