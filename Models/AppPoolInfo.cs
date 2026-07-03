namespace BackupDbServer.Models
{
    /// <summary>
    /// معلومات Application Pool في IIS مع حالة التحسين.
    /// </summary>
    public class AppPoolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string StartMode { get; set; } = string.Empty;
        public string PipelineMode { get; set; } = string.Empty;
        public string ManagedRuntimeVersion { get; set; } = string.Empty;

        /// <summary>مهلة الخمول بالدقائق (0 = معطّلة = محسّن).</summary>
        public double IdleTimeoutMinutes { get; set; }

        /// <summary>إعادة التدوير الدورية بالدقائق (0 = معطّلة = محسّن).</summary>
        public double PeriodicRestartMinutes { get; set; }

        public long QueueLength { get; set; }

        /// <summary>هل إعدادات التسريع مطبّقة بالكامل؟</summary>
        public bool IsOptimized { get; set; }
    }
}
