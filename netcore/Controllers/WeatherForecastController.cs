using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Com.Hus.Cdc;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace netcore.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {

        [HttpGet]
        public IEnumerable<Question> Get()
        {
            using (var db = new LiteDatabase(@"MyData.db"))
            {
                var questions = db.GetCollection<Question>("questions");
                return questions.FindAll().ToList();
            }
        }
    }
}
