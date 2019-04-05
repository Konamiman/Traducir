using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Traducir.Core.Models;
using Traducir.Core.Models.Services;
using Traducir.Core.Services;

namespace Traducir.Api
{
    // To be used in development environment only
    public class ReadonlyTransifexService : ITransifexService
    {
        private readonly TransifexService realService;
        private readonly ILogger logger;

        public ReadonlyTransifexService(TransifexService realService, ILoggerFactory loggerFactory)
        {
            this.realService = realService;
            this.logger = loggerFactory.CreateLogger("TRANSIFEX SERVICE");
        }

        public Task<ImmutableArray<TransifexString>> GetStringsFromTransifexAsync()
        {
            return realService.GetStringsFromTransifexAsync();
        }

        public Task<bool> PushStringsToTransifexAsync(ImmutableArray<SOString> strings)
        {
            logger.LogInformation($"{strings.Length} strings were requested to be pushed to Transifex (they weren't)");
            return Task.FromResult<bool>(true);
        }
    }
}