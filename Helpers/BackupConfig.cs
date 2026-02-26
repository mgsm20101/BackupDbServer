using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BackupDbServer.Helpers
{
    public class BackupConfig
    {
        public string Server { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string TempPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
      
        public List<string> SelectedDatabases { get; set; } = new List<string>();
        public bool UseWindowsAuthentication { get;  set; }

        public string DockerContainerName { get; set; } = "dbs-2022-CTP23-1r";
        public ConnectionType ConnectionType { get; set; } = ConnectionType.Local;
        public int Port { get; set; } = 1433;
        public int ConnectionTimeout { get; set; } = 30;
        public bool TrustServerCertificate { get; set; } = true;
        public bool Encrypt { get; set; } = false;


    }


}
