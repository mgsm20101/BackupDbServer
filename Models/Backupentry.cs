using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupDbServer.Models
{
    public class BackupEntry
    {
        public string Database { get; set; } = string.Empty;
        public DateTime BackupTime { get; set; }
        public string Status { get; set; } = string.Empty; // "Success" or "Failed"
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsSuccessful { get; internal set; }
        public object Message { get; internal set; }
    }

}
