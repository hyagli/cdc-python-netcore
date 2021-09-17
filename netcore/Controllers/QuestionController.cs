using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nest;
using netCoreClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace netCoreClient.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Question> Get()
        {
            var uri = new Uri("http://elasticsearch:9200");
            var settings = new ConnectionSettings(uri).DefaultIndex("question");
            var client = new ElasticClient(settings);
            var searchResponse = client.Search<Question>(s => s
                .Query(q => q.MatchAll())
                .Sort(q => q.Descending(question => question.Id))
                .Size(20)
            );
            return searchResponse.Documents;
        }
    }
}
