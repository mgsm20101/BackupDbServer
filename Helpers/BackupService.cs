using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupDbServer.Helpers
{
    public static class BackupService
    {
        public static List<BackupEntry> LoadBackupLog(string path)
        {
            var entries = new List<BackupEntry>();

            if (!File.Exists(path)) return entries;

            var lines = File.ReadAllLines(path);
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');

                if (parts.Length >= 4)
                {
                    entries.Add(new BackupEntry
                    {
                        Database = parts[0],
                        BackupTime = DateTime.ParseExact(parts[1], "yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture),
                        Status = parts[2],
                        ErrorMessage = parts[3]
                    });
                }
            }

            return entries;
        }
    }
}
