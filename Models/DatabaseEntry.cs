using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupDbServer.Models
{
    public class DatabaseStatus
    {
        public string Database { get; set; } = string.Empty;
        public int FailedCount { get; set; }
        public DateTime? LastSuccess { get; set; }
    }

}
