using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace netCoreClient.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string QuestionText { get; set; }
        public DateTime? PubDate { get; set; }
    }
}
