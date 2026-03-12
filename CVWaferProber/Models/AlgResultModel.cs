using CVCommCore;
using CVDB.Services.Algorithm;

namespace CVWaferProber.Models
{
    public class AlgResultModel
    {
        public object LoadAOIResults(string serialNumber)
        {
            var results = AlgResultService.LoadAlgResultByBatchCode(serialNumber);
            foreach (var result in results)
            {
                AlgorithmResultType resultType = (AlgorithmResultType)result.ImgFileType;
                if(resultType == AlgorithmResultType.OLED_RebuildPixelsMem)
                {
                    var detailResult = AlgResultService.GetPOIDetailResultFileByPid(result.Id);
                }
            }

            return results;
        }
    }
}
